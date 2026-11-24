# Run -STA. Shared widget-editor layout kit (Controls/SettingsLayout.cs) and the editors converted to it.
# Reuses the in-memory API/window fixture so editors render with the dashboard's theme resources.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$layoutTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $layoutTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    foreach($type in @([Windows.Controls.TextBox],[Windows.Controls.Expander],[Windows.Controls.CheckBox],[Windows.Controls.ComboBox],[Windows.Controls.Button])) {
        if(!$app.Resources.Contains($type)) { $app.Resources.Add($type,[Windows.Style]::new($type)) }
    }
    function Flush-Layout { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout() }
    function Get-Descendants($node) {
        $count = [Windows.Media.VisualTreeHelper]::GetChildrenCount($node)
        for($i = 0; $i -lt $count; $i++) { $child = [Windows.Media.VisualTreeHelper]::GetChild($node,$i); $child; Get-Descendants $child }
    }
    function Find-Named($root,$name) { @(Get-Descendants $root | Where-Object { $_ -is [Windows.FrameworkElement] -and $_.Name -eq $name })[0] }
    function Host-Editor($editor,$width) { $border = [Windows.Controls.Border]::new(); $border.Width = $width; $border.HorizontalAlignment = 'Left'; $border.Child = $editor; [void]$view.Content.Children.Add($border); Flush-Layout; $border }
    function Get-Ancestry($element) { $node = [Windows.Media.VisualTreeHelper]::GetParent($element); for($i = 0; $node -and $i -lt 8; $i++) { $node.GetType().Name; $node = [Windows.Media.VisualTreeHelper]::GetParent($node) } }
    # Every visible element must stay within the editor width: no clipped fields or horizontal scrolling.
    function Assert-Fits($border,$label) {
        foreach($element in @(Get-Descendants $border.Child | Where-Object { $_ -is [Windows.FrameworkElement] -and $_.IsVisible -and $_.ActualWidth -gt 0 })) {
            $bounds = $element.TransformToAncestor($border).TransformBounds([Windows.Rect]::new(0,0,$element.ActualWidth,$element.ActualHeight))
            if($bounds.Right -gt $border.ActualWidth + 1) { throw ("$label overflows at width $($border.Width): $($element.GetType().Name) '$($element.Name)$(if($element -is [Windows.Controls.TextBlock]){' '+$element.Text})' ends at $([Math]::Round($bounds.Right)) in $((Get-Ancestry $element) -join ' < ')") }
        }
    }

    # Two-column panel: side by side when wide, one column when narrow, spanning children take a full row.
    $columns = [LandingPage.Controls.SettingsColumns]::new()
    $first = [LandingPage.Controls.SettingsField]::new(); $first.Label = 'First'; [void]$first.Children.Add([Windows.Controls.ComboBox]::new())
    $second = [LandingPage.Controls.SettingsField]::new(); $second.Label = 'Second'; [void]$second.Children.Add([Windows.Controls.ComboBox]::new())
    $wide = [LandingPage.Controls.SettingsSlider]::new(); $wide.Label = 'Spanning slider'; [LandingPage.Controls.SettingsColumns]::SetSpan($wide,$true)
    foreach($child in @($first,$second,$wide)) { [void]$columns.Children.Add($child) }
    $columnsHost = Host-Editor $columns 400
    $secondLeft = $second.TransformToAncestor($columns).Transform([Windows.Point]::new(0,0)).X
    if($columns.ActiveColumns -ne 2 -or $secondLeft -lt 190 -or [Math]::Abs($first.ActualWidth - $second.ActualWidth) -gt 1) { throw "Settings columns are not two equal columns when wide (columns=$($columns.ActiveColumns), second at $secondLeft)" }
    if([Math]::Abs($wide.ActualWidth - 400) -gt 1) { throw 'Spanning slider does not use the full editor width' }
    $columnsHost.Width = 220; Flush-Layout
    $secondTop = $second.TransformToAncestor($columns).Transform([Windows.Point]::new(0,0))
    if($columns.ActiveColumns -ne 1 -or $secondTop.X -gt 1 -or $secondTop.Y -lt $first.ActualHeight - 1 -or [Math]::Abs($first.ActualWidth - 220) -gt 1) { throw 'Settings columns do not stack into one column when narrow' }
    $second.Visibility = 'Collapsed'; Flush-Layout
    if($wide.TransformToAncestor($columns).Transform([Windows.Point]::new(0,0)).Y -gt $first.ActualHeight + 1) { throw 'Collapsed settings fields still reserve space' }
    [void]$view.Content.Children.Remove($columnsHost)
    'PASS: settings columns switch between two equal columns and one stacked column; spanning and collapsed fields lay out correctly.'

    # Slider and numeric box stay synchronized with the bound setting in both directions.
    $image = [LandingPage.Settings.ImageWidgetSettings]::new()
    $imageEditor = [LandingPage.Views.Settings.ImageWidgetSettingsView]::new(); $imageEditor.DataContext = $image
    $imageHost = Host-Editor $imageEditor 520
    $opacity = Find-Named $imageEditor 'OpacitySlider'
    if($opacity.LabelBlock.Text -ne 'Image opacity' -or $opacity.Slider.Maximum -ne 1 -or $opacity.NumberBox.Value -ne $image.Opacity) { throw 'Image opacity slider is not labeled or bound to the setting' }
    $opacity.Slider.Value = 0.4; Flush-Layout
    if([Math]::Abs($image.Opacity - 0.4) -gt 0.001 -or [Math]::Abs($opacity.NumberBox.Value - 0.4) -gt 0.001) { throw 'Moving the opacity slider does not update the setting and numeric box' }
    $image.Opacity = 0.7; Flush-Layout
    if([Math]::Abs($opacity.Slider.Value - 0.7) -gt 0.001 -or [Math]::Abs($opacity.NumberBox.Value - 0.7) -gt 0.001) { throw 'Changing the opacity setting does not update the slider and numeric box' }
    'PASS: settings slider and numeric box stay synchronized with the setting.'

    # Size/B/I/color bar edits the text widget settings directly.
    $text = [LandingPage.Settings.TextWidgetSettings]::new()
    $textEditor = [LandingPage.Views.Settings.TextWidgetSettingsView]::new(); $textEditor.DataContext = $text
    $textHost = Host-Editor $textEditor 520
    $bar = Find-Named $textEditor 'FormatBar'
    if($bar.SizeBox.Value -ne $text.FontSize -or $bar.BoldButton.IsChecked -ne $text.Bold -or $bar.ColorPicker.Visibility -ne 'Visible') { throw 'Text format bar does not show the current text settings' }
    $bar.BoldButton.IsChecked = !$text.Bold; $bar.ItalicButton.IsChecked = !$text.Italic; $bar.ColorPicker.UseCustomColor = $true; $bar.ColorPicker.SelectedColor = '#FF336699'; Flush-Layout
    if($text.Bold -ne $bar.BoldButton.IsChecked -or $text.Italic -ne $bar.ItalicButton.IsChecked -or !$text.UseCustomColor -or $text.Color -ne '#FF336699') { throw 'Text format bar changes are not saved to the text settings' }
    $text.FontSize = 31; Flush-Layout
    if($bar.SizeBox.Value -ne 31) { throw 'Text size changes do not reach the format bar' }
    'PASS: text format bar edits size, bold, italic and custom color.'

    # Clock: time/date stay behind the text-element selector, custom date format appears only when chosen.
    $clock = [LandingPage.Settings.ClockSettings]::new()
    $clockEditor = [LandingPage.Views.Settings.ClockSettingsView]::new(); $clockEditor.DataContext = $clock
    $clockHost = Host-Editor $clockEditor 520
    $clockBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($clock)
    $timeBar = Find-Named $clockEditor 'TimeFormatBar'
    if($timeBar.BoldButton.Visibility -ne 'Collapsed' -or $timeBar.SizeBox.Value -ne $clock.TimeSize) { throw 'Clock time format bar shows unsupported bold/italic or the wrong size' }
    $clockEditor.FindName('TextEditor').ElementSelector.SelectedIndex = 1; Flush-Layout
    $customDate = $clockEditor.FindName('CustomDateFormatField')
    if($customDate.Visibility -ne 'Collapsed') { throw 'Custom date format is shown for a non-custom date format' }
    if([Newtonsoft.Json.JsonConvert]::SerializeObject($clock) -ne $clockBefore) { throw 'Opening the clock editor modified clock settings' }
    $clock.DateFormat = [LandingPage.Settings.ClockDateFormat]::Custom; Flush-Layout
    if($customDate.Visibility -ne 'Visible') { throw 'Custom date format is hidden for the custom date format' }
    'PASS: clock editor keeps time/date behind the selector and shows custom date format only when chosen.'

    # Divider: kit sliders keep the named thickness slider and expose opacity with a numeric box.
    $divider = [LandingPage.Settings.DividerWidgetSettings]::new(); $divider.Decoration = [LandingPage.Settings.DividerDecoration]::Circles; $divider.LineStyle = [LandingPage.Settings.DividerLineStyle]::Custom; $divider.ConnectEnds = $true
    $dividerEditor = [LandingPage.Views.Settings.DividerWidgetSettingsView]::new(); $dividerEditor.DataContext = $divider
    $dividerHost = Host-Editor $dividerEditor 520
    foreach($expander in @(Get-Descendants $dividerEditor | Where-Object { $_ -is [Windows.Controls.Expander] })) { $expander.IsExpanded = $true }; Flush-Layout
    $dividerBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($divider)
    $dividerOpacity = Find-Named $dividerEditor 'OpacitySlider'
    if(!$dividerOpacity -or $dividerOpacity.NumberBox.Value -ne $divider.LineOpacity) { throw 'Divider opacity is not a labeled slider with a numeric box' }
    $dividerEditor.FindName('ThicknessSlider').Value = 4.5; Flush-Layout
    if($divider.LineThickness -ne 4.5) { throw 'Divider thickness slider no longer updates the setting' }
    $divider.LineThickness = [Newtonsoft.Json.JsonConvert]::DeserializeObject($dividerBefore,[LandingPage.Settings.DividerWidgetSettings]).LineThickness; Flush-Layout
    if([Newtonsoft.Json.JsonConvert]::SerializeObject($divider) -ne $dividerBefore) { throw 'Opening the divider editor modified divider settings' }
    'PASS: divider editor uses kit sliders, keeps named controls and leaves settings untouched when opened.'

    # Shortcut: application entry shows all path rows; label formatting uses the shared bar.
    $shortcut = [LandingPage.Settings.ShortcutWidgetSettings]::new(); $app1 = [LandingPage.Settings.ShortcutEntry]::new(); $app1.Kind = [LandingPage.Settings.ShortcutKind]::Application; $shortcut.Items.Add($app1)
    $shortcutEditor = [LandingPage.Views.Settings.ShortcutWidgetSettingsView]::new(); $shortcutEditor.DataContext = $shortcut
    $shortcutHost = Host-Editor $shortcutEditor 520
    $shortcutEditor.FindName('EntrySelector').SelectedIndex = 0
    foreach($expander in @(Get-Descendants $shortcutEditor | Where-Object { $_ -is [Windows.Controls.Expander] })) { $expander.IsExpanded = $true }; Flush-Layout
    $shortcutBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($shortcut)
    if($shortcutEditor.FindName('ApplicationFields').Visibility -ne 'Visible' -or $shortcutEditor.FindName('TargetFields').Visibility -ne 'Visible' -or $shortcutEditor.FindName('GameFields').Visibility -ne 'Collapsed') { throw 'Shortcut fields do not follow the application entry type' }
    $labelBar = Find-Named $shortcutEditor 'LabelFormatBar'
    $labelBar.BoldButton.IsChecked = !$shortcut.Bold; Flush-Layout
    if($shortcut.Bold -ne $labelBar.BoldButton.IsChecked) { throw 'Shortcut label format bar does not save bold' }
    $labelBar.BoldButton.IsChecked = !$labelBar.BoldButton.IsChecked; Flush-Layout
    if([Newtonsoft.Json.JsonConvert]::SerializeObject($shortcut) -ne $shortcutBefore) { throw 'Opening the shortcut editor modified shortcut settings' }
    'PASS: shortcut editor shows the fields for the entry type and saves label formatting.'

    function Expand-All($editor) { for($pass = 0; $pass -lt 4; $pass++) { foreach($expander in @(Get-Descendants $editor | Where-Object { $_ -is [Windows.Controls.Expander] })) { $expander.IsExpanded = $true }; Flush-Layout } }

    # Toolbar: default items/groups render with paired fields; opening does not change settings.
    $toolbarSettings = [LandingPage.Settings.ToolbarWidgetSettings]::new(); $toolbarBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($toolbarSettings)
    $toolbarEditor = [LandingPage.Views.Settings.ToolbarSettingsView]::new(); $toolbarEditor.DataContext = $toolbarSettings
    $toolbarHost = Host-Editor $toolbarEditor 520; Expand-All $toolbarEditor
    if($toolbarEditor.FindName('GroupItemsList').Items.Count -lt 1 -or !$toolbarEditor.FindName('OptionPicker')) { throw 'Toolbar editor lost its item list or option picker' }
    $groupOpacity = @(Get-Descendants $toolbarEditor | Where-Object { $_ -is [LandingPage.Controls.SettingsSlider] -and $_.Label -eq 'Background opacity' })
    if(!$groupOpacity.Count -or $groupOpacity[0].NumberBox.Value -ne $toolbarSettings.Groups[0].BackgroundOpacity) { throw 'Toolbar group opacity slider inside the group template does not show the saved value' }
    # Inside a DataTemplate the binding is a template value; inner write-backs must not replace it.
    $groupOpacity[0].Slider.Value = 0.25; Flush-Layout
    if([Math]::Abs($toolbarSettings.Groups[0].BackgroundOpacity - 0.25) -gt 0.001 -or ![Windows.Data.BindingOperations]::GetBindingExpression($groupOpacity[0],[LandingPage.Controls.SettingsSlider]::ValueProperty)) { throw 'Templated settings slider lost its binding to the group opacity' }
    $toolbarSettings.Groups[0].BackgroundOpacity = 0.6; Flush-Layout
    if([Math]::Abs($groupOpacity[0].NumberBox.Value - 0.6) -gt 0.001) { throw 'Templated settings slider does not follow group opacity changes' }
    if([Newtonsoft.Json.JsonConvert]::SerializeObject($toolbarSettings) -ne $toolbarBefore) { throw 'Opening the toolbar editor modified toolbar settings' }
    'PASS: toolbar editor keeps its item list, picker and group opacity slider and leaves settings untouched when opened.'

    # Library statistics and charts: former bare sliders gain numeric boxes; rotation stays a real slider.
    $statsSettings = [LandingPage.Settings.LibraryStatisticsSettings]::new()
    $statsEditor = [LandingPage.Views.Settings.LibraryStatisticsSettingsView]::new(); $statsEditor.DataContext = $statsSettings
    $statsHost = Host-Editor $statsEditor 520; Expand-All $statsEditor
    $statsBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($statsSettings)
    $valueOpacity = @(Get-Descendants $statsEditor | Where-Object { $_ -is [LandingPage.Controls.SettingsSlider] -and $_.Label -eq 'Value opacity' })
    if(!$valueOpacity.Count) { throw 'Statistic value opacity is not a labeled slider with a numeric box' }
    $valueOpacity[0].Slider.Value = 0.5; Flush-Layout
    if([Math]::Abs($statsSettings.ValueOpacity - 0.5) -gt 0.001) { throw 'Statistic value opacity slider does not update the setting' }
    $valueOpacity[0].Slider.Value = [Newtonsoft.Json.JsonConvert]::DeserializeObject($statsBefore,[LandingPage.Settings.LibraryStatisticsSettings]).ValueOpacity; Flush-Layout
    if(![object]::ReferenceEquals($statsEditor.Content.Children[0],$statsEditor.FindName('CounterGridOptions'))) { throw 'Counter grid options are no longer first in statistic settings' }
    $chartSettings = [LandingPage.Settings.LibraryStatisticsSettings]::new()
    $chartsEditor = [LandingPage.Views.Settings.LibraryStatisticsSettingsView]::new(); $chartsEditor.ChartOnly = $true; $chartsEditor.DataContext = $chartSettings
    $chartsHost = Host-Editor $chartsEditor 520; Expand-All $chartsEditor
    $chartBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($chartSettings)
    $selectedChart = $chartsEditor.FindName('ExtraChartEditor').Content; Expand-All $selectedChart
    if($selectedChart.FindName('RotationSlider') -isnot [Windows.Controls.Slider]) { throw 'Chart rotation is no longer a slider' }
    $chartSliders = @(Get-Descendants $selectedChart | Where-Object { $_ -is [LandingPage.Controls.SettingsSlider] } | ForEach-Object Label)
    foreach($label in @('Segment border width (0 = none)','Doughnut hole size')) { if($chartSliders -notcontains $label) { throw "Chart slider '$label' has no numeric box" } }
    'PASS: library statistics/chart editors use labeled sliders with numeric boxes and keep their named sections.'

    # Featured game: format bars, opacity sliders and templated per-section appearance sliders keep their bindings.
    $featuredSettings = [LandingPage.Settings.FeaturedGameSettings]::new(); $featuredBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($featuredSettings)
    $featuredEditor = [LandingPage.Views.Settings.FeaturedGameSettingsView]::new(); $featuredEditor.DataContext = $featuredSettings
    $featuredHost = Host-Editor $featuredEditor 520; Expand-All $featuredEditor
    $titleBar = $featuredEditor.FindName('TitleFormatBar')
    if(!$titleBar -or $titleBar.SizeBox.Value -ne $featuredSettings.TitleSize -or $titleBar.BoldButton.Visibility -ne 'Collapsed') { throw 'Featured title size/color is not a format bar bound to the title size' }
    $sectionSliders = @(Get-Descendants $featuredEditor | Where-Object { $_ -is [LandingPage.Controls.SettingsSlider] -and $_.Label -eq 'Background opacity' })
    if($sectionSliders.Count -lt 2) { throw 'Shared and per-section appearance opacity sliders are missing' }
    $perSection = $sectionSliders[$sectionSliders.Count-1]; $sectionAppearance = $perSection.DataContext; $sectionOpacity = $sectionAppearance.BackgroundOpacity
    $perSection.Slider.Value = 0.33; Flush-Layout
    if([Math]::Abs($sectionAppearance.BackgroundOpacity - 0.33) -gt 0.001 -or ![Windows.Data.BindingOperations]::GetBindingExpression($perSection,[LandingPage.Controls.SettingsSlider]::ValueProperty)) { throw 'Per-section appearance slider inside the section template lost its binding' }
    $sectionAppearance.BackgroundOpacity = $sectionOpacity; Flush-Layout
    if([Newtonsoft.Json.JsonConvert]::SerializeObject($featuredSettings) -ne $featuredBefore) { throw 'Opening the featured game editor modified its settings' }
    'PASS: featured game editor uses format bars and kit sliders; templated section appearance sliders keep their bindings.'

    # Game detail and achievement editors keep their section switching and gain columns.
    $artworkId = @([LandingPage.Models.GameDetailWidgets]::Definitions | Where-Object IsArtwork)[0].Id
    $detailSettings = [LandingPage.Settings.FeaturedGameSettings]::new(); $detailBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($detailSettings)
    $detailEditor = [LandingPage.Views.Settings.GameDetailSettingsView]::new(); $detailEditor.WidgetId = $artworkId; $detailEditor.DataContext = $detailSettings
    $detailHost = Host-Editor $detailEditor 520; Expand-All $detailEditor
    if($detailEditor.FindName('ArtworkOptions').Visibility -ne 'Visible' -or $detailEditor.FindName('ScoresOptions').Visibility -ne 'Collapsed') { throw 'Game detail editor no longer shows only the artwork widget sections' }
    if(!@(Get-Descendants $detailEditor | Where-Object { $_ -is [LandingPage.Controls.SettingsSlider] -and $_.Label -eq 'Image opacity' }).Count) { throw 'Game detail image opacity is not a slider' }
    $achievementSettings = [LandingPage.Settings.AchievementWidgetSettings]::Create('AchievementProgress'); $achievementBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($achievementSettings)
    $achievementEditor = [LandingPage.Views.Settings.AchievementWidgetSettingsView]::new(); $achievementEditor.WidgetId = 'AchievementProgress'; $achievementEditor.DataContext = $achievementSettings
    $achievementHost = Host-Editor $achievementEditor 520; Expand-All $achievementEditor
    if(@(Get-Descendants $achievementEditor | Where-Object { $_ -is [LandingPage.Controls.SettingsColumns] -and $_.IsVisible }).Count -lt 2) { throw 'Achievement filters and display options are not in columns' }
    'PASS: game detail and achievement editors keep their widget-specific sections and use columns and sliders.'

    # Most played, shelves and the shared panel appearance section.
    $mostPlayedEditor = [LandingPage.Views.Settings.MostPlayedSettingsView]::new(); $mostPlayedEditor.DataContext = $settingsModel
    $mostPlayedHost = Host-Editor $mostPlayedEditor 520
    if($mostPlayedEditor.FindName('TimeframeRows').Items.Count -ne @([LandingPage.LandingPageSettings].GetProperty('MostPlayedOptions').GetValue($settingsModel.Settings)).Count) { throw 'Most played editor lost its timeframe rows' }
    $shelfSettings = [LandingPage.Settings.ShelvesSettings]::new(); $shelfBefore = [Newtonsoft.Json.JsonConvert]::SerializeObject($shelfSettings)
    $shelvesEditor = [LandingPage.Views.Settings.ShelvesSettingsView]::new(); $shelvesEditor.DataContext = [LandingPage.ViewModels.ShelvesSettingsViewModel]::new($settingsModel,$shelfSettings)
    $shelvesHost = Host-Editor $shelvesEditor 520
    $coverSlider = $shelvesEditor.FindName('MaxCoverWidthSlider')
    if($coverSlider.NumberBox.Value -ne $shelfSettings.MaxCoverWidth) { throw 'Shelf cover width slider does not show the saved width' }
    $coverSlider.Slider.Value = 180; Flush-Layout
    if($shelfSettings.MaxCoverWidth -ne 180) { throw 'Shelf cover width slider does not save the width' }
    $shelfSettings.MaxCoverWidth = [Newtonsoft.Json.JsonConvert]::DeserializeObject($shelfBefore,[LandingPage.Settings.ShelvesSettings]).MaxCoverWidth; Flush-Layout
    $panelNode = [LandingPage.Models.Layout.GridNode]::new(); $panelNode.ViewProperties = [LandingPage.Models.Layout.ViewProperties]::new(); $panelNode.ViewProperties.ViewId = 'TextWidget'
    $panelAppearance = [LandingPage.Views.PanelAppearanceView]::new(); $panelAppearance.DataContext = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($panelNode)
    $panelHost = Host-Editor $panelAppearance 520; Expand-All $panelAppearance
    $panelDimensions = Find-Named $panelAppearance 'PanelDimensionColumns'
    if(!$panelDimensions -or @($panelDimensions.Children | Where-Object { $_ -is [LandingPage.Controls.SettingsSlider] }).Count -ne 4) { throw 'Panel border/radius/padding/spacing are not labeled sliders with numeric boxes' }
    @($panelDimensions.Children)[0].Slider.Value = 3; Flush-Layout
    if($panelNode.ViewProperties.Appearance.BorderWidth -ne 3) { throw 'Panel border width slider does not save the width' }
    'PASS: most played rows, shelf sliders and the shared panel appearance sliders edit their settings.'

    # Editors fit narrow and wide settings panels and do not modify settings when shown.
    foreach($pair in @(@($textHost,'Text editor'),@($imageHost,'Image editor'),@($clockHost,'Clock editor'),@($dividerHost,'Divider editor'),@($shortcutHost,'Shortcut editor'),@($toolbarHost,'Toolbar editor'),@($statsHost,'Library statistics editor'),@($chartsHost,'Library charts editor'),@($featuredHost,'Featured game editor'),@($detailHost,'Game detail editor'),@($achievementHost,'Achievement editor'),@($mostPlayedHost,'Most played editor'),@($shelvesHost,'Shelves editor'),@($panelHost,'Panel appearance'))) {
        foreach($width in @(260,520)) { $pair[0].Width = $width; Flush-Layout; Assert-Fits $pair[0] $pair[1] }
    }
    $alignment = Find-Named $textEditor 'AlignmentColumns'
    $textHost.Width = 260; Flush-Layout; if($alignment.ActiveColumns -ne 1) { throw 'Text alignment fields stay side by side in a narrow editor' }
    $textHost.Width = 520; Flush-Layout; if($alignment.ActiveColumns -ne 2) { throw 'Text alignment fields are not side by side in a wide editor' }
    foreach($editorPair in @(@([LandingPage.Views.Settings.TextWidgetSettingsView],[LandingPage.Settings.TextWidgetSettings]),@([LandingPage.Views.Settings.ImageWidgetSettingsView],[LandingPage.Settings.ImageWidgetSettings]))) {
        $fresh = $editorPair[1]::new(); $before = [Newtonsoft.Json.JsonConvert]::SerializeObject($fresh)
        $freshEditor = $editorPair[0]::new(); $freshEditor.DataContext = $fresh; $freshHost = Host-Editor $freshEditor 300
        if([Newtonsoft.Json.JsonConvert]::SerializeObject($fresh) -ne $before) { throw ('Opening '+$editorPair[0].Name+' modified its settings') }
        [void]$view.Content.Children.Remove($freshHost)
    }
    if([Newtonsoft.Json.JsonConvert]::SerializeObject($toolbarSettings) -ne $toolbarBefore) { throw 'Resizing the toolbar editor modified toolbar settings' }
    foreach($check in @(@($featuredSettings,$featuredBefore,'featured game'),@($detailSettings,$detailBefore,'game detail'),@($achievementSettings,$achievementBefore,'achievement'),@($shelfSettings,$shelfBefore,'shelf'))) { if([Newtonsoft.Json.JsonConvert]::SerializeObject($check[0]) -ne $check[1]) { throw ('Opening or resizing the '+$check[2]+' editor modified its settings') } }
    if([Newtonsoft.Json.JsonConvert]::SerializeObject($statsSettings) -ne $statsBefore -or [Newtonsoft.Json.JsonConvert]::SerializeObject($chartSettings) -ne $chartBefore) { throw 'Opening or resizing the library editors modified their settings' }
    foreach($editorHost in @($textHost,$imageHost,$clockHost,$dividerHost,$shortcutHost,$toolbarHost,$statsHost,$chartsHost,$featuredHost,$detailHost,$achievementHost,$mostPlayedHost,$shelvesHost,$panelHost)) { [void]$view.Content.Children.Remove($editorHost) }
    'PASS: every converted editor fits 260px and 520px panels and leaves its settings untouched when opened.'

'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
