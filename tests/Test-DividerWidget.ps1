# Run -STA. Reuse the in-memory API/window fixture without executing the full dashboard suite.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$dividerTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $dividerTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    foreach($type in @([Windows.Controls.TextBox],[Windows.Controls.Expander],[Windows.Controls.CheckBox],[Windows.Controls.ComboBox],[Windows.Controls.Button])) {
        if(!$app.Resources.Contains($type)) { $app.Resources.Add($type,[Windows.Style]::new($type)) }
    }
    function Flush-Divider { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout() }
    $catalog = @($plugin.GetAvailableStartPageViews().Views | Where-Object ViewId -eq 'DividerWidget')
    if($catalog.Count -ne 1 -or !$catalog[0].AllowMultipleInstances) { throw 'Divider/spacer widget missing or single-instance only' }
    $props = [LandingPage.Models.Layout.ViewProperties]::new(); $props.ViewId = 'DividerWidget'
    $options = $props.DividerSettings; $options.CustomColor = $true; $options.Color = '#FF00FF'; $options.LineOpacity = 1; $options.LineThickness = 4
    $widget = $plugin.GetStartPageView('DividerWidget',[guid]::NewGuid())
    $node = [LandingPage.Models.Layout.GridNode]::new(); $node.ViewProperties = $props
    $model = [LandingPage.ViewModels.Layout.GridNodeViewModel]::new($node); $model.IsLeaf = $true; $model.HasView = $true
    [void]$model.View.Children.Add($widget)
    $hostPanel = [LandingPage.Views.GridNodeView]::new(); $hostPanel.DataContext = $model
    [void]$layout.Children.Add($hostPanel); Flush-Divider
    if(![object]::ReferenceEquals($widget.Settings,$options)) { throw 'Divider ancestor settings binding failed' }
    $drawing = $widget.GetType().GetField('drawing',$flags).GetValue($widget)
    function Divider-Pixels {
        Flush-Divider
        $width = [int][Math]::Ceiling($drawing.ActualWidth); $height = [int][Math]::Ceiling($drawing.ActualHeight)
        if($width -lt 1 -or $height -lt 1) { throw 'Divider drawing has no layout area' }
        $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($width,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($drawing); $pixels = [byte[]]::new($width*$height*4); $bitmap.CopyPixels($pixels,$width*4,0)
        $count = 0; for($index=3;$index -lt $pixels.Length;$index+=4) { if($pixels[$index] -gt 0) { $count++ } }; $count
    }
    $solid = Divider-Pixels
    $options.LineStyle = 'Dashed'; $dashed = Divider-Pixels
    $options.LineStyle = 'Dotted'; $dotted = Divider-Pixels
    if($solid -le 0 -or $dashed -le 0 -or $dotted -le 0 -or $dashed -ge $solid -or $dotted -ge $solid) { throw 'Rendered line styles are empty or indistinguishable' }
    foreach($decoration in [Enum]::GetValues([LandingPage.Settings.DividerDecoration])) {
        $options.Decoration = $decoration; $options.LineStyle = 'Solid'
        foreach($orientation in @('Horizontal','Vertical')) {
            $options.Orientation = $orientation
            if((Divider-Pixels) -le 0) { throw ('Decorative divider does not render: '+$decoration+' '+$orientation) }
            $smallBounds = [LandingPage.Views.DividerDrawing]::LineBounds([Windows.Size]::new(8,6),$options)
            if($smallBounds.Left -lt 0 -or $smallBounds.Top -lt 0 -or $smallBounds.Right -gt 8 -or $smallBounds.Bottom -gt 6) { throw 'Pattern bounds escape a small widget' }
        }
    }
    $options.Decoration = 'Circles'; $options.FillShapes = $false; $outline = Divider-Pixels
    $options.FillShapes = $true; if((Divider-Pixels) -le $outline) { throw 'Filled shapes do not differ from outlined shapes' }
    $options.Decoration = 'Line'; $options.Orientation = 'Horizontal'
    foreach($style in @('DashDot','DashDotDot','Custom')) { $options.LineStyle = $style; if((Divider-Pixels) -le 0) { throw ('Stroke style not rendered: '+$style) } }
    $options.LineStyle = 'Solid'
    'PASS: all ten divider types render in both orientations, small-panel bounds, filled/outline shapes and additional stroke patterns.'
    $options.Mode = 'Spacer'; if((Divider-Pixels) -ne 0) { throw 'Spacer mode still draws a line' }
    $options.Mode = 'Divider'; $options.LineStyle = 'Solid'; $options.Orientation = 'Vertical'; if((Divider-Pixels) -le 0) { throw 'Vertical divider does not render' }
    'PASS: multiple-instance registration, actual panel settings binding, rendered solid/dashed/dotted/vertical lines and truly blank spacer mode.'
    $size = [Windows.Size]::new(300,100); $options.Orientation = 'Horizontal'; $options.Inset = 20; $options.Length = 50; $options.Position = 'End'; $options.LengthAlignment = 'Start'
    $bounds = [LandingPage.Views.DividerDrawing]::LineBounds($size,$options)
    if($bounds.Left -ne 20 -or $bounds.Width -ne 130 -or $bounds.Bottom -ne 100) { throw 'Length, inset or placement is incorrect' }
    foreach($orientation in @('Horizontal','Vertical')) {
        $options.Orientation = $orientation; $options.Inset = 0; $options.LineThickness = 30
        $small = [LandingPage.Views.DividerDrawing]::LineBounds([Windows.Size]::new(3,2),$options)
        if($small.Left -lt 0 -or $small.Top -lt 0 -or $small.Right -gt 3 -or $small.Bottom -gt 2) { throw 'Line exceeds its small panel bounds' }
        $drawing.Measure([Windows.Size]::new(3,2))
        if($drawing.DesiredSize.Width -gt 3 -or $drawing.DesiredSize.Height -gt 2) { throw 'Preferred size forces a small panel to overflow' }
    }
    $options.Inset = 1000
    if(![LandingPage.Views.DividerDrawing]::LineBounds($size,$options).IsEmpty) { throw 'Oversized inset creates invalid line geometry' }
    $options.LineOpacity = [double]::NaN; if($options.LineOpacity -ne .6) { throw 'Non-finite opacity not normalized' }
    $clone = [LandingPage.Models.DashboardProfiles]::Clone($props); $clone.DividerSettings.Mode = 'Spacer'
    if($options.Mode -ne 'Divider') { throw 'Profile clone shares divider settings' }
    'PASS: placement/length geometry, small-slot containment, finite settings and independent profile snapshots.'
    $replacement = [LandingPage.Settings.DividerWidgetSettings]::new(); $props.DividerSettings = $replacement; Flush-Divider
    if(![object]::ReferenceEquals($widget.Settings,$replacement)) { throw 'Divider settings reset did not refresh widget' }
    $editor = [LandingPage.Views.Settings.DividerWidgetSettingsView]::new(); $editor.DataContext = $replacement
    [void]$view.Content.Children.Add($editor); Flush-Divider
    $thicknessSlider = $editor.FindName('ThicknessSlider'); $thicknessSlider.Value = 7.5; Flush-Divider
    if($replacement.LineThickness -ne 7.5) { throw 'Thickness slider did not update divider settings' }
    $editor.FindName('DecorationSelector').SelectedValue = [LandingPage.Settings.DividerDecoration]::Wave; Flush-Divider
    if($replacement.Decoration -ne 'Wave') { throw 'Divider type selection did not update settings' }

    [void]$view.Content.Children.Remove($editor); [void]$layout.Children.Remove($hostPanel); Flush-Divider
    if($widget.GetType().GetField('observed',$flags).GetValue($widget)) { throw 'Unloaded divider retains a settings subscription' }
    'PASS: settings editor opens, reset follows the actual panel and unload detaches listeners.'
    $dashboardConnections = $view.FindName('DividerConnections')
    if($dashboardConnections.GetType().GetField('widgets',$flags).GetValue($dashboardConnections).Contains($widget)) { throw 'Unloaded divider remains registered with dashboard connections' }
    $connectionWindow = [Windows.Window]::new(); $connectionWindow.Width = 420; $connectionWindow.Height = 300; $connectionWindow.Opacity = 0; $connectionWindow.ShowInTaskbar = $false; $connectionWindow.ShowActivated = $false
    $canvas = [Windows.Controls.Canvas]::new(); $canvas.Width = 400; $canvas.Height = 250; $connectionWindow.Content = $canvas
    $connectionLayer = [LandingPage.Controls.DividerConnectionLayer]::new(); $connectionLayer.Width = 400; $connectionLayer.Height = 250
    $vertical = [LandingPage.Views.DividerWidgetView]::new(); $vertical.Settings = [LandingPage.Settings.DividerWidgetSettings]::new(); $vertical.Width = 24; $vertical.Height = 70
    $vertical.Settings.Orientation = 'Vertical'; $vertical.Settings.Inset = 0; $vertical.Settings.ConnectEnds = $true; $vertical.Settings.ConnectStart = $false; $vertical.Settings.ConnectEnd = $true; $vertical.Settings.LineThickness = 2
    $vertical.Settings.CustomColor = $true; $vertical.Settings.Color = '#FF0000'; $vertical.Settings.LineOpacity = 1
    [Windows.Controls.Canvas]::SetLeft($vertical,140); [Windows.Controls.Canvas]::SetTop($vertical,10)
    $horizontal = [LandingPage.Views.DividerWidgetView]::new(); $horizontal.Settings = [LandingPage.Settings.DividerWidgetSettings]::new(); $horizontal.Settings.Inset = 0; $horizontal.Width = 260; $horizontal.Height = 24
    [Windows.Controls.Canvas]::SetLeft($horizontal,20); [Windows.Controls.Canvas]::SetTop($horizontal,150)
    [void]$canvas.Children.Add($vertical); [void]$canvas.Children.Add($horizontal); [void]$canvas.Children.Add($connectionLayer)
    function Flush-Connections { $connectionWindow.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $connectionWindow.UpdateLayout() }
    function Bridge-Count { $connectionLayer.GetType().GetField('bridges',$flags).GetValue($connectionLayer).Count }
    $register = $connectionLayer.GetType().GetMethod('Register',$flags); $unregister = $connectionLayer.GetType().GetMethod('Unregister',$flags)
    try {
        $connectionWindow.Show()
        $register.Invoke($connectionLayer,@($vertical)); $register.Invoke($connectionLayer,@($horizontal)); Flush-Connections
        if((Bridge-Count) -ne 1) { throw 'Vertical divider does not join the horizontal divider across the panel gap' }
        $pixels = [byte[]]::new(400*250*4); $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(400,250,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($connectionLayer); $bitmap.CopyPixels($pixels,1600,0)
        if($pixels[(120*400+152)*4+3] -eq 0) { throw 'Connection is not drawn in the space outside the original widget' }
        $externalDrawing = $vertical.GetType().GetField('drawing',$flags).GetValue($vertical)
        if(!$externalDrawing.GetType().GetProperty('RenderExternally',$flags).GetValue($externalDrawing)) { throw 'Connected source still renders separately from its extension' }
        # Fractional placement and scaling must not create a seam or a thickness change.
        [Windows.Controls.Canvas]::SetLeft($vertical,140.35); [Windows.Controls.Canvas]::SetTop($vertical,10.2)
        $vertical.RenderTransform = [Windows.Media.ScaleTransform]::new(1.25,1); $vertical.Opacity = .6; Flush-Connections
        $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(400,250,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($connectionLayer); $bitmap.CopyPixels($pixels,1600,0)
        for($column = 0;$column -lt 400;$column++) {
            if($pixels[(40*400+$column)*4+3] -ne $pixels[(120*400+$column)*4+3]) { throw 'Original divider and connector occupy different pixel columns or use different opacity' }
        }
        $vertical.RenderTransform = [Windows.Media.Transform]::Identity; $vertical.Opacity = 1
        [Windows.Controls.Canvas]::SetLeft($vertical,140); [Windows.Controls.Canvas]::SetTop($vertical,10); Flush-Connections
        $vertical.Settings.Decoration = 'Wave'; Flush-Connections
        if((Bridge-Count) -ne 1) { throw 'Decorative divider lost its connection' }
        $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(400,250,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($connectionLayer); $bitmap.CopyPixels($pixels,1600,0)
        $waveVisible = $false; for($column=135;$column -le 170;$column++) { if($pixels[(120*400+$column)*4+3] -gt 0) { $waveVisible = $true } }
        if(!$waveVisible) { throw 'Wave pattern is not drawn across the connector gap' }
        $horizontal.Settings.Decoration = 'Wave'; $horizontal.Settings.PatternSize = 18; $horizontal.Settings.PatternSpacing = 38; Flush-Connections
        $bridge = $connectionLayer.GetType().GetField('bridges',$flags).GetValue($connectionLayer)[0]
        if([Math]::Abs($bridge.End.Y-162) -lt 1) { throw 'Wave-to-wave junction still targets the invisible center axis' }
        $target = $bridge.GetType().GetField('Target',$flags).GetValue($bridge)
        $outline = $connectionLayer.GetType().GetMethod('Surface',$flags).Invoke($connectionLayer,@($target))
        # The endpoint must actually touch the painted target, rather than stop in a wave gap.
        $contact = $false
        foreach($offset in @(-.1,0,.1)) { if($outline.FillContains([Windows.Point]::new($bridge.End.X,$bridge.End.Y+$offset))) { $contact = $true } }
        if(!$contact) { throw 'Decorative junction does not touch the target stroke' }
        $canvas.Background = [Windows.Media.Brushes]::Black
        $junctionPreview = [Windows.Media.Imaging.RenderTargetBitmap]::new(400,250,96,96,[Windows.Media.PixelFormats]::Pbgra32); $junctionPreview.Render($canvas)
        $previewPath = Join-Path $tempRoot 'divider-wave-junction.png'
        $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($junctionPreview))
        $output = [IO.File]::Create($previewPath); try { $encoder.Save($output) } finally { $output.Dispose() }
        Write-Host ('Junction preview: '+$previewPath)
        foreach($sourceType in @('DoubleLine','TripleLine','DoubleWave','Zigzag','Circles','Squares','Diamonds','Chevrons')) {
            $vertical.Settings.Decoration = $sourceType; Flush-Connections
            if((Bridge-Count) -ne 1) { throw ('Decorative source lost its junction: '+$sourceType) }
            $bridge = $connectionLayer.GetType().GetField('bridges',$flags).GetValue($connectionLayer)[0]
            $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(400,250,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($connectionLayer); $bitmap.CopyPixels($pixels,1600,0)
            $touches = $false; $x = [int][Math]::Floor($bridge.End.X); $y = [int][Math]::Floor($bridge.End.Y)
            foreach($dx in @(-1,0,1)) { foreach($dy in @(-1,0,1)) { if($pixels[(($y+$dy)*400+$x+$dx)*4+3] -gt 0) { $touches = $true } } }
            if(!$touches) { throw ('Source pattern ends before its junction: '+$sourceType) }
        }
        $vertical.Settings.Decoration = 'Line'; $horizontal.Settings.Decoration = 'Line'; Flush-Connections
        [Windows.Controls.Canvas]::SetTop($horizontal,180); Flush-Connections
        if((Bridge-Count) -ne 0) { throw 'Connection exceeds its configured distance' }
        $vertical.Settings.ConnectionRange = 200; Flush-Connections
        if((Bridge-Count) -ne 1) { throw 'Connection distance changes are not applied' }
        $vertical.Settings.ConnectEnd = $false; Flush-Connections
        if((Bridge-Count) -ne 0) { throw 'Disabling an endpoint did not remove its connection' }
        $vertical.Settings.ConnectEnd = $true; $horizontal.Settings.Mode = 'Spacer'; Flush-Connections
        if((Bridge-Count) -ne 0) { throw 'Spacer mode remains a connection target' }
        $horizontal.Settings.Mode = 'Divider'; $horizontal.Visibility = 'Collapsed'; Flush-Connections
        if((Bridge-Count) -ne 0) { throw 'Hidden divider remains a connection target' }
        $horizontal.Visibility = 'Visible'; Flush-Connections
        if((Bridge-Count) -ne 1) { throw 'Restored divider does not reconnect' }
        $horizontal.Settings.Decoration = 'DoubleWave'; $horizontal.Settings.LineStyle = 'Solid'; $horizontal.Settings.LineThickness = 3.5; $horizontal.Settings.LineOpacity = .42
        $horizontal.Settings.CustomColor = $true; $horizontal.Settings.Color = '#45ABCD'; $horizontal.Settings.PatternSize = 13; $horizontal.Settings.PatternSpacing = 39
        $horizontal.Settings.DashLength = 11; $horizontal.Settings.DashGap = 4.5; $horizontal.Settings.RoundCaps = $true; $horizontal.Settings.FillShapes = $true; Flush-Connections
        $keptFields = @('Mode','Orientation','Position','LengthAlignment','Length','Inset','SpaceSize','ConnectEnds','ConnectStart','ConnectEnd','ConnectionRange')
        $keptValues = @($keptFields | ForEach-Object { $vertical.Settings.$_ })
        $matchEditor = [LandingPage.Views.Settings.DividerWidgetSettingsView]::new(); $matchEditor.DataContext = $vertical.Settings
        [void]$canvas.Children.Add($matchEditor); Flush-Connections
        $matchEditor.GetType().GetField('connectionLayer',$flags).SetValue($matchEditor,$connectionLayer)
        $matchEditor.FindName('MatchConnectionEnd').SelectedValue = '2'
        $matchEditor.GetType().GetMethod('UpdateMatch',$flags).Invoke($matchEditor,$null)
        if(!$matchEditor.FindName('MatchAppearanceButton').IsEnabled) { throw 'Match appearance button does not find its connected target' }
        $matchEditor.FindName('MatchAppearanceButton').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)); Flush-Connections
        foreach($property in @('Decoration','LineStyle','LineThickness','LineOpacity','CustomColor','Color','PatternSize','PatternSpacing','DashLength','DashGap','RoundCaps','FillShapes')) {
            if($vertical.Settings.$property -ne $horizontal.Settings.$property) { throw ('Matching appearance missed '+$property) }
        }
        for($index=0;$index -lt $keptFields.Count;$index++) { if($vertical.Settings.($keptFields[$index]) -ne $keptValues[$index]) { throw ('Matching appearance changed '+$keptFields[$index]) } }
        $horizontal.Settings.Color = '#FEDCBA'; Flush-Connections
        if($vertical.Settings.Color -ne '#45ABCD') { throw 'Appearance copy created a live/shared link' }
        $matchEditor.FindName('MatchConnectionEnd').SelectedValue = '1'
        if($matchEditor.FindName('MatchAppearanceButton').IsEnabled) { throw 'Match appearance accepts an unconnected endpoint' }
        [void]$canvas.Children.Remove($matchEditor)
        $vertical.Settings.ConnectEnds = $false; Flush-Connections
        if((Bridge-Count) -ne 0) { throw 'Disabling connections leaves an overlay behind' }
        if($externalDrawing.GetType().GetProperty('RenderExternally',$flags).GetValue($externalDrawing)) { throw 'Disconnecting did not restore local line drawing' }
    } finally { $unregister.Invoke($connectionLayer,@($vertical)); $unregister.Invoke($connectionLayer,@($horizontal)); $connectionWindow.Close() }
    'PASS: continuous pixel alignment, actual wave-curve contact, all decorative junctions, one-time appearance matching, endpoint selection, preserved placement and cleanup.'

'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
