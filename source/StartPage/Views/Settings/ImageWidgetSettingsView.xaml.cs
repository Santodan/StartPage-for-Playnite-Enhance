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
                "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico");
            if (!string.IsNullOrEmpty(path)) settings.ImagePath = path;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ImageWidgetSettings settings) settings.ImagePath = string.Empty;
        }
    }
}
