using System.Windows;
using System.Windows.Controls;
using LandingPage.ViewModels.Layout;

namespace LandingPage.Views
{
    public partial class PanelAppearanceView : UserControl
    {
        public PanelAppearanceView()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler Done;

        private void Done_Click(object sender, RoutedEventArgs e) => Done?.Invoke(this, e);

        private void ResetClock_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GridNodeViewModel model && model.GridNode.ViewProperties != null)
                model.GridNode.ViewProperties.ClockSettings = new LandingPage.Settings.ClockSettings();
        }

        private void ResetAppearance_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GridNodeViewModel model && model.GridNode.ViewProperties != null)
            {
                model.GridNode.ViewProperties.Appearance = new Models.Layout.PanelAppearance();
            }
        }
    }
}
