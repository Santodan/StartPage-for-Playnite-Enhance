$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name+'.dll'))) }
$definitions = [LandingPage.Models.GameDetailWidgets]::Definitions
if ($definitions.Count -ne 9 -or @($definitions | Group-Object Id | Where-Object Count -ne 1).Count) { throw 'Detail widget catalog is incomplete or has duplicate IDs' }
foreach ($definition in $definitions) {
    # The dashboard assigns ViewId to FrameworkElement.Name during creation.
    $element = [Windows.Controls.Border]::new(); $element.Name = $definition.Id
    $properties = [LandingPage.Models.Layout.ViewProperties]::new(); $properties.ViewId = $definition.Id
    if (!$properties.IsGameDetailWidget -or $properties.GameDetailSettings.Source -ne [LandingPage.Settings.FeaturedGameSource]::HoveredOrSelectedGame -or $properties.GameDetailSettings.ShowArtwork -ne $definition.IsArtwork) { throw ('Invalid defaults for ' + $definition.Id) }
    if (!$definition.IsArtwork -and $properties.GameDetailSettings.UseCustomSectionLayout) { throw 'Independent widgets must use dashboard layout rather than internal section layout' }
    if ($definition.Section -eq [LandingPage.Settings.FeaturedGameSection]::Description -and !$properties.GameDetailSettings.ShowDescriptionImages) { throw 'New Description widget should display description images by default' }
    $properties.GameDetailSettings.TextSize = 27; $properties.GameDetailSettings.HoverDelayMilliseconds = 75
    $properties.Appearance.Enabled = $true; $properties.Appearance.Background = '#123456'
    $copy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($properties))
    if ($copy.ViewId -ne $definition.Id -or $copy.GameDetailSettings.TextSize -ne 27 -or $copy.GameDetailSettings.HoverDelayMilliseconds -ne 75 -or $copy.Appearance.Background -ne '#123456' -or [object]::ReferenceEquals($copy.GameDetailSettings,$properties.GameDetailSettings)) { throw ('Detail widget settings did not persist independently: ' + $definition.Id) }
    $copy.GameDetailSettings.TextSize = 19
    if ($properties.GameDetailSettings.TextSize -ne 27 -or $properties.FeaturedGameSettings.TextSize -eq 27) { throw 'Detail widgets share settings with a clone or Featured Game' }
}
$legacy = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{"ViewId":"Clock"}',[LandingPage.Models.Layout.ViewProperties])
if ($legacy.IsGameDetailWidget -or [Newtonsoft.Json.JsonConvert]::SerializeObject($legacy).Contains('GameDetailSettings')) { throw 'New detail settings inflated unrelated widget profiles' }
'PASS: nine valid widget IDs, source/artwork defaults, independent layout/appearance/settings, profile round-trips and unchanged unrelated widget serialization.'
