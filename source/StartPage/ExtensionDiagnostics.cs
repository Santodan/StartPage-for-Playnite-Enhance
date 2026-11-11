using System;

namespace LandingPage
{
    internal static class ExtensionDiagnostics
    {
        private static readonly object gate = new object();
        internal static string GetPath(LandingPageExtension plugin) => System.IO.Path.Combine(
            plugin.PlayniteApi.Paths.ExtensionsDataPath, plugin.Id.ToString(), "diagnostics", "startpage-debug.log");
        internal static void Record(LandingPageExtension plugin, string message)
        {
            if (plugin?.SettingsViewModel?.Settings?.EnableDashboardDiagnostics != true) return;
            try
            {
                lock (gate)
                {
                    var path = GetPath(plugin);
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 4 * 1024 * 1024)
                    {
                        var previous = path + ".previous";
                        if (System.IO.File.Exists(previous)) System.IO.File.Delete(previous);
                        System.IO.File.Move(path, previous);
                    }
                    System.IO.File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + " | " + message + Environment.NewLine);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("StartPage debug log write failed: " + ex.Message); }
        }
    }

    internal sealed class ExtensionDebugLogger : Playnite.SDK.ILogger
    {
        private readonly Playnite.SDK.ILogger inner;
        internal ExtensionDebugLogger(Playnite.SDK.ILogger inner) { this.inner = inner; }
        private void Write(string level, Exception error, string message)
        {
            ExtensionDiagnostics.Record(LandingPageExtension.Instance, level + " | " + message
                + (error == null ? string.Empty : Environment.NewLine + error));
        }
        public void Info(string message) { inner.Info(message); Write("INFO", null, message); }
        public void Info(Exception error, string message) { inner.Info(error, message); Write("INFO", error, message); }
        public void Debug(string message) { inner.Debug(message); Write("DEBUG", null, message); }
        public void Debug(Exception error, string message) { inner.Debug(error, message); Write("DEBUG", error, message); }
        public void Warn(string message) { inner.Warn(message); Write("WARN", null, message); }
        public void Warn(Exception error, string message) { inner.Warn(error, message); Write("WARN", error, message); }
        public void Error(string message) { inner.Error(message); Write("ERROR", null, message); }
        public void Error(Exception error, string message) { inner.Error(error, message); Write("ERROR", error, message); }
        public void Trace(string message) { inner.Trace(message); Write("TRACE", null, message); }
        public void Trace(Exception error, string message) { inner.Trace(error, message); Write("TRACE", error, message); }
    }
}
