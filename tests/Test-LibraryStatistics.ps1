$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name+'.dll'))) }
$index = [LandingPage.Models.LibraryStatisticsIndex]::new()
$options = [LandingPage.Settings.LibraryStatisticsSettings]::new()
$options.UseCustomCompletedStatuses = $true
$status = [guid]::NewGuid(); $options.CompletedStatusIds = @($status)
$first = [Playnite.SDK.Models.Game]::new(); $first.Name = 'Played'; $first.Playtime = 7200; $first.IsInstalled = $true; $first.CompletionStatusId = $status; $first.SourceId = $status; $first.PlatformIds = @($status,$status); $first.GenreIds = [Collections.Generic.List[guid]]::new(); $first.GenreIds.Add($status)
$second = [Playnite.SDK.Models.Game]::new(); $second.Name = 'Unplayed'
$hidden = [Playnite.SDK.Models.Game]::new(); $hidden.Name = 'Hidden'; $hidden.Hidden = $true; $hidden.Playtime = 3600
$dated = [Playnite.SDK.Models.Game]::new(); $dated.Name = 'Played without time'; $dated.LastActivity = [datetime]::Now
foreach ($game in @($first,$second,$hidden,$dated)) { $index.Update($game) }
$totals = $index.Calculate($options)
if ($totals.Total -ne 3 -or $totals.Installed -ne 1 -or $totals.Unplayed -ne 1 -or $totals.Completed -ne 1 -or $totals.PlaytimeSeconds -ne 7200) { throw 'Default library scope or counters are incorrect' }
if (![object]::ReferenceEquals($totals,$index.Calculate($options))) { throw 'Unchanged statistics did not reuse cached totals' }
if ($totals.Sources[$status] -ne 1 -or $totals.Platforms[$status] -ne 1 -or $totals.Genres[$status] -ne 1 -or $totals.CompletionStatuses[$status] -ne 1) { throw 'Chart groups are missing or double-count repeated metadata IDs' }
if ($totals.SourcePlaytime[$status] -ne 7200 -or $totals.PlatformPlaytime[$status] -ne 7200 -or $totals.GenrePlaytime[$status] -ne 7200 -or $totals.CompletedPlaytime -ne 7200 -or $totals.InstalledPlaytime -ne 7200 -or $totals.GamesWithPlaytime -ne 1 -or $totals.PlaytimeRangeCounts[2] -ne 1 -or $totals.PlaytimeRangeSeconds[2] -ne 7200) { throw 'Playtime aggregation, duplicate metadata handling, average denominator or range counts failed' }
$options.Breakdown = [LandingPage.Settings.StatisticsBreakdown]::Games
$gameTotals = $index.Calculate($options)
if($gameTotals.GamePlaytime[$first.Id] -ne 7200 -or $gameTotals.GameNames[$first.Id] -ne 'Played') { throw 'Per-game playtime did not aggregate' }
$options.IncludeHidden = $true
if ($index.Calculate($options).Total -ne 4 -or $index.Calculate($options).PlaytimeSeconds -ne 10800) { throw 'Hidden scope failed' }
$options.InstalledOnly = $true
if ($index.Calculate($options).Total -ne 1) { throw 'Installed scope failed' }
$first.IsInstalled = $false; $first.Playtime = 0; $first.CompletionStatusId = [guid]::Empty
$index.Update($first); $index.Update($first)
if ($index.Calculate($options).Total -ne 0) { throw 'Changed game was double-counted or failed to leave filtered scope' }
$options.InstalledOnly = $false; $index.Remove($second.Id)
if ($index.Calculate($options).Total -ne 3 -or $index.Calculate($options).Completed -ne 0 -or $index.Calculate($options).Unplayed -ne 1) { throw 'Updates/removals retained stale statistics' }
$props = [LandingPage.Models.Layout.ViewProperties]::new(); $props.ViewId = 'LibraryStatistics'; $props.LibraryStatisticsSettings = $options
$options.Rows = $true; $options.LabelSize = 23; $options.ChartStyle = [LandingPage.Settings.StatisticsChartStyle]::Doughnut; $options.Breakdown = [LandingPage.Settings.StatisticsBreakdown]::Sources; $options.CustomChartColors = $true; $options.ChartColor1 = '#123456'
$copy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($props))
if ($copy.LibraryStatisticsSettings.ChartStyle -ne [LandingPage.Settings.StatisticsChartStyle]::Doughnut -or $copy.LibraryStatisticsSettings.ChartColor1 -ne '#123456' -or !$copy.LibraryStatisticsSettings.Rows -or $copy.LibraryStatisticsSettings.LabelSize -ne 23 -or $copy.LibraryStatisticsSettings.CompletedStatusIds[0] -ne $status) { throw 'Statistics settings did not survive profile cloning' }
$copy.LibraryStatisticsSettings.CompletedStatusIds[0] = [guid]::NewGuid()
if ($options.CompletedStatusIds[0] -ne $status) { throw 'Profile shares completion status array with original' }
$props.ViewId = 'TextWidget'
if ([Newtonsoft.Json.JsonConvert]::SerializeObject($props).Contains('LibraryStatisticsSettings')) { throw 'Unrelated widget serialization grew statistics settings' }
$options.ValueSize = [double]::NaN; $options.Spacing = -20
if ($options.ValueSize -lt 8 -or $options.Spacing -ne 0) { throw 'Invalid dimensions were accepted' }
$empty = [LandingPage.Models.LibraryStatisticsIndex]::new()
if ($empty.Calculate($options).Total -ne 0 -or $empty.Calculate($options).PlaytimeSeconds -ne 0) { throw 'Empty library failed' }
'PASS: statistics scope, completion selection, playtime, never-played definition, incremental updates/removals, empty library and independent profile persistence.'

