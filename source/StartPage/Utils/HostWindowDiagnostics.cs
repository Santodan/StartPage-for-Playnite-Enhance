using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LandingPage.Utils
{
    // Diagnose host sizing and guard the observed normal-window startup enlargement.
    internal sealed class HostWindowDiagnostics : IDisposable
    {
        private readonly LandingPageExtension plugin;
        private readonly Window window;
        private readonly DispatcherTimer settled;
        private HwndSource source;
        private bool disposed, userSizing;
        private string pendingReason;
        private DateTime displayChangeUntil;
        private DateTime startupDeadline;
        private Rect startupBounds = Rect.Empty;
        private IntPtr startupMonitor;
        private bool startupGuardCancelled,restorePending,restoring;
        private double startupScaleX,startupScaleY;
        internal static bool ShouldRestoreStartupBounds(Rect saved,Rect current,Rect monitor,WindowState state,bool userInteracted,bool withinStartup)
        {
            if(userInteracted || !withinStartup || state != WindowState.Normal || saved.IsEmpty || current.IsEmpty || monitor.IsEmpty || saved.Width <= 0 || saved.Height <= 0 || monitor.Width <= 0 || monitor.Height <= 0) return false;
            var fillsMonitor = Math.Abs(current.Left-monitor.Left) <= 2 && Math.Abs(current.Top-monitor.Top) <= 2 && Math.Abs(current.Width-monitor.Width) <= 2 && Math.Abs(current.Height-monitor.Height) <= 2;
            var wasSmaller = saved.Width < monitor.Width-8 || saved.Height < monitor.Height-8;
            return fillsMonitor && wasSmaller;
        }
        private bool Enabled => plugin.SettingsViewModel?.Settings?.EnableDashboardDiagnostics == true;
        internal HostWindowDiagnostics(LandingPageExtension plugin,Window window)
        {
            this.plugin = plugin; this.window = window;
            settled = new DispatcherTimer(DispatcherPriority.Background,window.Dispatcher) { Interval = TimeSpan.FromMilliseconds(600) };
            settled.Tick += Settled;
            window.SourceInitialized += SourceInitialized;
            window.SizeChanged += SizeChanged; window.StateChanged += StateChanged; window.Closed += Closed;
            Attach(); Snapshot("attached");
        }
        private void SourceInitialized(object sender,EventArgs args) => Attach();
        private void Attach()
        {
            if(source != null || disposed) return;
            source = PresentationSource.FromVisual(window) as HwndSource;
            source?.AddHook(Message);
            if(source != null && window.WindowState == WindowState.Normal && window.ActualWidth > 0 && window.ActualHeight > 0)
            {
                startupBounds = new Rect(window.Left,window.Top,window.ActualWidth,window.ActualHeight);
                startupMonitor = MonitorFromWindow(source.Handle,2);
                var scale = source.CompositionTarget.TransformToDevice; startupScaleX = scale.M11; startupScaleY = scale.M22;
                startupDeadline = DateTime.UtcNow.AddSeconds(30);
            }
        }
        private IntPtr Message(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            // Never consume the message or change the suggested rectangle.
            if(disposed) return IntPtr.Zero;
            if(message == 0x0231 || message == 0x0201 || message == 0x0204 || message == 0x0207 || message == 0x00A1 || message == 0x0100 || message == 0x0104 || message == 0x02E0 || message == 0x007E || message == 0x0112) startupGuardCancelled = true;
            if(!Enabled) return IntPtr.Zero;
            try
            {
                switch(message)
                {
                    case 0x0231: userSizing = true; Snapshot("user move/resize started"); break;
                    case 0x0232: userSizing = false; Schedule("user move/resize finished"); break;
                    case 0x007E:
                        displayChangeUntil = DateTime.UtcNow.AddSeconds(4);
                        var size = unchecked((uint)lParam.ToInt64());
                        Snapshot("WM_DISPLAYCHANGE pixels="+(size & 0xffff)+"x"+(size >> 16)); Schedule("display change settled"); break;
                    case 0x02E0:
                        displayChangeUntil = DateTime.UtcNow.AddSeconds(4);
                        var dpi = unchecked((uint)wParam.ToInt64());
                        var suggested = Marshal.PtrToStructure<NativeRect>(lParam);
                        Snapshot("WM_DPICHANGED dpi="+(dpi & 0xffff)+"x"+(dpi >> 16)+", suggestedPixels="+suggested); Schedule("DPI change settled"); break;
                    case 0x0218: Snapshot("WM_POWERBROADCAST event=0x"+wParam.ToInt64().ToString("X")); Schedule("power event settled"); break;
                    case 0x001A: Schedule("system settings changed"); break;
                    case 0x0046:
                        if(!userSizing && DateTime.UtcNow < displayChangeUntil)
                        {
                            var position = Marshal.PtrToStructure<WindowPosition>(lParam);
                            if((position.Flags & 1) == 0) Snapshot("WM_WINDOWPOSCHANGING proposedPixels="+position.Width+"x"+position.Height+", flags=0x"+position.Flags.ToString("X"));
                        }
                        break;
                }
            }
            catch(Exception ex) { System.Diagnostics.Debug.WriteLine("StartPage window diagnostics: "+ex.Message); }
            return IntPtr.Zero;
        }
        private void SizeChanged(object sender,SizeChangedEventArgs args)
        {
            ScheduleStartupRestore();
            if(!Enabled) return;
            if(!settled.IsEnabled) ExtensionDiagnostics.RecordAsync(plugin,"Host window size changed: previousDip="+args.PreviousSize+", newDip="+args.NewSize+", userSizing="+userSizing+".");
            Schedule("resize settled");
        }
        private void ScheduleStartupRestore()
        {
            if(restoring || restorePending || disposed || startupGuardCancelled || DateTime.UtcNow > startupDeadline || plugin.SettingsViewModel?.Settings?.PreserveWindowedStartupSize != true) return;
            restorePending = true;
            window.Dispatcher.BeginInvoke(new Action(() => {
                restorePending = false;
                if(disposed || startupGuardCancelled || source == null || window.WindowState != WindowState.Normal || plugin.SettingsViewModel?.Settings?.PreserveWindowedStartupSize != true) return;
                var scale = source.CompositionTarget.TransformToDevice;
                if(Math.Abs(scale.M11-startupScaleX) > .001 || Math.Abs(scale.M22-startupScaleY) > .001 || MonitorFromWindow(source.Handle,2) != startupMonitor) return;
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if(!GetMonitorInfo(startupMonitor,ref info)) return;
                var monitor = new Rect(info.Bounds.Left/scale.M11,info.Bounds.Top/scale.M22,(info.Bounds.Right-info.Bounds.Left)/scale.M11,(info.Bounds.Bottom-info.Bounds.Top)/scale.M22);
                var current = new Rect(window.Left,window.Top,window.ActualWidth,window.ActualHeight);
                if(!ShouldRestoreStartupBounds(startupBounds,current,monitor,window.WindowState,startupGuardCancelled,DateTime.UtcNow <= startupDeadline)) return;
                startupGuardCancelled = true; restoring = true;
                try
                {
                    ExtensionDiagnostics.RecordAsync(plugin,"Startup window safeguard: restoring "+startupBounds+" after automatic normal-window enlargement to "+current+".");
                    window.SetCurrentValue(Window.WidthProperty,startupBounds.Width); window.SetCurrentValue(Window.HeightProperty,startupBounds.Height);
                    window.SetCurrentValue(Window.LeftProperty,startupBounds.Left); window.SetCurrentValue(Window.TopProperty,startupBounds.Top);
                }
                finally { restoring = false; }
            }),DispatcherPriority.Loaded);
        }
        private void StateChanged(object sender,EventArgs args) { if(window.WindowState != WindowState.Normal) startupGuardCancelled = true; if(Enabled) { Snapshot("window state changed"); Schedule("state change settled"); } }
        private void Schedule(string reason)
        {
            if(!Enabled || disposed) return;
            pendingReason = reason; settled.Stop(); settled.Start();
        }
        private void Settled(object sender,EventArgs args) { settled.Stop(); Snapshot(pendingReason); }
        private void Snapshot(string reason)
        {
            if(!Enabled || disposed) return;
            try
            {
                var transform = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
                var handle = source?.Handle ?? IntPtr.Zero;
                GetWindowRect(handle,out var rectangle);
                var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                GetMonitorInfo(MonitorFromWindow(handle,2),ref monitor);
                var settings = plugin.SettingsViewModel.Settings;
                ExtensionDiagnostics.RecordAsync(plugin,string.Format(CultureInfo.InvariantCulture,
                    "Host window snapshot: reason={0}; state={1}; boundsDip={2:0.##},{3:0.##} {4:0.##}x{5:0.##}; restoreDip={6}; nativePixels={7}; scale={8:0.###}x{9:0.###}; monitorPixels={10}; workPixels={11}; sizeToContent={12}; minDip={13}x{14}; maxDip={15}x{16}; desiredDip={17}; userSizing={18}; startPageLoaded={19}; fixedGrid={20}; gridHeight={21}.",
                    reason,window.WindowState,window.Left,window.Top,window.ActualWidth,window.ActualHeight,window.RestoreBounds,
                    rectangle,transform.M11,transform.M22,monitor.Bounds,monitor.Work,window.SizeToContent,window.MinWidth,window.MinHeight,
                    window.MaxWidth,window.MaxHeight,window.DesiredSize,userSizing,plugin.startPageView?.IsLoaded == true,settings.FixGridSize,settings.FixedGridHeight));
            }
            catch(Exception ex) { System.Diagnostics.Debug.WriteLine("StartPage window snapshot: "+ex.Message); }
        }
        private void Closed(object sender,EventArgs args) => Dispose();
        public void Dispose()
        {
            if(disposed) return; disposed = true; settled.Stop(); settled.Tick -= Settled;
            window.SourceInitialized -= SourceInitialized; window.SizeChanged -= SizeChanged; window.StateChanged -= StateChanged; window.Closed -= Closed;
            if(source != null && !source.IsDisposed) source.RemoveHook(Message); source = null;
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect
        {
            public int Left,Top,Right,Bottom;
            public override string ToString() => Left+","+Top+" "+(Right-Left)+"x"+(Bottom-Top);
        }
        [StructLayout(LayoutKind.Sequential)] private struct WindowPosition { public IntPtr Window,After; public int X,Y,Width,Height; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Bounds,Work; public uint Flags; }
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle,out NativeRect rectangle);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr handle,uint flags);
        [DllImport("user32.dll",CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    }
}
