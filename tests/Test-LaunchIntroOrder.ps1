# Run -STA. Launch intro window order: by default it is not always-on-top; it keeps Playnite's own windows below it but
# lets other apps come in front (and leaves them in front when it ends). "Keep it above other apps" restores the
# always-on-top window. The test thread plays Playnite's UI thread; another thread plays another app.
$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source/StartPage/bin/Release'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
foreach ($name in @('Newtonsoft.Json','Playnite.SDK','StartPage.SDK','StartPage.SDK.Async','felixkmh.PlayniteCommon','StartPagePlugin')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $build ($name + '.dll'))) }
Add-Type -Namespace LaunchOrderTest -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr window, int index);
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
'@
function Pump($milliseconds = 30) {
    $until = [datetime]::UtcNow.AddMilliseconds($milliseconds)
    do { $frame = [Windows.Threading.DispatcherFrame]::new(); [void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::Background,[Action]{ $frame.Continue = $false }); [Windows.Threading.Dispatcher]::PushFrame($frame); Start-Sleep -Milliseconds 5 } while ([datetime]::UtcNow -lt $until)
}
$launch = [LandingPage.Utils.LaunchIntro]
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$isAbove = { param($window,$reference) [bool]$launch.GetMethod('IsAbove',$flags).Invoke($null,@($window,$reference)) }
$introHandle = { [IntPtr]$launch.GetField('introHandle',$flags).GetValue($null) }
# Moves `$window` directly below `$above` in the z-order (no activation).
$placeBelow = { param($window,$above) [void][LaunchOrderTest.Native]::SetWindowPos($window,$above,0,0,0,0,0x1 -bor 0x2 -bor 0x10) }

$settings = [LandingPage.Settings.StartupIntroSettings]::new()
if ($settings.LaunchAboveOtherApps) { throw 'The launch intro must not stay above other apps by default' }
$settings.LaunchAboveOtherApps = $true; $copy = [LandingPage.Models.DashboardProfiles]::Clone($settings)
if (!$copy.LaunchAboveOtherApps) { throw 'Keep above other apps did not round-trip' }
$settings.ResetOptions(); if ($settings.LaunchAboveOtherApps) { throw 'Reset did not turn off keep above other apps' }
if ([Newtonsoft.Json.JsonConvert]::DeserializeObject('{"Enabled":true}',[LandingPage.Settings.StartupIntroSettings]).LaunchAboveOtherApps) { throw 'Older settings must not keep the intro above other apps' }
'PASS: keep above other apps is off by default, round-trips, resets and stays off for older settings.'

$folder = Join-Path ([IO.Path]::GetTempPath()) ('sp-launch-order-' + [guid]::NewGuid()); [void][IO.Directory]::CreateDirectory($folder)
$png = Join-Path $folder 'still.png'
$bitmap = [Windows.Media.Imaging.WriteableBitmap]::new(8,8,96,96,[Windows.Media.PixelFormats]::Bgra32,$null)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create($png); $encoder.Save($stream); $stream.Close()

# "Another app": a window on its own thread (compiled helper; PowerShell script blocks cannot run on a bare thread).
$wpf = @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml') | ForEach-Object { [Reflection.Assembly]::LoadWithPartialName($_).Location }
Add-Type -ReferencedAssemblies $wpf -TypeDefinition @"
using System; using System.Threading; using System.Windows; using System.Windows.Interop; using System.Windows.Threading;
public static class OtherAppWindow {
    public static IntPtr Handle; public static Dispatcher Dispatcher;
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr w, IntPtr after, int x, int y, int cx, int cy, uint flags);
    public static void Start() {
        var ready = new ManualResetEventSlim(false);
        var thread = new Thread(() => {
            var window = new Window { Title = "Other app", Width = 300, Height = 200, Left = 150, Top = 150, ShowInTaskbar = false, ShowActivated = false };
            window.Show(); Handle = new WindowInteropHelper(window).Handle; Dispatcher = Dispatcher.CurrentDispatcher; ready.Set(); Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); ready.Wait(10000);
    }
    public static void BringToTop() { Dispatcher.Invoke(new Action(() => SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x13))); }
    public static void Stop() { if (Dispatcher != null) Dispatcher.InvokeShutdown(); }
}
"@
[OtherAppWindow]::Start(); $script:otherHandle = [OtherAppWindow]::Handle
$main = [Windows.Window]::new(); $main.Title = 'Playnite'; $main.Width = 500; $main.Height = 350; $main.Left = 80; $main.Top = 80; $main.ShowInTaskbar = $false; $main.ShowActivated = $false
try {
    foreach ($above in @($false,$true)) {
        $options = [LandingPage.Settings.StartupIntroSettings]::new(); $options.Enabled = $true; $options.MediaPath = $png
        $options.Length = 'FixedTime'; $options.FixedMs = 2500; $options.FadeInMs = 0; $options.ExitMs = 0; $options.ShowSkipHint = $false
        $options.LaunchAboveOtherApps = $above
        if (!$launch::Start($options,[Windows.Rect]::new(60,60,600,400),$false)) { throw 'The launch intro did not start' }
        $launch::PlayniteReady = $true; $launch::DashboardReady = $true  # Start resets these; Playnite reports ready while the intro plays
        $until = [datetime]::UtcNow.AddSeconds(5); while ((& $introHandle) -eq [IntPtr]::Zero) { if ([datetime]::UtcNow -gt $until) { throw 'The launch intro window did not appear' }; Pump 20 }
        Pump 200
        $intro = & $introHandle
        $topmost = ([LaunchOrderTest.Native]::GetWindowLong($intro,-20) -band 8) -ne 0
        if ($topmost -ne $above) { throw "The launch intro window topmost=$topmost, expected $above" }
        if (!$main.IsVisible) { $main.Show() }
        $mainHandle = [Windows.Interop.WindowInteropHelper]::new($main).Handle
        if (!$above) {
            # Playnite's window ends up above the intro (it appears, or its taskbar button is clicked): it goes back below it.
            & $placeBelow $intro $mainHandle
            if (!(& $isAbove $mainHandle $intro)) { throw 'Test setup: the Playnite window could not be placed above the intro' }
            Pump 250
            if (& $isAbove $mainHandle $intro) { throw 'A Playnite window stayed above the launch intro' }
            # Another app in front of the intro stays in front.
            & $placeBelow $intro $script:otherHandle
            if (!(& $isAbove $script:otherHandle $intro)) { throw 'Test setup: the other app could not be placed above the intro' }
            Pump 400
            if (!(& $isAbove $script:otherHandle $intro)) { throw 'The launch intro moved above another app' }
        }
        $until = [datetime]::UtcNow.AddSeconds(8); while ($launch::IsRunning) { if ([datetime]::UtcNow -gt $until) { $pr = $launch::Presenter; throw "The launch intro did not end (presenter=$($pr -ne $null) playing=$(if ($pr) { $pr.IsPlaying }) exiting=$(if ($pr) { $pr.IsExiting }) elapsed=$(if ($pr) { $pr.Elapsed.TotalMilliseconds }) window=$($launch::Window -ne $null) ready=$($launch::PlayniteReady))" }; Pump 30 }
        if ($above -and !$launch::HadFocusAtEnd) { throw 'Keep above other apps must give Playnite the focus when it ends' }
        "PASS: launch intro window order with keep above other apps $(if ($above) { 'on (always on top, Playnite focused at the end)' } else { 'off (above Playnite only, other apps can come in front)' })."
    }
} finally {
    $main.Close(); [OtherAppWindow]::Stop()
    Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
}