$chartProps = [LandingPage.Models.Layout.ViewProperties]::new(); $chartProps.ViewId = 'LibraryChart'
if(!$chartProps.LibraryStatisticsSettings.ShowChart -or $chartProps.LibraryStatisticsSettings.ShowTotal -or $chartProps.LibraryStatisticsSettings.ShowPlaytime -or $chartProps.LibraryStatisticsSettings.ShowCompleted) { throw 'Chart widget defaults include unwanted counters' }
$chartProps.LibraryStatisticsSettings.Metric = [LandingPage.Settings.StatisticsMetric]::Playtime
$chartProps.LibraryStatisticsSettings.LabelPosition = [LandingPage.Settings.StatisticsLabelPosition]::BesideSegments
$chartProps.LibraryStatisticsSettings.LegendPosition = [LandingPage.Settings.StatisticsLegendPosition]::Right
$chartProps.LibraryStatisticsSettings.LegendAlignment = [Windows.HorizontalAlignment]::Right
$chartProps.LibraryStatisticsSettings.LegendVerticalAlignment = [Windows.VerticalAlignment]::Bottom
$chartCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($chartProps))
if($chartCopy.LibraryStatisticsSettings.Metric -ne [LandingPage.Settings.StatisticsMetric]::Playtime -or $chartCopy.LibraryStatisticsSettings.LabelPosition -ne [LandingPage.Settings.StatisticsLabelPosition]::BesideSegments -or $chartCopy.LibraryStatisticsSettings.LegendPosition -ne [LandingPage.Settings.StatisticsLegendPosition]::Right -or $chartCopy.LibraryStatisticsSettings.LegendAlignment -ne [Windows.HorizontalAlignment]::Right -or $chartCopy.LibraryStatisticsSettings.LegendVerticalAlignment -ne [Windows.VerticalAlignment]::Bottom) { throw 'Chart metric/label settings did not survive profile cloning' }
$chartCopy.LibraryStatisticsSettings.LabelWidth = 231
if($chartProps.LibraryStatisticsSettings.LabelWidth -eq 231 -or [object]::ReferenceEquals($chartProps.LibraryStatisticsSettings,$props.LibraryStatisticsSettings)) { throw 'Chart instances share settings' }
if([LandingPage.Settings.LibraryStatisticsSettings]::new().ShowChart) { throw 'New statistics widgets should default to counters' }
'PASS: playtime grouping and ranges, independent chart defaults, metric/label placement/alignment persistence and isolated copies.'

