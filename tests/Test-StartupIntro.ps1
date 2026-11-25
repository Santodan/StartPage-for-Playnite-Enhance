# Run -STA. Startup intros (roadmap step 22): settings persistence, media resolution, playback length modes,
# skip/exit effects, coverage targets, page intros on page switch, the global intro on open, coordination with
# the dashboard reveal and the editor. Reuses the in-memory API/window fixture.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$introTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $introTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    function Pump-For($milliseconds) {
        $until = [datetime]::UtcNow.AddMilliseconds($milliseconds)
        do { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background); Start-Sleep -Milliseconds 10 } while ([datetime]::UtcNow -lt $until)
        $window.UpdateLayout()
    }
    function Wait-Intro($task,$limit = 15000) {
        $until = [datetime]::UtcNow.AddMilliseconds($limit)
        while (!$task.IsCompleted) { if ([datetime]::UtcNow -gt $until) { throw 'Intro did not finish in time' }; $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background); Start-Sleep -Milliseconds 10 }
        $task.Result
    }
    $static = [Reflection.BindingFlags]'Static,Public,NonPublic'
    # GetAdorners returns null (not an empty array) when nothing is adorned.
    function Count-Adorners($element) { $found = [Windows.Documents.AdornerLayer]::GetAdornerLayer($element).GetAdorners($element); if ($found) { $found.Count } else { 0 } }
    $json = { param($value) [Newtonsoft.Json.JsonConvert]::SerializeObject($value) }
    $introRoot = Join-Path $env:TEMP ('StartPage-intro-tests-' + [guid]::NewGuid()); [void][IO.Directory]::CreateDirectory($introRoot)
    # A real PNG for still-image intros.
    $png = Join-Path $introRoot 'intro.png'
    $visual = [Windows.Media.DrawingVisual]::new(); $dc = $visual.RenderOpen(); $dc.DrawRectangle([Windows.Media.Brushes]::OrangeRed,$null,[Windows.Rect]::new(0,0,64,36)); $dc.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(64,36,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create($png); $encoder.Save($stream); $stream.Dispose()
    $ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
    $gif = $null
    if ($ffmpeg) { $gif = Join-Path $introRoot 'intro.gif'; & $ffmpeg.Source -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=64x32:rate=10' -t 0.6 -loop -1 -y $gif }

    try {
    # Settings: off by default globally and per page, old configs load, clamping, media resolution, round-trip.
    $fresh = [LandingPage.LandingPageSettings]::new(); $global = Get-Setting $fresh 'StartupIntro'
    if ($global.Enabled -or $global.Coverage -ne 'PlayniteWindow' -or $global.Length -ne 'UntilMediaEnds' -or $global.Media.Fit -ne 'Fit' -or $global.Media.LoopMedia -or $global.Media.VideoMuted) { throw 'Startup intro defaults are wrong' }
    $legacyPage = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{"Name":"Old page"}',[LandingPage.Models.DashboardPage])
    if ($legacyPage.Intro.Enabled -or (Get-Setting ([Newtonsoft.Json.JsonConvert]::DeserializeObject('{}',[LandingPage.LandingPageSettings])) 'StartupIntro').Enabled) { throw 'Configs saved before intros do not load with intros off' }
    $global.FixedMs = 1; $global.MaximumMs = 999999; $global.ExitMs = -4
    if ($global.FixedMs -ne 500 -or $global.MaximumMs -ne 60000 -or $global.ExitMs -ne 0) { throw 'Intro values are not clamped' }
    $global.MediaPath = (Join-Path $introRoot 'missing.mp4')
    if ($global.ResolveMedia()) { throw 'A missing intro file resolved to media' }
    $global.MediaPath = $png
    if ($global.ResolveMedia() -ne $png) { throw 'The chosen intro file did not resolve' }
    $global.Source = 'RandomFromFolder'; $global.FolderPath = $introRoot
    if (@($png,$gif) -notcontains $global.ResolveMedia() -or !$global.UsesFolder) { throw 'Random folder intro did not pick a supported file' }
    $saved = & $json $global
    if ($saved -match '"UsesFolder"' -or $saved -match '"UsesFixedTime"') { throw 'Runtime-only intro flags were serialized' }
    $global.Enabled = $true; $global.Exit = 'Curtain'; $global.ResetOptions()
    if (!$global.Enabled -or $global.FolderPath -ne $introRoot -or $global.Exit -ne 'Fade' -or $global.ExitMs -ne 600) { throw 'Reset options changed the intro media or missed an option' }
    # Page intros travel with profiles.
    $pageSettings = [LandingPage.LandingPageSettings]::new(); $profilePage = [LandingPage.Models.DashboardPages]::Ensure($pageSettings)
    $profilePage.Intro.Enabled = $true; $profilePage.Intro.MediaPath = $png; $profilePage.Intro.Repeat = 'EveryTime'
    $profile = [LandingPage.Models.DashboardProfiles]::Capture($pageSettings,'Intro profile')
    $capturedPage = @($profile.Snapshot.Pages)[0]
    if (!$capturedPage.Intro.Enabled -or $capturedPage.Intro.MediaPath -ne $png -or $capturedPage.Intro.Repeat -ne 'EveryTime') { throw 'Dashboard profiles did not carry the page intro' }
    $capturedPage.Intro.MediaPath = 'changed'
    if ($profilePage.Intro.MediaPath -ne $png) { throw 'Profile page intro shares state with the live page' }
    'PASS: intro defaults, legacy load, clamping, file/folder resolution, runtime flags, reset and page intros in profiles.'

    # Playback on a stage element (StartPage-area coverage path).
    $stage = [Windows.Controls.Grid]::new(); $stage.Width = 320; $stage.Height = 180; $stage.HorizontalAlignment = 'Left'; $stage.VerticalAlignment = 'Top'
    [void]$layout.Children.Add($stage); $window.UpdateLayout()
    function New-Intro($length) { $intro = [LandingPage.Settings.StartupIntroSettings]::new(); $intro.Enabled = $true; $intro.MediaPath = $png; $intro.Length = $length; $intro.FadeInMs = 0; $intro.ExitMs = 100; $intro.MinimumMs = 0; $intro }
    $introHost = [LandingPage.Utils.IntroHost]
    $fixed = New-Intro 'FixedTime'; $fixed.FixedMs = 600
    $watch = [Diagnostics.Stopwatch]::StartNew(); $task = $introHost::PlayAsync($stage,$fixed,$null); Pump-For 60
    if (!$introHost::IsPlaying -or (Count-Adorners $stage) -ne 1 -or $introHost::Current.Artwork.Source -eq $null) { throw 'Fixed-time intro did not cover the target with the image' }
    if (!(Wait-Intro $task) -or $watch.ElapsedMilliseconds -lt 600 -or $watch.ElapsedMilliseconds -gt 2500) { throw "Fixed-time intro length was $($watch.ElapsedMilliseconds) ms" }
    if ((Count-Adorners $stage) -ne 0 -or $introHost::Current) { throw 'Intro left its overlay behind' }
    $ready = $false; $loaded = New-Intro 'UntilDashboardLoaded'; $loaded.MinimumMs = 200; $loaded.MaximumMs = 6000
    $watch.Restart(); $task = $introHost::PlayAsync($stage,$loaded,[Func[bool]]{ $script:ready }); Pump-For 700
    if (!$introHost::IsPlaying) { throw 'Until-loaded intro ended before the dashboard was ready' }
    $script:ready = $true; [void](Wait-Intro $task)
    if ($watch.ElapsedMilliseconds -gt 2500) { throw 'Until-loaded intro did not end once the dashboard was ready' }
    $capped = New-Intro 'UntilDashboardLoaded'; $capped.MaximumMs = 1000
    $watch.Restart(); [void](Wait-Intro ($introHost::PlayAsync($stage,$capped,[Func[bool]]{ $false })))
    if ($watch.ElapsedMilliseconds -gt 2600) { throw 'Intro ignored its maximum length' }
    $long = New-Intro 'FixedTime'; $long.FixedMs = 8000
    $watch.Restart(); $task = $introHost::PlayAsync($stage,$long,$null); Pump-For 200; $presenter = $introHost::Current; $introHost::SkipCurrent(); [void](Wait-Intro $task)
    if (!$presenter.WasSkipped -or $watch.ElapsedMilliseconds -gt 2000) { throw 'Skipping did not end the intro' }
    $missing = New-Intro 'UntilMediaEnds'; $missing.MediaPath = (Join-Path $introRoot 'none.mp4')
    if (Wait-Intro ($introHost::PlayAsync($stage,$missing,$null))) { throw 'An intro without media played in until-media-ends mode' }
    foreach ($exit in @('Fade','ZoomIn','ZoomOut','Spotlight','Curtain','Cut')) {
        $effect = New-Intro 'FixedTime'; $effect.FixedMs = 500; $effect.Exit = $exit; $effect.ExitMs = 200
        $task = $introHost::PlayAsync($stage,$effect,$null); $exitSeen = $false
        while (!$task.IsCompleted) { if ($introHost::Current -and $introHost::Current.IsExiting -and !$introHost::IsPlaying) { $exitSeen = $true }; Pump-For 15 }
        if (!$task.Result -or ($exit -ne 'Cut' -and !$exitSeen)) { throw "Exit $exit did not run its exit phase" }
        if ((Count-Adorners $stage) -ne 0) { throw "Exit $exit left its overlay behind" }
    }
    if ($gif) {
        $media = New-Intro 'UntilMediaEnds'; $media.MediaPath = $gif; $media.MaximumMs = 8000
        $watch.Restart(); $task = $introHost::PlayAsync($stage,$media,$null); [void](Wait-Intro $task)
        if ($watch.ElapsedMilliseconds -lt 400 -or $watch.ElapsedMilliseconds -gt 6000) { throw "GIF intro did not end with the GIF ($($watch.ElapsedMilliseconds) ms)" }
        $looping = New-Intro 'UntilMediaEnds'; $looping.MediaPath = $gif; $looping.Media.LoopMedia = $true; $looping.FixedMs = 900
        $watch.Restart(); [void](Wait-Intro ($introHost::PlayAsync($stage,$looping,$null)))
        if ($watch.ElapsedMilliseconds -lt 850 -or $watch.ElapsedMilliseconds -gt 3000) { throw 'Looping GIF intro did not fall back to the fixed time' }
        'PASS: GIF intro ends with the GIF; a looping GIF uses the fixed time.'
    } else { 'NOTE: ffmpeg not found; GIF intro length checks skipped.' }
    [void]$layout.Children.Remove($stage)
    'PASS: fixed time, until loaded (with minimum/maximum), skip, missing media, all exit effects and overlay cleanup.'

    # Coverage targets: the StartPage view or the whole window content.
    if (![object]::ReferenceEquals($introHost::Target('StartPageArea',$view),$view) -or ![object]::ReferenceEquals($introHost::Target('PlayniteWindow',$view),$window.Content)) { throw 'Intro coverage did not pick the StartPage area or the whole window' }
    'PASS: coverage picks the StartPage area or the whole Playnite window.'

    # Page intros on switching pages, and the global intro when StartPage opens.
    $introHost::ResetSession()
    $settingsObject = $vm.Settings.Settings
    $pages = Get-Setting $settingsObject 'DashboardPages'
    $origin = $vm.SelectedPage
    $introPage = [LandingPage.Models.DashboardPages]::Add($settingsObject,'Intro page')
    $introPage.Intro.Enabled = $true; $introPage.Intro.MediaPath = $png; $introPage.Intro.Length = 'FixedTime'; $introPage.Intro.FixedMs = 500
    $introPage.Intro.Coverage = 'StartPageArea'; $introPage.Intro.FadeInMs = 0; $introPage.Intro.ExitMs = 0; $introPage.Intro.Repeat = 'OncePerSession'
    $viewType = [LandingPage.Views.StartPageView]
    $viewType.GetMethod('WatchIntroModel',$flags).Invoke($view,@())
    function Switch-To($page) {
        $switch = $vm.SwitchPageAsync($page)
        try { $null = Wait-Task $switch } catch { $detail = $switch.Exception; while ($detail.InnerException) { $detail = $detail.InnerException }; throw ('Switch to ' + $page.Name + ' failed: ' + $detail.GetType().Name + ': ' + $detail.Message + ' @ ' + ($detail.StackTrace -split "`n" | Select-Object -First 3) -join ' | ') }
        if ($vm.SelectedPage.Id -ne $page.Id) { throw ('Could not switch to page ' + $page.Name + '; still on ' + $vm.SelectedPage.Name + '; canChange=' + $vm.CanChangePages) }
    }
    function Played-During($action) { $script:lastIntro = '(none)'; $null = & $action; $seen = $false; $until = [datetime]::UtcNow.AddMilliseconds(1500); while ([datetime]::UtcNow -lt $until) { if ($introHost::Current) { $seen = $true; $script:lastIntro = $introHost::Current.PlayedMedia + ' page=' + $vm.SelectedPage.Name + ' global=' + $introHost::GlobalPlayed }; Pump-For 20 }; return [bool]$seen }
    if (!(Played-During { Switch-To $introPage })) { throw 'Switching to a page with an intro did not play it' }
    Switch-To $origin
    if (Played-During { Switch-To $introPage }) { throw 'A first-switch-only page intro played twice in one session' }
    $introPage.Intro.Repeat = 'EveryTime'; Switch-To $origin
    if (!(Played-During { Switch-To $introPage })) { throw 'An every-switch page intro did not play again' }
    Switch-To $origin
    $globalIntro = Get-Setting $settingsObject 'StartupIntro'
    $globalIntro.Enabled = $true; $globalIntro.MediaPath = $png; $globalIntro.Length = 'FixedTime'; $globalIntro.FixedMs = 500; $globalIntro.Coverage = 'StartPageArea'; $globalIntro.FadeInMs = 0; $globalIntro.ExitMs = 0
    $globalIntro.Repeat = 'OncePerSession'; $introHost::GlobalPlayed = $true
    if (Played-During { $viewType.GetMethod('BeginIntroOnOpen',$flags).Invoke($view,@()) }) { throw ('A once-per-session global intro played after Playnite already showed it: ' + $script:lastIntro) }
    $globalIntro.Repeat = 'EveryTime'
    if (!(Played-During { $viewType.GetMethod('BeginIntroOnOpen',$flags).Invoke($view,@()) })) { throw 'An every-open global intro did not play when StartPage opened' }
    'PASS: page intros on first or every switch, and the global intro once per session or on every StartPage open.'

    # The dashboard reveal waits behind a playing intro and starts as the intro exits.
    $reveal = Get-Setting $settingsObject 'StartupAnimation'; $reveal.Effect = 'Fade'; $reveal.DurationMs = 150
    $viewType.GetField('DashboardAnimationsEnabled',$flags).SetValue($view,[Func[bool]]{ $true })
    $cover = New-Intro 'FixedTime'; $cover.FixedMs = 900; $cover.ExitMs = 200
    $introTask = $introHost::PlayAsync($view,$cover,$null)
    $revealTask = $viewType.GetMethod('PlayStartupAnimationAsync',$flags).Invoke($view,@($true)); Pump-For 400
    if ($revealTask.IsCompleted -or $view.StartupAnimator.IsRunning) { throw 'The dashboard reveal started while the intro covered the dashboard' }
    [void](Wait-Intro $introTask); Wait-Task $revealTask
    if (!$revealTask.Result) { throw 'The dashboard reveal did not run after the intro' }
    $viewType.GetMethod('CancelStartupAnimation',$flags).Invoke($view,@()); $reveal.Effect = 'None'; $globalIntro.Enabled = $false
    'PASS: the dashboard reveal waits for the intro and runs as it exits.'

    # Launch intro: its own window and UI thread while Playnite loads, held until Playnite reports it is ready.
    $launch = [LandingPage.Utils.LaunchIntro]
    $maximized = $false
    $normal = $launch::ParseBounds('{"Positions":{"Main_V2":{"Position":{"X":193.0,"Y":146.0},"Size":{"X":2119.0,"Y":1186.0},"State":0}}}',[ref]$maximized)
    if ($normal.X -ne 193 -or $normal.Width -ne 2119 -or $maximized) { throw 'Saved Playnite window bounds were not read' }
    [void]$launch::ParseBounds('{"Positions":{"Main_V2":{"Position":{"X":0.0,"Y":0.0},"Size":{"X":1600.0,"Y":900.0},"State":2}}}',[ref]$maximized)
    if (!$maximized) { throw 'A maximized Playnite window was not detected' }
    if (!$launch::ParseBounds('not json',[ref]$maximized).IsEmpty -or !$launch::ParseBounds('{"Positions":{}}',[ref]$maximized).IsEmpty) { throw 'Invalid window positions did not fall back' }
    $fallback = $launch::Visible([Windows.Rect]::new(-90000,-90000,800,600))
    if ($fallback.Width -lt 200 -or !$fallback.IntersectsWith([Windows.Rect]::new([Windows.SystemParameters]::VirtualScreenLeft,[Windows.SystemParameters]::VirtualScreenTop,[Windows.SystemParameters]::VirtualScreenWidth,[Windows.SystemParameters]::VirtualScreenHeight))) { throw 'Off-screen bounds were not moved onto a screen' }
    function Wait-Launch($limit) { $until = [datetime]::UtcNow.AddMilliseconds($limit); while ($launch::IsRunning -and [datetime]::UtcNow -lt $until) { Pump-For 30 }; !$launch::IsRunning }
    $area = [Windows.Rect]::new(40,40,320,180)
    $early = New-Intro 'FixedTime'; $early.FixedMs = 400; $early.MaximumMs = 15000; $early.ExitMs = 100
    if (!$launch::Start($early,$area,$false)) { throw 'Launch intro did not start' }
    Pump-For 1200
    if (!$launch::IsRunning -or !$introHost::IsPlaying -or !$launch::Window) { throw 'Launch intro did not hold until Playnite was ready' }
    if ($launch::Start($early,$area,$false)) { throw 'A second launch intro started while one was running' }
    $launch::PlayniteReady = $true
    if (!(Wait-Launch 4000)) { throw 'Launch intro did not end once Playnite was ready' }
    if ($introHost::IsPlaying -or $launch::Window -or $launch::Dispatcher) { throw 'Launch intro left its window or thread behind' }
    $capped = New-Intro 'FixedTime'; $capped.FixedMs = 300; $capped.MaximumMs = 1200; $capped.ExitMs = 0
    $watch.Restart(); [void]$launch::Start($capped,$area,$false)
    if (!(Wait-Launch 6000) -or $watch.ElapsedMilliseconds -gt 4500) { throw 'Launch intro ignored its maximum while Playnite was not ready' }
    $skippable = New-Intro 'FixedTime'; $skippable.FixedMs = 9000; $skippable.MaximumMs = 20000; $skippable.ExitMs = 0
    [void]$launch::Start($skippable,$area,$false); Pump-For 500; $launch::Skip()
    if (!(Wait-Launch 3000)) { throw 'Skipping did not end the launch intro' }
    $noMedia = New-Intro 'UntilMediaEnds'; $noMedia.MediaPath = (Join-Path $introRoot 'none.mp4')
    if ($launch::Start($noMedia,$area,$false)) { throw 'A launch intro without media started' }
    if ($gif) {
        # GIF frames decode and present on the launch intro's own UI thread.
        $gifLaunch = New-Intro 'UntilMediaEnds'; $gifLaunch.MediaPath = $gif; $gifLaunch.MaximumMs = 8000; $gifLaunch.ExitMs = 0
        $watch.Restart(); [void]$launch::Start($gifLaunch,$area,$false); $launch::PlayniteReady = $true
        $frameSeen = $false; $until = [datetime]::UtcNow.AddSeconds(8)
        while ($launch::IsRunning -and [datetime]::UtcNow -lt $until) { $presenter = $launch::Presenter; if ($presenter -and $presenter.PlayedMedia -eq $gif) { $frameSeen = $true }; Pump-For 30 }
        if (!$frameSeen -or $launch::IsRunning -or $watch.ElapsedMilliseconds -lt 400 -or $watch.ElapsedMilliseconds -gt 6500) { throw "GIF launch intro did not play to its end on its own thread ($($watch.ElapsedMilliseconds) ms)" }
    }
    'PASS: launch intro reads saved window bounds, runs on its own thread, holds until Playnite is ready, honors maximum and skip, and cleans up.'

    # Editors: global wording in settings, page wording in the page manager, folder field toggles.
    $editor = [LandingPage.Views.Settings.StartupIntroSettingsView]::new(); $editor.DataContext = [LandingPage.Settings.StartupIntroSettings]::new(); $editor.DataContext.Enabled = $true
    [void]$view.Content.Children.Add($editor); $window.UpdateLayout(); Pump-For 50
    if ($editor.FindName('RepeatOnce').Content -ne 'When Playnite starts' -or $editor.FindName('PlayOnOpenToggle').Visibility -ne 'Collapsed') { throw 'Global intro editor uses page wording' }
    if ($editor.FindName('FileField').Visibility -ne 'Visible' -or $editor.FindName('FolderField').Visibility -ne 'Collapsed') { throw 'Intro editor does not show the file field for a single file' }
    $editor.DataContext.Source = 'RandomFromFolder'; Pump-For 30
    if ($editor.FindName('FileField').Visibility -ne 'Collapsed' -or $editor.FindName('FolderField').Visibility -ne 'Visible') { throw 'Intro editor does not switch to the folder field' }
    if ($editor.FindName('LaunchOptions').Visibility -ne 'Visible') { throw 'Launch options are hidden for the whole-window startup intro' }
    $editor.DataContext.Coverage = 'StartPageArea'; Pump-For 30
    if ($editor.FindName('LaunchOptions').Visibility -ne 'Collapsed') { throw 'Launch options are shown for a StartPage-area intro' }
    $editor.DataContext.Coverage = 'PlayniteWindow'; $editor.DataContext.Repeat = 'EveryTime'; Pump-For 30
    if ($editor.FindName('LaunchOptions').Visibility -ne 'Collapsed') { throw 'Launch options are shown for an every-open intro' }
    $editor.DataContext.Repeat = 'OncePerSession'
    $editor.IsPageIntro = $true
    if ($editor.FindName('RepeatOnce').Content -notmatch 'this page' -or $editor.FindName('PlayOnOpenToggle').Visibility -ne 'Visible' -or $editor.FindName('LaunchOptions').Visibility -ne 'Collapsed') { throw 'Page intro editor does not use page wording' }
    [void]$view.Content.Children.Remove($editor)
    $view.ShowPages(); Pump-For 50
    $pageEditor = $view.FindName('PageIntroEditor')
    if (!$pageEditor.IsPageIntro -or ![object]::ReferenceEquals($pageEditor.DataContext,$vm.SelectedPage.Intro)) { throw 'Page manager intro editor is not bound to the selected page' }
    $view.GetType().GetMethod('ClosePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    'PASS: intro editors use global/page wording, switch file/folder fields and bind the page manager to the selected page.'

    # Editor preview: "until the dashboard has loaded" cannot be simulated, so a clip plays to its end; with a repeated
    # part, the preview then repeats that part three times before it ends.
    if ($gif) {
        $previewSettings = [LandingPage.Settings.StartupIntroSettings]::new(); $previewSettings.Enabled = $true; $previewSettings.MediaPath = $gif
        $previewSettings.Length = 'UntilDashboardLoaded'; $previewSettings.MinimumMs = 0; $previewSettings.FadeInMs = 0; $previewSettings.ExitMs = 0
        $previewSettings.AfterEnd = 'RepeatEnd'; $previewSettings.AfterEndRepeatMs = 300
        $previewEditor = [LandingPage.Views.Settings.StartupIntroSettingsView]::new(); $previewEditor.DataContext = $previewSettings
        [void]$view.Content.Children.Add($previewEditor); $window.UpdateLayout(); Pump-For 50
        $previewEditor.GetType().GetMethod('Preview_Click',$flags).Invoke($previewEditor,@($null,[Windows.RoutedEventArgs]::new()))
        $watch = [Diagnostics.Stopwatch]::StartNew(); $mostRepeats = 0; $until = [datetime]::UtcNow.AddSeconds(15)
        do {
            Pump-For 20
            if ($introHost::Current) { $mostRepeats = [Math]::Max($mostRepeats,$introHost::Current.Artwork.RepeatCount) }
            if ([datetime]::UtcNow -gt $until) { throw 'The intro preview did not finish' }
        } while ($previewEditor.FindName('PreviewStatus').Text -notmatch 'finished|could not')
        $status = $previewEditor.FindName('PreviewStatus').Text
        if ($mostRepeats -lt 3 -or $watch.ElapsedMilliseconds -lt 1100 -or $status -notmatch 'whole clip.*last 300 ms 3 times') { throw "The intro preview did not play the whole clip and repeat the last part three times ($mostRepeats repeats, $($watch.ElapsedMilliseconds) ms, '$status')" }
        if ($previewSettings.Length -ne 'UntilDashboardLoaded' -or $previewSettings.MaximumMs -eq 60000) { throw 'The intro preview changed the saved intro settings' }
        $previewSettings.AfterEnd = 'HoldLastFrame'
        $previewEditor.GetType().GetMethod('Preview_Click',$flags).Invoke($previewEditor,@($null,[Windows.RoutedEventArgs]::new()))
        $watch.Restart(); $until = [datetime]::UtcNow.AddSeconds(15)
        do { Pump-For 20; if ([datetime]::UtcNow -gt $until) { throw 'The plain intro preview did not finish' } } while ($previewEditor.FindName('PreviewStatus').Text -notmatch 'finished|could not')
        if ($watch.ElapsedMilliseconds -lt 400 -or $previewEditor.FindName('PreviewStatus').Text -notmatch 'whole clip') { throw "The intro preview of an until-loaded intro did not play the whole clip ($($watch.ElapsedMilliseconds) ms)" }
        [void]$view.Content.Children.Remove($previewEditor)
        'PASS: the intro preview plays the whole clip, then the repeated part three times, without changing the saved settings.'
    } else { 'SKIP: ffmpeg not found; intro preview of the repeated part not checked.' }
    } finally { $introHost::ResetSession(); Remove-Item -Recurse -Force $introRoot -ErrorAction SilentlyContinue }

'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
