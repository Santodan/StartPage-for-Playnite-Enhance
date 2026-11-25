using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LandingPage.Utils
{
    // Resizes Playnite's window to a size given in physical pixels (display scaling aware), restoring it from
    // maximized/minimized first, keeping it centered where it was and inside the monitor's work area.
    public static class WindowSizer
    {
        // Device pixels per WPF unit for the window (1.0 at 100% scaling, 1.5 at 150%).
        public static Vector Scale(Visual visual)
        {
            var source = visual == null ? null : PresentationSource.FromVisual(visual);
            var matrix = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            return new Vector(matrix.M11 <= 0 ? 1 : matrix.M11,matrix.M22 <= 0 ? 1 : matrix.M22);
        }

        // The window's current size in physical pixels.
        public static Size CurrentPixels(Window window)
        {
            if (window == null) return Size.Empty;
            var scale = Scale(window);
            return new Size(Math.Round(window.ActualWidth * scale.X),Math.Round(window.ActualHeight * scale.Y));
        }

        // New bounds (WPF units) for a window currently at `current`: the wanted size, shrunk to fit `work`,
        // centered on the old center and moved fully inside `work`.
        public static Rect Fit(Rect current,Size wanted,Rect work)
        {
            var width = Math.Min(wanted.Width,work.Width); var height = Math.Min(wanted.Height,work.Height);
            var centerX = current.IsEmpty ? work.Left + work.Width/2 : current.Left + current.Width/2;
            var centerY = current.IsEmpty ? work.Top + work.Height/2 : current.Top + current.Height/2;
            var left = Math.Max(work.Left,Math.Min(work.Right - width,centerX - width/2));
            var top = Math.Max(work.Top,Math.Min(work.Bottom - height,centerY - height/2));
            return new Rect(left,top,width,height);
        }

        // Applies the size; returns the bounds used (WPF units) and whether it had to shrink to fit the screen.
        public static Rect Apply(Window window,int widthPixels,int heightPixels,out bool shrunk)
        {
            shrunk = false;
            if (window == null) return Rect.Empty;
            if (window.WindowState != WindowState.Normal) window.WindowState = WindowState.Normal;
            var scale = Scale(window);
            var wanted = new Size(widthPixels / scale.X,heightPixels / scale.Y);
            var work = WorkArea(window,scale);
            var bounds = Fit(new Rect(window.Left,window.Top,window.ActualWidth > 0 ? window.ActualWidth : window.Width,window.ActualHeight > 0 ? window.ActualHeight : window.Height),wanted,work);
            shrunk = bounds.Width + 0.5 < wanted.Width || bounds.Height + 0.5 < wanted.Height;
            window.Left = bounds.Left; window.Top = bounds.Top; window.Width = bounds.Width; window.Height = bounds.Height;
            return bounds;
        }

        // Work area (excluding the taskbar) of the monitor showing the window, in WPF units.
        public static Rect WorkArea(Window window,Vector scale)
        {
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                var monitor = handle == IntPtr.Zero ? IntPtr.Zero : MonitorFromWindow(handle,2); // MONITOR_DEFAULTTONEAREST
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor,ref info))
                    return new Rect(info.Work.Left / scale.X,info.Work.Top / scale.Y,(info.Work.Right - info.Work.Left) / scale.X,(info.Work.Bottom - info.Work.Top) / scale.Y);
            }
            catch (Exception) { }
            return SystemParameters.WorkArea;
        }

        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr handle,uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    }
}
