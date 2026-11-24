# Run -STA. Startup animations (roadmap step 22): settings persistence, panel ordering, the animation engine's
# exact restoration (including bound opacity) and the StartPage integration (loading wait, skip, reduced motion,
# first-open vs every-open). Reuses the in-memory API/window fixture.
$fixture = Get-Content (Join-Path $PSScriptRoot 'Test-DashboardPages.ps1') -Raw
$marker = '    $window.Show()'
$prefix = $fixture.Substring(0,$fixture.IndexOf($marker)+$marker.Length)
$startupTestRoot = Split-Path $PSScriptRoot -Parent
$prefix = $prefix.Replace('$root = Split-Path $PSScriptRoot -Parent','$root = $startupTestRoot')
$cleanup = $fixture.Substring($fixture.LastIndexOf('} finally {'))
$body = @'
    function Pump-For($milliseconds) {
        $until = [datetime]::UtcNow.AddMilliseconds($milliseconds)
        do { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background); Start-Sleep -Milliseconds 10 } while ([datetime]::UtcNow -lt $until)
        $window.UpdateLayout()
    }
    $static = [Reflection.BindingFlags]'Static,Public,NonPublic'
    function Get-Descendants($node) {
        $count = [Windows.Media.VisualTreeHelper]::GetChildrenCount($node)
        for($i = 0; $i -lt $count; $i++) { $child = [Windows.Media.VisualTreeHelper]::GetChild($node,$i); $child; Get-Descendants $child }
    }
    $json = { param($value) [Newtonsoft.Json.JsonConvert]::SerializeObject($value) }

    # Settings: opt-in default, old configs load without an animation, clamped values, runtime flags never saved.
    $fresh = [LandingPage.LandingPageSettings]::new(); $startup = Get-Setting $fresh 'StartupAnimation'
    if ($startup.Effect -ne 'None' -or $startup.IsEnabled -or $startup.Trigger -ne 'FirstOpen' -or !$startup.AnimateBackground -or !$startup.SkipOnInput) { throw 'Startup animation defaults are not opt-in' }
    $legacy = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{"DashboardTransition":1}',[LandingPage.LandingPageSettings])
    if ((Get-Setting $legacy 'StartupAnimation').Effect -ne 'None') { throw 'Configs saved before startup animations do not load with the effect off' }
    $startup.Effect = 'Ripple'; $startup.DurationMs = 99999; $startup.StaggerMs = -5; $startup.StartScale = 9; $startup.Distance = [double]::NaN
    if ($startup.DurationMs -ne 3000 -or $startup.StaggerMs -ne 0 -or $startup.StartScale -ne 1.3 -or $startup.Distance -ne 48) { throw 'Startup animation values are not clamped' }
    $startup.Order = 'EdgesIn'; $startup.Origin = 'BottomRight'; $startup.Easing = 'Elastic'; $startup.Trigger = 'EveryOpen'
    $saved = & $json $startup
    foreach ($runtime in @('IsEnabled','IsPanelEffect','UsesOrder','UsesDirection','UsesOrigin','UsesDistance','UsesScale')) { if ($saved -match ('"'+$runtime+'"')) { throw "Runtime-only startup flag $runtime was serialized" } }
    $copy = [Newtonsoft.Json.JsonConvert]::DeserializeObject($saved,[LandingPage.Settings.StartupAnimationSettings])
    if ((& $json $copy) -ne $saved -or !$copy.UsesOrigin -or $copy.UsesOrder) { throw 'Startup animation settings did not round-trip' }
    $copy.ResetOptions()
    if ($copy.Effect -ne 'Ripple' -or $copy.DurationMs -ne 650 -or $copy.Trigger -ne 'FirstOpen' -or $copy.Origin -ne 'Center') { throw 'Reset options changed the effect or missed an option' }
    $roundTrip = [Newtonsoft.Json.JsonConvert]::DeserializeObject((& $json $fresh),[LandingPage.LandingPageSettings])
    if ((Get-Setting $roundTrip 'StartupAnimation').Effect -ne 'Ripple') { throw 'Startup animation did not persist with LandingPageSettings' }
    'PASS: opt-in defaults, legacy config load, clamping, runtime-only flags, round-trip and option reset.'

    # Panel ordering: a 2x2 grid of panels inside a 400x300 dashboard.
    $rects = [Collections.Generic.List[Windows.Rect]]::new()
    foreach ($r in @(@(0,0),@(200,0),@(0,150),@(200,150))) { $rects.Add([Windows.Rect]::new($r[0],$r[1],200,150)) }
    $area = [Windows.Size]::new(400,300)
    function Ranks($order,$origin,$fromOrigin,$seed = 1) { (@([LandingPage.Controls.StartupAnimator]::Ranks($rects,$area,$order,$origin,$fromOrigin,$seed)) -join ',') }
    $expect = @{ Reading = '0,1,2,3'; TopToBottom = '0,1,2,3'; BottomToTop = '2,3,0,1'; LeftToRight = '0,2,1,3'; RightToLeft = '2,0,3,1' }
    foreach ($key in $expect.Keys) { $got = Ranks $key 'Center' $false; if ($got -ne $expect[$key]) { throw "Panel order $key gave $got, expected $($expect[$key])" } }
    if ((Ranks 'TopToBottom' 'TopLeft' $true) -notmatch '^0,') { throw 'Ripple from the top-left did not start with the top-left panel' }
    if ((Ranks 'TopToBottom' 'BottomRight' $true) -notmatch ',0$') { throw 'Ripple from the bottom-right did not start with the bottom-right panel' }
    if ((Ranks 'Random' 'Center' $false 7) -ne (Ranks 'Random' 'Center' $false 7)) { throw 'Random order is not reproducible for one seed' }
    if ((@([LandingPage.Controls.StartupAnimator]::Ranks($rects,$area,'Random','Center',$false,7)) | Sort-Object) -join ',' -ne '0,1,2,3') { throw 'Random order does not rank every panel once' }
    $origin = [LandingPage.Controls.StartupAnimator]::OriginPoint('BottomRight',$area)
    if ($origin.X -ne 400 -or $origin.Y -ne 300) { throw 'Origin point is not at the requested corner' }
    $offset = [LandingPage.Controls.StartupAnimator]::StartOffset('Up',40)
    if ($offset.Y -ne 40 -or $offset.X -ne 0) { throw 'Rising content does not start below its final place' }
    'PASS: reading/top/bottom/left/right orders, origin-based ripple, reproducible random order, origins and offsets.'

    # Engine: animate real elements, then verify exact restoration, including a bound opacity.
    $source = [LandingPage.Settings.StartupAnimationSettings]::new(); $source.DurationMs = 200; $source.StaggerMs = 40; $source.DelayMs = 0
    $stage = [Windows.Controls.Grid]::new(); $stage.Width = 400; $stage.Height = 300; $stage.HorizontalAlignment = 'Left'; $stage.VerticalAlignment = 'Top'
    $opacitySource = [LandingPage.Settings.ImageWidgetSettings]::new(); $opacitySource.Opacity = 0.8
    $binding = [Windows.Data.Binding]::new('Opacity'); $binding.Source = $opacitySource
    [void][Windows.Data.BindingOperations]::SetBinding($stage,[Windows.UIElement]::OpacityProperty,$binding)
    $panels = [Collections.Generic.List[Collections.Generic.KeyValuePair[Windows.FrameworkElement,Windows.Rect]]]::new()
    $original = [Windows.Media.RotateTransform]::new(0)
    for ($i = 0; $i -lt 4; $i++) {
        $panel = [Windows.Controls.Border]::new(); $panel.Width = 200; $panel.Height = 150; $panel.HorizontalAlignment = 'Left'; $panel.VerticalAlignment = 'Top'
        $panel.Margin = [Windows.Thickness]::new($rects[$i].X,$rects[$i].Y,0,0); $panel.Background = [Windows.Media.Brushes]::SteelBlue
        if ($i -eq 1) { $panel.RenderTransform = $original }
        [void]$stage.Children.Add($panel); $panels.Add([Collections.Generic.KeyValuePair[Windows.FrameworkElement,Windows.Rect]]::new($panel,$rects[$i]))
    }
    [void]$layout.Children.Add($stage); $window.UpdateLayout()
    $animator = [LandingPage.Controls.StartupAnimator]::new()
    foreach ($effect in @('Cascade','Ripple','Fade','Rise','Zoom','Spotlight','Curtain')) {
        $source.Effect = $effect
        $animator.Play($stage,$null,$panels,$source,$null,3)
        if (!$animator.IsRunning) { throw "$effect did not start" }
        if ($effect -eq 'Cascade') {
            # Animated values apply on the first animation clock tick, which precedes each rendered frame.
            Pump-For 20
            $first = $panels[0].Key; $last = $panels[3].Key
            if ($last.Opacity -gt 0.01 -or $first.Opacity -ge 0.99) { throw ('Cascade panels are visible before their turn (first='+$first.Opacity+', last='+$last.Opacity+')') }
            if (!($last.RenderTransform -is [Windows.Media.TranslateTransform]) -or !($panels[1].Key.RenderTransform -is [Windows.Media.TransformGroup])) { throw 'Cascade did not move panels or dropped an existing transform' }
            $expected = 3 * 40 + 200
            if ([Math]::Abs($animator.Length.TotalMilliseconds - $expected) -gt 1) { throw "Cascade length $($animator.Length.TotalMilliseconds) ms, expected $expected ms" }
        }
        if ($effect -eq 'Spotlight' -and !($stage.Clip -is [Windows.Media.EllipseGeometry])) { throw 'Spotlight did not clip the content with a circle' }
        if ($effect -eq 'Curtain' -and !($stage.Clip -is [Windows.Media.RectangleGeometry])) { throw 'Curtain did not clip the content' }
        Pump-For ($animator.Length.TotalMilliseconds + 250)
        if ($animator.IsRunning) { throw "$effect did not finish on time" }
        foreach ($pair in $panels) {
            $panel = $pair.Key
            if ($panel.HasAnimatedProperties -or [Math]::Abs($panel.Opacity - 1) -gt 0.001 -or $panel.Clip) { throw "$effect left animation state on a panel" }
        }
        if (![object]::ReferenceEquals($panels[1].Key.RenderTransform,$original) -or $panels[0].Key.ReadLocalValue([Windows.UIElement]::RenderTransformProperty) -ne [Windows.DependencyProperty]::UnsetValue) { throw "$effect did not restore panel transforms exactly" }
        if ($stage.Clip -or $stage.HasAnimatedProperties -or ![Windows.Data.BindingOperations]::GetBindingExpression($stage,[Windows.UIElement]::OpacityProperty) -or [Math]::Abs($stage.Opacity - 0.8) -gt 0.001) { throw "$effect broke or replaced the content's bound opacity" }
    }
    $animator.Hide($stage,$null); Pump-For 20
    if ($stage.Opacity -ne 0 -or ![Windows.Data.BindingOperations]::GetBindingExpression($stage,[Windows.UIElement]::OpacityProperty)) { throw 'Hiding while loading replaced the bound opacity' }
    $opacitySource.Opacity = 0.6; $animator.Finish()
    if ([Math]::Abs($stage.Opacity - 0.6) -gt 0.001) { throw 'Finishing after hide did not return to the current bound opacity' }
    $source.Effect = 'Spotlight'; $animator.Play($stage,$null,$panels,$source,$null,3); $animator.Finish()
    if ($animator.IsRunning -or $stage.Clip) { throw 'Skipping did not restore the content immediately' }
    [void]$layout.Children.Remove($stage)
    'PASS: all seven effects run and restore opacity, transforms and clips exactly; bound opacity survives hide, play and skip.'

    # Integration with the real StartPage view.
    $viewType = [LandingPage.Views.StartPageView]
    $viewType.GetField('StartupLoadTimeoutMs',$static).SetValue($null,400)
    $play = { param($preview) $viewType.GetMethod('PlayStartupAnimationAsync',$flags).Invoke($view,@([bool]$preview)) }
    $viewSettings = Get-Setting $vm.Settings.Settings 'StartupAnimation'
    $viewSettings.Effect = 'Cascade'; $viewSettings.DurationMs = 150; $viewSettings.StaggerMs = 20; $viewSettings.Trigger = 'FirstOpen'
    $viewType.GetField('DashboardAnimationsEnabled',$flags).SetValue($view,[Func[bool]]{ $true })
    $content = $view.FindName('StartPageScrollViewer')
    $viewType.GetMethod('ResetStartupSession',$static).Invoke($null,@())
    $vm.IsLoading = $true
    $task = & $play $false; Pump-For 120
    if ($content.Opacity -ne 0 -or $task.IsCompleted -or $view.StartupAnimator.IsRunning) { throw 'Dashboard content is visible or animating while the page is still loading' }
    $vm.IsLoading = $false
    Wait-Task $task
    if (!$task.Result -or !$view.StartupAnimator.IsRunning) { throw 'Startup animation did not start after loading' }
    $found = $viewType.GetMethod('StartupPanels',$flags).Invoke($view,@())
    if ($found.Count -lt 1) { throw 'No visible dashboard panels were found for panel effects' }
    $viewType.GetMethod('SkipStartupOnInput',$flags).Invoke($view,@())
    if ($view.StartupAnimator.IsRunning -or $content.HasAnimatedProperties -or $content.Opacity -ne 1) { throw 'Input did not skip to the finished dashboard' }
    $second = & $play $false; Wait-Task $second
    if ($second.Result) { throw 'First-open animation played twice in one session' }
    $viewSettings.Trigger = 'EveryOpen'
    $third = & $play $false; Wait-Task $third
    if (!$third.Result) { throw 'Every-open animation did not play again' }
    $viewType.GetMethod('CancelStartupAnimation',$flags).Invoke($view,@())
    $viewType.GetField('DashboardAnimationsEnabled',$flags).SetValue($view,[Func[bool]]{ $false })
    $reduced = & $play $true; Wait-Task $reduced
    if ($reduced.Result -or $content.HasAnimatedProperties) { throw 'Startup animation ignored the Windows animation preference' }
    $viewSettings.Effect = 'None'
    $off = & $play $true; Wait-Task $off
    if ($off.Result) { throw 'An effect of None still animated' }
    $viewType.GetField('DashboardAnimationsEnabled',$flags).SetValue($view,[Func[bool]]{ $true })
    $viewType.GetField('StartupLoadTimeoutMs',$static).SetValue($null,6000)
    'PASS: content hidden while loading, panel discovery, skip on input, first-open vs every-open, reduced motion and None.'
    # Editor: the page manager section shows only the options the chosen effect uses.
    $view.ShowPages(); $section = $view.FindName('StartupAnimationSection'); $section.IsExpanded = $true; $window.UpdateLayout(); Pump-For 50
    function Visible-Labels { @(Get-Descendants $section | Where-Object { $_ -is [LandingPage.Controls.SettingsField] -or $_ -is [LandingPage.Controls.SettingsSlider] } | Where-Object IsVisible | ForEach-Object Label) }
    $viewSettings.Effect = 'None'; Pump-For 30
    if ((Visible-Labels) -contains 'Duration (ms)') { throw 'Startup options are shown while the effect is None' }
    $viewSettings.Effect = 'Cascade'; Pump-For 30; $labels = Visible-Labels
    foreach ($label in @('Effect','Play','Duration (ms)','Delay between panels (ms)','Panel order','Direction','Movement distance (px)')) { if ($labels -notcontains $label) { throw "Cascade editor is missing $label" } }
    if ($labels -contains 'Origin' -or $labels -contains 'Start scale') { throw 'Cascade editor shows ripple-only options' }
    $viewSettings.Effect = 'Spotlight'; Pump-For 30; $labels = Visible-Labels
    if ($labels -notcontains 'Origin' -or $labels -contains 'Panel order' -or $labels -contains 'Delay between panels (ms)' -or $labels -contains 'Movement distance (px)') { throw 'Spotlight editor shows the wrong options' }
    $viewSettings.Effect = 'None'
    $view.GetType().GetMethod('ClosePages_Click',$flags).Invoke($view,@($null,[Windows.RoutedEventArgs]::new()))
    'PASS: startup animation editor shows only the options used by each effect.'

'@
Invoke-Expression ($prefix + "`n" + $body + "`n" + $cleanup)
