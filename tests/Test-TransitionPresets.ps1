$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll')))
}
$preset = [LandingPage.Models.DashboardTransitionPreset]::new()
$preset.Name = 'Shared motion'; $preset.Easing = [LandingPage.Models.TransitionEasing]::Cubic
$preset.Incoming.StartX = 17; $preset.Outgoing.DelayMs = 50
$json = [LandingPage.Models.TransitionPresets]::Export($preset)
$copy = [LandingPage.Models.TransitionPresets]::Import($json)
if ($copy.Name -ne $preset.Name -or $copy.Easing -ne $preset.Easing -or $copy.Incoming.StartX -ne 17 -or $copy.Outgoing.DelayMs -ne 50) { throw 'Transition JSON round-trip lost data' }
$copy.Incoming.StartX = 42
if ($preset.Incoming.StartX -ne 17) { throw 'Transition presets share mutable motion objects' }
foreach ($invalid in @(
    $json.Replace('"Version": 1','"Version": 99'),
    $json.Replace('StartPage.TransitionPreset','Other.Format'),
    $json.Replace('"DurationMs": 200','"DurationMs": 5000'),
    $json.Replace('"StartX": 17.0','"StartX": 101.0'),
    $json.Replace('"Cubic"','"UnsupportedEase"'),
    $json.Replace('"Name": "Shared motion"','"Name": "Shared motion", "Script": "anything"'),
    ($json + '{}'), '{}', ('x' * 65537)
)) {
    $rejected = $false
    try { [void][LandingPage.Models.TransitionPresets]::Import($invalid) } catch { $rejected = $true }
    if (!$rejected) { throw ('Invalid transition JSON accepted: ' + $invalid.Substring(0,[Math]::Min(100,$invalid.Length))) }
}
$sample = Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/transition-presets/gentle-slide.json'
$example = [LandingPage.Models.TransitionPresets]::Import([IO.File]::ReadAllText($sample))
if ($example.Name -ne 'Gentle slide') { throw 'Documented transition example is invalid' }
'PASS: transition JSON round-trip, independent motion objects, validation/format/version/unknown-field/size rejection, and documented example.'
