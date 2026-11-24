using System.Windows;
using System.Windows.Controls;
using LandingPage.Settings;

namespace LandingPage.Views.Settings
{
    public partial class ImageWidgetSettingsView : UserControl
    {
        public ImageWidgetSettingsView() { InitializeComponent(); }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is ImageWidgetSettings settings)) return;
            var path = LandingPageExtension.Instance.PlayniteApi.Dialogs.SelectFile(
                "Images, GIFs and videos|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico;*.webp;*.mp4;*.m4v;*.wmv;*.avi;*.mov;*.mkv;*.webm|All files|*.*");
            if (!string.IsNullOrEmpty(path)) settings.ImagePath = path;
        }

        private void Restart_Click(object sender, RoutedEventArgs e) => (DataContext as ImageWidgetSettings)?.RestartMedia();

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ImageWidgetSettings settings) settings.ImagePath = string.Empty;
        }
    }
}
