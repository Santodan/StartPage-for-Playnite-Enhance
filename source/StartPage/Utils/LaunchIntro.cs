using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LandingPage.Controls;
using LandingPage.Settings;
using Newtonsoft.Json.Linq;

namespace LandingPage.Utils
{
    // The launch intro: started from the plugin constructor while Playnite is still loading (before its main window
    // exists), in a borderless window on its own UI thread so playback stays smooth while Playnite's UI thread is busy.
    // The window stays above Playnite's own windows (or above every app with LaunchAboveOtherApps). It stays up until Playnite reports it has started (and, in "until loaded" mode, until the dashboard is
    // ready), bounded by the intro's maximum, then reveals Playnite with the exit effect and focuses it.
    public static class LaunchIntro
    {
        private static volatile bool running, covering;
        // Set from Playnite's UI thread once its main window is up / the dashboard has loaded.
        public static volatile bool PlayniteReady;
        public static volatile bool DashboardReady;
        public static bool IsRunning => running;
        // True until the intro starts its exit, so a dashboard reveal can run beneath the exit.
        public static bool IsCovering => covering && !(Presenter?.IsExiting ?? false);
        public static IntroPresenter Presenter { get; private set; }
        public static Window Window { get; private set; }
        public static Dispatcher Dispatcher { get; private set; }
        public static event Action Completed;

        // Playnite's saved main window bounds (windowPositions.json, entry Main_V2). Empty when unknown.
        public static Rect ParseBounds(string json,out bool maximized)
        {
            maximized = false;
            try
            {
                var main = JObject.Parse(json)["Positions"]?["Main_V2"];
                if (main == null) return Rect.Empty;
                maximized = (int?)main["State"] == (int)WindowState.Maximized;
                double x = (double?)main["Position"]?["X"] ?? double.NaN, y = (double?)main["Position"]?["Y"] ?? double.NaN;
                double width = (double?)main["Size"]?["X"] ?? 0, height = (double?)main["Size"]?["Y"] ?? 0;
                if (double.IsNaN(x) || double.IsNaN(y) || width < 200 || height < 150) return Rect.Empty;
                return new Rect(x,y,width,height);
            }
            catch (Exception) { return Rect.Empty; }
        }

        public static Rect MainWindowBounds(string configurationPath,out bool maximized)
        {
            maximized = false;
            try
            {
                var file = string.IsNullOrEmpty(configurationPath) ? null : Path.Combine(configurationPath,"windowPositions.json");
                return file != null && File.Exists(file) ? ParseBounds(File.ReadAllText(file),out maximized) : Rect.Empty;
            }
            catch (Exception) { return Rect.Empty; }
        }

