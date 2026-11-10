using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LandingPage.Controls;
using LandingPage.Settings;

namespace LandingPage.Views.Settings
{
    public partial class ToolbarSettingsView : UserControl
    {
        public ObservableCollection<ToolbarOption> AvailableOptions { get; } = new ObservableCollection<ToolbarOption>();
        public ToolbarSettingsView()
        {
            InitializeComponent();
            Loaded += (sender, args) => RefreshOptions();
            DataContextChanged += (sender, args) => RefreshOptions();
            RefreshOptions();
        }
        public void RefreshOptions()
        {
            var selected = OptionPicker.SelectedValue as string;
            var window = Window.GetWindow(this);
            var options = NativeToolbarCatalog.Discover(NativeToolbarCatalog.FindNativeTopPanel(window), window?.DataContext);
            var upgrades = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<ToolbarEntry, string>>();
            if (DataContext is ToolbarWidgetSettings settings)
                foreach (var entry in settings.Items.Where(entry => entry != null && !string.IsNullOrEmpty(entry.Id)))
                    if (!options.Any(option => option.Id == entry.Id))
                    {
                        var resolved = NativeToolbarCatalog.Resolve(options, entry.Id);
                        var previous = AvailableOptions.FirstOrDefault(item => item.Id == entry.Id);
                        if (resolved == null && previous?.NativeControl != null)
                            resolved = options.FirstOrDefault(item => ReferenceEquals(item.NativeControl, previous.NativeControl));
                        if (resolved?.Id.StartsWith("extension.") == true) upgrades.Add(new System.Collections.Generic.KeyValuePair<ToolbarEntry, string>(entry, resolved.Id));
                        options.Add(resolved == null ? new ToolbarOption { Id = entry.Id, Title = "Unavailable: " + entry.Id } : NativeToolbarCatalog.WithId(resolved, entry.Id));
                    }
            // Keep existing option objects while editors are bound. Clearing the list
            // would write null selections back into saved toolbar entries.
            foreach (var option in options)
            {
                var existing = AvailableOptions.FirstOrDefault(item => item.Id == option.Id);
                if (existing == null) AvailableOptions.Add(option);
                else
                {
                    if (!option.Title.StartsWith("Unavailable:")) existing.Title = option.Title;
                    if (option.NativeControl != null) existing.NativeControl = option.NativeControl;
                    if (option.IconTemplate != null) existing.IconTemplate = option.IconTemplate;
                    existing.IsNativeFont = option.IsNativeFont;
                    existing.Command = option.Command;
                    existing.CommandParameter = option.CommandParameter;
                    existing.NativeModel = option.NativeModel;
                    existing.SidebarItem = option.SidebarItem;
                }
            }
            // Upgrade old title-based selections only after their stable choices exist.
            foreach (var upgrade in upgrades) upgrade.Key.Id = upgrade.Value;
            OptionPicker.SelectedValue = selected ?? "page.select";
            (DataContext as ToolbarWidgetSettings)?.RequestNativeRefresh();
        }
        private void EntrySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox box && (box.IsDropDownOpen || box.IsKeyboardFocusWithin)
                && box.DataContext is ToolbarEntry entry && box.SelectedItem is ToolbarOption option) entry.Id = option.Id;
        }
        private void ResetGroupAppearance_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ToolbarGroup group) group.ResetAppearance();
        }
        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ToolbarWidgetSettings settings) settings.Groups.Add(new ToolbarGroup { Name = "Group " + (settings.Groups.Count + 1) });
        }
        private void RemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is ToolbarWidgetSettings settings) || settings.Groups.Count < 2 || !((sender as FrameworkElement)?.DataContext is ToolbarGroup group)) return;
            var destination = settings.Groups.First(item => item != group);
            foreach (var item in settings.Items.Where(item => item.GroupId == group.Id)) item.GroupId = destination.Id;
            settings.Groups.Remove(group);
        }
        private void MoveGroup(object sender, int delta)
        {
            if (!(DataContext is ToolbarWidgetSettings settings) || !((sender as FrameworkElement)?.DataContext is ToolbarGroup group)) return;
            var index = settings.Groups.IndexOf(group);
            if (index >= 0 && index + delta >= 0 && index + delta < settings.Groups.Count) settings.Groups.Move(index, index + delta);
        }
        private void MoveGroupEarlier_Click(object sender, RoutedEventArgs e) => MoveGroup(sender, -1);
        private void MoveGroupLater_Click(object sender, RoutedEventArgs e) => MoveGroup(sender, 1);
        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ToolbarWidgetSettings settings && OptionPicker.SelectedValue is string id)
                settings.Items.Add(new ToolbarEntry { Id = id, GroupId = settings.Groups.FirstOrDefault()?.Id ?? "main" });
        }
        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ToolbarWidgetSettings settings && (sender as FrameworkElement)?.DataContext is ToolbarEntry entry) settings.Items.Remove(entry);
        }
        private void Move(object sender, int delta)
        {
            if (!(DataContext is ToolbarWidgetSettings settings) || !((sender as FrameworkElement)?.DataContext is ToolbarEntry entry)) return;
            var index = settings.Items.IndexOf(entry);
            var destination = index + delta;
            if (index >= 0 && destination >= 0 && destination < settings.Items.Count) settings.Items.Move(index, destination);
        }
        private void MoveEarlier_Click(object sender, RoutedEventArgs e) => Move(sender, -1);
        private void MoveLater_Click(object sender, RoutedEventArgs e) => Move(sender, 1);
        private void RefreshItems_Click(object sender, RoutedEventArgs e) { NativeToolbarCatalog.RefreshProviders(); RefreshOptions(); }
    }
}
