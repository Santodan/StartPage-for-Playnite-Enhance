# powershell.exe -STA -File tests/Test-StarterProfiles.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'source\StartPage\bin\Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Gu.Wpf.NumericInput','Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll')))
}
$context = [LandingPage.LandingPageSettings]::new()
$settingsType = [LandingPage.LandingPageSettings]
$providerProperty = $settingsType.GetProperty('SelectedAchievementsProvider')
$provider = [Enum]::GetValues($providerProperty.PropertyType)[1]; $providerProperty.SetValue($context,$provider)
$settingsType.GetProperty('BackgroundImagePath').SetValue($context,'Z:\Private\my-background.jpg')
$settingsType.GetProperty('BackgroundImageUri').SetValue($context,[uri]'file:///Z:/Private/my-background.jpg')
$builtin = [guid]'a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5'
$templates = [LandingPage.Models.DashboardStarterTemplates]::Create($context,$builtin)
if ($templates.Count -ne 6 -or (@($templates | ForEach-Object Key) -join ',') -ne 'minimal,achievements,library,spotlight,inspector,insights') { throw 'Expected six starter templates' }
if ($settingsType.GetProperty('DashboardPages').GetValue($context).Count -ne 0 -or $settingsType.GetProperty('BackgroundImagePath').GetValue($context) -ne 'Z:\Private\my-background.jpg') { throw 'Generating templates changed the live dashboard' }
$allowed = @('DigitalClock','TextWidget','ToolbarWidget','GameShelves','RecentAchivements','FeaturedGame','GameDetailArtwork','GameDetailTitle','GameDetailDescription','GameDetailActivity','GameDetailActions','LibraryStatistics','LibraryChart')
$previewHashes = [Collections.Generic.HashSet[string]]::new()
$output = Join-Path $env:TEMP ('StartPage-starter-previews-' + [guid]::NewGuid())
[void][IO.Directory]::CreateDirectory($output)
foreach ($template in $templates) {
    [LandingPage.Models.DashboardProfiles]::Validate($template.Profile)
    if (!$template.Description -or !$template.Notes) { throw 'Starter cards need descriptions and dependency notes' }
    $snapshot = $template.Profile.Snapshot
    $shelfIds = [Collections.Generic.HashSet[guid]]::new()
    foreach ($page in $snapshot.Pages) {
        $toolbarCount = 0
        foreach ($node in $page.Layout.Enumerate()) {
            $view = $node.ViewProperties
            if (!$view) { continue }
            if ($view.PluginId -ne $builtin -or $view.ViewId -notin $allowed) { throw 'Starter should use supported built-in widgets only' }
            if ($view.ViewId -eq 'ToolbarWidget') { ++$toolbarCount; if ($view.ToolbarSettings.Items.Count -ne 5) { throw 'Starter navigation/profile toolbar is incomplete' } }
            if ($view.ViewId -eq 'GameShelves') { [void]$shelfIds.Add($view.InstanceId) }
        }
        if ($toolbarCount -ne 1) { throw 'Every starter page needs navigation' }
        if ($page.Background.ImageUri -or $page.Background.ImagePath) { throw 'Starters must not embed private background paths' }
    }
    if ($snapshot.Shelves.Count -ne $shelfIds.Count) { throw 'Starter shelves are not mapped to widget instances' }
    foreach ($id in $shelfIds) { if (!$snapshot.Shelves.ContainsKey($id)) { throw 'Missing starter shelf configuration' } }
    if ($snapshot.SharedSettings['SelectedAchievementsProvider'].ToObject($providerProperty.PropertyType) -ne $provider) { throw 'Achievement template should respect the selected data provider' }
    $json = [LandingPage.Models.DashboardProfiles]::Export($template.Profile)
    $copy = [LandingPage.Models.DashboardProfiles]::Import($json)
    if ($copy.Snapshot.Pages.Count -ne $snapshot.Pages.Count) { throw 'Starter profile export/import failed' }

    $preview = [LandingPage.Controls.DashboardTemplatePreview]::new(); $preview.Layout = $snapshot.Pages[0].Layout; $preview.Shelves = $snapshot.Shelves
    $preview.Measure([Windows.Size]::new(400,160)); $preview.Arrange([Windows.Rect]::new(0,0,400,160)); $preview.UpdateLayout()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(400,160,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($preview)
    $pixels = [byte[]]::new(400*160*4); $bitmap.CopyPixels($pixels,400*4,0)
    $hash = [Convert]::ToBase64String([Security.Cryptography.SHA256]::Create().ComputeHash($pixels))
    if (!$previewHashes.Add($hash)) { throw 'Starter previews must show distinct layouts' }
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $output ($template.Key + '.png')))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
$library = $templates | Where-Object Key -eq 'library'
if ($library.Profile.Snapshot.Pages.Count -ne 2 -or $library.Profile.Snapshot.Pages[1].Name -ne 'Backlog') { throw 'Library starter should include a navigable backlog page' }
$backlogRules = @($library.Profile.Snapshot.Shelves.Values | ForEach-Object { $_.ShelveProperties })
if (!($backlogRules | Where-Object NeverPlayedOnly) -or !($backlogRules | Where-Object InstalledOnly) -or !($backlogRules | Where-Object FavoritesOnly)) { throw 'Library starter is missing useful shelf rules' }
$achievement = $templates | Where-Object Key -eq 'achievements'
if (!@($achievement.Profile.Snapshot.Shelves.Values | ForEach-Object { $_.ShelveProperties } | Where-Object ShowAchievementProgress)) { throw 'Achievement starter lacks progress metadata' }
$minimal = $templates | Where-Object Key -eq 'minimal'
$prepared = [LandingPage.Models.DashboardProfiles]::Prepare($minimal.Profile,$builtin)
$prepared.Pages[0].Name = 'Edited copy'
if ($minimal.Profile.Snapshot.Pages[0].Name -ne 'Home') { throw 'Applying/editing a template changes the recipe' }
$other = [LandingPage.Models.DashboardStarterTemplates]::Create($context,$builtin)
$other[0].Profile.Snapshot.Pages[0].Name = 'Separate gallery'
if ($templates[0].Profile.Snapshot.Pages[0].Name -ne 'Home') { throw 'Starter generations share mutable data' }
$spotlight = $templates | Where-Object Key -eq 'spotlight'
$feature = @($spotlight.Profile.Snapshot.Pages[0].Layout.Enumerate() | Where-Object { $_.ViewProperties.ViewId -eq 'FeaturedGame' })[0]
if ($feature.ViewProperties.FeaturedGameSettings.Source -ne [LandingPage.Settings.FeaturedGameSource]::HoveredOrSelectedGame) { throw 'Spotlight does not follow hover/selection' }
$inspector = $templates | Where-Object Key -eq 'inspector'
$details = @($inspector.Profile.Snapshot.Pages[0].Layout.Enumerate() | Where-Object { $_.ViewProperties.ViewId -like 'GameDetail*' })
if ($details.Count -ne 5 -or @($details | Where-Object { $_.ViewProperties.GameDetailSettings.Source -ne [LandingPage.Settings.FeaturedGameSource]::HoveredOrSelectedGame }).Count) { throw 'Inspector sections do not follow the same game' }
$description = @($details | Where-Object { $_.ViewProperties.ViewId -eq 'GameDetailDescription' })[0]
if ($description.ViewProperties.GameDetailSettings.DescriptionLength -ne 0 -or !$description.ViewProperties.GameDetailSettings.ShowDescriptionImages) { throw 'Inspector is missing full HTML description/images' }
$insights = $templates | Where-Object Key -eq 'insights'
$chart = @($insights.Profile.Snapshot.Pages[0].Layout.Enumerate() | Where-Object { $_.ViewProperties.ViewId -eq 'LibraryChart' })[0].ViewProperties.LibraryStatisticsSettings
if ($chart.ExtraCharts.Count -ne 2 -or $chart.ChartStyle -ne [LandingPage.Settings.StatisticsChartStyle]::Doughnut -or $chart.ExtraCharts[0].Metric -ne [LandingPage.Settings.StatisticsMetric]::Playtime -or $chart.ExtraCharts[1].ChartStyle -ne [LandingPage.Settings.StatisticsChartStyle]::VerticalBars) { throw 'Insights requires three independent chart recipes' }
'PASS: six portable starters, page navigation, shelf recipes, achievement provider, independent snapshots, profile round-trip, and distinct rendered previews.'
('Preview artifacts: ' + $output)
