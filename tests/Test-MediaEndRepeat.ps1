# Run -STA. "If the GIF or video ends first" for startup intros and transition media: hold the last frame (default),
# repeat the first part or repeat the last part of X ms while the intro/transition is still shown. Needs ffmpeg.
$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll'))) }
[Threading.SynchronizationContext]::SetSynchronizationContext([Windows.Threading.DispatcherSynchronizationContext]::new([Windows.Threading.Dispatcher]::CurrentDispatcher))
function Pump($milliseconds = 30) {
    $until = [datetime]::UtcNow.AddMilliseconds($milliseconds)
    do { $frame = [Windows.Threading.DispatcherFrame]::new(); [void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::Background,[Action]{ $frame.Continue = $false }); [Windows.Threading.Dispatcher]::PushFrame($frame); Start-Sleep -Milliseconds 5 } while ([datetime]::UtcNow -lt $until)
}

# Settings: defaults, limits, editor flag and loading older settings.
foreach ($type in @([LandingPage.Settings.StartupIntroSettings],[LandingPage.Settings.TransitionMediaSettings])) {
    $options = $type::new()
    if ($options.AfterEnd -ne [LandingPage.Settings.MediaEndBehavior]::HoldLastFrame -or $options.AfterEndRepeatMs -ne 1000 -or $options.RepeatsAfterEnd) { throw "$($type.Name) must default to holding the last frame" }
    $options.AfterEndRepeatMs = 1; if ($options.AfterEndRepeatMs -ne 1) { throw "$($type.Name) repeated part must allow 1 ms" }
    $options.AfterEndRepeatMs = 0; if ($options.AfterEndRepeatMs -ne 1) { throw "$($type.Name) repeated part minimum not clamped to 1 ms" }
    $options.AfterEndRepeatMs = 99999; if ($options.AfterEndRepeatMs -ne 10000) { throw "$($type.Name) repeated part maximum not clamped" }
    $options.AfterEnd = [LandingPage.Settings.MediaEndBehavior]::RepeatEnd; $options.AfterEndRepeatMs = 700
    if (!$options.RepeatsAfterEnd) { throw "$($type.Name) does not report a repeating choice" }
    $copy = [LandingPage.Models.DashboardProfiles]::Clone($options)
    if ($copy.AfterEnd -ne [LandingPage.Settings.MediaEndBehavior]::RepeatEnd -or $copy.AfterEndRepeatMs -ne 700) { throw "$($type.Name) end behavior did not round-trip" }
    $old = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{"Enabled":true}',$type)
    if ($old.AfterEnd -ne [LandingPage.Settings.MediaEndBehavior]::HoldLastFrame) { throw "$($type.Name) older settings must keep holding the last frame" }
    $options.ResetOptions(); if ($options.RepeatsAfterEnd -or $options.AfterEndRepeatMs -ne 1000) { throw "$($type.Name) reset did not restore the end behavior" }
}
'PASS: end behavior defaults to holding the last frame, limits 1-10000 ms, round-trips, loads older settings and resets.'

