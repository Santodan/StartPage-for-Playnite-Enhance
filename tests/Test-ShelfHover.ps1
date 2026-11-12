$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll'))) }
$settings = [LandingPage.Models.ShelveProperties]::new()
$settings.HoverEffect = [LandingPage.Models.ShelfHoverEffect]::Dock
$settings.HoverScale = 1.4; $settings.HoverRadius = 2
$center = [LandingPage.Controls.ShelfHoverMath]::Evaluate($settings,$true,$true,0,0)
$neighbor = [LandingPage.Controls.ShelfHoverMath]::Evaluate($settings,$true,$false,1,0)
$far = [LandingPage.Controls.ShelfHoverMath]::Evaluate($settings,$true,$false,3,0)
if ([Math]::Abs($center.Scale-1.4) -gt 0.001 -or $neighbor.Scale -le 1 -or $neighbor.Scale -ge $center.Scale -or $far.Scale -ne 1) { throw 'Dock magnification falloff is invalid' }
foreach ($mode in @('Dock','Lift','Bounce')) {
    $settings.HoverEffect = [Enum]::Parse([LandingPage.Models.ShelfHoverEffect],$mode)
    $settings.HoverDirection = [LandingPage.Models.ShelfHoverDirection]::Down
    $down = [LandingPage.Controls.ShelfHoverMath]::Evaluate($settings,$true,$true,0,0)
    $settings.HoverDirection = [LandingPage.Models.ShelfHoverDirection]::Up
    $up = [LandingPage.Controls.ShelfHoverMath]::Evaluate($settings,$true,$true,0,0)
    if ($down.Lift -ne $settings.HoverLift -or $up.Lift -ne -$settings.HoverLift -or $up.Scale -ne $down.Scale) { throw ('Direction did not reverse movement: ' + $mode) }
}
$settings.HoverEffect = [LandingPage.Models.ShelfHoverEffect]::Tilt
$tilt = [LandingPage.Controls.ShelfHoverMath]::Evaluate($settings,$true,$true,0,-1)
if ($tilt.Angle -ne -$settings.HoverTilt) { throw 'Tilt does not follow pointer direction' }
$settings.HoverDimOthers = $true
$dim = [LandingPage.Controls.ShelfHoverMath]::Evaluate($settings,$true,$false,4,0)
$exit = [LandingPage.Controls.ShelfHoverMath]::Evaluate($settings,$false,$false,4,0)
if ($dim.Opacity -ne $settings.HoverOtherOpacity -or $exit.Opacity -ne 1 -or $exit.Scale -ne 1 -or $exit.Angle -ne 0) { throw 'Dimming or leave reset is invalid' }
$settings.HoverEffect = [LandingPage.Models.ShelfHoverEffect]::None
if ([LandingPage.Controls.ShelfHoverMath]::Evaluate($settings,$true,$true,0,1).Scale -ne 1) { throw 'None still magnifies artwork' }
$settings.HoverScale = 99; $settings.HoverDuration = -1
if ($settings.HoverScale -ne 1.5 -or $settings.HoverDuration -ne 50) { throw 'Unsafe hover parameters were not bounded' }
$settings.HoverDirection = [LandingPage.Models.ShelfHoverDirection]::Down
$settings.HoverShowDetails = $false
$copy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Models.ShelveProperties]).Invoke($null,@($settings))
if ($copy.HoverDirection -ne [LandingPage.Models.ShelfHoverDirection]::Down -or $copy.HoverShowDetails -or $copy.HoverScale -ne 1.5 -or !$copy.HoverDimOthers) { throw 'Hover settings did not persist' }
$settings.ResetHoverAppearance()
if ($settings.HoverDirection -ne [LandingPage.Models.ShelfHoverDirection]::Up -or $settings.HoverEffect -ne [LandingPage.Models.ShelfHoverEffect]::Classic -or $settings.HoverDimOthers -or $settings.HoverGlow) { throw 'Reset did not restore classic behavior' }
'PASS: dock proximity/falloff, tilt, dimming/leave reset, None, bounds, persistence and reset.'
