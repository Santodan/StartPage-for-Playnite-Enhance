using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace LandingPage.Converters
{
    public class UriToBitmapImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Uri uri = null;

            if (value is Uri imageUri)
            {
                uri = imageUri;
            }
            else if (value is string uriString 
                && uriString.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)
                && Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out var imageUri2))
            {
                uri = imageUri2;
            } else if (value is string databasePath)
            {
                if (File.Exists(databasePath))
                {
                    uri = new Uri(databasePath);
                } else
                {
                    var localPath = LandingPageExtension.Instance.PlayniteApi.Database.GetFullFilePath(databasePath);
                    if (File.Exists(localPath))
                    {
                        uri = new Uri(localPath);
                    }
                }
            }

            if (uri is Uri)
            {
                try
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    if (parameter is bool resize && resize)
                    {
                        if (LandingPageExtension.Instance.Settings.DownScaleCovers)
                        {
                            bmp.DecodePixelWidth = (int)Math.Round(LandingPageExtension.Instance.Settings.MaxCoverWidth * 1.15);
                        }
                    }
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.DelayCreation;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    if (parameter is string maxHeightString && int.TryParse(maxHeightString, out var maxHeight))
                    {
                        bmp.DecodePixelHeight = maxHeight;
                    }
                    bmp.UriSource = uri;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch (Exception ex)
                {
                    LandingPageExtension.logger.Error(ex, $"Failed to load image at {uri.OriginalString}");
                }
            }
            
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class UriToBitmapImageMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 0)
            {
                return DependencyProperty.UnsetValue;
            }

            Uri uri = null;

            if (values[0] is Uri imageUri)
            {
                uri = imageUri;
            }
            else if (values[0] is string uriString
                && uriString.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)
                && Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out var imageUri2))
            {
                uri = imageUri2;
            }
            else if (values[0] is string databasePath)
            {
                if (File.Exists(databasePath))
                {
                    uri = new Uri(databasePath);
                }
                else
                {
                    var localPath = LandingPageExtension.Instance.PlayniteApi.Database.GetFullFilePath(databasePath);
                    if (File.Exists(localPath))
                    {
                        uri = new Uri(localPath);
                    }
                }
            }

            if (uri is Uri)
            {
                try
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    if (LandingPageExtension.Instance.Settings.DownScaleCovers)
                    {
                        if (values[1] is Settings.ShelvesSettings shelveSettings)
                        {
                            bmp.DecodePixelWidth = (int)Math.Round(shelveSettings.MaxCoverWidth * 1.15);
                        }
                    }
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.DelayCreation;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    if (parameter is string maxHeightString && int.TryParse(maxHeightString, out var maxHeight))
                    {
                        bmp.DecodePixelHeight = maxHeight;
                    }
                    bmp.UriSource = uri;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch (Exception ex)
                {
                    LandingPageExtension.logger.Error(ex, $"Failed to load image at {uri.OriginalString}");
                }
            }

            return DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

namespace LandingPage.Converters
{
    // Called by an asynchronous image binding. Keep a small, bounded set of frozen
    // local backgrounds so revisiting a page does not repeat image decoding.
    internal static class DashboardBackgroundCache
    {
        private sealed class Entry
        {
            public string Key;
            public BitmapImage Image;
            public long Bytes;
        }
        private static readonly object gate = new object();
        private static readonly LinkedList<Entry> images = new LinkedList<Entry>();
        private const long MaxBytes = 64L * 1024 * 1024;
        private static long bytes;
        public static object Load(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri || !uri.IsFile) return uri;
            try
            {
                var file = new FileInfo(uri.LocalPath);
                if (!file.Exists) return uri;
                var key = file.FullName + "|" + file.LastWriteTimeUtc.Ticks + "|" + file.Length;
                lock (gate)
                {
                    var found = images.First;
                    while (found != null)
                    {
                        if (found.Value.Key == key)
                        {
                            images.Remove(found); images.AddFirst(found);
                            return found.Value.Image;
                        }
                        found = found.Next;
                    }
                    var decodeWatch = System.Diagnostics.Stopwatch.StartNew();
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
                    bitmap.UriSource = uri;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    LandingPageExtension.logger?.Debug($"Dashboard background decoded: {bitmap.PixelWidth}x{bitmap.PixelHeight}, {decodeWatch.ElapsedMilliseconds} ms.");
                    var size = (long)bitmap.PixelWidth * bitmap.PixelHeight * Math.Max(4, (bitmap.Format.BitsPerPixel + 7) / 8);
                    if (size <= MaxBytes)
                    {
                        while (images.Count > 0 && (images.Count >= 8 || bytes + size > MaxBytes))
                        { bytes -= images.Last.Value.Bytes; images.RemoveLast(); }
                        images.AddFirst(new Entry { Key = key, Image = bitmap, Bytes = size }); bytes += size;
                    }
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                LandingPageExtension.logger?.Debug(ex, "Unable to cache dashboard background.");
                return uri;
            }
        }
    }
}
