# Run -STA. Extension settings window (roadmap step 24, Transitions tab from 25.9): tab structure, every live setting still has a control, dead
# legacy options stay hidden, edits save, opening changes nothing, tabs fit a narrow window, polished English labels.
# Reuses the in-memory API/window fixture.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$windowTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $windowTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    function Pump-For($milliseconds) {
        $until = [datetime]::UtcNow.AddMilliseconds($milliseconds)
        do { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background); Start-Sleep -Milliseconds 10 } while ([datetime]::UtcNow -lt $until)
        $window.UpdateLayout()
    }
    function Get-Descendants($node) { $count = [Windows.Media.VisualTreeHelper]::GetChildrenCount($node); for($i = 0; $i -lt $count; $i++) { $child = [Windows.Media.VisualTreeHelper]::GetChild($node,$i); $child; Get-Descendants $child } }
    foreach($type in @([Windows.Controls.TextBox],[Windows.Controls.Expander],[Windows.Controls.CheckBox],[Windows.Controls.ComboBox],[Windows.Controls.Button],[Windows.Controls.TabControl],[Windows.Controls.TabItem],[Windows.Controls.Slider])) { if(!$app.Resources.Contains($type)) { $app.Resources.Add($type,[Windows.Style]::new($type)) } }
    # Binding paths of every element in a subtree (including bindings inside templates of our own controls).
    function Get-BindingPaths($node) {
        foreach ($element in @($node) + @(Get-Descendants $node)) {
            if (!($element -is [Windows.DependencyObject])) { continue }
            $values = $element.GetLocalValueEnumerator()
            while ($values.MoveNext()) {
                $expression = [Windows.Data.BindingOperations]::GetBindingExpressionBase($element,$values.Current.Property)
                if ($expression -is [Windows.Data.BindingExpression] -and $expression.ParentBinding.Path) { $expression.ParentBinding.Path.Path }
            }
        }
    }
    $settingsObject = $settingsModel.Settings
    $before = [Newtonsoft.Json.JsonConvert]::SerializeObject($settingsObject)
    $settingsView = [LandingPage.LandingPageSettingsView]::new(); $settingsView.DataContext = $settingsModel
    $settingsHost = [Windows.Controls.Border]::new(); $settingsHost.Width = 560; $settingsHost.Height = 700; $settingsHost.HorizontalAlignment = 'Left'; $settingsHost.VerticalAlignment = 'Top'; $settingsHost.Child = $settingsView
    [void]$view.Content.Children.Add($settingsHost); $window.UpdateLayout(); Pump-For 100
    $tabs = $settingsView.FindName('SettingsTabs')
    $names = @($tabs.Items | ForEach-Object Name)
    if (($names -join ',') -ne 'GeneralTab,CoversTab,BackgroundTab,TransitionsTab,IntroTab,ToolsTab') { throw "Settings tabs are not General, Covers and shelves, Background, Transitions, Startup intro, Updates and tools: $($names -join ',')" }
    $paths = [Collections.Generic.HashSet[string]]::new()
    foreach ($tab in $tabs.Items) {
        $tabs.SelectedItem = $tab; Pump-For 120
        $content = $tab.Content
        foreach ($path in @(Get-BindingPaths $content)) { [void]$paths.Add($path) }
        # Nothing may extend past the visible width of the tab (no clipped controls or sideways scrolling).
        # Content starts after the scroll area padding; the visible edge is padding + viewport width.
        $limit = $content.ViewportWidth; if (!$limit -or $limit -le 0) { $limit = $content.ActualWidth - $content.Padding.Left - $content.Padding.Right }; $limit += $content.Padding.Left
        foreach ($element in @(Get-Descendants $content.Content | Where-Object { $_ -is [Windows.FrameworkElement] -and $_.IsVisible -and $_.ActualWidth -gt 0 })) {
            $bounds = $element.TransformToAncestor($content).TransformBounds([Windows.Rect]::new(0,0,$element.ActualWidth,$element.ActualHeight))
            if ($bounds.Right -gt $limit + 1 -and !($element -is [Windows.Controls.Primitives.Popup])) { throw "Tab $($tab.Name) overflows at 560px: $($element.GetType().Name) ends at $([Math]::Round($bounds.Right)) of $([Math]::Round($limit))" }
        }
    }
    $required = 'EnableStartupOverride','StartPage','StartPageOptions','MoveToTopOfList','PreserveWindowedStartupSize','LockLayout','FixGridSize','FixedGridHeight','EnableGlobalProgressBar',
        'EnableNotifications','MinimizeNotificationsOnLaunch','ShowNotificationButtons','EnableTagCreation','SelectedAchievementsProvider',
        'CoverAspectRatio','DownScaleCovers','HideScrollbarInShelves','MaxCoverWidth','ShowTitleOnCover','ShowDetails','SkipGamesInPreviousShelves',
        'EnableGlobalBackground','ShowBackgroundOutsideStartPage','GlobalBackgroundOpacity','BackgroundImageSource','BackgroundImagePath','BackgroundRefreshInterval','AnimationDuration',
        'BlurAmount','OverlayOpacity','NoiseOpacity','BackgroundGameInfoOpacity','ShowCurrentlyPlayedBackground','DisableBlurForCurrentlyPlayed','BackgroundAppearance','StartupIntro','GlobalTransition','DashboardRespectWindowsAnimations',
        'CheckForUpdatesAutomatically','EnableDashboardDiagnostics'
    $missing = @($required | Where-Object { !$paths.Contains('Settings.' + $_) })
    if ($missing.Count) { throw "Settings without a control: $($missing -join ', ')" }
    foreach ($other in @('OriginalSettingsPath','OriginalImportStatus','DebugLogPath','SelectImagePathCommand','SelectImageFolderPathCommand','ClearImagePathCommand')) { if (!$paths.Contains($other)) { throw "Settings window lost the $other binding" } }
    $dead = @('NumberOfGames','ShowClock','ShowMostPlayedGames','EnableGameActivity','ShowRecentAchievements','KeepInMemory') | Where-Object { $paths.Contains('Settings.' + $_) }
    if ($dead.Count) { throw "Options that no longer affect the dashboard are shown: $($dead -join ', ')" }
    'PASS: six tabs, every live setting has a control, legacy options with no effect are hidden, and no tab overflows at 560px.'

    if ([Newtonsoft.Json.JsonConvert]::SerializeObject($settingsObject) -ne $before) { throw 'Opening the settings window changed saved settings' }
    $tabs.SelectedItem = $settingsView.FindName('CoversTab'); Pump-For 80
    $coverWidth = $settingsView.FindName('DefaultCoverWidthSlider'); $original = Get-Setting $settingsObject 'MaxCoverWidth'
    $coverWidth.Slider.Value = 173; Pump-For 30
    if ((Get-Setting $settingsObject 'MaxCoverWidth') -ne 173) { throw 'Default cover width slider does not save' }
    Set-Setting $settingsObject 'MaxCoverWidth' $original; Pump-For 30
    if ($coverWidth.NumberBox.Value -ne $original) { throw 'Default cover width slider does not follow the setting' }
    # Transitions tab: the global transition editor edits the same transition the dashboard uses, with the saved presets and no preview.
    $tabs.SelectedItem = $settingsView.FindName('TransitionsTab'); Pump-For 80
    $globalEditor = $settingsView.FindName('GlobalTransitionEditor')
    $globalTransition = Get-Setting $settingsObject 'GlobalTransition'
    if (![object]::ReferenceEquals($globalEditor.DataContext,$globalTransition) -or ![object]::ReferenceEquals($globalEditor.PresetLibrary,(Get-Setting $settingsObject 'DashboardTransitionPresets')) -or !$globalEditor.PreviewInBox -or $globalEditor.FindName('PreviewArea').Visibility -ne 'Visible') { throw 'Transitions tab does not edit the global transition with the saved presets and a preview box' }
    $originalDuration = Get-Setting $settingsObject 'DashboardTransitionMilliseconds'
    $globalTransition.DurationMs = 345; Pump-For 30
    if ((Get-Setting $settingsObject 'DashboardTransitionMilliseconds') -ne 345) { throw 'Global transition editor does not save into the transition settings' }
    Set-Setting $settingsObject 'DashboardTransitionMilliseconds' $originalDuration; Pump-For 30
    # Preview plays in the box between the two sample pages (no dashboard behind the settings window).
    $box = $globalEditor.FindName('PreviewBox'); $wasFirst = $box.ShowingFirst
    $originalEffect = Get-Setting $settingsObject 'DashboardTransition'
    Set-Setting $settingsObject 'DashboardTransition' ([LandingPage.DashboardTransition]::Wipe); Set-Setting $settingsObject 'DashboardTransitionMilliseconds' 80
    $preview = $globalEditor.PreviewInBoxAsync(); $until = [datetime]::UtcNow.AddSeconds(10)
    while (!$preview.IsCompleted) { if ([datetime]::UtcNow -gt $until) { throw 'The settings preview did not finish' }; Pump-For 20 }
    if ($box.ShowingFirst -eq $wasFirst -or $box.IsPlaying -or $box.LastPlayer.ActiveTransition -ne [LandingPage.DashboardTransition]::Wipe) { throw 'The settings preview did not play the global transition between the sample pages' }
    Set-Setting $settingsObject 'DashboardTransition' $originalEffect; Set-Setting $settingsObject 'DashboardTransitionMilliseconds' $originalDuration; Pump-For 30
    'PASS: the Transitions tab edits the global page transition with the shared saved presets and previews it in a box.'
    [void]$view.Content.Children.Remove($settingsHost)
    'PASS: opening the window keeps saved settings unchanged and its controls save and follow settings.'

    # Polished English labels: no trailing colons or "Shelve" spelling in the settings labels.
    [xml]$strings = Get-Content (Join-Path $windowTestRoot 'source\StartPage\Localization\en_US.xaml') -Raw -Encoding UTF8
    $text = @{}; foreach ($node in $strings.DocumentElement.ChildNodes) { if ($node.Key) { $text[$node.Key] = $node.InnerText } }
    foreach ($key in 'LOC_SPG_BlurAmount','LOC_SPG_OverlayOpacity','LOC_SPG_NoiseOpacity','LOC_SPG_BackgroundGameInfoOpacity','LOC_SPG_BackgroundAnimationDuration','LOC_SPG_BackgroundRefreshInterval','LOC_SPG_BackgroundImageSource','LOC_SPG_BackgroundImagePath','LOC_SPG_FixGridHeight','LOC_SPG_HideShelveScrollbar') {
        if (!$text.ContainsKey($key) -or $text[$key].TrimEnd() -match ':$' -or $text[$key] -match 'Shelve') { throw "Label $key is not polished: '$($text[$key])'" }
    }
    'PASS: settings labels use sentence case without trailing colons.'

'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
