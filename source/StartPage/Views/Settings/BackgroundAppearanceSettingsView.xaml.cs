using System.Windows;
using System.Windows.Controls;
using LandingPage.Settings;
namespace LandingPage.Views.Settings
{
    public partial class BackgroundAppearanceSettingsView : UserControl
    {
        public BackgroundAppearanceSettingsView() { InitializeComponent(); }
        private void RestartMedia_Click(object sender,RoutedEventArgs e) { (DataContext as BackgroundAppearanceSettings)?.RestartMedia(); }
        private void Reset_Click(object sender,RoutedEventArgs e)
        {
            if(!(DataContext is BackgroundAppearanceSettings options)) return;
            var defaults = new BackgroundAppearanceSettings();
            foreach(var property in typeof(BackgroundAppearanceSettings).GetProperties())
                if(property.CanRead && property.CanWrite) property.SetValue(options,property.GetValue(defaults));
        }
    }
}
