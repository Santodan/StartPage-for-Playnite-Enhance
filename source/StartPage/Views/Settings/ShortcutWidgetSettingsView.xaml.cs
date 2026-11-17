using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LandingPage.Settings;
using Playnite.SDK;

namespace LandingPage.Views.Settings
{
    public partial class ShortcutWidgetSettingsView : UserControl
    {
        private ShortcutWidgetSettings observed;
        private System.Collections.ObjectModel.ObservableCollection<ShortcutEntry> items;
        private ShortcutEntry selected;
        private ShortcutEntry Entry => EntrySelector.SelectedItem as ShortcutEntry;
        public ShortcutWidgetSettingsView()
        {
            InitializeComponent(); KindSelector.ItemsSource = Enum.GetValues(typeof(ShortcutKind)); LayoutSelector.ItemsSource = Enum.GetValues(typeof(ShortcutLayout));
            DataContextChanged += (s,e) => Observe(); Loaded += (s,e) => Observe(); Unloaded += (s,e) => Detach(); UpdateFields();
        }
        private void Detach()
        {
            if(observed != null) observed.PropertyChanged -= SettingsChanged;
            if(items != null) items.CollectionChanged -= ItemsChanged;
            if(selected != null) selected.PropertyChanged -= EntryChanged;
            observed = null; items = null; selected = null;
        }
        private void Observe()
        {
            Detach(); if(!IsLoaded) return; observed = DataContext as ShortcutWidgetSettings;
            if(observed == null) return;
            observed.PropertyChanged += SettingsChanged; items = observed.Items; items.CollectionChanged += ItemsChanged;
            if(EntrySelector.SelectedIndex < 0 && items.Count > 0) EntrySelector.SelectedIndex = 0;
            WatchSelected(); UpdateFields();
        }
        private void SettingsChanged(object sender,PropertyChangedEventArgs e) { if(e.PropertyName == nameof(ShortcutWidgetSettings.Items)) Observe(); }
        private void ItemsChanged(object sender,NotifyCollectionChangedEventArgs e) { if(EntrySelector.SelectedIndex < 0 && items.Count > 0) EntrySelector.SelectedIndex = 0; UpdateFields(); }
        private void WatchSelected() { if(selected != null) selected.PropertyChanged -= EntryChanged; selected = Entry; if(selected != null) selected.PropertyChanged += EntryChanged; }
        private void EntryChanged(object sender,PropertyChangedEventArgs e) { if(e.PropertyName == nameof(ShortcutEntry.Kind)) UpdateFields(); }
        private void Selection_Changed(object sender,SelectionChangedEventArgs e) { if(EntryEditor == null) return; WatchSelected(); UpdateFields(); }
        private void UpdateFields()
        {
            if(EntryEditor == null) return;
            var entry = Entry; EntryEditor.Visibility = entry == null ? Visibility.Collapsed : Visibility.Visible;
            DuplicateButton.IsEnabled = RemoveButton.IsEnabled = entry != null;
            UpButton.IsEnabled = entry != null && EntrySelector.SelectedIndex > 0;
            DownButton.IsEnabled = entry != null && DataContext is ShortcutWidgetSettings options && EntrySelector.SelectedIndex < options.Items.Count-1;
            GameFields.Visibility = entry?.Kind == ShortcutKind.Game ? Visibility.Visible : Visibility.Collapsed;
            TargetFields.Visibility = entry != null && entry.Kind != ShortcutKind.Game ? Visibility.Visible : Visibility.Collapsed;
            ApplicationFields.Visibility = entry?.Kind == ShortcutKind.Application ? Visibility.Visible : Visibility.Collapsed;
            BrowseTargetButton.Visibility = entry?.Kind == ShortcutKind.Website ? Visibility.Collapsed : Visibility.Visible;
        }
        private void Add_Click(object sender,RoutedEventArgs e)
        {
            if(!(DataContext is ShortcutWidgetSettings options)) return;
            var entry = new ShortcutEntry { Label = "" }; options.Items.Add(entry); EntrySelector.SelectedItem = entry;
        }
        private void Duplicate_Click(object sender,RoutedEventArgs e)
        {
            if(!(DataContext is ShortcutWidgetSettings options) || Entry == null) return;
            var entry = Models.DashboardProfiles.Clone(Entry); entry.Id = Guid.NewGuid();
            options.Items.Insert(options.Items.IndexOf(Entry)+1,entry); EntrySelector.SelectedItem = entry;
        }
        private void Remove_Click(object sender,RoutedEventArgs e)
        {
            if(!(DataContext is ShortcutWidgetSettings options) || Entry == null) return;
            var index = options.Items.IndexOf(Entry); options.Items.RemoveAt(index);
            EntrySelector.SelectedIndex = options.Items.Count == 0 ? -1 : Math.Min(index,options.Items.Count-1);
        }
        private void Move(int delta) { if(!(DataContext is ShortcutWidgetSettings options) || Entry == null) return; var index = options.Items.IndexOf(Entry); var target = index+delta; if(target >= 0 && target < options.Items.Count) { var entry = Entry; options.Items.Move(index,target); EntrySelector.SelectedItem = entry; UpdateFields(); } }
        private void Up_Click(object sender,RoutedEventArgs e) => Move(-1);
        private void Down_Click(object sender,RoutedEventArgs e) => Move(1);
        private sealed class GameChoice : GenericItemOption { public Guid GameId { get; set; } }
        private void ChooseGame_Click(object sender,RoutedEventArgs e)
        {
            var entry = Entry; if(entry == null) return;
            var api = LandingPageExtension.Instance.PlayniteApi;
            Func<string,List<GenericItemOption>> search = text => api.Database.Games.Where(game => (game.Name ?? "").IndexOf(text ?? "",StringComparison.CurrentCultureIgnoreCase)>=0).OrderBy(game => game.Name).Take(100).Select(game => (GenericItemOption)new GameChoice { Name = game.Name,GameId = game.Id }).ToList();
            var choice = api.Dialogs.ChooseItemWithSearch(search(""),search,"Choose shortcut game","Search your library") as GameChoice;
            if(choice != null) { entry.GameId = choice.GameId; entry.Target = choice.Name; }
        }
        private void BrowseTarget_Click(object sender,RoutedEventArgs e)
        {
            var entry = Entry; if(entry == null) return;
            var dialogs = LandingPageExtension.Instance.PlayniteApi.Dialogs;
            var path = entry.Kind == ShortcutKind.Folder ? dialogs.SelectFolder() : dialogs.SelectFile("Applications|*.exe;*.com;*.bat;*.cmd;*.lnk");
            if(!string.IsNullOrWhiteSpace(path)) entry.Target = path;
        }
        private void WorkingDirectory_Click(object sender,RoutedEventArgs e) { if(Entry == null) return; var path = LandingPageExtension.Instance.PlayniteApi.Dialogs.SelectFolder(); if(!string.IsNullOrWhiteSpace(path)) Entry.WorkingDirectory = path; }
        private void Icon_Click(object sender,RoutedEventArgs e) { if(Entry == null) return; var path = LandingPageExtension.Instance.PlayniteApi.Dialogs.SelectFile("Images|*.png;*.jpg;*.jpeg;*.bmp;*.ico"); if(!string.IsNullOrWhiteSpace(path)) Entry.IconPath = path; }
        private void ClearIcon_Click(object sender,RoutedEventArgs e) { if(Entry != null) Entry.IconPath = ""; }
    }
}