$auto = [LandingPage.Settings.LibraryStatisticsSettings]::new()
$completedStatus = [Playnite.SDK.Models.CompletionStatus]::new(); $completedStatus.Name = 'Completed'
$beatenStatus = [Playnite.SDK.Models.CompletionStatus]::new(); $beatenStatus.Name = 'Beaten'
$statuses = [Collections.Generic.List[Playnite.SDK.Models.CompletionStatus]]::new(); $statuses.Add($completedStatus); $statuses.Add($beatenStatus)
$auto.CompletedStatusIds = @($beatenStatus.Id)
$autoIds = [LandingPage.Models.LibraryStatisticsIndex]::ResolveCompletedStatuses($auto,$statuses)
if($autoIds.Count -ne 1 -or $autoIds[0] -ne $completedStatus.Id) { throw 'Automatic completion did not choose only Completed or used old manual selections' }
$autoIndex = [LandingPage.Models.LibraryStatisticsIndex]::new()
$completedGame = [Playnite.SDK.Models.Game]::new(); $completedGame.CompletionStatusId = $completedStatus.Id; $completedGame.Playtime = 3600
$beatenGame = [Playnite.SDK.Models.Game]::new(); $beatenGame.CompletionStatusId = $beatenStatus.Id; $beatenGame.Playtime = 7200
$autoIndex.Update($completedGame); $autoIndex.Update($beatenGame)
$autoTotals = $autoIndex.Calculate($auto,$autoIds)
if($autoTotals.Completed -ne 1 -or $autoTotals.CompletedPlaytime -ne 3600) { throw 'Automatic Completed counter/playtime includes Beaten games' }
$auto.UseCustomCompletedStatuses = $true
$overrideIds = [LandingPage.Models.LibraryStatisticsIndex]::ResolveCompletedStatuses($auto,$statuses)
if($overrideIds.Count -ne 1 -or $overrideIds[0] -ne $beatenStatus.Id -or $autoIndex.Calculate($auto,$overrideIds).CompletedPlaytime -ne 7200) { throw 'Explicit custom completion override failed' }
$auto.UseCustomCompletedStatuses = $false; $completedStatus.Name = ' Completed '
if([LandingPage.Models.LibraryStatisticsIndex]::ResolveCompletedStatuses($auto,$statuses).Count -ne 1) { throw 'Automatic matching did not normalize whitespace' }
'PASS: Completed detected automatically, Beaten excluded, old manual lists ignored until explicitly enabled, correct completion playtime and opt-in custom override.'

$auto.UseCustomCompletedStatuses = $true
$chartAutoIds = [LandingPage.Models.LibraryStatisticsIndex]::ResolveCompletedStatuses($auto,$statuses,$true)
if($chartAutoIds.Count -ne 1 -or $chartAutoIds[0] -ne $completedStatus.Id) { throw 'Chart still uses a hidden custom completion override' }
'PASS: charts always use automatic Completed detection, independent of old custom mappings.'

$multi = [LandingPage.Settings.LibraryStatisticsSettings]::CreateChartSettings(); $multi.LabelBold = $true; $multi.LabelItalic = $true; $multi.LabelUnderline = $true; $multi.LabelFontFamily = 'Consolas'; $multi.CustomLabelColor = $true; $multi.LabelColor = '#123456'
$multi.ChartColumns = 2; $multi.UseCounterGrid = $true; $multi.CounterRows = 4; $multi.CounterColumns = 3; $multi.CounterPlacements[0].Row = 3; $multi.CounterPlacements[0].Column = 2
$extra = [LandingPage.Settings.LibraryStatisticsSettings]::CreateChartSettings(); $extra.ChartName = 'Extra'; $extra.ChartTitle = 'Independent'; $extra.Metric = [LandingPage.Settings.StatisticsMetric]::Playtime; $multi.ExtraCharts.Add($extra)
$multiCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($multi))
if($multiCopy.ExtraCharts.Count -ne 1 -or $multiCopy.ExtraCharts[0].ChartTitle -ne 'Independent' -or !$multiCopy.LabelBold -or $multiCopy.LabelFontFamily -ne 'Consolas' -or $multiCopy.LabelColor -ne '#123456' -or $multiCopy.CounterPlacements[0].Row -ne 3 -or $multiCopy.CounterPlacements[0].Column -ne 2 -or $multiCopy.ChartColumns -ne 2) { throw 'Multiple charts, label styling or counter layout did not persist' }
$multiCopy.ExtraCharts[0].ChartTitle = 'Changed'; $multiCopy.CounterPlacements[0].Row = 4
if($multi.ExtraCharts[0].ChartTitle -ne 'Independent' -or $multi.CounterPlacements[0].Row -ne 3) { throw 'Profile clones share nested charts or counter positions' }
$multiCopy.ResetCounterPositions(); if($multiCopy.CounterPlacements.Count -ne [enum]::GetValues([LandingPage.Settings.StatisticsCounter]).Count -or $multiCopy.CounterPlacements[0].Row -ne 1) { throw 'Counter placement reset failed' }
'PASS: label typography/color, multiple independent charts, chart columns, explicit counter grid and nested profile/reset persistence.'

