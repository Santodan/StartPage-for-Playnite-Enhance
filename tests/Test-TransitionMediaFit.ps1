# Run -STA. Transition media Size option (fit, fill/crop, stretch, original size centered) renders the image with the
# expected framing and never distorts it except with Stretch; the transition editor preview box plays media too.
$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll'))) }
[Threading.SynchronizationContext]::SetSynchronizationContext([Windows.Threading.DispatcherSynchronizationContext]::new([Windows.Threading.Dispatcher]::CurrentDispatcher))
function Pump($milliseconds = 30) {
    $until = [datetime]::UtcNow.AddMilliseconds($milliseconds)
    do { $frame = [Windows.Threading.DispatcherFrame]::new(); [void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::Background,[Action]{ $frame.Continue = $false }); [Windows.Threading.Dispatcher]::PushFrame($frame); Start-Sleep -Milliseconds 5 } while ([datetime]::UtcNow -lt $until)
}
$folder = Join-Path ([IO.Path]::GetTempPath()) ('sp-media-fit-' + [guid]::NewGuid()); [void][IO.Directory]::CreateDirectory($folder)
# 40x20 image: the left quarter (x < 10) green, the rest red.
$bitmap = [Windows.Media.Imaging.WriteableBitmap]::new(40,20,96,96,[Windows.Media.PixelFormats]::Bgra32,$null)
$pixels = [byte[]]::new(40*20*4)
for ($y = 0; $y -lt 20; $y++) { for ($x = 0; $x -lt 40; $x++) { $i = ($y*40 + $x)*4; if ($x -lt 10) { $pixels[$i+1] = 255 } else { $pixels[$i+2] = 255 }; $pixels[$i+3] = 255 } }
$bitmap.WritePixels([Windows.Int32Rect]::new(0,0,40,20),$pixels,160,0)
$image = Join-Path $folder 'band.png'; $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create($image); $encoder.Save($stream); $stream.Close()

$window = [Windows.Window]::new(); $window.SizeToContent = 'WidthAndHeight'; $window.ShowInTaskbar = $false; $window.WindowStyle = 'None'; $window.Left = -2000
$layer = [LandingPage.Controls.TransitionMediaLayer]::new(); $layer.Width = 200; $layer.Height = 200
$window.Content = $layer
function Color-At($x,$y) {
    $target = [Windows.Media.Imaging.RenderTargetBitmap]::new(200,200,96,96,[Windows.Media.PixelFormats]::Pbgra32); $target.Render($layer)
    $pixel = [byte[]]::new(4); $target.CopyPixels([Windows.Int32Rect]::new($x,$y,1,1),$pixel,4,0)
    if ($pixel[1] -gt 150 -and $pixel[2] -lt 100) { 'green' } elseif ($pixel[2] -gt 150 -and $pixel[1] -lt 100) { 'red' } elseif ($pixel[0] -lt 40 -and $pixel[1] -lt 40 -and $pixel[2] -lt 40) { 'black' } else { 'other(' + ($pixel -join ',') + ')' }
}
try {
    $window.Show(); Pump
    $options = [LandingPage.Settings.TransitionMediaSettings]::new()
    if ($options.Media.Fit -ne [LandingPage.Settings.BackgroundFit]::Fit) { throw 'Transition media must default to Fit (whole image, no crop or distortion)' }
    # Points: top middle (100,5); left middle (30,100); center (100,100).
    $expected = @{
        Fit          = @('black','green','red')    # 200x100 band across the middle, black above and below
        Fill         = @('red','red','red')        # 400x200 cropped: the green quarter is cut off
        Stretch      = @('red','green','red')      # 200x200 distorted: the green quarter is 50 px wide
        OriginalSize = @('black','black','red')    # 40x20 in the center
    }
    foreach ($fit in @('Fit','Fill','Stretch','OriginalSize')) {
        $options.Media.Fit = [Enum]::Parse([LandingPage.Settings.BackgroundFit],$fit)
        if (!$layer.Prepare($options,$image,$true)) { throw 'The media layer did not prepare the image' }
        Pump 60
        $seen = @((Color-At 100 5),(Color-At 30 100),(Color-At 100 100))
        if (($seen -join ',') -ne ($expected[$fit] -join ',')) { throw "Size $fit rendered $($seen -join ',') instead of $($expected[$fit] -join ',')" }
        $layer.Clear()
    }
    'PASS: transition media Size fits the whole image, fills with cropping, stretches only when chosen and keeps the original size centered.'

    # The editor preview box (extension settings) plays the media revealed by the effect, then shows the other page.
    $window.Content = $null
    $box = [LandingPage.Controls.TransitionPreviewBox]::new(); $box.Width = 320; $window.Content = $box; Pump
    $transition = [LandingPage.Settings.PageTransitionSettings]::new(); $transition.Transition = [LandingPage.DashboardTransition]::Wipe; $transition.DurationMs = 80
    $transition.Media.Enabled = $true; $transition.Media.MediaPath = $image; $transition.Media.Length = [LandingPage.Settings.TransitionMediaLength]::FixedTime; $transition.Media.FixedMs = 300
    $script:shown = $false
    $play = $box.PlayAsync($transition); $until = [datetime]::UtcNow.AddSeconds(10)
    while (!$play.IsCompleted) {
        if ([datetime]::UtcNow -gt $until) { throw 'The preview box did not finish' }
        if ($box.MediaLayer.Visibility -eq 'Visible' -and $box.MediaLayer.PreparedMedia -eq $image) { $script:shown = $true }
        Pump 10
    }
    if (!$play.Result -or !$script:shown -or $box.ShowingFirst -or $box.IsPlaying -or $box.MediaLayer.Visibility -ne 'Collapsed' -or $box.LastPlayer.ActiveMediaPlacement -ne [LandingPage.Settings.TransitionMediaPlacement]::RevealedByEffect) { throw 'The preview box did not play the media revealed by the effect and switch pages' }
    $back = $box.PlayAsync($transition); $box.Stop(); $until = [datetime]::UtcNow.AddSeconds(10)
    while (!$back.IsCompleted) { if ([datetime]::UtcNow -gt $until) { throw 'Stopping the preview did not end it' }; Pump 10 }
    if (!$box.ShowingFirst -or $box.IsPlaying) { throw 'A stopped preview did not end on the other page' }
    'PASS: the preview box plays the transition with its media, switches sample pages and stops on request.'
} finally { $window.Close(); Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue }
