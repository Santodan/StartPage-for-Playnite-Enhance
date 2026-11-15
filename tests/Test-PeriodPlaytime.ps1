$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name+'.dll'))) }
$now = [datetime]::SpecifyKind([datetime]'2026-11-12T12:00:00',[DateTimeKind]::Local)
$sessions = [Collections.Generic.List[LandingPage.Models.GameActivity.Session]]::new()
function Add-Session([datetime]$local,[uint64]$seconds) { $session = [LandingPage.Models.GameActivity.Session]::new(); $session.DateSession = [datetime]::SpecifyKind($local,[DateTimeKind]::Local).ToUniversalTime(); $session.ElapsedSeconds = $seconds; $sessions.Add($session) }
Add-Session '2026-11-12T09:00:00' 7200
Add-Session '2026-11-11T23:30:00' 3600
Add-Session '2026-11-08T10:00:00' 3600
Add-Session '2026-10-31T23:30:00' 3600
Add-Session '2025-12-31T23:30:00' 3600
Add-Session '2026-11-13T10:00:00' 7200
Add-Session '2026-11-12T11:30:00' 7200
$totals = [LandingPage.Models.PeriodPlaytime]::Calculate($sessions,$now,[DayOfWeek]::Monday)
if($totals.TodaySeconds -ne 10800 -or $totals.WeekSeconds -ne 12600 -or $totals.MonthSeconds -ne 18000 -or $totals.YearSeconds -ne 21600) { throw ('Period totals or split boundary sessions are incorrect: ' + [Newtonsoft.Json.JsonConvert]::SerializeObject($totals)) }
$sunday = [LandingPage.Models.PeriodPlaytime]::Calculate($sessions,$now,[DayOfWeek]::Sunday)
if($sunday.WeekSeconds -ne 16200) { throw 'Custom week start did not include Sunday' }
$empty = [LandingPage.Models.PeriodPlaytime]::Calculate([LandingPage.Models.GameActivity.Session[]]@(),$now,[DayOfWeek]::Monday)
if($empty.TodaySeconds -ne 0 -or $empty.YearSeconds -ne 0) { throw 'Empty history should produce zero recorded hours' }
$session = [LandingPage.Models.GameActivity.Session]::new(); $session.DateSession = [datetime]::SpecifyKind($now.AddHours(-1).ToUniversalTime(),[DateTimeKind]::Unspecified); $session.ElapsedSeconds = 3600
$legacy = [LandingPage.Models.PeriodPlaytime]::Calculate([LandingPage.Models.GameActivity.Session[]]@($session),$now,[DayOfWeek]::Monday)
if($legacy.TodaySeconds -ne 3600) { throw 'Offset-less legacy UTC timestamps were interpreted incorrectly' }
$options = [LandingPage.Settings.LibraryStatisticsSettings]::new(); $options.ShowTodayPlaytime = $true; $options.ShowWeekPlaytime = $true; $options.ShowMonthPlaytime = $true; $options.ShowYearPlaytime = $true; $options.WeekStartsOn = [DayOfWeek]::Sunday
$clone = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($options))
if(!$clone.ShowTodayPlaytime -or !$clone.ShowWeekPlaytime -or !$clone.ShowMonthPlaytime -or !$clone.ShowYearPlaytime -or $clone.WeekStartsOn -ne [DayOfWeek]::Sunday) { throw 'Period settings did not persist in profiles' }
'PASS: day/week/month/year totals, local calendar boundaries, cross-midnight/month/year sessions, custom week start, ongoing/future limits, legacy timestamps, empty history and settings persistence.'
