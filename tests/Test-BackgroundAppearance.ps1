# Run -STA. Temporary settings and rendered synthetic images only.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$backgroundTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $backgroundTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    foreach($type in @([Windows.Controls.TextBox],[Windows.Controls.Expander],[Windows.Controls.CheckBox],[Windows.Controls.ComboBox],[Windows.Controls.Button])) {
        if(!$app.Resources.Contains($type)) { $app.Resources.Add($type,[Windows.Style]::new($type)) }
    }
    function Flush-Background { $window.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout() }
    function Pixels($visual,$width=100,$height=100) {
        Flush-Background
        $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($width,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($visual); $bytes = [byte[]]::new($width*$height*4); $bitmap.CopyPixels($bytes,$width*4,0); return ,$bytes
    }
    $appearance = [LandingPage.Settings.BackgroundAppearanceSettings]::new()
    $viewport = [Windows.Size]::new(100,100); $imageSize = [Windows.Size]::new(200,100)
    $bounds = [LandingPage.Controls.BackgroundArtwork]::ImageBounds($viewport,$imageSize,$appearance)
    if($bounds.X -ne -50 -or $bounds.Y -ne 0 -or $bounds.Width -ne 200) { throw 'Default background no longer matches centered fill' }
    $appearance.Fit = 'Fit'; $bounds = [LandingPage.Controls.BackgroundArtwork]::ImageBounds($viewport,$imageSize,$appearance)
    if($bounds.Width -ne 100 -or $bounds.Height -ne 50 -or $bounds.Y -ne 25) { throw 'Fit crops or distorts the image' }
    $appearance.Fit = 'Stretch'; $bounds = [LandingPage.Controls.BackgroundArtwork]::ImageBounds($viewport,$imageSize,$appearance)
    if($bounds.Width -ne 100 -or $bounds.Height -ne 100) { throw 'Stretch does not fill the viewport' }
    $appearance.Fit = 'OriginalSize'; $appearance.Zoom = 2
    if([LandingPage.Controls.BackgroundArtwork]::ImageBounds($viewport,$imageSize,$appearance).Width -ne 400) { throw 'Original size or zoom is incorrect' }
    $drawing = [Windows.Media.DrawingVisual]::new(); $context = $drawing.RenderOpen()
    $context.DrawRectangle([Windows.Media.Brushes]::Red,$null,[Windows.Rect]::new(0,0,100,100)); $context.DrawRectangle([Windows.Media.Brushes]::Blue,$null,[Windows.Rect]::new(100,0,100,100)); $context.Close()
    $image = [Windows.Media.Imaging.RenderTargetBitmap]::new(200,100,96,96,[Windows.Media.PixelFormats]::Pbgra32); $image.Render($drawing); $image.Freeze()
    $art = [LandingPage.Controls.BackgroundArtwork]::new(); $art.HorizontalAlignment = 'Left'; $art.VerticalAlignment = 'Top'; $art.Width = 100; $art.Height = 100; $art.Source = $image; $art.Appearance = $appearance
    [void]$view.Content.Children.Add($art)
    $appearance.Fit = 'Fill'; $appearance.Zoom = 1; $appearance.PositionX = 0
    $bytes = Pixels $art; $middle = (50*100+50)*4
    if($bytes[$middle+2] -ne 255 -or $bytes[$middle] -ne 0) { throw 'Left focal position does not show left artwork' }
    $appearance.PositionX = 1; $bytes = Pixels $art
    if($bytes[$middle] -ne 255 -or $bytes[$middle+2] -ne 0) { throw 'Right focal position does not show right artwork' }
    $appearance.FlipHorizontal = $true; $bytes = Pixels $art
    if($bytes[$middle+2] -ne 255) { throw 'Horizontal flip is not rendered' }
    $appearance.FlipHorizontal = $false; $appearance.Fit = 'Fit'; $bytes = Pixels $art
    if($bytes[(10*100+50)*4+3] -ne 0 -or $bytes[$middle+3] -eq 0) { throw 'Fit mode does not expose letterbox space' }
    $appearance.ImageOpacity = .25; $bytes = Pixels $art
    if([Math]::Abs($bytes[$middle+3]-64) -gt 1) { throw 'Image opacity is not independent of queue opacity' }
    $art.Measure([Windows.Size]::new(100,100)); if($art.DesiredSize.Width -gt 100 -or $art.DesiredSize.Height -gt 100) { throw 'Image content enlarges the dashboard layout' }
    $nativePixels = [byte[]]::new(20*10*4); for($index=0;$index -lt $nativePixels.Length;$index+=4) { $nativePixels[$index+2] = 255; $nativePixels[$index+3] = 255 }
    $lowDpiImage = [Windows.Media.Imaging.BitmapSource]::Create(20,10,12,12,[Windows.Media.PixelFormats]::Bgra32,$null,$nativePixels,80); $lowDpiImage.Freeze()
    $nativeSize = [LandingPage.Controls.BackgroundArtwork]::NaturalImageSize($lowDpiImage,[LandingPage.Settings.BackgroundFit]::OriginalSize,1,1)
    $scaledSize = [LandingPage.Controls.BackgroundArtwork]::NaturalImageSize($lowDpiImage,[LandingPage.Settings.BackgroundFit]::OriginalSize,1.25,1.25)
    if($nativeSize.Width -ne 20 -or $nativeSize.Height -ne 10 -or $scaledSize.Width -ne 16 -or $scaledSize.Height -ne 8) { throw 'Original size uses image DPI metadata or ignores monitor scaling' }
    $art.Source = $lowDpiImage; $appearance.Fit = 'OriginalSize'; $appearance.Zoom = 1; $appearance.PositionX = .5; $appearance.ImageOpacity = 1
    $bytes = Pixels $art; $opaque = 0; for($index=3;$index -lt $bytes.Length;$index+=4) { if($bytes[$index] -gt 0) { $opaque++ } }
    if($opaque -ne 200) { throw ('Original image pixels were enlarged or blurred: '+$opaque+' instead of 200') }
    $art.Source = $image
    'PASS: original-size pixel rendering ignores low-DPI metadata and accounts for display scaling.'
    'PASS: framing geometry, rendered focal positions/flip/fit/opacity and layout containment.'

    $effects = [LandingPage.Controls.BackgroundEffectsLayer]::new(); $effects.HorizontalAlignment = 'Left'; $effects.VerticalAlignment = 'Top'; $effects.Width = 100; $effects.Height = 100; $effects.Appearance = $appearance; [void]$view.Content.Children.Add($effects)
    $appearance.TintEnabled = $true; $appearance.TintColor = '#00FF00'; $appearance.TintOpacity = .5; $bytes = Pixels $effects
    if([Math]::Abs($bytes[$middle+1]-128) -gt 1) { throw 'Tint color/strength is not rendered' }
    $appearance.TintEnabled = $false; $appearance.Fade = 'Bottom'; $appearance.FadeOpacity = 1; $bytes = Pixels $effects
    if($bytes[(90*100+50)*4+3] -le $bytes[(10*100+50)*4+3]) { throw 'Fade direction is inverted' }
    $appearance.Fade = 'None'; $appearance.Vignette = 1; $bytes = Pixels $effects
    if($bytes[3] -le $bytes[$middle+3]) { throw 'Vignette does not darken the edges more than the center' }
    $appearance.Vignette = 0; $appearance.UseBaseColor = $true; $appearance.BaseColor = '#123456'; $effects.BaseLayer = $true; $bytes = Pixels $effects
    if($bytes[$middle] -ne 0x56 -or $bytes[$middle+2] -ne 0x12) { throw 'Base color is not rendered' }
    'PASS: tinted overlay, edge fade direction, vignette and solid base color.'

    $sourcePath = Join-Path $tempRoot 'background-test.png'; $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($image))
    $output = [IO.File]::Create($sourcePath); try { $encoder.Save($output) } finally { $output.Dispose() }
    Set-Setting $settingsModel.Settings 'BackgroundImagePath' $sourcePath; Set-Setting $settingsModel.Settings 'BackgroundImageUri' ([uri]$sourcePath)
    Set-Setting $settingsModel.Settings 'BlurAmount' ([double]0); Set-Setting $settingsModel.Settings 'RenderScale' ([double]1); Set-Setting $settingsModel.Settings 'OverlayOpacity' ([double]0); Set-Setting $settingsModel.Settings 'NoiseOpacity' ([double]0)
    $background.BackgroundImageQueue.Clear(); $queueItem = [LandingPage.ViewModels.BackgroundQueueItem]::new([uri]$sourcePath,1); $background.BackgroundImageQueue.Add($queueItem)
    Set-Setting $settingsModel.Settings 'BackgroundAppearance' ([LandingPage.Settings.BackgroundAppearanceSettings]::new())
    $backgroundView = [LandingPage.Views.BackgroundView]::new(); $backgroundView.DataContext = $background; $backgroundView.HorizontalAlignment = 'Left'; $backgroundView.VerticalAlignment = 'Top'; $backgroundView.Width = 100; $backgroundView.Height = 100; [void]$view.Content.Children.Add($backgroundView); Flush-Background
    function Find-Artwork($root) {
        if($root -is [LandingPage.Controls.BackgroundArtwork] -and $root -isnot [LandingPage.Controls.BackgroundEffectsLayer]) { $root; return }
        for($index=0;$index -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($root);$index++) { Find-Artwork ([Windows.Media.VisualTreeHelper]::GetChild($root,$index)) }
    }
    $boundArt = @(Find-Artwork $backgroundView)[0]
    if(!$boundArt -or $boundArt.ActualWidth -ne 100 -or $boundArt.ActualHeight -ne 100 -or !$boundArt.Source) { throw 'Real background template no longer fills its allocated viewport' }
    $imageLayer = $backgroundView.FindName('BackgroundImageLayer')
    Set-Setting $settingsModel.Settings 'RenderScale' ([double]0.05); Set-Setting $settingsModel.Settings 'BlurAmount' ([double]2)
    $nativeOptions = Get-Setting $settingsModel.Settings 'BackgroundAppearance'
    $items = @($imageLayer.Children | Where-Object { $_ -is [Windows.Controls.ItemsControl] })[0]
    foreach($mode in [Enum]::GetValues([LandingPage.Settings.BackgroundFit])) {
        $nativeOptions.Fit = $mode; $nativeOptions.Zoom = 2; Flush-Background
        if($null -ne $imageLayer.CacheMode) { throw ('Sizing mode still upscales a low-resolution render cache: '+$mode) }
        if($items.Effect.Radius -ne 2) { throw ('Sizing mode still amplifies the configured blur: '+$mode) }
        if([Windows.Media.RenderOptions]::GetBitmapScalingMode($boundArt) -ne [Windows.Media.BitmapScalingMode]::HighQuality) { throw 'Background image scaling is not high quality' }
    }
    $nativeOptions.Fit = 'Fill'; $nativeOptions.Zoom = 1
    Set-Setting $settingsModel.Settings 'RenderScale' ([double]1); Set-Setting $settingsModel.Settings 'BlurAmount' ([double]0); Flush-Background
    'PASS: all sizing/zoom modes bypass low-resolution render caching, use the actual blur radius and enable high-quality image scaling.'
    $cachedImage = $boundArt.Source; $activeAppearance = (Get-Setting $settingsModel.Settings 'BackgroundAppearance'); $activeAppearance.Zoom = 1.5; $activeAppearance.PositionX = .2; Flush-Background
    if(![object]::ReferenceEquals($boundArt.Source,$cachedImage) -or $background.BackgroundImageQueue[0] -ne $queueItem) { throw 'Framing changes reload or replace the cached background' }
    $capture = [LandingPage.Models.DashboardBackground]::Capture($settingsModel.Settings); $copy = $capture.Copy(); $copy.Appearance.Zoom = 3
    if($capture.Appearance.Zoom -ne 1.5 -or $activeAppearance.Zoom -ne 1.5) { throw 'Captured/copied page appearances share settings' }
    $vm.CaptureCurrentPage(); $firstPage = $vm.SelectedPage
    $secondPage = [LandingPage.Models.DashboardPages]::Add($settingsModel.Settings,'Background appearance test'); $secondPage.Background.Appearance.Zoom = 2.5
    Wait-Task ($vm.SwitchPageAsync($secondPage))
    if((Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom -ne 2.5 -or $firstPage.Background.Appearance.Zoom -ne 1.5) { throw 'Page switch does not retain independent background appearance' }
    Wait-Task ($vm.SwitchPageAsync($firstPage))
    $vm.BeginPageEditing(); (Get-Setting $settingsModel.Settings 'BackgroundAppearance').Vignette = .9; Wait-Task ($vm.EndPageEditingAsync($false))
    if((Get-Setting $settingsModel.Settings 'BackgroundAppearance').Vignette -ne 0) { throw 'Cancelling page editing retained background appearance edits' }
    'PASS: actual image template, cached-source reuse, independent page snapshots/switching and cancel rollback.'
    Set-Field $settingsModel 'plugin' $plugin
    $privateFirst = $vm.SelectedPage; $privateSecond = $vm.Pages | Where-Object Name -eq 'Background appearance test'
    Set-Field $settingsModel '<editingClone>k__BackingField' ([LandingPage.Models.DashboardProfiles]::Clone($settingsModel.Settings)); Set-Setting $settingsModel.Settings 'EnableGlobalBackground' $true
    (Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom = 3.25
    $settingsModel.EndEdit(); $vm.CaptureCurrentPage()
    Wait-Task ($vm.SwitchPageAsync($privateSecond))
    if((Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom -ne 3.25 -or $privateFirst.Background.Appearance.Zoom -ne 1.5 -or $privateSecond.Background.Appearance.Zoom -ne 2.5) { throw 'Global background does not remain shared or overwrites private page backgrounds' }
    $sharedAppearance = Get-Setting $settingsModel.Settings 'BackgroundAppearance'
    Wait-Task ($vm.SwitchPageAsync($privateFirst))
    if(![object]::ReferenceEquals($sharedAppearance,(Get-Setting $settingsModel.Settings 'BackgroundAppearance'))) { throw 'Global background reloaded on dashboard navigation' }
    [void][LandingPage.Models.DashboardPages]::Ensure($settingsModel.Settings)
    if($privateFirst.Background.Appearance.Zoom -ne 1.5) { throw 'Startup migration overwrites private background in global mode' }
    $vm.UseGlobalBackgroundForPage = $false
    if((Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom -ne 1.5 -or $vm.SelectedPage.UseGlobalBackground) { throw 'Page opt-out does not restore the private background' }
    (Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom = 1.75; $vm.CaptureCurrentPage()
    Wait-Task ($vm.SwitchPageAsync($privateSecond))
    if((Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom -ne 3.25 -or !$vm.UseGlobalBackgroundForPage) { throw 'Opt-out changes the shared background on other pages' }
    Wait-Task ($vm.SwitchPageAsync($privateFirst))
    if((Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom -ne 1.75 -or $vm.UseGlobalBackgroundForPage) { throw 'Private override does not survive switching pages' }
    $privateProfile = [LandingPage.Models.DashboardProfiles]::Capture($settingsModel.Settings,'Private background override')
    $privateTarget = [LandingPage.LandingPageSettings]::new()
    [LandingPage.Models.DashboardProfiles]::ApplyData($privateTarget,$privateProfile.Snapshot)
    if((Get-Setting $privateTarget 'BackgroundAppearance').Zoom -ne 1.75) { throw 'Profile ignores page opt-out' }
    $vm.BeginPageEditing(); $vm.UseGlobalBackgroundForPage = $true; Wait-Task ($vm.EndPageEditingAsync($false))
    if($vm.UseGlobalBackgroundForPage -or (Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom -ne 1.75) { throw 'Cancelling page edits does not restore the override' }
    $privateFirst = $vm.SelectedPage; $privateSecond = $vm.Pages | Where-Object Name -eq 'Background appearance test'
    $vm.UseGlobalBackgroundForPage = $true
    if((Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom -ne 3.25) { throw 'Re-enabling global background fails to restore the shared image/appearance' }
    $vm.UseGlobalBackgroundForPage = $false
    (Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom = 1.5
    $vm.UseGlobalBackgroundForPage = $true
    'PASS: per-dashboard global background opt-out, independent edits, navigation, profile persistence, cancellation and rejoining shared background.'
    $globalProfile = [LandingPage.Models.DashboardProfiles]::Capture($settingsModel.Settings,'Global background test')
    $profileTarget = [LandingPage.LandingPageSettings]::new()
    [LandingPage.Models.DashboardProfiles]::ApplyData($profileTarget,$globalProfile.Snapshot)
    if((Get-Setting $profileTarget 'BackgroundAppearance').Zoom -ne 3.25 -or !(Get-Setting $profileTarget 'EnableGlobalBackground')) { throw 'Profile loses shared global background' }
    Set-Field $settingsModel '<editingClone>k__BackingField' ([LandingPage.Models.DashboardProfiles]::Clone($settingsModel.Settings)); Set-Setting $settingsModel.Settings 'EnableGlobalBackground' $false; $settingsModel.EndEdit()
    if((Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom -ne 1.5) { throw 'Disabling global background does not restore active private background' }
    Wait-Task ($vm.SwitchPageAsync($privateSecond))
    if((Get-Setting $settingsModel.Settings 'BackgroundAppearance').Zoom -ne 2.5) { throw 'Disabling global background loses inactive private background' }
    Wait-Task ($vm.SwitchPageAsync($privateFirst))
    'PASS: shared global backgrounds across pages, no reload, private background preservation, startup/profile restoration and disabling global mode.'
    $upgraded = [LandingPage.LandingPageSettings]::new(); Set-Setting $upgraded 'EnableGlobalBackground' $true
    (Get-Setting $upgraded 'BackgroundAppearance').Zoom = 2.75
    $upgradedOther = [LandingPage.Models.DashboardPage]::new(); $upgradedOther.Name = 'Other'; $upgradedOther.Background.Appearance.Zoom = 1.25
    $upgradedActive = [LandingPage.Models.DashboardPage]::new(); $upgradedActive.Name = 'Active'
    Set-Setting $upgraded 'DashboardPages' ([Collections.ObjectModel.ObservableCollection[LandingPage.Models.DashboardPage]]::new([LandingPage.Models.DashboardPage[]]@($upgradedActive,$upgradedOther)))
    Set-Setting $upgraded 'ActiveDashboardPageId' $upgradedActive.Id
    [void][LandingPage.Models.DashboardPages]::Ensure($upgraded)
    $seeded = Get-Setting $upgraded 'GlobalDashboardBackground'
    if(!$seeded -or $seeded.Appearance.Zoom -ne 2.75) { throw 'Configs saved before shared backgrounds do not seed the shared background from the active projection' }
    'PASS: pre-existing global-background configs seed the shared background on load.'
    $editor = [LandingPage.Views.Settings.BackgroundAppearanceSettingsView]::new(); $editor.DataContext = (Get-Setting $settingsModel.Settings 'BackgroundAppearance')
    [void]$view.Content.Children.Add($editor); Flush-Background
    $editor.FindName('ExcludeSidebarToggle').IsChecked = $true; if(!$editor.DataContext.ExcludeSidebar) { throw 'Sidebar exclusion checkbox does not update settings' }
    $editor.FindName('ZoomSlider').Value = 2; $editor.FindName('FitSelector').SelectedValue = [LandingPage.Settings.BackgroundFit]::Fit; Flush-Background
    if($editor.DataContext.Zoom -ne 2 -or $editor.DataContext.Fit -ne 'Fit') { throw 'Background appearance editor bindings do not update live settings' }
    $editor.GetType().GetMethod('Reset_Click',$flags).Invoke($editor,@($null,[Windows.RoutedEventArgs]::new())); Flush-Background
    if($editor.DataContext.Zoom -ne 1 -or $editor.DataContext.Fit -ne 'Fill' -or (Get-Setting $settingsModel.Settings 'BlurAmount') -ne 0 -or (Get-Setting $settingsModel.Settings 'BackgroundImagePath') -ne $sourcePath) { throw 'Appearance reset does not restore defaults independently from the selected image/legacy blur controls' }
    [void]$view.Content.Children.Remove($editor)
    'PASS: real framing editor controls and appearance-only reset preserve the source image and existing background controls.'
    Set-Setting $settingsModel.Settings 'EnableGlobalBackground' $true
    $nativeWindow = [Windows.Window]::new(); $nativeWindow.Width = 600; $nativeWindow.Height = 360; $nativeWindow.Opacity = 0; $nativeWindow.ShowActivated = $false; $nativeWindow.ShowInTaskbar = $false
    $nativeRoot = [Windows.Controls.Grid]::new(); $nativeWindow.Content = $nativeRoot
    $globalView = [LandingPage.Views.BackgroundView]::new(); $globalView.IsGlobalBackground = $true; $globalView.DataContext = $background; [void]$nativeRoot.Children.Add($globalView)
    $dock = [Windows.Controls.DockPanel]::new(); $dock.Margin = [Windows.Thickness]::new(0,20,0,0); [void]$nativeRoot.Children.Add($dock)
    $sidebar = [Windows.Controls.Border]::new(); $sidebar.Name = 'PART_Sidebar'; $sidebar.Width = 60; [Windows.Controls.DockPanel]::SetDock($sidebar,[Windows.Controls.Dock]::Left)
    $themeBorder = [Windows.Controls.Border]::new(); $themeBorder.Name = 'BorderContentHolder'; $themeBorder.BorderThickness = [Windows.Thickness]::new(0,0,1,0); $themeBorder.Resources['TestSidebarBrush'] = [Windows.Media.Brushes]::Blue; $themeBorder.SetResourceReference([Windows.Controls.Border]::BorderBrushProperty,'TestSidebarBrush'); $sidebar.Child = $themeBorder
    $content = [Windows.Controls.Border]::new(); $content.Background = [Windows.Media.Brushes]::Black; $content.Name = 'PART_ContentView'; [void]$dock.Children.Add($sidebar); [void]$dock.Children.Add($content)
    function Flush-NativeBackground { $nativeWindow.UpdateLayout(); $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $nativeWindow.UpdateLayout() }
    try {
        $nativeWindow.Show(); (Get-Setting $settingsModel.Settings 'BackgroundAppearance').ExcludeSidebar = $true; Flush-NativeBackground
        $viewportPanel = $globalView.FindName('BackgroundViewport')
        if($viewportPanel.Margin.Left -ne 60 -or $viewportPanel.Margin.Top -ne 20 -or [Math]::Abs($viewportPanel.ActualWidth-($globalView.ActualWidth-60)) -gt 1) { throw 'Global background still extends behind the left sidebar' }
        if($backgroundView.FindName('BackgroundViewport').Margin.Left -ne 0) { throw 'Sidebar exclusion also offsets the dashboard-local background' }
        $lines = Get-Setting $settingsModel.Settings 'BackgroundAppearance'
        $lines.ShowSidebarLine = $true; $lines.ShowTopBarLine = $true; $lines.SidebarLineThickness = 3; $lines.TopBarLineThickness = 2
        $lines.SidebarLineColor = '#FF00FF'; $lines.TopBarLineColor = '#00FF00'; $lines.LineOpacity = .6
        $chrome = [Windows.Shell.WindowChrome]::new(); $chrome.CaptionHeight = 40
        [Windows.Shell.WindowChrome]::SetWindowChrome($nativeWindow,$chrome); Flush-NativeBackground
        $separator = $globalView.FindName('SeparatorCanvas'); $side = $separator.Children[0]; $top = $separator.Children[1]
        if($separator.Parent -ne $nativeRoot -or [Windows.Controls.Panel]::GetZIndex($separator) -ne [int]::MaxValue) { throw 'Separators are obscured behind native content' }
        if($themeBorder.BorderBrush -ne [Windows.Media.Brushes]::Transparent) { throw 'Native sidebar border still doubles the custom separator' }
        if($side.Visibility -ne 'Visible' -or $side.Width -ne 3 -or [Windows.Controls.Canvas]::GetLeft($side) -ne 58.5 -or $side.Fill.Color.ToString() -ne '#FFFF00FF' -or $side.Opacity -ne .6) { throw 'Sidebar separator appearance/position is incorrect' }
        if($top.Visibility -ne 'Visible' -or $top.Height -ne 2 -or [Windows.Controls.Canvas]::GetTop($top) -ne 40 -or [Windows.Controls.Canvas]::GetLeft($top) -ne 61.5) { throw 'Top bar separator is not aligned with the caption and sidebar' }
        $caption = [Windows.Controls.Button]::new(); $caption.Name = 'PART_ButtonClose'; $caption.Width = 40; $caption.Height = 60
        $caption.VerticalAlignment = 'Top'; $caption.HorizontalAlignment = 'Right'; [void]$nativeRoot.Children.Add($caption)
        $globalView.GetType().GetField('captionButtons',$flags).SetValue($globalView,$null); Flush-NativeBackground
        if([Windows.Controls.Canvas]::GetTop($top) -ne 60) { throw 'Top separator cuts through tall native caption buttons' }
        $sideBounds = [Windows.Rect]::new([Windows.Controls.Canvas]::GetLeft($side),[Windows.Controls.Canvas]::GetTop($side),$side.Width,$side.Height)
        $topBounds = [Windows.Rect]::new([Windows.Controls.Canvas]::GetLeft($top),[Windows.Controls.Canvas]::GetTop($top),$top.Width,$top.Height)
        $intersection = [Windows.Rect]::Intersect($sideBounds,$topBounds)
        if(!$intersection.IsEmpty -and $intersection.Width*$intersection.Height -gt 0) { throw 'Separators overlap and double opacity at their junction' }
        $renderedLines = Pixels $nativeRoot; $linePixel = (60*100+90)*4
        if($renderedLines[$linePixel+1] -lt 140 -or $renderedLines[$linePixel+2] -gt 10) { throw 'Top separator is not painted above opaque Playnite content' }
        $caption.Height = 48; Flush-NativeBackground
        if([Windows.Controls.Canvas]::GetTop($top) -ne 48) { throw 'Top separator does not follow caption button resizing' }
        if($globalView.IsHitTestVisible -and $separator.IsHitTestVisible) { throw 'Separator intercepts clicks' }
        $copyLines = $lines.Copy(); $lines.ShowSidebarLine = $false; $lines.ShowTopBarLine = $false; Flush-NativeBackground
        if($themeBorder.BorderBrush -ne [Windows.Media.Brushes]::Blue) { throw 'Native sidebar border not restored after disabling custom line' }
        $themeBorder.Resources['TestSidebarBrush'] = [Windows.Media.Brushes]::Green; Flush-NativeBackground
        if($themeBorder.BorderBrush -ne [Windows.Media.Brushes]::Green) { throw 'Sidebar restoration lost the theme resource expression' }
        if(!$copyLines.ShowSidebarLine -or $copyLines.SidebarLineThickness -ne 3 -or $side.Visibility -ne 'Collapsed' -or $top.Visibility -ne 'Collapsed') { throw 'Separator copy or toggle is incorrect' }
        'PASS: sidebar/top bar separators, colors, thickness, opacity, placement, copy isolation and hit testing.'
        $sidebar.Width = 85; Flush-NativeBackground
        if($viewportPanel.Margin.Left -ne 85) { throw 'Background does not follow sidebar resizing' }
        [Windows.Controls.DockPanel]::SetDock($sidebar,[Windows.Controls.Dock]::Right); Flush-NativeBackground
        if($viewportPanel.Margin.Right -ne 85 -or $viewportPanel.Margin.Left -ne 0) { throw 'Right sidebar exclusion is incorrect' }
        $sidebar.Width = [double]::NaN; $sidebar.Height = 48; [Windows.Controls.DockPanel]::SetDock($sidebar,[Windows.Controls.Dock]::Top); Flush-NativeBackground
        if($viewportPanel.Margin.Top -ne 68) { throw 'Top sidebar exclusion is incorrect' }
        [Windows.Controls.DockPanel]::SetDock($sidebar,[Windows.Controls.Dock]::Bottom); Flush-NativeBackground
        if($viewportPanel.Margin.Bottom -ne 48 -or $viewportPanel.Margin.Top -ne 20) { throw 'Bottom sidebar exclusion is incorrect' }
        $sidebar.Visibility = 'Collapsed'; Flush-NativeBackground
        if($viewportPanel.Margin.Bottom -ne 0) { throw 'Hidden sidebar leaves a stale background gap' }
        (Get-Setting $settingsModel.Settings 'BackgroundAppearance').ExcludeSidebar = $false; Flush-NativeBackground
        if($viewportPanel.Margin -ne [Windows.Thickness]::new(0)) { throw 'Disabling sidebar exclusion does not restore the full-window background' }
    } finally { $nativeWindow.Close(); Flush-Background }
    if($nativeRoot.Children.Contains($separator)) { throw 'Separator overlay remains after unload' }
    if($globalView.GetType().GetField('hostContent',$flags).GetValue($globalView)) { throw 'Background retains native theme controls after unload' }
    $scopeSettings = $plugin.GetType().GetProperty('Settings',$flags).GetValue($plugin)
    [LandingPage.LandingPageSettings].GetProperty('EnableGlobalBackground').SetValue($scopeSettings,$true)
    if([LandingPage.LandingPageSettings].GetProperty('ShowBackgroundOutsideStartPage').GetValue($scopeSettings)) { throw 'Background must stay in StartPage by default' }
    Set-Field $plugin 'globalBackground' $globalView
    $updateScope = $plugin.GetType().GetMethod('UpdateGlobalBackgroundVisibility',$flags)
    foreach($case in @(@($true,$false,'Visible'),@($false,$false,'Collapsed'),@($false,$true,'Visible'),@($true,$true,'Visible'))) {
        Set-Field $plugin 'isViewOpen' $case[0]
        [LandingPage.LandingPageSettings].GetProperty('ShowBackgroundOutsideStartPage').SetValue($scopeSettings,$case[1])
        [void]$updateScope.Invoke($plugin,@())
        if($globalView.Visibility.ToString() -ne $case[2]) { throw 'Background scope did not follow navigation and opt-in' }
    }
    [LandingPage.LandingPageSettings].GetProperty('EnableGlobalBackground').SetValue($scopeSettings,$false)
    [void]$updateScope.Invoke($plugin,@())
    if($globalView.Visibility -ne 'Collapsed') { throw 'Disabled global background is visible' }
    Set-Field $plugin 'globalBackground' $null
    [LandingPage.LandingPageSettings].GetProperty('ShowBackgroundOutsideStartPage').SetValue($scopeSettings,$false)
    'PASS: background navigation isolation, explicit global opt-in and disabled state.'
    'PASS: optional sidebar exclusion, local/global isolation, resizing, all four sidebar placements, hidden-sidebar recovery and unload cleanup.'
    [void]$view.Content.Children.Remove($backgroundView); [void]$view.Content.Children.Remove($art); [void]$view.Content.Children.Remove($effects); Flush-Background
    if($art.GetType().GetField('observed',$flags).GetValue($art)) { throw 'Unloaded background retains its settings listener' }
'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
