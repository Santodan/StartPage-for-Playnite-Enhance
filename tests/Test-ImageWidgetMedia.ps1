# Run -STA. Image widget GIF/video playback (roadmap step 23): settings defaults and persistence, still vs animated
# display, option sync with the shared media player, link behaviour, pausing when hidden and releasing on unload,
# and the editor. Reuses the in-memory API/window fixture.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$imageTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $imageTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    function Pump-For($milliseconds) {
        $until = [datetime]::UtcNow.AddMilliseconds($milliseconds)
        do { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background); Start-Sleep -Milliseconds 10 } while ([datetime]::UtcNow -lt $until)
        $window.UpdateLayout()
    }
    $private = [Reflection.BindingFlags]'Instance,NonPublic'
    $mediaRoot = Join-Path $env:TEMP ('StartPage-image-media-' + [guid]::NewGuid()); [void][IO.Directory]::CreateDirectory($mediaRoot)
    $png = Join-Path $mediaRoot 'still.png'
    $visual = [Windows.Media.DrawingVisual]::new(); $dc = $visual.RenderOpen(); $dc.DrawRectangle([Windows.Media.Brushes]::SteelBlue,$null,[Windows.Rect]::new(0,0,48,32)); $dc.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(48,32,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create($png); $encoder.Save($stream); $stream.Dispose()
    $ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
    $gif = $null; $video = $null
    if ($ffmpeg) {
        $gif = Join-Path $mediaRoot 'clip.gif'; & $ffmpeg.Source -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=64x32:rate=10' -t 1 -loop 0 -y $gif
        $video = Join-Path $mediaRoot 'clip.mp4'; & $ffmpeg.Source -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=64x32:rate=15' -t 1 -pix_fmt yuv420p -y $video
    }
    try {
    # Settings: muted, looping, playing by default; older layouts load; values clamp; display modes map to the player.
    $settings = [LandingPage.Settings.ImageWidgetSettings]::new()
    if (!$settings.PlayMedia -or !$settings.LoopMedia -or !$settings.VideoMuted -or !$settings.PauseWhenInactive -or $settings.GifMaxDimension -ne 1280) { throw 'Image widget playback defaults are wrong' }
    $legacy = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{"ImagePath":"C:\\banner.png","Stretch":3,"Opacity":0.5}',[LandingPage.Settings.ImageWidgetSettings])
    if (!$legacy.VideoMuted -or !$legacy.PlayMedia -or $legacy.Opacity -ne 0.5 -or $legacy.Stretch -ne 'UniformToFill') { throw 'Older Image widget settings do not load with playback defaults' }
    $settings.PlaybackSpeed = 9; $settings.VideoVolume = -1; $settings.GifFrameRate = 500
    if ($settings.PlaybackSpeed -ne 2 -or $settings.VideoVolume -ne 0 -or $settings.GifFrameRate -ne 60) { throw 'Image widget playback values are not clamped' }
    $expected = @{ Uniform = 'Fit'; UniformToFill = 'Fill'; Fill = 'Stretch'; None = 'OriginalSize' }
    foreach ($key in $expected.Keys) { if ([LandingPage.Settings.ImageWidgetSettings]::FitFor($key) -ne $expected[$key]) { throw "Image display $key does not map to $($expected[$key])" } }
    $copy = [Newtonsoft.Json.JsonConvert]::DeserializeObject([Newtonsoft.Json.JsonConvert]::SerializeObject($settings),[LandingPage.Settings.ImageWidgetSettings])
    if ($copy.PlaybackSpeed -ne 2 -or $copy.GifFrameRate -ne 60 -or $copy.VideoVolume -ne 0) { throw 'Image widget playback settings did not round-trip' }
    'PASS: playback defaults (muted), legacy load, clamping, display-mode mapping and round-trip.'

    # View: still images use the image; GIFs and videos use the shared media player.
    $widget = [LandingPage.Views.ImageWidgetView]::new(); $widget.Width = 320; $widget.Height = 180
    $widgetSettings = [LandingPage.Settings.ImageWidgetSettings]::new(); $widget.Settings = $widgetSettings
    [void]$layout.Children.Add($widget); $window.UpdateLayout(); Pump-For 50
    $mediaControl = $widget.FindName('WidgetMedia'); $appearance = $mediaControl.Appearance
    $widgetSettings.ImagePath = $png; Pump-For 50
    if (!$widget.ImageSource -or $mediaControl.Visibility -ne 'Collapsed' -or $mediaControl.MediaUri -or !$widget.HasDisplayedMedia) { throw 'A still image did not use the image element' }
    $widgetSettings.ImagePath = (Join-Path $mediaRoot 'missing.gif'); Pump-For 30
    if ($widget.ImageSource -or $mediaControl.MediaUri -or $widget.Status -notmatch 'not found' -or $widget.HasDisplayedMedia) { throw 'A missing file did not show the not-found status' }
    if ($gif) {
        $widgetSettings.Stretch = 'UniformToFill'; $widgetSettings.Opacity = 0.7
        $widgetSettings.ImagePath = $gif; Pump-For 600
        if ($widget.ImageSource -or $mediaControl.Visibility -ne 'Visible' -or $mediaControl.MediaUri.LocalPath -ne $gif -or !$widget.HasDisplayedMedia -or $widget.Status) { throw 'A GIF did not use the media player' }
        if ($appearance.Fit -ne 'Fill' -or [Math]::Abs($appearance.ImageOpacity - 0.7) -gt 0.001 -or !$appearance.VideoMuted -or $appearance.GifMaxDimension -ne 1280) { throw 'Image widget options did not reach the media player' }
        $frame = [LandingPage.Controls.BackgroundMediaArtwork].GetField('gifFrame',$private).GetValue($mediaControl)
        if (!$frame) { throw 'The GIF did not decode a frame' }
        $widgetSettings.VideoMuted = $false; $widgetSettings.PlaybackSpeed = 1.5; $widgetSettings.LoopMedia = $false; Pump-For 30
        if ($appearance.VideoMuted -or $appearance.PlaybackSpeed -ne 1.5 -or $appearance.LoopMedia) { throw 'Changing playback options did not update the media player' }
        $widgetSettings.LoopMedia = $true; $widgetSettings.RestartMedia(); Pump-For 300
        if (![LandingPage.Controls.BackgroundMediaArtwork].GetField('gifFrame',$private).GetValue($mediaControl)) { throw 'Restarting playback lost the GIF' }
        # Hidden widgets stop decoding; unloaded widgets release their media.
        $widget.Visibility = 'Collapsed'; Pump-For 50
        if ([LandingPage.Controls.BackgroundMediaArtwork].GetField('cancellation',$private).GetValue($mediaControl)) { throw 'A hidden Image widget kept decoding its GIF' }
        $widget.Visibility = 'Visible'; Pump-For 400
        if (![LandingPage.Controls.BackgroundMediaArtwork].GetField('cancellation',$private).GetValue($mediaControl)) { throw 'A visible Image widget did not resume its GIF' }
        'PASS: GIF plays through the media player with the widget display mode/opacity, follows option changes and restart, and pauses while hidden.'
    } else { 'NOTE: ffmpeg not found; GIF playback checks skipped.' }
    if ($video) {
        $widgetSettings.ImagePath = $video; Pump-For 800
        $player = [LandingPage.Controls.BackgroundMediaArtwork].GetField('player',$private).GetValue($mediaControl)
        if ($mediaControl.MediaUri.LocalPath -ne $video -or !$player) { throw 'A video did not open in the media player' }
        if ($mediaControl.MediaError) { 'NOTE: video codec unavailable in this environment: ' + $mediaControl.MediaError }
        'PASS: videos open in the media player.'
    } else { 'NOTE: ffmpeg not found; video checks skipped.' }
    $widgetSettings.ImagePath = $png; Pump-For 50
    if (!$widget.ImageSource -or $mediaControl.MediaUri -or $mediaControl.Visibility -ne 'Collapsed') { throw 'Switching back to a still image left the media player active' }
    if ($gif) { $widgetSettings.ImagePath = $gif; Pump-For 300 }
    [void]$layout.Children.Remove($widget); Pump-For 50
    if ([LandingPage.Controls.BackgroundMediaArtwork].GetField('cancellation',$private).GetValue($mediaControl) -or [LandingPage.Controls.BackgroundMediaArtwork].GetField('player',$private).GetValue($mediaControl)) { throw 'Removing the Image widget did not release its media' }
    'PASS: still/animated switching, missing files, and media released when the widget is removed.'

    # Editor: playback section binds to the settings and fits narrow and wide panels.
    foreach($type in @([Windows.Controls.TextBox],[Windows.Controls.Expander],[Windows.Controls.CheckBox],[Windows.Controls.ComboBox],[Windows.Controls.Button])) { if(!$app.Resources.Contains($type)) { $app.Resources.Add($type,[Windows.Style]::new($type)) } }
    $editorSettings = [LandingPage.Settings.ImageWidgetSettings]::new()
    $editor = [LandingPage.Views.Settings.ImageWidgetSettingsView]::new(); $editor.DataContext = $editorSettings
    $border = [Windows.Controls.Border]::new(); $border.Width = 260; $border.HorizontalAlignment = 'Left'; $border.Child = $editor
    [void]$view.Content.Children.Add($border); $window.UpdateLayout(); $editor.FindName('PlaybackSection').IsExpanded = $true; Pump-For 50
    function Get-Descendants($node) { $count = [Windows.Media.VisualTreeHelper]::GetChildrenCount($node); for($i = 0; $i -lt $count; $i++) { $child = [Windows.Media.VisualTreeHelper]::GetChild($node,$i); $child; Get-Descendants $child } }
    foreach ($width in @(260,520)) {
        $border.Width = $width; Pump-For 30
        foreach ($element in @(Get-Descendants $editor | Where-Object { $_ -is [Windows.FrameworkElement] -and $_.IsVisible -and $_.ActualWidth -gt 0 })) {
            $bounds = $element.TransformToAncestor($border).TransformBounds([Windows.Rect]::new(0,0,$element.ActualWidth,$element.ActualHeight))
            if ($bounds.Right -gt $border.ActualWidth + 1) { throw "Image editor overflows at $width px: $($element.GetType().Name)" }
        }
    }
    $speed = @(Get-Descendants $editor | Where-Object { $_ -is [LandingPage.Controls.SettingsSlider] -and $_.Label -eq 'Playback speed' })
    if (!$speed.Count) { throw 'Image editor has no playback speed slider' }
    $speed[0].Slider.Value = 0.5; Pump-For 30
    if ($editorSettings.PlaybackSpeed -ne 0.5) { throw 'Image editor playback speed does not save' }
    [void]$view.Content.Children.Remove($border)
    'PASS: Image editor playback section saves options and fits 260px and 520px panels.'
    } finally { Remove-Item -Recurse -Force $mediaRoot -ErrorAction SilentlyContinue }

'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
