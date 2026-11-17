using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Playnite.SDK;

namespace LandingPage
{
    public partial class LandingPageExtension
    {
        private Utils.GitHubUpdateChecker updateChecker;
        private CancellationTokenSource updateCancellation;
        private Timer updateTimer;
        private int checkingUpdates;
        private string notifiedUpdate;
        private void StartUpdateChecks()
        {
            updateCancellation = new CancellationTokenSource();
            updateChecker = new Utils.GitHubUpdateChecker();
            updateTimer = new Timer(async state => await CheckForUpdatesAsync(false),null,TimeSpan.FromSeconds(30),TimeSpan.FromHours(6));
        }
        private void StopUpdateChecks()
        {
            updateTimer?.Dispose(); updateTimer = null;
            updateCancellation?.Cancel();
            // Let any in-flight request observe cancellation before disposing its client.
            updateChecker?.Dispose();
        }
        internal async Task<string> CheckForUpdatesAsync(bool manual)
        {
            if (!manual && SettingsViewModel?.Settings?.CheckForUpdatesAutomatically != true) return "Automatic update checks are disabled.";
            if (Interlocked.CompareExchange(ref checkingUpdates,1,0) != 0) return "An update check is already running.";
            try {
                var token = updateCancellation?.Token ?? CancellationToken.None;
                var checker = updateChecker;
                if(checker == null) return "Update checks are available after Playnite starts.";
                var installed = typeof(LandingPageExtension).Assembly.GetName().Version;
                logger.Debug("Checking StartPage GitHub releases; installed="+installed+"; includePrereleases="+(installed.Major<1));
                var release = await checker.CheckAsync(installed,token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (release == null) { logger.Info("StartPage update check: no newer packaged release."); return "No newer packaged release is available. Installed: "+installed.ToString(3)+"."; }
                var text = "StartPage Enhanced: "+release.Tag+(release.Prerelease ? " (prerelease)" : "")+" is available. Click to view the release and download its package.";
                await Application.Current.Dispatcher.InvokeAsync(() => {
                    if(token.IsCancellationRequested || (!manual && SettingsViewModel?.Settings?.CheckForUpdatesAutomatically != true)) return;
                    if(manual || notifiedUpdate != release.Tag) {
                        PlayniteApi.Notifications.Add(new NotificationMessage("santodan_StartPage_Enhanced_update",text,NotificationType.Info,() => {
                            try { Process.Start(new ProcessStartInfo(release.Url) { UseShellExecute = true }); }
                            catch(Exception error) { logger.Error(error,"Unable to open the StartPage release page."); }
                        }));
                        notifiedUpdate = release.Tag;
                    }
                });
                logger.Info("StartPage update available: "+release.Tag);
                return text;
            }
            catch(OperationCanceledException) { return "Update check was cancelled or timed out. Try again later."; }
            catch(Exception error) { logger.Warn(error,"StartPage GitHub update check failed."); return "Could not check GitHub for updates. Check your connection and try again later."; }
            finally { Interlocked.Exchange(ref checkingUpdates,0); }
        }
    }
}
