using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LandingPage.Models;
using LandingPage.Settings;
using LandingPage.Views.Settings;

namespace LandingPage.Controls
{
    // Toolbar editor host for the Appearance panel: shared toolbar options on top, then the toolbar editor pointed at
    // the settings this toolbar shows (its own, or the shared toolbar's, so editing any following toolbar edits the shared one).
    public sealed class SharedToolbarEditor : StackPanel
    {
        public static readonly DependencyProperty OwnSettingsProperty = DependencyProperty.Register(nameof(OwnSettings),typeof(ToolbarWidgetSettings),typeof(SharedToolbarEditor),
            new PropertyMetadata(null,(sender,args) => ((SharedToolbarEditor)sender).OwnChanged(args.OldValue as ToolbarWidgetSettings)));
        public ToolbarWidgetSettings OwnSettings { get => (ToolbarWidgetSettings)GetValue(OwnSettingsProperty); set => SetValue(OwnSettingsProperty,value); }
        public static readonly DependencyProperty InstanceIdProperty = DependencyProperty.Register(nameof(InstanceId),typeof(Guid),typeof(SharedToolbarEditor),
            new PropertyMetadata(Guid.Empty,(sender,args) => ((SharedToolbarEditor)sender).Refresh()));
        public Guid InstanceId { get => (Guid)GetValue(InstanceIdProperty); set => SetValue(InstanceIdProperty,value); }
        // Settings source for tests; defaults to the extension's settings.
        public LandingPageSettings GlobalSettings { get => global ?? LandingPageExtension.Instance?.Settings; set { Watch(false); global = value; if (IsLoaded) Watch(true); Refresh(); } }

        public ToolbarSettingsView Editor { get; } = new ToolbarSettingsView();
        public SettingsHint Status { get; } = new SettingsHint();
        public Button ShareButton { get; } = new Button { Content = "Use this toolbar on every dashboard", Padding = new Thickness(10,4,10,4), Margin = new Thickness(0,6,6,0), HorizontalAlignment = HorizontalAlignment.Left };
        public Button StopButton { get; } = new Button { Content = "Stop sharing", Padding = new Thickness(10,4,10,4), Margin = new Thickness(0,6,6,0), HorizontalAlignment = HorizontalAlignment.Left };
        public CheckBox KeepOwnToggle { get; } = new CheckBox { Content = "Keep this dashboard's own toolbar", Margin = new Thickness(0,6,0,0) };
        private LandingPageSettings global, watched;

        public SharedToolbarEditor()
        {
            var header = new TextBlock { Text = "Shared toolbar", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,2) };
            header.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            KeepOwnToggle.SetResourceReference(Control.ForegroundProperty,"TextBrush");
            var buttons = new WrapPanel(); buttons.Children.Add(ShareButton); buttons.Children.Add(StopButton);
            Children.Add(header); Children.Add(Status); Children.Add(buttons); Children.Add(KeepOwnToggle);
            Children.Add(new Separator { Margin = new Thickness(0,10,0,6), Opacity = .4 });
            Children.Add(Editor);
            ShareButton.Click += (sender,args) => MakeSource();
            StopButton.Click += (sender,args) => { if (GlobalSettings != null) GlobalSettings.UseSharedToolbar = false; };
            KeepOwnToggle.Click += (sender,args) => { if (OwnSettings != null) OwnSettings.KeepOwnToolbar = KeepOwnToggle.IsChecked == true; };
            Loaded += (sender,args) => { Watch(true); Refresh(); };
            Unloaded += (sender,args) => Watch(false);
        }

        public void MakeSource()
        {
            var settings = GlobalSettings; if (settings == null || InstanceId == Guid.Empty) return;
            if (OwnSettings != null) OwnSettings.KeepOwnToolbar = false;
            settings.SharedToolbarId = InstanceId; settings.UseSharedToolbar = true;
        }

        private void Watch(bool watch)
        {
            if (watched != null) watched.PropertyChanged -= GlobalChanged;
            watched = watch ? GlobalSettings : null;
            if (watched != null) watched.PropertyChanged += GlobalChanged;
        }
        private void GlobalChanged(object sender,PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(LandingPageSettings.UseSharedToolbar) || args.PropertyName == nameof(LandingPageSettings.SharedToolbarId)) Refresh();
        }
        private void OwnChanged(ToolbarWidgetSettings previous)
        {
            if (previous != null) previous.PropertyChanged -= OwnPropertyChanged;
            if (OwnSettings != null) OwnSettings.PropertyChanged += OwnPropertyChanged;
            Refresh();
        }
        private void OwnPropertyChanged(object sender,PropertyChangedEventArgs args) { if (args.PropertyName == nameof(ToolbarWidgetSettings.KeepOwnToolbar)) Refresh(); }

        public void Refresh()
        {
            var settings = GlobalSettings; var own = OwnSettings;
            var source = SharedToolbar.FindSource(settings,out var sourcePage);
            var isSource = source != null && source.InstanceId == InstanceId;
            var follows = SharedToolbar.Follows(settings,own,InstanceId);
            var sharing = settings?.UseSharedToolbar == true;
            var pageName = sourcePage?.Name ?? "another dashboard";
            if (!sharing) Status.Text = "Each dashboard has its own toolbar. Share this one to show the same toolbar on every dashboard; you can then edit it from any of them.";
            else if (source == null) Status.Text = "The shared toolbar no longer exists, so every dashboard uses its own toolbar. Share this one to replace it.";
            else if (isSource) Status.Text = "This toolbar is shared with every dashboard. Changes here apply everywhere, except on dashboards that keep their own toolbar.";
            else if (follows) Status.Text = $"This dashboard shows the shared toolbar from \"{pageName}\". Changes below edit the shared toolbar on every dashboard.";
            else Status.Text = $"This dashboard keeps its own toolbar instead of the shared one from \"{pageName}\".";
            ShareButton.Content = sharing && source != null ? "Make this the shared toolbar" : "Use this toolbar on every dashboard";
            ShareButton.Visibility = isSource || InstanceId == Guid.Empty ? Visibility.Collapsed : Visibility.Visible;
            StopButton.Visibility = sharing ? Visibility.Visible : Visibility.Collapsed;
            KeepOwnToggle.Visibility = sharing && source != null && !isSource ? Visibility.Visible : Visibility.Collapsed;
            KeepOwnToggle.IsChecked = own?.KeepOwnToolbar == true;
            var edited = SharedToolbar.Effective(settings,own,InstanceId);
            if (!ReferenceEquals(Editor.DataContext,edited)) Editor.DataContext = edited;
        }
    }
}
