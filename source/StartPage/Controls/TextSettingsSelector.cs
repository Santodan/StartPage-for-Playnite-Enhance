using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace LandingPage.Controls
{
    public sealed class TextSettingsEntry : ContentControl
    {
        public string Header { get; set; }
        internal TextSettingsSelector Owner;
        private DependencyPropertyDescriptor visibility;
        private UIElement watched;
        public static readonly DependencyProperty IsAvailableProperty = DependencyProperty.Register(nameof(IsAvailable),typeof(bool),typeof(TextSettingsEntry),new PropertyMetadata(true,(sender,e) => ((TextSettingsEntry)sender).Owner?.Refresh()));
        public bool IsAvailable { get => (bool)GetValue(IsAvailableProperty); set => SetValue(IsAvailableProperty,value); }
        internal bool Available => IsAvailable && (!(Content is UIElement element) || element.Visibility != Visibility.Collapsed);
        public TextSettingsEntry() { HorizontalContentAlignment = HorizontalAlignment.Stretch; Loaded += (s,e) => Watch(); Unloaded += (s,e) => Unwatch(); }
        protected override void OnContentChanged(object oldContent,object newContent) { Unwatch(); base.OnContentChanged(oldContent,newContent); if (IsLoaded) Watch(); Owner?.Refresh(); }
        private void Watch() { Unwatch(); watched = Content as UIElement; if (watched == null) return; visibility = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty,watched.GetType()); visibility.AddValueChanged(watched,VisibilityChanged); }
        private void Unwatch() { if (watched != null && visibility != null) visibility.RemoveValueChanged(watched,VisibilityChanged); watched = null; visibility = null; }
        private void VisibilityChanged(object sender,EventArgs e) => Owner?.Refresh();
    }
    [ContentProperty(nameof(Entries))]
    public sealed class TextSettingsSelector : StackPanel
    {
        public ObservableCollection<TextSettingsEntry> Entries { get; } = new ObservableCollection<TextSettingsEntry>();
        public ComboBox ElementSelector { get; } = new ComboBox { DisplayMemberPath = "Header", Margin = new Thickness(0,0,0,6), ToolTip = "Choose the text element to edit" };
        private readonly TextBlock label = new TextBlock { Text = "Text element", Margin = new Thickness(0,4,0,2) };
        private readonly StackPanel fields = new StackPanel();
        private readonly Dictionary<TextSettingsEntry,Choice> choicesByEntry = new Dictionary<TextSettingsEntry,Choice>();
        public TextSettingsSelector() {
            label.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); Children.Add(label); Children.Add(ElementSelector); Children.Add(fields);
            ElementSelector.SelectionChanged += (s,e) => ShowSelection(); Entries.CollectionChanged += Changed; Loaded += (s,e) => Refresh();
        }
        public TextSettingsEntry AddEntry(string header,FrameworkElement content) { var entry = new TextSettingsEntry { Header = header, Content = content }; Entries.Add(entry); return entry; }
        private void Changed(object sender,NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Reset) { foreach (var old in fields.Children.OfType<TextSettingsEntry>()) old.Owner = null; fields.Children.Clear(); choicesByEntry.Clear(); }
            if (e.OldItems != null) foreach (TextSettingsEntry entry in e.OldItems) { entry.Owner = null; fields.Children.Remove(entry); choicesByEntry.Remove(entry); }
            if (e.NewItems != null) foreach (TextSettingsEntry entry in e.NewItems) { entry.Owner = this; fields.Children.Add(entry); }
            Refresh();
        }
        public sealed class Choice
        {
            public string Header { get; set; }
            public TextSettingsEntry Entry { get; set; }
            public override string ToString() => Header ?? string.Empty;
        }
        internal void Refresh() {
            var selected = (ElementSelector.SelectedItem as Choice)?.Entry; var available = Entries.Where(entry => entry.Available).ToArray();
            var choices = available.Select(entry => {
                if (!choicesByEntry.TryGetValue(entry,out var choice)) choicesByEntry[entry] = choice = new Choice { Entry = entry };
                choice.Header = entry.Header; return choice;
            }).ToArray();
            ElementSelector.ItemsSource = choices; ElementSelector.SelectedItem = choices.FirstOrDefault(choice => ReferenceEquals(choice.Entry,selected)) ?? choices.FirstOrDefault();
            label.Visibility = ElementSelector.Visibility = available.Length > 1 ? Visibility.Visible : Visibility.Collapsed; ShowSelection();
        }
        private void ShowSelection() { foreach (var entry in Entries) entry.Visibility = ReferenceEquals(entry,(ElementSelector.SelectedItem as Choice)?.Entry) ? Visibility.Visible : Visibility.Collapsed; }
    }
}
