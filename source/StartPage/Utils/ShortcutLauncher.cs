using System;
using System.Diagnostics;
using System.IO;
using LandingPage.Settings;

namespace LandingPage.Utils
{
    public static class ShortcutLauncher
    {
        public static bool TryCreateStartInfo(ShortcutEntry entry,out ProcessStartInfo info,out string error)
        {
            info = null; error = null;
            if(entry == null || !entry.Enabled) { error = "This shortcut is disabled."; return false; }
            if(entry.Kind == ShortcutKind.Game) { error = entry.GameId == Guid.Empty ? "Choose a game in shortcut settings." : null; return error == null; }
            var target = Environment.ExpandEnvironmentVariables(entry.Target ?? "").Trim();
            try {
                if(entry.Kind == ShortcutKind.Website) {
                    if(!Uri.TryCreate(target,UriKind.Absolute,out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) { error = "Enter a website starting with https:// or http://."; return false; }
                    info = new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }; return true;
                }
                if(!Path.IsPathRooted(target)) { error = "Choose an absolute local path."; return false; }
                if(entry.Kind == ShortcutKind.Folder) {
                    if(!Directory.Exists(target)) { error = "Folder not found."; return false; }
                    info = new ProcessStartInfo(target) { UseShellExecute = true }; return true;
                }
                if(entry.Kind != ShortcutKind.Application) { error = "Unknown shortcut type."; return false; }
                if(!File.Exists(target)) { error = "Application not found."; return false; }
                var extension = Path.GetExtension(target).ToLowerInvariant();
                if(extension != ".exe" && extension != ".com" && extension != ".bat" && extension != ".cmd" && extension != ".lnk") { error = "Choose an application or Windows shortcut file."; return false; }
                var directory = Environment.ExpandEnvironmentVariables(entry.WorkingDirectory ?? "").Trim();
                if(directory.Length == 0) directory = Path.GetDirectoryName(target);
                if(!Directory.Exists(directory)) { error = "Working directory not found."; return false; }
                info = new ProcessStartInfo(target) { UseShellExecute = true,Arguments = entry.Arguments ?? "",WorkingDirectory = directory }; return true;
            }
            catch(Exception ex) when(ex is ArgumentException || ex is NotSupportedException || ex is IOException || ex is System.Security.SecurityException) { error = "Invalid shortcut path."; return false; }
        }
    }
}
