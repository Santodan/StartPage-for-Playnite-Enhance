using System;
using System.Linq;
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
            Loaded += (sender,args) => SetDefaultExpansion();
            DataContextChanged += (sender,args) => SetDefaultExpansion();
        }

        private void SetDefaultExpansion()
        {
            Dispatcher.BeginInvoke(new Action(() => {
                GeneralAppearanceSection.IsExpanded = !SettingsSections.Children.OfType<Expander>().Any(section => section != GeneralAppearanceSection && section.Visibility == Visibility.Visible);
            }),System.Windows.Threading.DispatcherPriority.Loaded);
        }
        public event RoutedEventHandler Done;

        private void Done_Click(object sender, RoutedEventArgs e) => Done?.Invoke(this, e);

        private void ResetLibraryStatistics_Click(object sender, RoutedEventArgs e)
        {
            if(DataContext is GridNodeViewModel model && model.GridNode.ViewProperties != null) model.GridNode.ViewProperties.LibraryStatisticsSettings = model.GridNode.ViewProperties.ViewId == "LibraryChart" ? LandingPage.Settings.LibraryStatisticsSettings.CreateChartSettings() : new LandingPage.Settings.LibraryStatisticsSettings();
        }

        private void ResetGameDetail_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GridNodeViewModel model && model.GridNode.ViewProperties != null)
                model.GridNode.ViewProperties.GameDetailSettings = Models.GameDetailWidgets.CreateSettings(model.GridNode.ViewProperties.ViewId);
        }
        private void ResetFeaturedGame_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GridNodeViewModel model && model.GridNode.ViewProperties != null)
                model.GridNode.ViewProperties.FeaturedGameSettings = new LandingPage.Settings.FeaturedGameSettings();
        }

        private void ResetToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GridNodeViewModel model && model.GridNode.ViewProperties != null)
                model.GridNode.ViewProperties.ToolbarSettings = new LandingPage.Settings.ToolbarWidgetSettings();
        }

        private void ResetImage_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GridNodeViewModel model && model.GridNode.ViewProperties != null)
                model.GridNode.ViewProperties.ImageSettings = new LandingPage.Settings.ImageWidgetSettings();
        }

        private void ResetText_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GridNodeViewModel model && model.GridNode.ViewProperties != null)
                model.GridNode.ViewProperties.TextSettings = new LandingPage.Settings.TextWidgetSettings();
        }

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