$visual = [LandingPage.Settings.LibraryStatisticsSettings]::CreateChartSettings(); $visual.ChartStyle = [LandingPage.Settings.StatisticsChartStyle]::VerticalBars; $visual.ChartWidth = 700; $visual.ChartScale = 1.5; $visual.ChartRotation = 120; $visual.OutsideLabelDistance = 40; $visual.LabelOffsetX = 15; $visual.LabelOffsetY = -10; $visual.ConnectorThickness = 3; $visual.ConnectorStyle = [LandingPage.Settings.ChartConnectorStyle]::Straight; $visual.CustomConnectorColor = $true; $visual.ConnectorColor = '#ABCDEF'
$offset = [LandingPage.Settings.ChartLabelOffset]::new(); $offset.Key = 'test'; $offset.Name = 'Test label'; $offset.X = 23; $offset.Y = -7; $visual.LabelOffsets.Add($offset)
$visualCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($visual))
if($visualCopy.ChartStyle -ne [LandingPage.Settings.StatisticsChartStyle]::VerticalBars -or $visualCopy.ChartWidth -ne 700 -or $visualCopy.ChartScale -ne 1.5 -or $visualCopy.ChartRotation -ne 120 -or $visualCopy.LabelOffsets[0].X -ne 23 -or $visualCopy.ConnectorColor -ne '#ABCDEF') { throw 'Sizing, rotation or label/connector customization did not survive profile cloning' }
$visualCopy.LabelOffsets[0].X = 50; if($visual.LabelOffsets[0].X -ne 23) { throw 'Label offsets are shared between cloned profiles' }
'PASS: vertical chart type, sizing/rotation and independent label/connector settings persist in profiles.'

$visual.EditCharts = $true
$editClone = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($visual))
if($editClone.EditCharts) { throw 'Temporary chart-edit mode was persisted in a profile' }
'PASS: chart-edit mode remains temporary while size/label choices persist.'

$visual.GlossySeries = $false; $visual.ChartAnimations = $true; $visual.ShowChartAxes = $false; $visual.ShowChartGrid = $false; $visual.SliceSeparatorWidth = 2.5
$rendererClone = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($visual))
if($rendererClone.GlossySeries -or !$rendererClone.ChartAnimations -or $rendererClone.ShowChartAxes -or $rendererClone.ShowChartGrid -or $rendererClone.SliceSeparatorWidth -ne 2.5) { throw 'Renderer appearance options did not survive profile cloning' }
'PASS: LiveCharts appearance options persist independently with existing geometry and label settings.'

$visual.CustomSegmentBorderColor = $true; $visual.SegmentBorderColor = '#2266AA'; $visual.SliceSeparatorWidth = 3
$borderCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($visual))
if(!$borderCopy.CustomSegmentBorderColor -or $borderCopy.SegmentBorderColor -ne '#2266AA' -or $borderCopy.SliceSeparatorWidth -ne 3) { throw 'Segment border settings did not survive profile cloning' }
'PASS: segment border color and width persist independently in chart settings.'

$text = [LandingPage.Settings.LibraryStatisticsSettings]::new(); $text.ValueFontFamily = 'Consolas'; $text.ValueBold = $true; $text.ValueItalic = $true; $text.ValueUnderline = $true; $text.ValueStrikeThrough = $true
$text.ValueColorMode = [LandingPage.Settings.StatisticsValueColor]::Custom; $text.ValueColor = '#1177AA'; $text.ValueOpacity = .7; $text.ValueSizePreset = [LandingPage.Settings.StatisticsTextSize]::ThemeLargest
$text.LabelSizePreset = [LandingPage.Settings.StatisticsTextSize]::ThemeLarge; $text.LabelStrikeThrough = $true; $text.LabelOpacity = .8; $text.LabelLineHeight = 24; $text.WrapCounterText = $false; $text.CounterTextTrimming = [Windows.TextTrimming]::CharacterEllipsis
$text.InheritTextRendering = $false; $text.TextFormatting = [Windows.Media.TextFormattingMode]::Display; $text.TextRendering = [Windows.Media.TextRenderingMode]::Grayscale
$typography = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($text))
if($typography.ValueFontFamily -ne 'Consolas' -or !$typography.ValueBold -or !$typography.ValueItalic -or !$typography.ValueUnderline -or !$typography.ValueStrikeThrough -or $typography.ValueColor -ne '#1177AA' -or $typography.ValueColorMode -ne $text.ValueColorMode -or $typography.ValueOpacity -ne .7 -or $typography.LabelOpacity -ne .8 -or !$typography.LabelStrikeThrough -or $typography.LabelLineHeight -ne 24 -or $typography.WrapCounterText -or $typography.CounterTextTrimming -ne $text.CounterTextTrimming -or $typography.InheritTextRendering -or $typography.TextRendering -ne $text.TextRendering -or $typography.ValueSizePreset -ne $text.ValueSizePreset -or $typography.LabelSizePreset -ne $text.LabelSizePreset) { throw 'Statistic text customization did not survive profile cloning' }
'PASS: independent value/label typography, theme size presets, color modes, wrapping/trimming and rendering settings survive profiles.'

