using System;
using System.IO;
namespace LandingPage.Utils
{
    public static class BackgroundMediaFiles
    {
        public const string Filter = "Background images and videos|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.mp4;*.m4v;*.wmv;*.avi;*.mov;*.mkv;*.webm|All files|*.*";
        public static bool IsGif(Uri uri) => Extension(uri) == ".gif";
        public static bool IsVideo(Uri uri) { var extension = Extension(uri); return extension == ".mp4" || extension == ".m4v" || extension == ".wmv" || extension == ".avi" || extension == ".mov" || extension == ".mkv" || extension == ".webm"; }
        public static bool IsAnimated(Uri uri) => IsGif(uri) || IsVideo(uri);
        private static string Extension(Uri uri) { try { return uri == null ? "" : Path.GetExtension(uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString).ToLowerInvariant(); } catch(ArgumentException) { return ""; } }
        public static bool IsSupportedFile(string path) {
            if(!Uri.TryCreate(path,UriKind.RelativeOrAbsolute,out var uri)) return false;
            var extension = Extension(uri);
            return IsAnimated(uri) || extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp" || extension == ".tif" || extension == ".tiff" || extension == ".webp";
        }
    }
}
