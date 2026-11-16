using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
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
        public static readonly DependencyProperty SelectedGroupProperty = DependencyProperty.Register(nameof(SelectedGroup),
            typeof(ToolbarGroup), typeof(ToolbarSettingsView), new PropertyMetadata(null, (sender,args) => ((ToolbarSettingsView)sender).ItemsView?.Refresh()));
        public ToolbarGroup SelectedGroup { get => (ToolbarGroup)GetValue(SelectedGroupProperty); set => SetValue(SelectedGroupProperty,value); }
        public static readonly DependencyProperty ItemsViewProperty = DependencyProperty.Register(nameof(ItemsView), typeof(ICollectionView), typeof(ToolbarSettingsView));
        public ICollectionView ItemsView { get => (ICollectionView)GetValue(ItemsViewProperty); private set => SetValue(ItemsViewProperty,value); }
        private ToolbarWidgetSettings observedSettings;
        private ObservableCollection<ToolbarEntry> observedItems;
        private ObservableCollection<ToolbarGroup> observedGroups;
        private readonly HashSet<ToolbarEntry> observedEntries = new HashSet<ToolbarEntry>();
        private void DetachSettings()
        {
            if (observedSettings != null) observedSettings.PropertyChanged -= SettingsChanged;
            if (observedItems != null) observedItems.CollectionChanged -= ItemsChanged;
            if (observedGroups != null) observedGroups.CollectionChanged -= GroupsChanged;
            foreach (var entry in observedEntries) entry.PropertyChanged -= EntryChanged;
            observedEntries.Clear(); observedItems = null; observedGroups = null; observedSettings = null;
        }
        private void ObserveSettings()
        {
            DetachSettings();
            observedSettings = DataContext as ToolbarWidgetSettings;
            if (observedSettings == null) { ItemsView = null; SelectedGroup = null; return; }
            if (observedSettings.Groups.Count == 0) observedSettings.Groups.Add(new ToolbarGroup { Id = "main", Name = "Main" });
            observedSettings.PropertyChanged += SettingsChanged;
            observedItems = observedSettings.Items; observedGroups = observedSettings.Groups;
            observedItems.CollectionChanged += ItemsChanged; observedGroups.CollectionChanged += GroupsChanged;
            // A private view keeps the widget and other editors' collections unfiltered.
            var groups = observedGroups;
            ItemsView = new ListCollectionView(observedItems) { Filter = item => item is ToolbarEntry entry && SelectedGroup != null && EffectiveGroup(entry, groups) == SelectedGroup.Id };
            WatchEntries(); RefreshGroups();
        }
        private static string EffectiveGroup(ToolbarEntry entry, ObservableCollection<ToolbarGroup> groups) => groups.Any(group => group.Id == entry.GroupId)
            ? entry.GroupId : groups.FirstOrDefault()?.Id;
        private void WatchEntries()
        {
            foreach (var entry in observedEntries) entry.PropertyChanged -= EntryChanged;
            observedEntries.Clear();
            foreach (var entry in observedItems.Where(item => item != null))
                if (observedEntries.Add(entry)) entry.PropertyChanged += EntryChanged;
        }
        private void RefreshGroups()
        {
            SelectedGroup = observedGroups.FirstOrDefault(group => group.Id == SelectedGroup?.Id) ?? observedGroups.FirstOrDefault();
            ItemsView?.Refresh();
        }
        private void SettingsChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ToolbarWidgetSettings.Items) || args.PropertyName == nameof(ToolbarWidgetSettings.Groups)) ObserveSettings();
        }
        private void ItemsChanged(object sender, NotifyCollectionChangedEventArgs args) { WatchEntries(); }
        private void GroupsChanged(object sender, NotifyCollectionChangedEventArgs args) => RefreshGroups();
        private void EntryChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ToolbarEntry.GroupId)) ItemsView?.Refresh();
        }
        private void EntryGroupSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (sender is ComboBox box && (box.IsDropDownOpen || box.IsKeyboardFocusWithin)
                && box.DataContext is ToolbarEntry entry && box.SelectedItem is ToolbarGroup group) entry.GroupId = group.Id;
        }
        public ToolbarSettingsView()
        {
            InitializeComponent();
            Loaded += (sender, args) => { ObserveSettings(); RefreshOptions(); };
            Unloaded += (sender, args) => DetachSettings();
            DataContextChanged += (sender, args) => { ObserveSettings(); RefreshOptions(); };
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
            if (DataContext is ToolbarWidgetSettings settings)
            {
                var group = new ToolbarGroup { Name = "Group " + (settings.Groups.Count + 1) };
                settings.Groups.Add(group); SelectedGroup = group;
            }
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
                settings.Items.Add(new ToolbarEntry { Id = id, GroupId = SelectedGroup?.Id ?? settings.Groups.FirstOrDefault()?.Id ?? "main" });
        }
        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ToolbarWidgetSettings settings && (sender as FrameworkElement)?.DataContext is ToolbarEntry entry) settings.Items.Remove(entry);
        }
        private void Move(object sender, int delta)
        {
            if (!(DataContext is ToolbarWidgetSettings settings) || !((sender as FrameworkElement)?.DataContext is ToolbarEntry entry)) return;
            var groupItems = ItemsView?.Cast<ToolbarEntry>().ToList();
            if (groupItems == null) return;
            var index = groupItems.IndexOf(entry);
            var destination = index + delta;
            if (index >= 0 && destination >= 0 && destination < groupItems.Count)
                settings.Items.Move(settings.Items.IndexOf(entry), settings.Items.IndexOf(groupItems[destination]));
        }
        private void MoveEarlier_Click(object sender, RoutedEventArgs e) => Move(sender, -1);
        private void MoveLater_Click(object sender, RoutedEventArgs e) => Move(sender, 1);
        private void RefreshItems_Click(object sender, RoutedEventArgs e) { NativeToolbarCatalog.RefreshProviders(); RefreshOptions(); }
    }
}
