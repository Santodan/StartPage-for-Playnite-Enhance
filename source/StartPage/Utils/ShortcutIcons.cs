using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LandingPage.Utils
{
    public static class ShortcutIcons
    {
        [StructLayout(LayoutKind.Sequential,CharSet = CharSet.Unicode)]
        private struct StockIconInfo
        {
            public uint Size;
            public IntPtr Icon;
            public int SystemIndex;
            public int IconIndex;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst = 260)] public string Path;
        }
        [DllImport("shell32.dll",CharSet = CharSet.Unicode)] private static extern int SHGetStockIconInfo(uint id,uint flags,ref StockIconInfo info);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
        private static readonly Lazy<ImageSource> folder = new Lazy<ImageSource>(() => Stock(3));
        private static readonly Lazy<ImageSource> website = new Lazy<ImageSource>(() => Stock(13));
        public static ImageSource Folder => folder.Value;
        public static ImageSource Website => website.Value;
        private static ImageSource Stock(uint id)
        {
            var info = new StockIconInfo { Size = (uint)Marshal.SizeOf(typeof(StockIconInfo)) };
            try {
                if(SHGetStockIconInfo(id,0x100,ref info) != 0 || info.Icon == IntPtr.Zero) return null;
                var source = Imaging.CreateBitmapSourceFromHIcon(info.Icon,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
                source.Freeze(); return source;
            } catch(Exception ex) when(ex is ExternalException || ex is ArgumentException) { return null; }
            finally { if(info.Icon != IntPtr.Zero) DestroyIcon(info.Icon); }
        }
    }
}