$extended = [LandingPage.Models.LibraryStatisticsIndex]::new(); $settingsEx = [LandingPage.Settings.LibraryStatisticsSettings]::new()
$sourceA = [guid]::NewGuid(); $sourceB = [guid]::NewGuid(); $category = [guid]::NewGuid(); $tag = [guid]::NewGuid(); $company = [guid]::NewGuid()
$alpha = [Playnite.SDK.Models.Game]::new(); $alpha.Name = 'Alpha'; $alpha.SourceId = $sourceA; $alpha.Playtime = 3600; $alpha.PlayCount = 3; $alpha.IsInstalled = $true; $alpha.Favorite = $true; $alpha.CoverImage = 'cover.jpg'; $alpha.Description = 'Description'; $alpha.DeveloperIds = @($company,$company); $alpha.PublisherIds = [Collections.Generic.List[guid]]::new([guid[]]@($company)); $alpha.TagIds = @($tag,$tag); $alpha.CategoryIds = [Collections.Generic.List[guid]]::new([guid[]]@($category)); $alpha.FeatureIds = [Collections.Generic.List[guid]]::new([guid[]]@($tag))
$beta = [Playnite.SDK.Models.Game]::new(); $beta.Name = 'Beta'; $beta.SourceId = $sourceB; $beta.Playtime = 10800; $beta.PlayCount = 2
$gamma = [Playnite.SDK.Models.Game]::new(); $gamma.Name = 'Gamma'; $gamma.Hidden = $true; $gamma.Playtime = 7200; $gamma.Favorite = $true
foreach($g in @($alpha,$beta,$gamma)) { $extended.Update($g) }
$ex = $extended.Calculate($settingsEx)
if($ex.Total -ne 2 -or $ex.Favorites -ne 1 -or $ex.Hidden -ne 0 -or $ex.TotalLaunches -ne 5 -or $ex.MedianPlaytimeSeconds -ne 7200 -or $ex.LongestPlaytimeSeconds -ne 10800 -or $ex.MostPlayedGameName -ne 'Beta') { throw 'Extended counts/playtime summaries ignored scope or median/longest rules' }
if($ex.Tags[$tag] -ne 1 -or $ex.Developers[$company] -ne 1 -or $ex.Publishers[$company] -ne 1 -or $ex.TagPlaytime[$tag] -ne 3600 -or $ex.Categories[$category] -ne 1 -or $ex.Features[$tag] -ne 1 -or $ex.Tags[[guid]::Empty] -ne 1) { throw 'Extended metadata grouping duplicated memberships or lost missing groups/playtime' }
if($ex.MissingCover -ne 1 -or $ex.MissingDescription -ne 1 -or $ex.MissingIcon -ne 2 -or $ex.NoReleaseDate -ne 2 -or $ex.NeverLaunched -ne 0) { throw 'Metadata coverage/launch counters are incorrect' }
$settingsEx.IncludeHidden = $true; $allEx = $extended.Calculate($settingsEx)
if($allEx.Hidden -ne 1 -or $allEx.Favorites -ne 2 -or $allEx.NeverLaunched -ne 1 -or $allEx.MedianPlaytimeSeconds -ne 7200) { throw 'Hidden-inclusive extended statistics failed' }
$settingsEx.InstalledOnly = $true; if($extended.Calculate($settingsEx).Total -ne 1) { throw 'Installed-only scope did not apply to extended totals' }
$extended.Remove($beta.Id); $settingsEx.InstalledOnly = $false; $settingsEx.IncludeHidden = $false
if($extended.Calculate($settingsEx).MostPlayedGameName -ne 'Alpha') { throw 'Removing most-played game left stale cached leader' }
$settingsEx.AdditionalCounters[0].Enabled = $true; $settingsEx.StatisticGroups[0].Enabled = $true; $settingsEx.StatisticGroups[0].ShowPercentages = $true; $settingsEx.StatisticGroups[0].Metric = [LandingPage.Settings.StatisticsMetric]::Playtime
$copyEx = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($settingsEx))
if(!$copyEx.AdditionalCounters[0].Enabled -or !$copyEx.StatisticGroups[0].Enabled -or !$copyEx.StatisticGroups[0].ShowPercentages -or $copyEx.StatisticGroups[0].Metric -ne [LandingPage.Settings.StatisticsMetric]::Playtime) { throw 'New statistic choices and grouped display settings did not persist' }
$copyEx.AdditionalCounters[0].Enabled = $false; if(!$settingsEx.AdditionalCounters[0].Enabled) { throw 'Profile copies share statistic options' }
$legacyEx = [LandingPage.Settings.LibraryStatisticsSettings]::new(); $legacyEx.CounterPlacements.Clear(); $legacyPlacement = [LandingPage.Settings.CounterPlacement]::new(); $legacyPlacement.Counter = [LandingPage.Settings.StatisticsCounter]::Total; $legacyPlacement.Row = 3; $legacyPlacement.Column = 2; $legacyEx.CounterPlacements.Add($legacyPlacement); $legacyEx.EnsureStatisticOptions()
if($legacyEx.CounterPlacements.Count -ne [enum]::GetValues([LandingPage.Settings.StatisticsCounter]).Count -or $legacyEx.CounterPlacements[0].Row -ne 3 -or $legacyEx.CounterPlacements[0].Column -ne 2 -or @($legacyEx.AdditionalCounters | Where-Object Enabled).Count -ne 0) { throw 'Old counter placement migration changed existing choices or enabled new statistics' }
'PASS: expanded counters, distinct metadata memberships, missing metadata, lifetime playtime summaries, all scopes, incremental removal, profile isolation and legacy grid migration.'

