using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LandingPage.Controls;
using LandingPage.Settings;
namespace LandingPage.Views.Settings
{
    public partial class DividerWidgetSettingsView : UserControl
    {
        private DividerConnectionLayer connectionLayer;
        public DividerWidgetSettingsView()
        {
            InitializeComponent();
            Loaded += (s,e) => Attach(); Unloaded += (s,e) => Detach();
            DataContextChanged += (s,e) => { if(IsLoaded) Attach(); };
        }
        private int Endpoint => int.TryParse((MatchConnectionEnd.SelectedItem as ComboBoxItem)?.Tag?.ToString(),out var value) ? value : 0;
        private void Detach() { if(connectionLayer != null) connectionLayer.ConnectionsChanged -= ConnectionsChanged; connectionLayer = null; }
        private void Attach()
        {
            Detach(); DependencyObject parent = this;
            while(parent != null && !(parent is StartPageView)) parent = VisualTreeHelper.GetParent(parent);
            connectionLayer = (parent as StartPageView)?.FindName("DividerConnections") as DividerConnectionLayer;
            if(connectionLayer != null) connectionLayer.ConnectionsChanged += ConnectionsChanged;
            UpdateMatch();
        }
        private void ConnectionsChanged(object sender,EventArgs e) => UpdateMatch();
        private void MatchEnd_Changed(object sender,SelectionChangedEventArgs e) { if(MatchAppearanceButton != null) UpdateMatch(); }
        private void UpdateMatch()
        {
            var target = connectionLayer?.AppearanceTarget(DataContext as DividerWidgetSettings,Endpoint);
            MatchAppearanceButton.IsEnabled = target != null;
            MatchAppearanceButton.ToolTip = target == null ? "Enable connections and place another divider within range of the selected end."
                : "Copy type, stroke, thickness, color, opacity and pattern settings once. Orientation, placement, spacing and connection settings are kept.";
        }
        private void MatchAppearance_Click(object sender,RoutedEventArgs e)
        {
            if(!(DataContext is DividerWidgetSettings source)) return;
            var target = connectionLayer?.AppearanceTarget(source,Endpoint);
            if(target != null) source.CopyAppearanceFrom(target);
        }
    }
}