        // Keep the window on a visible screen; fall back to a centered 1280x720 area on the primary screen.
        public static Rect Visible(Rect bounds)
        {
            var screen = new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);
            if (!bounds.IsEmpty && bounds.IntersectsWith(screen)) return bounds;
            var work = SystemParameters.WorkArea; var width = Math.Min(1280,work.Width); var height = Math.Min(720,work.Height);
            return new Rect(work.Left + (work.Width-width)/2,work.Top + (work.Height-height)/2,width,height);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window,uint command);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window,IntPtr insertAfter,int x,int y,int width,int height,uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread,EnumWindowsProc callback,IntPtr parameter);
        private delegate bool EnumWindowsProc(IntPtr window,IntPtr parameter);
        private const uint GW_HWNDPREV = 3, SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_ASYNCWINDOWPOS = 0x4000;

        // Playnite's UI thread (Start is called from the plugin constructor on it) and the intro window's handle.
        private static uint playniteThread;
        private static IntPtr introHandle;
        // Whether the intro still had the focus when it ended; only then is Playnite's window activated.
        private static volatile bool hadFocusAtEnd;
        public static bool HadFocusAtEnd => hadFocusAtEnd;

        // Without "above other apps", the intro is a normal window kept just above Playnite's own windows: any Playnite
        // window that ends up above it (the main window appearing, a click on Playnite's taskbar button) is moved back
        // below it, without activating anything or changing its order relative to other apps. Runs on the intro thread;
        // SWP_ASYNCWINDOWPOS keeps it from waiting on Playnite's busy UI thread. Returns how many windows were moved.
        internal static int KeepAbovePlaynite(IntPtr intro,uint thread)
        {
            if (intro == IntPtr.Zero || thread == 0) return 0;
            var moved = 0;
            EnumThreadWindows(thread,(window,parameter) =>
            {
                if (window != intro && IsWindowVisible(window) && IsAbove(window,intro))
                {
                    SetWindowPos(window,intro,0,0,0,0,SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
                    moved++;
                }
                return true;
            },IntPtr.Zero);
            return moved;
        }

        // True when `window` is higher than `reference` in the z-order.
        internal static bool IsAbove(IntPtr window,IntPtr reference)
        {
            for (var current = GetWindow(reference,GW_HWNDPREV); current != IntPtr.Zero; current = GetWindow(current,GW_HWNDPREV))
                if (current == window) return true;
            return false;
        }

        // Returns false when the intro cannot start (already running, or nothing to show).
        public static bool Start(StartupIntroSettings settings,Rect bounds,bool maximized)
        {
            if (running || settings == null) return false;
            playniteThread = GetCurrentThreadId(); hadFocusAtEnd = true;
            var copy = Models.DashboardProfiles.Clone(settings); copy.Enabled = true;
            if (copy.ResolveMedia() == null && copy.Length == IntroLength.UntilMediaEnds) return false;
            PlayniteReady = false; DashboardReady = false; running = true; covering = true;
            var area = Visible(bounds);
            var thread = new Thread(() => Run(copy,area,maximized)) { IsBackground = true, Name = "StartPage launch intro" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return true;
        }

        public static void Skip() { var dispatcher = Dispatcher; var presenter = Presenter; if (dispatcher != null && presenter != null) dispatcher.BeginInvoke(new Action(presenter.Skip)); }

        private static void Run(StartupIntroSettings options,Rect area,bool maximized)
        {
            try
            {
                Dispatcher = Dispatcher.CurrentDispatcher;
                var window = new Window
                {
                    Title = "Playnite", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true,
                    // Above every app only when asked; otherwise a normal window kept above Playnite's own windows.
                    Background = Brushes.Transparent, Topmost = options.LaunchAboveOtherApps, ShowInTaskbar = false, ShowActivated = true,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = area.Left, Top = area.Top, Width = area.Width, Height = area.Height
                };
                var presenter = new IntroPresenter();
                window.Content = presenter; Window = window; Presenter = presenter;
                DispatcherTimer order = null;
                window.SourceInitialized += (sender,args) =>
                {
                    introHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    if (options.LaunchAboveOtherApps) return;
                    order = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(30) };
                    order.Tick += (s,a) => KeepAbovePlaynite(introHandle,playniteThread);
                    order.Start();
                };
                window.Closing += (sender,args) =>
                {
                    order?.Stop();
                    // If the user switched to another app while Playnite loaded, leave it in front.
                    hadFocusAtEnd = options.LaunchAboveOtherApps || GetForegroundWindow() == introHandle;
                };
                window.PreviewKeyDown += (sender,args) => { if (options.SkipOnInput) presenter.Skip(); };
                var started = false;
                window.ContentRendered += async (sender,args) =>
                {
                    if (started) return; started = true;
                    try
                    {
                        var played = await presenter.PlayAsync(options,() => PlayniteReady && DashboardReady,() => PlayniteReady);
                        var line = $"Launch intro: played={played}; media={presenter.PlayedMedia ?? "(none)"}; elapsed={presenter.Elapsed.TotalMilliseconds:0} ms; skipped={presenter.WasSkipped}; playniteReady={PlayniteReady}.";
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() => ExtensionDiagnostics.Record(LandingPageExtension.Instance,line)));
                    }
                    catch (Exception ex) { LandingPageExtension.logger?.Warn(ex,"Launch intro playback failed."); }
                    finally { window.Close(); }
                };
                window.Closed += (sender,args) => { Finish(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); };
                if (maximized) window.WindowState = WindowState.Maximized;
                window.Show();
                Dispatcher.Run();
            }
            catch (Exception ex) { LandingPageExtension.logger?.Warn(ex,"Launch intro could not be shown; Playnite starts normally."); Finish(); }
        }

        private static void Finish()
        {
            if (!running) return;
            covering = false; running = false; Presenter = null; Window = null; Dispatcher = null; introHandle = IntPtr.Zero;
            // Give Playnite's main window the focus the intro window held, unless the user had switched to another app.
            if (hadFocusAtEnd)
                try { Application.Current?.Dispatcher.BeginInvoke(new Action(() => { var main = IntroHost.MainWindow(); if (main != null && main.IsVisible) main.Activate(); })); } catch (Exception) { }
            try { Completed?.Invoke(); } catch (Exception) { }
        }
    }
}
