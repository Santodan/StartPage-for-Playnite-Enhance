using System.Windows;
using System.Windows.Controls;
using LandingPage.Settings;
using LandingPage.Utils;

namespace LandingPage.Views.Settings
{
    // Editor for the GIF/video played during dashboard page transitions (Manage dashboard pages > Dashboard transitions).
    public partial class TransitionMediaSettingsView : UserControl
    {
        public TransitionMediaSettingsView() { InitializeComponent(); }

        private TransitionMediaSettings Options => DataContext as TransitionMediaSettings;

        private void BrowseFile_Click(object sender,RoutedEventArgs e)
        {
            if (Options == null) return;
            var path = LandingPageExtension.Instance?.PlayniteApi?.Dialogs.SelectFile(BackgroundMediaFiles.Filter);
            if (!string.IsNullOrEmpty(path)) Options.MediaPath = path;
        }

        private void BrowseFolder_Click(object sender,RoutedEventArgs e)
        {
            if (Options == null) return;
            var path = LandingPageExtension.Instance?.PlayniteApi?.Dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(path)) Options.FolderPath = path;
        }

        private void Reset_Click(object sender,RoutedEventArgs e) => Options?.ResetOptions();
    }
}
