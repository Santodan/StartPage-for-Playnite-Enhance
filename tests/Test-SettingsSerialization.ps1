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
    if ($pages[0].Layout.ViewProperties.ToolbarSettings.Items.Count -ne 5 -or $pages[0].Layout.ViewProperties.ToolbarSettings.Items[4].Id -ne 'native.sort') { throw 'Playnite settings clone duplicated or lost toolbar items' }

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
