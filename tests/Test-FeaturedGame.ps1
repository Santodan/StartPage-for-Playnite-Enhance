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
if (!$settings.ShowTitle -or $settings.ShowGameDetails -or $settings.ShowLibraryDetails -or $settings.ShowScores -or $settings.ShowLinks) { throw 'New sections changed existing widget defaults' }
$settings.ShowTitle = $false; $settings.ShowGameDetails = $true; $settings.ShowLibraryDetails = $true
$settings.ShowScores = $true; $settings.ShowUserScore = $false; $settings.ShowLinks = $true
$settings.ArtworkStretch = [Windows.Media.Stretch]::Uniform
$settings.UseCustomSectionLayout = $true; $settings.EditSectionLayout = $true
$sectionLayout = $settings.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Scores)
$sectionLayout.X = 321; $sectionLayout.Y = 123; $sectionLayout.Width = 432; $sectionLayout.Height = 234
$properties = [LandingPage.Models.Layout.ViewProperties]::new(); $properties.ViewId = 'FeaturedGame'; $properties.FeaturedGameSettings = $settings
$copy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($properties))
if ($copy.FeaturedGameSettings.Source -ne $settings.Source -or [object]::ReferenceEquals($copy.FeaturedGameSettings,$settings)) { throw 'Featured game settings do not persist independently with the widget' }
if ($copy.FeaturedGameSettings.ShowTitle -or !$copy.FeaturedGameSettings.ShowGameDetails -or !$copy.FeaturedGameSettings.ShowLibraryDetails -or !$copy.FeaturedGameSettings.ShowScores -or $copy.FeaturedGameSettings.ShowUserScore -or !$copy.FeaturedGameSettings.ShowLinks) { throw 'Featured section/field settings were lost on profile round-trip' }
if ($copy.FeaturedGameSettings.ArtworkStretch -ne [Windows.Media.Stretch]::Uniform) { throw 'Artwork fit mode was lost on profile round-trip' }
$sectionCopy = $copy.FeaturedGameSettings.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Scores)
if (!$copy.FeaturedGameSettings.UseCustomSectionLayout -or $copy.FeaturedGameSettings.EditSectionLayout -or $sectionCopy.X -ne 321 -or $sectionCopy.Y -ne 123 -or $sectionCopy.Width -ne 432 -or $sectionCopy.Height -ne 234 -or [object]::ReferenceEquals($sectionCopy,$sectionLayout)) { throw 'Section geometry or temporary edit state did not round-trip correctly' }
$sectionCopy.X = -999; $sectionCopy.Width = 99999
if ($sectionCopy.X -ne 0 -or $sectionCopy.Width -ne 1400 -or $sectionLayout.X -ne 321) { throw 'Section layout bounds or independent profile copies failed' }
if ($copy.FeaturedGameSettings.SectionLayouts.Count -ne 8) { throw 'Default section layouts were duplicated during serialization' }
'Success: featured selection modes, hidden/installed filters, explicit/missing pins, random stability/shuffle, descriptions and independent settings serialization.'

foreach ($mode in [Enum]::GetValues([LandingPage.Settings.FeaturedArtworkSizing])) {
    $settings.ArtworkSizing = $mode
    $sizingCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.FeaturedGameSettings]).Invoke($null,@($settings))
    if ($sizingCopy.ArtworkSizing -ne $mode) { throw ('Artwork sizing mode did not survive profile round-trip: ' + $mode) }
}
$legacyFit = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{"ArtworkStretch":2}',[LandingPage.Settings.FeaturedGameSettings])
if ($legacyFit.ArtworkSizing -ne [LandingPage.Settings.FeaturedArtworkSizing]::Fit) { throw 'Legacy Fit setting was not preserved' }
'PASS: all five artwork sizing modes persist, including existing Fit profiles.'

$settings.Source = [LandingPage.Settings.FeaturedGameSource]::HoveredGame
$settings.InstalledOnly = $false; $settings.IncludeHidden = $false
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings,$null,$false,$a.Id,$c.Id).Id -ne $c.Id) { throw 'Hovered selection failed' }
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings,$null,$false,$a.Id,$null)) { throw 'Empty hover source picked an unrelated game' }
$settings.Source = [LandingPage.Settings.FeaturedGameSource]::HoveredOrSelectedGame
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings,$null,$false,$a.Id,$c.Id).Id -ne $c.Id) { throw 'Hover did not take precedence over selection' }
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings,$null,$false,$a.Id,$null).Id -ne $a.Id) { throw 'Selected fallback failed' }
if ([LandingPage.Models.FeaturedGameSelection]::Select($library,$settings,$null,$false,$a.Id,$b.Id).Id -ne $a.Id) { throw 'Filtered hover did not fall back to selection' }
$settings.KeepLastHoveredGame = $false; $settings.HoverDelayMilliseconds = 75
$hoverCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.FeaturedGameSettings]).Invoke($null,@($settings))
if ($hoverCopy.Source -ne $settings.Source -or $hoverCopy.KeepLastHoveredGame -or $hoverCopy.HoverDelayMilliseconds -ne 75) { throw 'Hover settings did not persist' }
'PASS: hovered selection, selected fallback, filtering, empty source and hover settings persistence.'

