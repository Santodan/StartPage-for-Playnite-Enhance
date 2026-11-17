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
            Loaded += (sender,args) => { LoadProviderSettings(); SetDefaultExpansion(); };
            DataContextChanged += (sender,args) => { providerModel = null; ProviderSettingsHost.Content = null; LoadProviderSettings(); SetDefaultExpansion(); };
        }

        private GridNodeViewModel providerModel;
        private void LoadProviderSettings()
        {
            var model = DataContext as GridNodeViewModel;
            PanelLayoutSection.Visibility = model != null && (model.IndependentColumnHeightsCommand?.CanExecute(null) == true || model.IndependentRowWidthsCommand?.CanExecute(null) == true)
                ? Visibility.Visible : Visibility.Collapsed;
            if (model != null && ReferenceEquals(providerModel,model)) return;
            ProviderSettingsHost.Content = null;
            ProviderSettingsSection.Visibility = Visibility.Collapsed;
            providerModel = model;
            if (model?.GridNode.ViewProperties?.StartPageViewArgs?.HasSettings != true) return;
            try {
                var settings = model.ViewSettings;
                if (settings == null) return;
                ProviderSettingsHost.Content = settings;
                var id = model.GridNode.ViewProperties.ViewId;
                ProviderSettingsSection.Header = id == "GameShelves" ? "Shelf settings" : id == "MostPlayed" ? "Most Played settings" : "Widget-specific settings";
                ProviderSettingsSection.Visibility = Visibility.Visible;
            } catch (Exception ex) {
                LandingPageExtension.logger?.Warn(ex,"Unable to open widget-specific settings in the unified editor.");
                var message = new TextBlock { Text = "The widget provider could not open its settings. See the Playnite log for details.", TextWrapping = TextWrapping.Wrap };
                message.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
                ProviderSettingsHost.Content = message; ProviderSettingsSection.Visibility = Visibility.Visible;
            }
        }

        private void IndependentLayout_Click(object sender,RoutedEventArgs e)
        {
            // Let the button command finish reparenting before dismissing the editor.
            Dispatcher.BeginInvoke(new Action(() => Done?.Invoke(this,new RoutedEventArgs())),System.Windows.Threading.DispatcherPriority.Loaded);
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
