$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Gu.Wpf.NumericInput','Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll')))
}
$builtin = 'a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5'
$shelf = [guid]::NewGuid().ToString()
$external = [guid]::NewGuid().ToString()
$json = @"
{"GridLayout":{"Children":[{"ViewProperties":{"ViewId":"GameShelves","InstanceId":"$shelf","PluginId":"$builtin"}},{"ViewProperties":{"ViewId":"ExternalWidget","InstanceId":"$external","PluginId":"$external"}}]},"Padding":17,"BackgroundImagePath":"D:\\Artwork\\background.jpg","BlurAmount":13,"ShelveInstances":{"$shelf":[{"Name":"Imported shelf","NumberOfGames":12}]},"MostPlayedOptions":null}
"@
$profile = [LandingPage.Models.DashboardProfiles]::ImportOriginalSettings($json)
if ($profile.Snapshot.Pages.Count -ne 1 -or $profile.Snapshot.Pages[0].Name -ne 'Original StartPage') { throw 'Original page missing' }
if ($profile.Snapshot.Pages[0].Background.Blur -ne 13 -or $profile.Snapshot.Pages[0].Background.ImagePath -ne 'D:\Artwork\background.jpg') { throw 'Original background lost' }
if ($profile.Snapshot.Shelves[[guid]$shelf].ShelveProperties[0].NumberOfGames -ne 12) { throw 'Legacy shelf-instance settings lost' }
if ($profile.Snapshot.Pages[0].Layout.Children[1].ViewProperties.PluginId -ne [guid]$external) { throw 'External provider identity changed' }
if ($profile.Snapshot.SharedSettings['Padding'].ToString() -ne '17') { throw 'Shared settings lost' }
$roundtrip = [LandingPage.Models.DashboardProfiles]::Import([LandingPage.Models.DashboardProfiles]::Export($profile))
if ($roundtrip.Snapshot.Shelves[[guid]$shelf].ShelveProperties.Count -ne 1) { throw 'Imported profile cannot be saved/reloaded' }
$globalJson = $json.Replace('"ShelveInstances":', '"ShelveProperties":[{"NumberOfGames":23}],"UnusedLegacyInstances":')
$global = [LandingPage.Models.DashboardProfiles]::ImportOriginalSettings($globalJson)
if ($global.Snapshot.Shelves[[guid]$shelf].ShelveProperties[0].NumberOfGames -ne 23) { throw 'Original shared shelves not migrated to their widget' }
foreach ($bad in @('{}', 'null', '{"Format":"StartPage.DashboardProfile"}', '{"GridLayout":null}', '{"GridLayout":{},"Padding":-1}', '{"GridLayout":{},"$type":"Unknown.Type,Unknown.Assembly"} trailing')) {
    $rejected = $false
    try { [void][LandingPage.Models.DashboardProfiles]::ImportOriginalSettings($bad) } catch { $rejected = $true }
    if (!$rejected) { throw "Accepted invalid import: $bad" }
}
$metadata = [LandingPage.Models.DashboardProfiles]::ImportOriginalSettings($json.Replace('"GridLayout":', '"$type":"Unknown.Type,Unknown.Assembly","GridLayout":'))
if ($metadata.Snapshot.Pages.Count -ne 1) { throw 'JSON metadata was interpreted as a CLR type' }
'PASS: original layout/background/settings, legacy instance and shared shelves, external provider identity, null options, profile round-trip and invalid JSON isolation.'
