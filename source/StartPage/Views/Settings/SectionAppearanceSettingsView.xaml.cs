using System.Windows;
using System.Windows.Controls;
using LandingPage.Models.Layout;
namespace LandingPage.Views.Settings
{
    public partial class SectionAppearanceSettingsView : UserControl
    {
        public SectionAppearanceSettingsView() { InitializeComponent(); }
        private void Reset_Click(object sender, RoutedEventArgs args)
        {
            if (!(DataContext is PanelAppearance target)) return;
            var defaults = new PanelAppearance();
            foreach (var property in typeof(PanelAppearance).GetProperties())
                if (property.CanRead && property.CanWrite) property.SetValue(target,property.GetValue(defaults));
        }
    }
}