$selection = $settingsEx.StatisticGroups[0]; $selection.SelectedOnly = $true; $selection.SelectedId = $sourceA; $selection.ShowIcon = $true; $selection.ShowName = $false; $selection.ShowHeading = $false; $selection.IconSize = 37
$choice = [LandingPage.Settings.StatisticSelectionChoice]::new(); $choice.Id = $sourceA; $choice.Name = 'Runtime metadata choice'; $selection.Choices.Add($choice)
$selectedCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($settingsEx))
$sc = $selectedCopy.StatisticGroups[0]
if(!$sc.SelectedOnly -or $sc.SelectedId -ne $sourceA -or !$sc.ShowIcon -or $sc.ShowName -or $sc.ShowHeading -or $sc.IconSize -ne 37 -or $sc.Choices.Count -ne 0) { throw 'Selected source/icon preferences were lost or runtime choices leaked into profiles' }
'PASS: selected source/platform and independent text/icon/heading preferences persist without serializing dropdown choices.'

$colorSettings = [LandingPage.Settings.LibraryStatisticsSettings]::new(); $colors = $colorSettings.CounterPlacements[0].Colors; $colors.UseGlobalValueColor = $false; $colors.ValueColor = '#114488'; $colors.UseGlobalLabelColor = $false; $colors.LabelColor = '#AA5522'
$colorSettings.CounterPlacements[0].Row = 9; $colorSettings.ResetCounterPositions()
if($colorSettings.CounterPlacements[0].Row -ne 1 -or ![object]::ReferenceEquals($colors,$colorSettings.CounterPlacements[0].Colors)) { throw 'Resetting grid positions erased per-statistic colors' }
$colorCopy = [LandingPage.Models.DashboardProfiles].GetMethod('Clone').MakeGenericMethod([LandingPage.Settings.LibraryStatisticsSettings]).Invoke($null,@($colorSettings))
if($colorCopy.CounterPlacements[0].Colors.UseGlobalValueColor -or $colorCopy.CounterPlacements[0].Colors.ValueColor -ne '#FF114488' -or $colorCopy.CounterPlacements[0].Colors.LabelColor -ne '#FFAA5522') { throw 'Individual statistic colors did not survive a profile copy' }
$colorCopy.CounterPlacements[0].Colors.ValueColor = '#FFFFFF'; if($colors.ValueColor -ne '#FF114488') { throw 'Individual statistic colors are shared across profiles' }
'PASS: per-statistic global/custom color modes persist independently and survive position resets.'
