# Use Playnite's actual serializer/resolver, rather than Json.NET defaults.
# powershell.exe -STA -File tests/Test-SettingsSerialization.ps1
param([string]$PlayniteDirectory = 'E:\Programs\Playnite')
$ErrorActionPreference = 'Stop'
# Playnite's desktop assembly targets x86. Use a matching process for this test.
if ([Environment]::Is64BitProcess) {
    & "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -STA -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -PlayniteDirectory $PlayniteDirectory
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
try {
    $serializerField.SetValue($null, [Playnite.Common.DataSerializer]::new())
    $pageCloneMethod = [Playnite.SDK.Data.Serialization].GetMethods() | Where-Object { $_.Name -eq 'GetClone' -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1
    $dividerProps = [LandingPage.Models.Layout.ViewProperties]::new(); $dividerProps.ViewId = 'DividerWidget'
    $dividerProps.DividerSettings.Mode = 'Spacer'; $dividerProps.DividerSettings.Orientation = 'Vertical'; $dividerProps.DividerSettings.SpaceSize = 57
    $dividerProps.DividerSettings.ConnectEnds = $true; $dividerProps.DividerSettings.ConnectStart = $false; $dividerProps.DividerSettings.ConnectionRange = 175
    $dividerProps.DividerSettings.Decoration = 'DoubleWave'; $dividerProps.DividerSettings.PatternSize = 13; $dividerProps.DividerSettings.PatternSpacing = 37; $dividerProps.DividerSettings.DashGap = 7; $dividerProps.DividerSettings.FillShapes = $true
    $dividerProps.DividerSettings.LineStyle = 'Dotted'; $dividerProps.DividerSettings.Color = '#12ABEF'; $dividerProps.DividerSettings.CustomColor = $true
    $dividerClone = $pageCloneMethod.MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($dividerProps))
    if($dividerClone.DividerSettings.Mode -ne 'Spacer' -or $dividerClone.DividerSettings.Orientation -ne 'Vertical' -or $dividerClone.DividerSettings.SpaceSize -ne 57 -or $dividerClone.DividerSettings.Color -ne '#12ABEF' -or $dividerClone.DividerSettings.LineStyle -ne 'Dotted' -or !$dividerClone.DividerSettings.ConnectEnds -or $dividerClone.DividerSettings.ConnectStart -or $dividerClone.DividerSettings.ConnectionRange -ne 175 -or $dividerClone.DividerSettings.Decoration -ne 'DoubleWave' -or $dividerClone.DividerSettings.PatternSize -ne 13 -or $dividerClone.DividerSettings.PatternSpacing -ne 37 -or $dividerClone.DividerSettings.DashGap -ne 7 -or !$dividerClone.DividerSettings.FillShapes) { throw 'Playnite serializer lost divider settings' }
    $dividerClone.DividerSettings.SpaceSize = 19
    if($dividerProps.DividerSettings.SpaceSize -ne 57) { throw 'Divider settings share mutable profile data' }
    'PASS: actual Playnite serializer preserves independent divider/spacer mode, orientation, spacing, style and color.'
    $shortcutProps = [LandingPage.Models.Layout.ViewProperties]::new(); $shortcutProps.ViewId = 'ShortcutWidget'
    $shortcutProps.ShortcutSettings.FontSize = 19; $shortcutProps.ShortcutSettings.CustomBackground = $true
    $shortcutEntry = [LandingPage.Settings.ShortcutEntry]::new(); $shortcutEntry.Kind = 'Application'; $shortcutEntry.Target = 'C:\Example\app.exe'; $shortcutEntry.Arguments = '--flag "a b"'; $shortcutEntry.WorkingDirectory = 'C:\Example'; $shortcutEntry.Label = 'App'
    $shortcutProps.ShortcutSettings.Items.Add($shortcutEntry)
    $shortcutRoundTrip = $pageCloneMethod.MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($shortcutProps))
    if($shortcutRoundTrip.ShortcutSettings.Items.Count -ne 1 -or $shortcutRoundTrip.ShortcutSettings.Items[0].Arguments -ne '--flag "a b"' -or $shortcutRoundTrip.ShortcutSettings.FontSize -ne 19 -or !$shortcutRoundTrip.ShortcutSettings.CustomBackground) { throw 'Playnite serializer lost shortcut configuration' }
    $shortcutRoundTrip.ShortcutSettings.Items[0].Label = 'Changed clone'
    if($shortcutProps.ShortcutSettings.Items[0].Label -ne 'App') { throw 'Shortcut clone shares original entries' }
    'PASS: actual Playnite serializer preserves independent shortcut entries, launch fields and appearance.'
    $achievementProps = [LandingPage.Models.Layout.ViewProperties]::new(); $achievementProps.ViewId = 'AchievementGoals'
    $achievementProps.AchievementSettings.TitleSize = 33; $achievementProps.AchievementSettings.ProgressColor = '#22CC88'
    $goal = [LandingPage.Settings.AchievementGoal]::new(); $goal.GameId = [guid]::NewGuid(); $goal.GameName = 'Goal fixture'; $goal.TargetPercent = 85
    $achievementProps.AchievementSettings.Goals.Add($goal)
    $achievementClone = $pageCloneMethod.MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($achievementProps))
    if ($achievementClone.AchievementSettings.Goals.Count -ne 1 -or $achievementClone.AchievementSettings.Goals[0].TargetPercent -ne 85 -or $achievementClone.AchievementSettings.TitleSize -ne 33 -or $achievementClone.AchievementSettings.ProgressColor -ne '#FF22CC88') { throw 'Actual Playnite serializer lost achievement settings/goals' }
    'PASS: actual Playnite serializer preserves achievement card styling and individual percentage goals.'
    $recentProps = [LandingPage.Models.Layout.ViewProperties]::new(); $recentProps.ViewId = 'RecentAchivements'
    $recentProps.AchievementSettings.Limit = 37; $recentProps.AchievementSettings.PerGameLimit = 7; $recentProps.AchievementSettings.RecentDays = 14; $recentProps.AchievementSettings.GroupByGame = $false
    $recentProps.AchievementSettings.TextStyles['Description'].UseDefaults = $false; $recentProps.AchievementSettings.TextStyles['Description'].Size = 24; $recentProps.AchievementSettings.TextStyles['Description'].Bold = $true
    $recentClone = $pageCloneMethod.MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($recentProps))
    if ($recentClone.AchievementSettings.Limit -ne 37 -or $recentClone.AchievementSettings.PerGameLimit -ne 7 -or $recentClone.AchievementSettings.RecentDays -ne 14 -or $recentClone.AchievementSettings.GroupByGame) { throw 'Recent achievement appearance settings did not persist' }
    if ($recentClone.AchievementSettings.TextStyles['Description'].UseDefaults -or $recentClone.AchievementSettings.TextStyles['Description'].Size -ne 24 -or !$recentClone.AchievementSettings.TextStyles['Description'].Bold) { throw 'Independent achievement text formatting was not serialized' }
    'PASS: actual Playnite serializer retains recent achievement widget counts, date range and grouping.'

    $defaultPage = [LandingPage.Models.DashboardPage]::new()
    $defaultCopy = $pageCloneMethod.MakeGenericMethod([LandingPage.Models.DashboardPage]).Invoke($null,@($defaultPage))
    if ($defaultCopy.Layout.Children.Count -ne 2) { throw 'Playnite serializer appended default panels to a saved page' }
    $defaultPage.Layout = [LandingPage.Models.Layout.GridNode]::new()
    $defaultPage.Layout.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $defaultPage.Layout.ViewProperties.ViewId = 'TextWidget'
    $customCopy = $pageCloneMethod.MakeGenericMethod([LandingPage.Models.DashboardPage]).Invoke($null,@($defaultPage))
    if ($customCopy.Layout.Children.Count -ne 0 -or $customCopy.Layout.ViewProperties.ViewId -ne 'TextWidget') { throw 'Playnite serializer injected a default toolbar into an existing custom layout' }
    'PASS: actual Playnite serializer replaces page defaults without duplicating panels or changing custom layouts.'
    $detailProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $detailProperties.ViewId = 'GameDetailDescription'
    $detailProperties.GameDetailSettings.Source = [LandingPage.Settings.FeaturedGameSource]::HoveredGame
    $detailProperties.GameDetailSettings.DescriptionLength = 0; $detailProperties.GameDetailSettings.TextSize = 29
    $detailProperties.Appearance.Enabled = $true; $detailProperties.Appearance.Background = '#123456'
    $detailCloneMethod = [Playnite.SDK.Data.Serialization].GetMethods() | Where-Object { $_.Name -eq 'GetClone' -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1
    $actualDetailCopy = $detailCloneMethod.MakeGenericMethod([LandingPage.Models.Layout.ViewProperties]).Invoke($null,@($detailProperties))
    if ($actualDetailCopy.GameDetailSettings.Source -ne [LandingPage.Settings.FeaturedGameSource]::HoveredGame -or $actualDetailCopy.GameDetailSettings.TextSize -ne 29 -or $actualDetailCopy.GameDetailSettings.DescriptionLength -ne 0 -or $actualDetailCopy.Appearance.Background -ne '#123456') { throw 'Actual Playnite settings serialization lost independent detail widget configuration' }
    $settings = [LandingPage.LandingPageSettings]::new()
    $mostOptions = [Collections.ObjectModel.ObservableCollection[LandingPage.Models.GameActivity.MostPlayedOptions]]::new()
    foreach ($timeframe in @('Last14Days','Last3Month','Last6Month','LastYear')) {
        $option = [LandingPage.Models.GameActivity.MostPlayedOptions]::new()
        $option.Timeframe = [Enum]::Parse([LandingPage.Models.GameActivity.Timeframe],$timeframe)
        $mostOptions.Add($option)
    }
    [LandingPage.LandingPageSettings].GetProperty('MostPlayedOptions').SetValue($settings,$mostOptions)
    [LandingPage.LandingPageSettings].GetProperty('DashboardTransition').SetValue($settings,[LandingPage.DashboardTransition]::Slide)
    [LandingPage.LandingPageSettings].GetProperty('DashboardSlideDirection').SetValue($settings,[LandingPage.DashboardSlideDirection]::Up)
    [LandingPage.LandingPageSettings].GetProperty('DashboardTransitionMilliseconds').SetValue($settings,350)
    [LandingPage.LandingPageSettings].GetProperty('EnableDashboardDiagnostics').SetValue($settings,$true)
    $customMotion = [LandingPage.Models.DashboardTransitionPreset]::new(); $customMotion.Name = 'Persisted custom transition'
    $customMotion.Incoming.StartX = 17; $customMotion.Outgoing.DelayMs = 30
    [LandingPage.LandingPageSettings].GetProperty('DashboardCustomTransition').SetValue($settings,$customMotion)
    [LandingPage.LandingPageSettings].GetProperty('DashboardTransitionPresets').GetValue($settings).Add([LandingPage.Models.TransitionPresets]::Clone($customMotion))

    $rules = [LandingPage.Models.ShelveProperties]::new()
    $rules.IncludedNamePatterns = '*Quest*'; $rules.ExcludedInstallPathPatterns = 'D:\Games\Demos\*'
    $rules.IncludedPublisherNamePatterns = 'glob:*[A-Z]*'
    $rules.NeverPlayedOnly = $true
    $rules.HoverEffect = [LandingPage.Models.ShelfHoverEffect]::Dock; $rules.HoverScale = 1.35
    $rules.HoverGlow = $true; $rules.HoverGlowColor = '#123456'; $rules.HoverDuration = 220

    $shelves = [Collections.ObjectModel.ObservableCollection[LandingPage.Models.ShelveProperties]]::new(); $shelves.Add($rules)
    [LandingPage.LandingPageSettings].GetProperty('ShelveProperties').SetValue($settings,$shelves)
    $instanceSettings = [Collections.Generic.Dictionary[guid,LandingPage.Settings.ShelvesSettings]]::new()
    $instance = [LandingPage.Settings.ShelvesSettings]::new(); $instance.ShelveProperties = $shelves
    $instanceSettings.Add([guid]::NewGuid(),$instance)
    [LandingPage.LandingPageSettings].GetProperty('ShelveInstanceSettings').SetValue($settings,$instanceSettings)
    $page = [LandingPage.Models.DashboardPages]::Ensure($settings)
    $properties = [LandingPage.Models.Layout.ViewProperties]::new(); $properties.TextSettings.Text = 'Preserve page widget'
    $properties.FeaturedGameSettings.Source = [LandingPage.Settings.FeaturedGameSource]::Pinned
    $properties.FeaturedGameSettings.PinnedGameId = [guid]::NewGuid()
    $properties.FeaturedGameSettings.TitleSize = 42
    $properties.FeaturedGameSettings.MatchMetadataTextSize = $false; $properties.FeaturedGameSettings.MetadataTextSize = 19
    $properties.FeaturedGameSettings.MatchMetadataTextColor = $false; $properties.FeaturedGameSettings.MetadataTextColor = '#123456'
    $properties.FeaturedGameSettings.MatchMetadataIconSize = $false; $properties.FeaturedGameSettings.MetadataIconSize = 25
    $properties.FeaturedGameSettings.UseCustomSectionLayout = $true; $properties.FeaturedGameSettings.EditSectionLayout = $true
    $sectionLayout = $properties.FeaturedGameSettings.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Description)
    $sectionLayout.X = 90; $sectionLayout.Y = 75; $sectionLayout.Width = 345; $sectionLayout.Height = 123
    $properties.FeaturedGameSettings.SectionAppearance.Enabled = $true; $properties.FeaturedGameSettings.SectionAppearance.Background = '#112233'
    $sectionLayout.OverrideAppearance = $true; $sectionLayout.Appearance.Enabled = $true; $sectionLayout.Appearance.Background = '#334455'
    $featuredPin = $properties.FeaturedGameSettings.PinnedGameId
    $properties.ToolbarSettings.NotificationBadgeStyle = [LandingPage.Settings.NotificationBadgeStyle]::Count
    $toolbarEntry = [LandingPage.Settings.ToolbarEntry]::new(); $toolbarEntry.Id = 'native.sort'; $properties.ToolbarSettings.Items.Add($toolbarEntry)
    $properties.ToolbarSettings.Groups[0].WidthSizing = [LandingPage.Settings.ToolbarGroupSizing]::Fixed; $properties.ToolbarSettings.Groups[0].Width = 280
    $properties.ToolbarSettings.Groups[0].ShowBackground = $true; $properties.ToolbarSettings.Groups[0].Background = '#123456'
    $page.Layout.ViewProperties = $properties
    $completeProfile = [LandingPage.Models.DashboardProfiles]::Capture($settings,'Stored full dashboard')
    [LandingPage.LandingPageSettings].GetProperty('DashboardProfiles').GetValue($settings).Add($completeProfile)
    $model = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([LandingPage.LandingPageSettingsViewModel])
    $model.Settings = $settings

    # Reproduces the exact BeginEdit -> Serialization.GetClone path in the crash log.
    $model.BeginEdit()
    $rules.IncludedNamePatterns = '*Changed*'
    $rules.NeverPlayedOnly = $false
    $model.CancelEdit()
    $restored = $model.Settings
    $restoredMotion = [LandingPage.LandingPageSettings].GetProperty('DashboardCustomTransition').GetValue($restored)
    $savedMotions = [LandingPage.LandingPageSettings].GetProperty('DashboardTransitionPresets').GetValue($restored)
    if ($restoredMotion.Incoming.StartX -ne 17 -or $savedMotions.Count -ne 1 -or $savedMotions[0].Outgoing.DelayMs -ne 30 -or [object]::ReferenceEquals($restoredMotion,$customMotion)) { throw 'Playnite settings clone lost custom transition data or independence' }

    if (![LandingPage.LandingPageSettings].GetProperty('EnableDashboardDiagnostics').GetValue($restored)) { throw 'Playnite settings clone lost extension debug preference' }
    if ([LandingPage.LandingPageSettings].GetProperty('DashboardTransition').GetValue($restored) -ne [LandingPage.DashboardTransition]::Slide -or [LandingPage.LandingPageSettings].GetProperty('DashboardSlideDirection').GetValue($restored) -ne [LandingPage.DashboardSlideDirection]::Up -or [LandingPage.LandingPageSettings].GetProperty('DashboardTransitionMilliseconds').GetValue($restored) -ne 350) { throw 'Playnite settings clone lost dashboard transition preferences' }

    $mostRestored = [LandingPage.LandingPageSettings].GetProperty('MostPlayedOptions').GetValue($restored)
    if (($mostRestored | ForEach-Object { $_.Timeframe.ToString() }) -join ',' -ne 'Last14Days,Last3Month,Last6Month,LastYear') { throw 'Most Played options duplicated or changed during Playnite settings clone' }
    for ($iteration = 0; $iteration -lt 4; ++$iteration) {
        $model.BeginEdit(); $model.CancelEdit()
        $roundtripOptions = [LandingPage.LandingPageSettings].GetProperty('MostPlayedOptions').GetValue($model.Settings)
        if ($roundtripOptions.Count -ne 4) { throw 'Repeated settings cloning grows Most Played options' }
    }

    $restoredRules = [LandingPage.LandingPageSettings].GetProperty('ShelveProperties').GetValue($restored)[0]
    if ($restoredRules.IncludedNamePatterns -ne '*Quest*' -or !$restoredRules.NeverPlayedOnly -or [object]::ReferenceEquals($restoredRules,$rules)) { throw 'Cancel settings failed to restore an independent clone' }
    $restoredInstances = [LandingPage.LandingPageSettings].GetProperty('ShelveInstanceSettings').GetValue($restored)
    $modernRules = @($restoredInstances.Values)[0].ShelveProperties[0]
    if ($modernRules.HoverEffect -ne [LandingPage.Models.ShelfHoverEffect]::Dock -or $modernRules.HoverScale -ne 1.35 -or !$modernRules.HoverGlow -or $modernRules.HoverGlowColor -ne '#123456' -or $modernRules.HoverDuration -ne 220) { throw 'Playnite settings clone lost shelf hover settings' }

    if ($modernRules.ExcludedInstallPathPatterns -ne 'D:\Games\Demos\*' -or $modernRules.IncludedPublisherNamePatterns -ne 'glob:*[A-Z]*') { throw 'Cloning lost per-widget shelf filters' }
    $pages = [LandingPage.LandingPageSettings].GetProperty('DashboardPages').GetValue($restored)
    if ($pages.Count -ne 1 -or $pages[0].Layout.ViewProperties.TextSettings.Text -ne 'Preserve page widget') { throw 'Settings cloning lost dashboard page data' }

    $featuredSettings = $pages[0].Layout.ViewProperties.FeaturedGameSettings
    $sectionLayout = $featuredSettings.GetSectionLayout([LandingPage.Settings.FeaturedGameSection]::Description)
    if (!$featuredSettings.SectionAppearance.Enabled -or $featuredSettings.SectionAppearance.Background -ne '#112233' -or !$sectionLayout.OverrideAppearance -or $sectionLayout.Appearance.Background -ne '#334455') { throw 'Actual Playnite serializer lost shared/individual section appearance' }
    if (!$featuredSettings.UseCustomSectionLayout -or $featuredSettings.EditSectionLayout -or $featuredSettings.SectionLayouts.Count -ne 8 -or $sectionLayout.X -ne 90 -or $sectionLayout.Y -ne 75 -or $sectionLayout.Width -ne 345 -or $sectionLayout.Height -ne 123) { throw 'Playnite settings clone lost or duplicated section geometry or persisted temporary edit mode' }

    if ($featuredSettings.MatchMetadataTextSize -or $featuredSettings.MetadataTextSize -ne 19 -or $featuredSettings.MatchMetadataTextColor -or $featuredSettings.MetadataTextColor -ne '#123456' -or $featuredSettings.MatchMetadataIconSize -or $featuredSettings.MetadataIconSize -ne 25) { throw 'Playnite settings clone lost featured metadata formatting' }
    if ($pages[0].Layout.ViewProperties.FeaturedGameSettings.PinnedGameId -ne $featuredPin -or $pages[0].Layout.ViewProperties.FeaturedGameSettings.TitleSize -ne 42) { throw 'Playnite settings clone lost featured game settings' }
    if ($pages[0].Layout.ViewProperties.ToolbarSettings.NotificationBadgeStyle -ne [LandingPage.Settings.NotificationBadgeStyle]::Count) { throw 'Playnite settings clone lost notification badge preference' }
    if ($pages[0].Layout.ViewProperties.ToolbarSettings.Items.Count -ne $properties.ToolbarSettings.Items.Count -or $pages[0].Layout.ViewProperties.ToolbarSettings.Items[$properties.ToolbarSettings.Items.Count - 1].Id -ne 'native.sort') { throw 'Playnite settings clone duplicated or lost toolbar items' }

    if ($pages[0].Layout.ViewProperties.ToolbarSettings.Groups.Count -ne 1 -or $pages[0].Layout.ViewProperties.ToolbarSettings.Groups[0].Width -ne 280 -or $pages[0].Layout.ViewProperties.ToolbarSettings.Groups[0].Background -ne '#123456') { throw 'Playnite clone lost or duplicated group sizing/appearance' }

    $profiles = [LandingPage.LandingPageSettings].GetProperty('DashboardProfiles').GetValue($restored)
    if ($profiles.Count -ne 1 -or $profiles[0].Snapshot.Pages[0].Layout.ViewProperties.ToolbarSettings.Groups[0].Width -ne 280) { throw 'Actual Playnite settings clone lost stored profile data' }
    [LandingPage.Models.DashboardProfiles]::Validate($profiles[0])

    $json = [Playnite.SDK.Data.Serialization]::ToJson($restored,$false)
    if ($json -match '"(Item|Error)"\s*:') { throw 'Validation-only properties must not be serialized' }
    if ($json -notmatch 'IncludedPublisherNamePatterns') { throw 'Pattern configuration must still be serialized' }
    $restoredRules.IncludedNamePatterns = 'regex:('
    if (!([ComponentModel.IDataErrorInfo]$restoredRules)['IncludedNamePatterns']) { throw 'Pattern validation stopped working' }
    # Even invalid user input must be cloneable when opening settings.
    $model.BeginEdit(); $model.CancelEdit()
    $unchanged = $model.Settings
    $model.BeginEdit(); $model.CancelEdit()
    if (![object]::ReferenceEquals($unchanged,$model.Settings)) { throw 'Closing unchanged settings replaced the live dashboard settings' }
    $model.BeginEdit(); $model.EndEdit()
    if (![object]::ReferenceEquals($unchanged,$model.Settings)) { throw 'Accepting unchanged settings replaced the live dashboard settings' }
    $beforeImport = ([LandingPage.LandingPageSettings].GetProperty('DashboardProfiles').GetValue($model.Settings)).Count
    $model.BeginEdit()
    $originalProfile = [LandingPage.Models.DashboardProfiles]::ImportOriginalSettings('{"GridLayout":{},"Padding":7}')
    [LandingPage.LandingPageSettings].GetProperty('DashboardProfiles').GetValue($model.Settings).Add($originalProfile)
    $model.CancelEdit()
    if (([LandingPage.LandingPageSettings].GetProperty('DashboardProfiles').GetValue($model.Settings)).Count -ne $beforeImport) { throw 'Cancel retained the original StartPage import' }
    [LandingPage.LandingPageSettings].GetProperty('DashboardProfiles').GetValue($model.Settings).Add($originalProfile)
    $persistedImport = $detailCloneMethod.MakeGenericMethod([LandingPage.LandingPageSettings]).Invoke($null,@($model.Settings))
    if (([LandingPage.LandingPageSettings].GetProperty('DashboardProfiles').GetValue($persistedImport)).Count -ne ($beforeImport + 1)) { throw 'Playnite serialization lost imported original profile' }
    'PASS: original profile imports persist with Playnite serialization and Cancel discards the pending import.'
    'PASS: actual Playnite settings BeginEdit/CancelEdit clone, independent shelf rules, page data, validation-property exclusion, and invalid-pattern validation/persistence.'
} finally { $serializerField.SetValue($null,$previousSerializer) }
