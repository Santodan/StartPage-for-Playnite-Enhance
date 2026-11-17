# Use Playnite's actual serializer/resolver, rather than Json.NET defaults.
# powershell.exe -STA -File tests/Test-ConfigStartup.ps1 [-ConfigurationFile path]
param([string]$PlayniteDirectory = 'E:\Programs\Playnite', [string]$ConfigurationFile)
$ErrorActionPreference = 'Stop'
# Playnite's desktop assembly targets x86. Use a matching process for this test.
if ([Environment]::Is64BitProcess) {
    $extra = @(); if ($ConfigurationFile) { $extra = @('-ConfigurationFile',$ConfigurationFile) }
    & "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -STA -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -PlayniteDirectory $PlayniteDirectory @extra
    exit $LASTEXITCODE
}
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'source\StartPage\bin\Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Gu.Wpf.NumericInput','Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll')))
}
[void][Reflection.Assembly]::LoadFrom((Join-Path $PlayniteDirectory 'Playnite.dll'))
$serializerField = [Playnite.SDK.Data.Serialization].GetField('serializer',[Reflection.BindingFlags]'Static,NonPublic')
$previousSerializer = $serializerField.GetValue($null)

$instanceField = [LandingPage.LandingPageExtension].GetField('<Instance>k__BackingField',[Reflection.BindingFlags]'Static,NonPublic')
$previousInstance = $instanceField.GetValue($null)
try {
    $serializerField.SetValue($null,[Playnite.Common.DataSerializer]::new())
    # Reproduce startup: the singleton exists, but its settings view model has not been assigned yet.
    $plugin = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.LandingPageExtension])
    $instanceField.SetValue($null,$plugin)
    $json = if ($ConfigurationFile) { [IO.File]::ReadAllText($ConfigurationFile) } else { '{"MaxNumberRecentAchievements":17,"MaxNumberRecentAchievementsPerGame":3,"GridLayout":{"Children":[{"ViewProperties":{"ViewId":"AchievementProgress","AchievementSettings":{"TitleSize":29}}},{"ViewProperties":{"ViewId":"RecentAchivements"}},{"ViewProperties":{"ViewId":"RecentAchivements","AchievementSettings":{"Limit":37,"PerGameLimit":7}}}]}}' }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $read = [Playnite.SDK.Data.Serialization].GetMethod('FromJson').MakeGenericMethod([LandingPage.LandingPageSettings])
    $settings = $read.Invoke($null,@([string]$json))
    if (!$settings) { throw 'Configuration returned no settings' }
    [LandingPage.Settings.AchievementWidgetSettings]::MigrateLegacy($settings)
    $source = [Newtonsoft.Json.Linq.JObject]::Parse($json)
    $total = [LandingPage.LandingPageSettings].GetProperty('MaxNumberRecentAchievements').GetValue($settings)
    $perGame = [LandingPage.LandingPageSettings].GetProperty('MaxNumberRecentAchievementsPerGame').GetValue($settings)
    foreach ($node in ([LandingPage.LandingPageSettings].GetProperty('GridLayout').GetValue($settings)).Enumerate()) {
        if ($node.ViewProperties.ViewId -eq 'RecentAchivements' -and $node.ViewProperties.AchievementSettings.Limit -notin @($total,37)) { throw 'Legacy or explicit recent counts were not preserved' }
    }
    # A complete in-memory round-trip must retain all saved pages/profiles and their identities.
    $saved = [Playnite.SDK.Data.Serialization]::ToJson($settings,$false)
    $again = $read.Invoke($null,@([string]$saved))
    $originalPages = [LandingPage.LandingPageSettings].GetProperty('DashboardPages').GetValue($settings)
    $againPages = [LandingPage.LandingPageSettings].GetProperty('DashboardPages').GetValue($again)
    if ($ConfigurationFile) {
        $sourcePages = $source['DashboardPages']; $sourceProfiles = $source['DashboardProfiles']
        if ($sourcePages.Count -ne $originalPages.Count -or $sourceProfiles.Count -ne ([LandingPage.LandingPageSettings].GetProperty('DashboardProfiles').GetValue($again)).Count) { throw 'Configuration lost pages/profiles' }
    } else {
        $nodes = @(([LandingPage.LandingPageSettings].GetProperty('GridLayout').GetValue($settings)).Enumerate() | Where-Object { $_.ViewProperties.ViewId -eq 'RecentAchivements' })
        if ($nodes.Count -ne 2 -or $nodes[0].ViewProperties.AchievementSettings.Limit -ne 17 -or $nodes[0].ViewProperties.AchievementSettings.PerGameLimit -ne 3 -or $nodes[1].ViewProperties.AchievementSettings.Limit -ne 37) { throw 'Post-load migration overwrote legacy or customized limits' }
    }

    if ((($originalPages | ForEach-Object Id) -join ',') -ne (($againPages | ForEach-Object Id) -join ',')) { throw 'Page identities changed during round-trip' }

    $pages = [LandingPage.LandingPageSettings].GetProperty('DashboardPages').GetValue($settings)
    $profiles = [LandingPage.LandingPageSettings].GetProperty('DashboardProfiles').GetValue($settings)
    $oldProfile = [LandingPage.Models.DashboardProfile]::new(); $oldProfile.Snapshot = [LandingPage.Models.DashboardSnapshot]::new()
    $oldPage = [LandingPage.Models.DashboardPage]::new(); $oldPage.Layout = [LandingPage.Models.Layout.GridNode]::new()
    $oldPage.Layout.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $oldPage.Layout.ViewProperties.ViewId = 'RecentAchivements'
    $oldPage.Background = [LandingPage.Models.DashboardBackground]::Capture([LandingPage.LandingPageSettings]::new()); $oldProfile.Snapshot.Pages.Add($oldPage); $oldProfile.Snapshot.ActivePageId = $oldPage.Id
    $oldProfile.Snapshot.SharedSettings.Add('MaxNumberRecentAchievements',[Newtonsoft.Json.Linq.JToken]::Parse('11'))
    $oldProfile.Snapshot.SharedSettings.Add('MaxNumberRecentAchievementsPerGame',[Newtonsoft.Json.Linq.JToken]::Parse('2'))
    $prepared = [LandingPage.Models.DashboardProfiles]::Prepare($oldProfile,[guid]'a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5')
    if ($prepared.Pages[0].Layout.ViewProperties.AchievementSettings.Limit -ne 11 -or $prepared.Pages[0].Layout.ViewProperties.AchievementSettings.PerGameLimit -ne 2 -or $oldPage.Layout.ViewProperties.ShouldSerializeAchievementSettings()) { throw 'Preparing an old profile lost its limits or modified the original' }
    'PASS: configuration deserializes during startup without an initialized settings view model.'
    ('Configuration load: ' + $watch.ElapsedMilliseconds + ' ms; pages=' + $pages.Count + '; profiles=' + $profiles.Count)
} finally { $instanceField.SetValue($null,$previousInstance); $serializerField.SetValue($null,$previousSerializer) }