$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (!$ffmpeg) { 'SKIP: ffmpeg not found; playback of repeated parts not checked.'; return }
$folder = Join-Path ([IO.Path]::GetTempPath()) ('sp-end-repeat-' + [guid]::NewGuid()); [void][IO.Directory]::CreateDirectory($folder)
# 0.6 s, 10 fps, non-looping (ffmpeg keeps the first frame about 200 ms, so the repeated parts are 400 ms).
$gif = Join-Path $folder 'clip.gif'; & $ffmpeg.Source -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=64x32:rate=10' -t 0.6 -loop -1 -y $gif
$video = Join-Path $folder 'clip.mp4'; & $ffmpeg.Source -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=64x32:rate=10' -t 1 -pix_fmt yuv420p -y $video
$window = [Windows.Window]::new(); $window.SizeToContent = 'WidthAndHeight'; $window.ShowInTaskbar = $false; $window.WindowStyle = 'None'; $window.Left = -2000
$layer = [LandingPage.Controls.TransitionMediaLayer]::new(); $layer.Width = 64; $layer.Height = 32; $window.Content = $layer
function Frame-Hash {
    $target = [Windows.Media.Imaging.RenderTargetBitmap]::new(64,32,96,96,[Windows.Media.PixelFormats]::Pbgra32); $target.Render($layer)
    $bytes = [byte[]]::new(64*32*4); $target.CopyPixels($bytes,256,0)
    [BitConverter]::ToString([Security.Cryptography.MD5]::Create().ComputeHash($bytes))
}
function Play-Gif($behavior) {
    $options = [LandingPage.Settings.TransitionMediaSettings]::new()
    $options.Media.Fit = [LandingPage.Settings.BackgroundFit]::Stretch; $options.AfterEnd = $behavior; $options.AfterEndRepeatMs = 400
    $script:finishedCount = 0; $handler = [EventHandler]{ $script:finishedCount++ }
    $layer.Artwork.add_MediaFinished($handler)
    try {
        if (!$layer.Prepare($options,$gif,$true)) { throw 'The GIF did not prepare' }
        Pump 600
        $first = Frame-Hash
        $layer.Start(); $until = [datetime]::UtcNow.AddSeconds(5)
        while ($script:finishedCount -eq 0) { if ([datetime]::UtcNow -gt $until) { throw "The GIF never reached its end ($behavior)" }; Pump 10 }
        $after = [Collections.Generic.List[string]]::new()
        for ($sample = 0; $sample -lt 30; $sample++) { Pump 30; $after.Add((Frame-Hash)) }
        [pscustomobject]@{ First = $first; Distinct = @($after | Select-Object -Unique).Count; SawFirst = $after.Contains($first); Repeating = $layer.Artwork.IsRepeatingPart; Finished = $script:finishedCount }
    } finally { $layer.Artwork.remove_MediaFinished($handler); $layer.Clear() }
}
try {
    $window.Show(); Pump
    $hold = Play-Gif ([LandingPage.Settings.MediaEndBehavior]::HoldLastFrame)
    if ($hold.Distinct -ne 1 -or $hold.Repeating -or $hold.Finished -ne 1) { throw "Hold the last frame kept changing after the end (distinct=$($hold.Distinct), repeating=$($hold.Repeating))" }
    $start = Play-Gif ([LandingPage.Settings.MediaEndBehavior]::RepeatStart)
    if ($start.Distinct -lt 2 -or !$start.SawFirst -or !$start.Repeating -or $start.Finished -ne 1) { throw "Repeat the first part did not cycle the opening frames (distinct=$($start.Distinct), sawFirst=$($start.SawFirst), finished=$($start.Finished))" }
    $end = Play-Gif ([LandingPage.Settings.MediaEndBehavior]::RepeatEnd)
    if ($end.Distinct -lt 2 -or $end.SawFirst -or !$end.Repeating -or $end.Finished -ne 1) { throw "Repeat the last part did not cycle only the closing frames (distinct=$($end.Distinct), sawFirst=$($end.SawFirst), finished=$($end.Finished))" }
    'PASS: after a GIF ends it holds the last frame, or repeats only its first or only its last 400 ms; the end is reported once.'

    # A fixed time longer than the clip must keep the UI responsive after the clip ends (no busy wait).
    $options = [LandingPage.Settings.TransitionMediaSettings]::new(); $options.Length = [LandingPage.Settings.TransitionMediaLength]::FixedTime; $options.FixedMs = 2000
    $options.AfterEnd = [LandingPage.Settings.MediaEndBehavior]::RepeatEnd; $options.AfterEndRepeatMs = 400
    [void]$layer.Prepare($options,$gif,$true); Pump 400; $layer.Start(); $wait = $layer.WaitAsync($null)
    $slowest = 0; $until = [datetime]::UtcNow.AddSeconds(5)
    while (!$wait.IsCompleted) {
        if ([datetime]::UtcNow -gt $until) { throw 'The fixed-time wait did not finish' }
        $tick = [Diagnostics.Stopwatch]::StartNew(); Pump 20; $slowest = [Math]::Max($slowest,$tick.ElapsedMilliseconds)
    }
    $repeated = $layer.Artwork.IsRepeatingPart; $layer.Clear()
    if ($slowest -gt 400 -or !$repeated) { throw "The UI thread was blocked while waiting after the clip ended (slowest pump $slowest ms, repeating=$repeated)" }
    'PASS: waiting a fixed time after the clip ended keeps the UI responsive while the part repeats.'

    # Transition previews (settings preview box) play the whole clip, then the repeated part three times, even with
    # "until the media ends"; normal playback of the same settings ends with the clip.
    $window.Content = $null
    $box = [LandingPage.Controls.TransitionPreviewBox]::new(); $box.Width = 200; $window.Content = $box; Pump
    $previewTransition = [LandingPage.Settings.PageTransitionSettings]::new(); $previewTransition.Transition = [LandingPage.DashboardTransition]::Fade; $previewTransition.DurationMs = 60
    $previewTransition.Media.Enabled = $true; $previewTransition.Media.MediaPath = $gif; $previewTransition.Media.Length = [LandingPage.Settings.TransitionMediaLength]::UntilMediaEnds
    $previewTransition.Media.AfterEnd = [LandingPage.Settings.MediaEndBehavior]::RepeatStart; $previewTransition.Media.AfterEndRepeatMs = 400
    $watch = [Diagnostics.Stopwatch]::StartNew(); $mostRepeats = 0
    $play = $box.PlayAsync($previewTransition); $until = [datetime]::UtcNow.AddSeconds(15)
    while (!$play.IsCompleted) { if ([datetime]::UtcNow -gt $until) { throw 'The transition preview did not finish' }; $mostRepeats = [Math]::Max($mostRepeats,$box.MediaLayer.Artwork.RepeatCount); Pump 15 }
    if ($mostRepeats -lt 3 -or $watch.ElapsedMilliseconds -lt 1500) { throw "The transition preview did not play the whole clip and three repeats ($mostRepeats repeats, $($watch.ElapsedMilliseconds) ms)" }
    $normal = [LandingPage.Models.DashboardProfiles]::Clone($previewTransition.Media)
    $window.Content = $layer; Pump
    [void]$layer.Prepare($normal,$gif,$true); Pump 400; $layer.Start(); $wait = $layer.WaitAsync($null); $watch.Restart(); $until = [datetime]::UtcNow.AddSeconds(10)
    while (!$wait.IsCompleted) { if ([datetime]::UtcNow -gt $until) { throw 'Normal playback did not end with the clip' }; Pump 15 }
    $normalMs = $watch.ElapsedMilliseconds; $layer.Clear()
    if ($normalMs -gt 1300) { throw "Outside previews, 'until the media ends' must end with the clip, not wait for repeats ($normalMs ms)" }
    'PASS: transition previews play the whole clip and the repeated part three times; real transitions still end with the clip.'

    # Startup intros pass the choice to their media and keep repeating while the intro is still shown.
    $window.Content = $null
    $presenter = [LandingPage.Controls.IntroPresenter]::new(); $presenter.Width = 64; $presenter.Height = 32; $window.Content = $presenter; Pump
    $intro = [LandingPage.Settings.StartupIntroSettings]::new(); $intro.Enabled = $true; $intro.MediaPath = $gif
    $intro.Length = [LandingPage.Settings.IntroLength]::FixedTime; $intro.FixedMs = 2500; $intro.FadeInMs = 0; $intro.ExitMs = 0; $intro.ShowSkipHint = $false
    $intro.AfterEnd = [LandingPage.Settings.MediaEndBehavior]::RepeatEnd; $intro.AfterEndRepeatMs = 300
    $playing = $presenter.PlayAsync($intro,$null,$null); $until = [datetime]::UtcNow.AddSeconds(4)
    while (!$presenter.Artwork.IsRepeatingPart) { if ([datetime]::UtcNow -gt $until -or $playing.IsCompleted) { throw ("The startup intro did not repeat the last part while it was still shown (completed=$($playing.IsCompleted) result=$(if ($playing.IsCompleted) { $playing.Result }) error=$($playing.Exception) uri=$($presenter.Artwork.MediaUri) mediaError=$($presenter.Artwork.MediaError) gifPos=$($presenter.Artwork.GifPositionMs) elapsed=$($presenter.Elapsed))") }; Pump 20 }
    if ($presenter.Artwork.EndBehavior -ne [LandingPage.Settings.MediaEndBehavior]::RepeatEnd -or $presenter.Artwork.EndRepeatMs -ne 300) { throw 'The startup intro did not pass its end behavior to the media' }
    $presenter.Skip(); $until = [datetime]::UtcNow.AddSeconds(4)
    while (!$playing.IsCompleted) { if ([datetime]::UtcNow -gt $until) { throw 'The intro did not end after skip' }; Pump 20 }
    'PASS: startup intros repeat the chosen part while they are still shown.'

    # Videos: after the end, the first 300 ms keep repeating (the position stays inside that part).
    $window.Content = $layer
    $options = [LandingPage.Settings.TransitionMediaSettings]::new(); $options.AfterEnd = [LandingPage.Settings.MediaEndBehavior]::RepeatStart; $options.AfterEndRepeatMs = 300
    $script:videoEnded = 0; $handler = [EventHandler]{ $script:videoEnded++ }; $layer.Artwork.add_MediaFinished($handler)
    try {
        [void]$layer.Prepare($options,$video,$true); Pump 400; $layer.Start()
        $until = [datetime]::UtcNow.AddSeconds(8)
        while ($script:videoEnded -eq 0 -and [datetime]::UtcNow -lt $until) { Pump 20 }
        if ($layer.Artwork.MediaError -or $script:videoEnded -eq 0) { "SKIP: video playback unavailable here ($($layer.Artwork.MediaError)); video repeat not checked." }
        else {
            $player = $layer.Artwork.GetType().GetField('player',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($layer.Artwork)
            $positions = for ($sample = 0; $sample -lt 25; $sample++) { Pump 40; $player.Position.TotalMilliseconds }
            $over = @($positions | Where-Object { $_ -gt 500 }).Count
            if (!$layer.Artwork.IsRepeatingPart -or $over -gt 2 -or $script:videoEnded -ne 1) { throw "A video did not repeat its first part after the end (positions: $($positions -join ','))" }
            'PASS: a video repeats its first part after it ends.'
        }
    } finally { $layer.Artwork.remove_MediaFinished($handler); $layer.Clear() }
} finally { $window.Close(); Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue }
