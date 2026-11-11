$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll'))) }
$a = [Playnite.SDK.Models.Game]::new(); $a.Name = 'Alpha'; $a.Added = [datetime]::Now.AddDays(-10); $a.LastActivity = [datetime]::Now.AddDays(-1); $a.IsInstalled = $true
$b = [Playnite.SDK.Models.Game]::new(); $b.Name = 'Beta'; $b.Added = [datetime]::Now; $b.LastActivity = [datetime]::Now; $b.Hidden = $true; $b.Favorite = $true
$c = [Playnite.SDK.Models.Game]::new(); $c.Name = 'Gamma'; $c.Added = [datetime]::Now.AddDays(-2); $c.Favorite = $true
$library = [Playnite.SDK.Models.Game[]]@($a,$b,$c)
$settings = [LandingPage.Settings.FeaturedGameSettings]::new()
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings).Id -ne $a.Id) { throw 'Last played must exclude hidden games' }
$settings.Source = [LandingPage.Settings.FeaturedGameSource]::LastAdded
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings).Id -ne $c.Id) { throw 'Recently added selection failed' }
$settings.InstalledOnly = $true
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings).Id -ne $a.Id) { throw 'Installed filter failed' }
$settings.Source = [LandingPage.Settings.FeaturedGameSource]::Pinned; $settings.PinnedGameId = $b.Id
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings).Id -ne $b.Id) { throw 'Explicit pin should override automatic filters' }
$settings.PinnedGameId = [guid]::NewGuid()
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings)) { throw 'Missing pinned game must not silently select another game' }
$settings.InstalledOnly = $false; $settings.Source = [LandingPage.Settings.FeaturedGameSource]::RandomFavorite
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings).Id -ne $c.Id) { throw 'Random favorites filter failed' }
$settings.Source = [LandingPage.Settings.FeaturedGameSource]::Random
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings,$a.Id,$false).Id -ne $a.Id) { throw 'Random game changed on revisit' }
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings,$a.Id,$true).Id -eq $a.Id) { throw 'Another game repeated the current selection despite alternatives' }
$settings.Source = [LandingPage.Settings.FeaturedGameSource]::SelectedGame
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings,$null,$false,$c.Id).Id -ne $c.Id) { throw 'Library selection mode failed' }
if ([LandingPage.Models.FeaturedGameSelection]::Description('<p>Fish &amp; chips</p><br>Ready',100) -ne 'Fish & chips Ready') { throw 'Plain description conversion failed' }
if ([LandingPage.Models.FeaturedGameSelection]::Description('Long description',4) -ne ('Long' + [char]0x2026)) { throw 'Description truncation failed' }
$properties = [LandingPage.Models.Layout.ViewProperties]::new(); $properties.ViewId = 'FeaturedGame'; $properties.FeaturedGameSettings = $settings
$copy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($properties))
if ($copy.FeaturedGameSettings.Source -ne $settings.Source -or [object]::ReferenceEquals($copy.FeaturedGameSettings,$settings)) { throw 'Featured game settings do not persist independently with the widget' }
'Success: featured selection modes, hidden/installed filters, explicit/missing pins, random stability/shuffle, descriptions and independent settings serialization.'