$longDescription = 'x' * 200001
if ([LandingPage.Models.FeaturedGameSelection]::Description($longDescription,0).Length -ne 200001) { throw 'Unlimited description was truncated' }
$settings.DescriptionLength = 99999
if ($settings.DescriptionLength -ne 99999) { throw 'Large description limit was rejected' }
$descriptionParts = [LandingPage.Models.FeaturedDescription]::Parse('<p>Before</p><img src="https://example.com/a.png"><p>After</p>',0,$true)
if ($descriptionParts.Count -ne 3 -or $descriptionParts[0].Text -ne 'Before' -or !$descriptionParts[1].ImageUri -or $descriptionParts[2].Text -ne 'After') { throw 'Description text/image ordering failed' }
$withoutImages = [LandingPage.Models.FeaturedDescription]::Parse('<p>Before</p><img src="https://example.com/a.png"><p>After</p>',0,$false)
if (@($withoutImages | Where-Object ImageUri).Count -ne 0 -or !$withoutImages[0].Text.Contains('After')) { throw 'Disabling description images lost the text or retained images' }
$limitedParts = [LandingPage.Models.FeaturedDescription]::Parse('<p>Before</p><img src="https://example.com/a.png"><p>After</p>',3,$true)
if ($limitedParts.Count -ne 1 -or $limitedParts[0].Text -ne ('Bef'+[char]0x2026)) { throw 'Text limit did not stop following description images' }
$blockedParts = [LandingPage.Models.FeaturedDescription]::Parse('<script>bad</script><!--<img src="https://example.com/hidden">--><img src="javascript:bad"><p>Visible</p>',0,$true)
if (@($blockedParts | Where-Object ImageUri).Count -ne 0 -or $blockedParts[0].Text -ne 'Visible') { throw 'Non-content description markup was rendered' }
$settings.DescriptionLength = 0; $settings.ShowDescriptionImages = $true; $settings.AutoScrollContent = $true; $settings.AutoScrollSpeed = 37; $settings.PauseAutoScrollOnHover = $false
$descriptionCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.FeaturedGameSettings]).Invoke($null,@($settings))
if ($descriptionCopy.DescriptionLength -ne 0 -or !$descriptionCopy.ShowDescriptionImages -or !$descriptionCopy.AutoScrollContent -or $descriptionCopy.AutoScrollSpeed -ne 37 -or $descriptionCopy.PauseAutoScrollOnHover) { throw 'Description/scroll settings did not persist' }
'PASS: unlimited/large descriptions, optional ordered images, truncation, plain content filtering and scrolling settings persistence.'

$settings.SectionAppearance.Enabled = $true; $settings.SectionAppearance.Background = '#123456'
$styledSection = $settings.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Description)
$styledSection.OverrideAppearance = $true; $styledSection.Appearance.Enabled = $true; $styledSection.Appearance.Background = '#654321'; $styledSection.Appearance.Padding = 17
$styledCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.FeaturedGameSettings]).Invoke($null,@($settings))
$copiedSection = $styledCopy.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Description)
if (!$styledCopy.SectionAppearance.Enabled -or $styledCopy.SectionAppearance.Background -ne '#123456' -or !$copiedSection.OverrideAppearance -or $copiedSection.Appearance.Background -ne '#654321' -or $copiedSection.Appearance.Padding -ne 17) { throw 'Section appearance did not persist' }
$copiedSection.Appearance.Background = '#ABCDEF'
if ($styledSection.Appearance.Background -ne '#654321' -or $styledCopy.SectionAppearance.Background -ne '#123456') { throw 'Section appearance instances are shared across profiles or global/local scopes' }
$styledCopy.ResetSectionLayout()
if (!$styledCopy.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Description).OverrideAppearance -or $styledCopy.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Description).Appearance.Background -ne '#ABCDEF') { throw 'Resetting section positions discarded section appearance' }
'PASS: shared/local appearance persistence, independent profile copies and appearance preservation on layout reset.'
