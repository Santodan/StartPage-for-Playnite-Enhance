$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
foreach ($name in @('Playnite.SDK','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name+'.dll'))) }
$app = [Windows.Application]::new()
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
function Layout { $window.UpdateLayout() }
try {
    $window = [Windows.Window]::new(); $window.Width = 400; $window.Height = 300; $window.Opacity = 0; $window.ShowActivated = $false
    $scroll = [Windows.Controls.ScrollViewer]::new(); $scroll.Height = 150; $scroll.VerticalScrollBarVisibility = [Windows.Controls.ScrollBarVisibility]::Auto
    $body = [Windows.Controls.Border]::new(); $body.Height = 1000; $scroll.Content = $body; $window.Content = $scroll
    $window.Show(); Layout
    # Enable after Loaded, the same way dynamic Featured Game content is configured.
    [LandingPage.Controls.ContentAutoScroll]::SetEnabled($scroll,$true)
    [LandingPage.Controls.ContentAutoScroll]::SetSpeed($scroll,40)
    [LandingPage.Controls.ContentAutoScroll]::SetPauseOnHover($scroll,$false)
    $stateProperty = [LandingPage.Controls.ContentAutoScroll].GetField('StateProperty',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
    $state = $scroll.GetValue($stateProperty)
    $advance = $state.GetType().GetMethod('Advance',$flags)
    $wait = $state.GetType().GetField('wait',$flags)
    [void]$advance.Invoke($state,@(1.0)); [void]$advance.Invoke($state,@(1.0)); Layout
    if ($scroll.VerticalOffset -ne 0) { throw 'Auto scroll skipped its opening pause' }
    [void]$advance.Invoke($state,@(0.5)); Layout
    if ([Math]::Abs($scroll.VerticalOffset-20) -gt 1) { throw 'Auto-scroll speed is incorrect' }
    [LandingPage.Controls.ContentAutoScroll]::SetPaused($scroll,$true)
    [void]$advance.Invoke($state,@(1.0)); Layout
    if ([Math]::Abs($scroll.VerticalOffset-20) -gt 1) { throw 'Paused content still scrolled' }
    [LandingPage.Controls.ContentAutoScroll]::SetPaused($scroll,$false)
    $scroll.ScrollToVerticalOffset($scroll.ScrollableHeight-5); Layout; $wait.SetValue($state,0.0)
    [void]$advance.Invoke($state,@(0.5)); Layout
    [void]$advance.Invoke($state,@(1.0)); Layout
    if ($scroll.VerticalOffset -lt $scroll.ScrollableHeight-1) { throw 'Auto scroll did not pause at the bottom' }
    [void]$advance.Invoke($state,@(1.0)); [void]$advance.Invoke($state,@(0.1)); Layout
    if ($scroll.VerticalOffset -ne 0) { throw 'Auto scroll did not loop to the top' }
    [LandingPage.Controls.ContentAutoScroll]::SetEnabled($scroll,$false)
    [void]$advance.Invoke($state,@(1.0)); Layout
    if ($scroll.VerticalOffset -ne 0 -or $state.GetType().GetField('timer',$flags).GetValue($state).IsEnabled) { throw 'Disabled auto scroll retained motion/timer' }
    # Exercise the real DispatcherTimer in a popup presentation source without
    # activating a test window or stealing focus from another application.
    $popup = [Windows.Controls.Primitives.Popup]::new(); $popup.AllowsTransparency = $true; $popup.PlacementTarget = $scroll
    $timerView = [Windows.Controls.ScrollViewer]::new(); $timerView.Width = 200; $timerView.Height = 120; $timerView.Opacity = 0
    $timerBody = [Windows.Controls.Border]::new(); $timerBody.Height = 600; $timerView.Content = $timerBody
    $popup.Child = $timerView; $popup.IsOpen = $true; Layout
    [LandingPage.Controls.ContentAutoScroll]::SetPauseOnHover($timerView,$false)
    [LandingPage.Controls.ContentAutoScroll]::SetSpeed($timerView,20)
    [LandingPage.Controls.ContentAutoScroll]::SetEnabled($timerView,$true)
    $timerState = $timerView.GetValue($stateProperty)
    [Windows.Application].GetMethod('OnActivated',$flags).Invoke($app,@([EventArgs]::Empty))
    $timerState.GetType().GetField('wait',$flags).SetValue($timerState,0.0)
    $deadline = [datetime]::UtcNow.AddSeconds(2)
    while ($timerView.VerticalOffset -le 0 -and [datetime]::UtcNow -lt $deadline) { $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); Start-Sleep -Milliseconds 10 }
    if ($timerView.VerticalOffset -le 0) { throw ('Actual timer did not scroll: loaded=' + $timerView.IsLoaded + ' visible=' + $timerView.IsVisible + ' overflow=' + $timerView.ScrollableHeight + ' status=' + $timerState.GetType().GetField('lastStatus',$flags).GetValue($timerState) + ' timer=' + $timerState.GetType().GetField('timer',$flags).GetValue($timerState).IsEnabled + ' wait=' + $timerState.GetType().GetField('wait',$flags).GetValue($timerState)) }
    [Windows.Application].GetMethod('OnDeactivated',$flags).Invoke($app,@([EventArgs]::Empty))
    if ($timerState.GetType().GetField('timer',$flags).GetValue($timerState).IsEnabled) { throw 'Inactive Playnite application kept auto scrolling' }
    $popup.IsOpen = $false
    $window.Content = $null
    $app.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $window.Close()
    if ($state.GetType().GetField('owner',$flags).GetValue($state)) { throw 'Unloaded auto scroll retained its window subscription' }
    'PASS: initial pause, scroll speed, editing pause, bottom pause, looping, disable and unload cleanup.'
} finally { if ($popup) { $popup.IsOpen = $false }; if ($window) { $window.Close() }; $app.Shutdown() }
