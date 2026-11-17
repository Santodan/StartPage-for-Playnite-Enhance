# Run -STA. Requires ffmpeg only for generating tiny synthetic local test fixtures.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$mediaTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $mediaTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    $ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
    $gifPath = Join-Path $tempRoot 'animated.gif'; $videoPath = Join-Path $tempRoot 'video.mp4'
    & $ffmpeg -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=64x32:rate=10' -t 0.5 -loop 0 -y $gifPath
    if($LASTEXITCODE -ne 0) { throw 'Unable to generate GIF fixture' }
    & $ffmpeg -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=64x32:rate=10' -t 1 -an -c:v libx264 -pix_fmt yuv420p -movflags +faststart -y $videoPath
    if($LASTEXITCODE -ne 0) { throw 'Unable to generate video fixture' }
    $decoder = [LandingPage.Utils.BackgroundGifDecoder]::new([IO.File]::ReadAllBytes($gifPath),32)
    try {
        if($decoder.Width -ne 32 -or $decoder.Height -ne 16 -or $decoder.NaturalWidth -ne 64 -or $decoder.FrameCount -lt 2) { throw 'GIF dimensions/frame metadata incorrect' }
        if($decoder.FrameAt(0,$true) -ne 0 -or $decoder.FrameAt($decoder.Duration,$true) -ne 0 -or $decoder.FrameAt($decoder.Duration,$false) -ne $decoder.FrameCount-1) { throw 'GIF delay/loop selection incorrect' }
        $firstFrame = [Convert]::ToBase64String($decoder.Pixels); [void]$decoder.Render($decoder.FrameCount-1)
        if([Convert]::ToBase64String($decoder.Pixels) -eq $firstFrame -or $decoder.Pixels.Length -ne 32*16*4) { throw 'GIF does not decode changing frames into its bounded output buffer' }
    } finally { $decoder.Dispose() }
    $referencePath = Join-Path $tempRoot 'gif-reference.bgra'
    & $ffmpeg -hide_banner -loglevel error -i $gifPath -f rawvideo -pix_fmt bgra -y $referencePath
    if($LASTEXITCODE -ne 0) { throw 'Unable to decode reference GIF frames' }
    $reference = [IO.File]::ReadAllBytes($referencePath); $native = [LandingPage.Utils.BackgroundGifDecoder]::new([IO.File]::ReadAllBytes($gifPath),0)
    try {
        foreach($index in @(0,($native.FrameCount-1),1)) {
            [void]$native.Render($index); $expected = [byte[]]::new($native.Pixels.Length); [Array]::Copy($reference,$index*$expected.Length,$expected,0,$expected.Length)
            if([Convert]::ToBase64String($expected) -ne [Convert]::ToBase64String($native.Pixels)) { throw ('Native GIF composition differs from reference at frame '+$index) }
        }
    } finally { $native.Dispose() }
    'PASS: GIF native timing, looping, resolution limit and decoded frame changes.'

    function Pump-Media($condition,$seconds=10) {
        $deadline = [datetime]::UtcNow.AddSeconds($seconds)
        while(!(& $condition)) {
            if([datetime]::UtcNow -gt $deadline) { throw 'Media condition timed out' }
            $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 15
        }
    }
    function Tick-Media($milliseconds) {
        $until = [datetime]::UtcNow.AddMilliseconds($milliseconds)
        while([datetime]::UtcNow -lt $until) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
    }
    $options = [LandingPage.Settings.BackgroundAppearanceSettings]::new(); $options.PauseMediaWhenInactive = $false; $options.GifMaxDimension = 32
    $media = [LandingPage.Controls.BackgroundMediaArtwork]::new(); $media.Width = 128; $media.Height = 64; $media.Appearance = $options; $media.MediaUri = [uri]$gifPath
    [void]$view.Content.Children.Add($media); $window.UpdateLayout()
    $frameField = $media.GetType().GetField('gifFrame',$flags); $playingField = $media.GetType().GetField('playing',$flags)
    Pump-Media { $frameField.GetValue($media) -or $media.MediaError }
    if($media.MediaError) { throw $media.MediaError }
    $frame = $frameField.GetValue($media); $pixels = [byte[]]::new(32*16*4); $frame.CopyPixels($pixels,128,0); $first = [Convert]::ToBase64String($pixels)
    $changed = $false
    Pump-Media { $frame.CopyPixels($pixels,128,0); $changed = [Convert]::ToBase64String($pixels) -ne $first; $changed }
    if(![object]::ReferenceEquals($frame,$frameField.GetValue($media))) { throw 'GIF allocates a new WPF bitmap every frame' }
    $options.PauseMediaWhenInactive = $true
    if(!$window.IsActive -and $playingField.GetValue($media)) { throw 'Inactive-window policy does not pause animated background' }
    $options.PauseMediaWhenInactive = $false
    $options.PlayMedia = $false; Tick-Media 100; $frame.CopyPixels($pixels,128,0); $paused = [Convert]::ToBase64String($pixels); Tick-Media 200; $frame.CopyPixels($pixels,128,0)
    if($playingField.GetValue($media) -or [Convert]::ToBase64String($pixels) -ne $paused) { throw 'Paused GIF still advances' }
    $options.PlayMedia = $true; $options.LoopMedia = $false; $options.PlaybackSpeed = 2
    Pump-Media { $media.GetType().GetField('ended',$flags).GetValue($media) }
    $options.LoopMedia = $true; $options.RestartMedia()
    Pump-Media { $frameField.GetValue($media) -and ![object]::ReferenceEquals($frame,$frameField.GetValue($media)) }
    $media.Opacity = 0; Tick-Media 100
    if($media.GetType().GetField('cancellation',$flags).GetValue($media) -or $playingField.GetValue($media)) { throw 'Invisible GIF retains active playback session' }
    $media.Opacity = 1; Pump-Media { $frameField.GetValue($media) }
    $media.PlaybackVisible = $false; Tick-Media 100
    if($media.GetType().GetField('cancellation',$flags).GetValue($media)) { throw 'Fully transparent parent background retains active decoder' }
    $media.PlaybackVisible = $true; Pump-Media { $frameField.GetValue($media) }

    [void]$view.Content.Children.Remove($media); Tick-Media 100
    if($media.GetType().GetField('cancellation',$flags).GetValue($media) -or $media.GetType().GetField('window',$flags).GetValue($media)) { throw 'GIF unload does not release session/window listeners' }
    'PASS: live GIF animation, reusable bitmap, pause/resume, one-shot/restart and invisible/unloaded cleanup.'

    $video = [LandingPage.Controls.BackgroundMediaArtwork]::new(); $video.Width = 128; $video.Height = 64
    $video.Appearance = [LandingPage.Settings.BackgroundAppearanceSettings]::new(); $video.Appearance.PauseMediaWhenInactive = $false; $video.MediaUri = [uri]$videoPath
    [void]$view.Content.Children.Add($video); $window.UpdateLayout()
    $playerField = $video.GetType().GetField('player',$flags)
    Pump-Media { $video.GetType().GetField('natural',$flags).GetValue($video).Width -gt 0 -or $video.MediaError } 15
    if($video.MediaError) { throw $video.MediaError }
    $player = $playerField.GetValue($video)
    if(!$player.IsMuted -or $player.NaturalVideoWidth -ne 64) { throw 'Video is not decoded or is unexpectedly audible by default' }
    Pump-Media { $player.Position.TotalMilliseconds -gt 100 } 10
    $video.Appearance.PlayMedia = $false; Tick-Media 100; $position = $player.Position; Tick-Media 200
    if([Math]::Abs(($player.Position-$position).TotalMilliseconds) -gt 80) { throw 'Video does not pause' }
    $video.Appearance.VideoMuted = $false; $video.Appearance.VideoVolume = .2
    if($player.IsMuted -or [Math]::Abs($player.Volume-.2) -gt .001) { throw 'Video sound controls not applied' }
    $video.Appearance.VideoMuted = $true; $video.Appearance.LoopMedia = $false; $video.Appearance.PlayMedia = $true
    Pump-Media { $video.GetType().GetField('ended',$flags).GetValue($video) }
    $video.Appearance.RestartMedia()
    Pump-Media { $playerField.GetValue($video) -and ![object]::ReferenceEquals($player,$playerField.GetValue($video)) -and $video.GetType().GetField('natural',$flags).GetValue($video).Width -gt 0 }
    [void]$view.Content.Children.Remove($video); Tick-Media 100
    if($playerField.GetValue($video)) { throw 'Video player remains open after unload' }
    if(![LandingPage.Utils.BackgroundMediaFiles]::IsSupportedFile($videoPath) -or ![LandingPage.Utils.BackgroundMediaFiles]::IsSupportedFile($gifPath)) { throw 'Background picker/folder scan rejects animated media' }
    'PASS: actual MP4 playback through Windows, muted default, pause/volume controls and player cleanup.'
    $invalidPath = Join-Path $tempRoot 'invalid.gif'; [IO.File]::WriteAllText($invalidPath,'not a GIF')
    $invalid = [LandingPage.Controls.BackgroundMediaArtwork]::new(); $invalid.Appearance = [LandingPage.Settings.BackgroundAppearanceSettings]::new(); $invalid.MediaUri = [uri]$invalidPath
    [void]$view.Content.Children.Add($invalid); $window.UpdateLayout(); Pump-Media { $invalid.MediaError }
    if($invalid.GetType().GetField('cancellation',$flags).GetValue($invalid)) { throw 'Failed GIF leaves an active session behind' }
    [void]$view.Content.Children.Remove($invalid)
    $singleFolder = Join-Path $tempRoot 'single-background'; [void][IO.Directory]::CreateDirectory($singleFolder); $singleGif = Join-Path $singleFolder 'only.gif'; [IO.File]::Copy($gifPath,$singleGif)
    Set-Setting $settingsModel.Settings 'BackgroundImageUri' $null; Set-Setting $settingsModel.Settings 'BackgroundImagePath' $singleFolder
    $background.UpdateBackgroundImagePath($true); $once = $background.BackgroundImagePath; $background.UpdateBackgroundImagePath($true)
    if($once.LocalPath -ne $singleGif -or $background.BackgroundImagePath -ne $once) { throw 'Single-file media folder switches to an unrelated game background' }
    'PASS: invalid media reports a safe error and single-file GIF folders retain their background.'

'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
