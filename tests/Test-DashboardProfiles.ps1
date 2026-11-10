# powershell.exe -STA -File tests/Test-DashboardProfiles.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'source\StartPage\bin\Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Gu.Wpf.NumericInput','Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll')))
}
function Get-Setting($settings,$name) { return ,([LandingPage.LandingPageSettings].GetProperty($name).GetValue($settings)) }
function Set-Setting($settings,$name,$value) { [LandingPage.LandingPageSettings].GetProperty($name).SetValue($settings,$value) }
$builtin = [guid]'a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5'
$settings = [LandingPage.LandingPageSettings]::new()
$layout = [LandingPage.Models.Layout.GridNode]::new()
$text = $null; $shelf = $null; $external = $null
foreach ($id in @('TextWidget','DigitalClock','ImageWidget','ToolbarWidget','GameShelves','ExternalWidget')) {
    $node = [LandingPage.Models.Layout.GridNode]::new(); $node.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new()
    $node.ViewProperties.PluginId = $builtin; $node.ViewProperties.ViewId = $id
    $node.ViewProperties.Appearance.Enabled = $true; $node.ViewProperties.Appearance.Background = '#102030'
    if ($id -eq 'TextWidget') { $text = $node; $text.ViewProperties.TextSettings.Text = 'Original note'; $text.ViewProperties.TextSettings.Bold = $true }
    if ($id -eq 'DigitalClock') { $node.ViewProperties.ClockSettings.TimeSize = 177 }
    if ($id -eq 'ImageWidget') { $node.ViewProperties.ImageSettings.ImagePath = 'Z:\Missing\banner.png' }
    if ($id -eq 'ToolbarWidget') { $node.ViewProperties.ToolbarSettings.Groups[0].ShowBackground = $true; $node.ViewProperties.ToolbarSettings.Groups[0].Width = 311 }
    if ($id -eq 'GameShelves') { $shelf = $node }
    if ($id -eq 'ExternalWidget') { $external = $node; $external.ViewProperties.PluginId = [guid]::NewGuid() }
    $layout.Children.Add($node)
}
Set-Setting $settings 'GridLayout' $layout
$first = [LandingPage.Models.DashboardPages]::Ensure($settings)
$first.Background.ImagePath = 'Z:\Missing\background.jpg'; $first.Background.Blur = 13
$second = [LandingPage.Models.DashboardPages]::Add($settings,'Second')
$second.Layout.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $second.Layout.ViewProperties.PluginId = $builtin; $second.Layout.ViewProperties.ViewId = 'TextWidget'
$second.Layout.ViewProperties.TextSettings.Text = 'Other page'
$shelfSettings = [LandingPage.Settings.ShelvesSettings]::new()
$rule = [LandingPage.Models.ShelveProperties]::new(); $rule.IncludedNamePatterns = '*Quest*'; $rule.ExcludedInstallPathPatterns = 'D:\Demos\*'; $rule.UnlimitedGames = $true
$shelfSettings.ShelveProperties.Add($rule)
$shelfMap = [Collections.Generic.Dictionary[guid,LandingPage.Settings.ShelvesSettings]]::new(); $shelfMap.Add($shelf.ViewProperties.InstanceId,$shelfSettings); $shelfMap.Add($external.ViewProperties.InstanceId,[LandingPage.Settings.ShelvesSettings]::new())
Set-Setting $settings 'ShelveInstanceSettings' $shelfMap
Set-Setting $settings 'Padding' ([double]17)
$profile = [LandingPage.Models.DashboardProfiles]::Capture($settings,'Complete dashboard')
$text.ViewProperties.TextSettings.Text = 'Changed live note'; $rule.IncludedNamePatterns = '*Changed*'; $first.Background.Blur = 99
Set-Setting $settings 'Padding' ([double]90)
if ($profile.Snapshot.Pages[0].Layout.Children[0].ViewProperties.TextSettings.Text -ne 'Original note' -or $profile.Snapshot.Shelves[$shelf.ViewProperties.InstanceId].ShelveProperties[0].IncludedNamePatterns -ne '*Quest*' -or $profile.Snapshot.Pages[0].Background.Blur -ne 13) { throw 'Saved profile shares mutable settings with the active dashboard' }
$json = [LandingPage.Models.DashboardProfiles]::Export($profile)
if ($json -match '"(StartPageViewArgs|DesignatorSize|IsLeaf|ViewCount|Item|Error)"\s*:') { throw 'Profiles must exclude runtime/computed/validation properties' }
$imported = [LandingPage.Models.DashboardProfiles]::Import($json)
if ($imported.Id -eq $profile.Id -or $imported.Snapshot.Pages.Count -ne 2 -or $imported.Snapshot.Pages[0].Layout.Children[3].ViewProperties.ToolbarSettings.Items.Count -ne 4) { throw 'Profile round-trip failed or defaults duplicated' }
$prepared = [LandingPage.Models.DashboardProfiles]::Prepare($imported,$builtin)
if ($prepared.Pages[0].Id -eq $profile.Snapshot.Pages[0].Id -or $prepared.Pages[0].Layout.Children[0].ViewProperties.InstanceId -eq $text.ViewProperties.InstanceId) { throw 'Applying must allocate independent built-in page/widget IDs' }
if ($prepared.Pages[0].Layout.Children[5].ViewProperties.InstanceId -ne $external.ViewProperties.InstanceId) { throw 'External widget identity must remain available for its provider settings' }
$newShelfId = $prepared.Pages[0].Layout.Children[4].ViewProperties.InstanceId
if (!$prepared.Shelves.ContainsKey($external.ViewProperties.InstanceId)) { throw 'External host shelf settings must retain their instance mapping' }
if (!$prepared.Shelves.ContainsKey($newShelfId) -or $prepared.Shelves[$newShelfId].ShelveProperties[0].IncludedNamePatterns -ne '*Quest*') { throw 'Shelf settings were not remapped with widget IDs' }
$target = [LandingPage.LandingPageSettings]::new()
Set-Setting $target 'KeepInMemory' $false
[LandingPage.Models.DashboardProfiles]::ApplyData($target,$prepared)
if ((Get-Setting $target 'Padding') -ne 17 -or (Get-Setting $target 'DashboardPages').Count -ne 2 -or (Get-Setting $target 'KeepInMemory')) { throw 'Applying profile missed shared presentation settings or changed unrelated operational settings' }
$prepared.Pages[0].Layout.Children[0].ViewProperties.TextSettings.Text = 'Editing applied profile'
if ($profile.Snapshot.Pages[0].Layout.Children[0].ViewProperties.TextSettings.Text -ne 'Original note' -or $imported.Snapshot.Pages[0].Layout.Children[0].ViewProperties.TextSettings.Text -ne 'Original note') { throw 'Applied settings share mutable profile snapshot objects' }
foreach ($bad in @('{}',$json.Replace('StartPage.DashboardProfile','OtherFormat'),$json.Replace('"Version": 1','"Version": 99'),($json + '{}'))) {
    $rejected = $false
    try { [void][LandingPage.Models.DashboardProfiles]::Import($bad) } catch { $rejected = $true }
    if (!$rejected) { throw 'Invalid/unsupported profile must be rejected before application' }
}
$invalid = [LandingPage.Models.DashboardProfiles]::Import($json); $invalid.Snapshot.ActivePageId = [guid]::NewGuid()
$rejected = $false; try { [LandingPage.Models.DashboardProfiles]::Validate($invalid) } catch { $rejected = $true }
if (!$rejected) { throw 'Missing active page must be rejected' }
$copy = [LandingPage.Models.DashboardProfiles]::Clone($layout)
$layout.Children[0].ViewProperties.Appearance.Background = '#FFFFFF'
if ($copy.Children[0].ViewProperties.Appearance.Background -eq '#FFFFFF') { throw 'Legacy layout preset cloning still aliases the live layout' }
'PASS: complete snapshots, independent page/widget/appearance/shelf/background data, shared settings, JSON format/validation, instance remapping, external identity, missing asset references, and mutable-preset isolation.'
