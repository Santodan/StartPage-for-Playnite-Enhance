using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using LandingPage.Controls;
using LandingPage.Settings;
using LandingPage.ViewModels;

namespace LandingPage.Views
{
    public class ToolbarWidgetView : UserControl
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(ToolbarWidgetSettings), typeof(ToolbarWidgetView),
            new PropertyMetadata(null, (sender, args) => ((ToolbarWidgetView)sender).AttachSettings()));
        public ToolbarWidgetSettings Settings { get => (ToolbarWidgetSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
        private ToolbarWidgetSettings observedSettings;
        private readonly List<ToolbarEntry> observedEntries = new List<ToolbarEntry>();
        private System.Collections.ObjectModel.ObservableCollection<ToolbarEntry> observedItems;
        private readonly List<ToolbarGroup> observedGroups = new List<ToolbarGroup>();
        private System.Collections.ObjectModel.ObservableCollection<ToolbarGroup> groupCollection;
        private ContextMenu openNativeMenu;
        private bool renderPending;
        private readonly HashSet<string> reportedMissing = new HashSet<string>();
        private readonly HashSet<string> unresolvedNativeItems = new HashSet<string>();
        private readonly DispatcherTimer nativeRetryTimer;

        public ToolbarWidgetView()
        {
            WindowChrome.SetIsHitTestVisibleInChrome(this, true);
            SetResourceReference(ForegroundProperty, "TextBrush");
            var textStyle = new Style(typeof(TextBlock), TryFindResource("BaseTextBlockStyle") as Style);
            textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextBrush")));
            Resources[typeof(TextBlock)] = textStyle;
            nativeRetryTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
            nativeRetryTimer.Tick += (sender, args) => RefreshPendingNative();
            Settings = new ToolbarWidgetSettings();
            SetBinding(SettingsProperty, new Binding("DataContext.GridNode.ViewProperties.ToolbarSettings")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(GridNodeView), 1),
                FallbackValue = Settings, TargetNullValue = Settings
            });
            Loaded += (sender, args) => { AttachSettings(); ScheduleRender(); nativeRetryTimer.Start(); };
            Unloaded += (sender, args) => { nativeRetryTimer.Stop(); DetachSettings(); if (openNativeMenu != null) openNativeMenu.IsOpen = false; };
        }

        private void DetachSettings()
        {
            if (observedSettings != null)
            {
                observedSettings.PropertyChanged -= SettingChanged;
                observedSettings.NativeRefreshRequested -= NativeRefreshRequested;
            }
            if (observedItems != null) observedItems.CollectionChanged -= ItemsChanged;
            foreach (var item in observedEntries) item.PropertyChanged -= SettingChanged;
            if (groupCollection != null) groupCollection.CollectionChanged -= ItemsChanged;
            foreach (var group in observedGroups) group.PropertyChanged -= SettingChanged;
            observedGroups.Clear(); groupCollection = null;
            observedEntries.Clear(); observedItems = null; observedSettings = null;
        }
        private void AttachSettings()
        {
            DetachSettings();
            observedSettings = Settings;
            if (observedSettings != null)
            {
                observedSettings.PropertyChanged += SettingChanged;
                observedSettings.NativeRefreshRequested += NativeRefreshRequested;
                groupCollection = observedSettings.Groups;
                groupCollection.CollectionChanged += ItemsChanged;
                foreach (var group in groupCollection) { group.PropertyChanged += SettingChanged; observedGroups.Add(group); }
                observedItems = observedSettings.Items;
                observedItems.CollectionChanged += ItemsChanged;
                foreach (var item in observedItems.Where(item => item != null)) { item.PropertyChanged += SettingChanged; observedEntries.Add(item); }
            }
            ScheduleRender();
        }
        private void NativeRefreshRequested(object sender, EventArgs args) => ScheduleRender();

        private void RefreshPendingNative()
        {
            if (unresolvedNativeItems.Count == 0) return;
            var window = Window.GetWindow(this);
            var options = NativeToolbarCatalog.Discover(NativeToolbarCatalog.FindNativeTopPanel(window), window?.DataContext);
            // Only replace the rendered controls when a missing source becomes ready;
            // do not interrupt an open menu or search field on every timer tick.
            if (unresolvedNativeItems.Any(id => NativeToolbarCatalog.Resolve(options, id)?.NativeControl != null)) ScheduleRender();
        }

        private void ItemsChanged(object sender, NotifyCollectionChangedEventArgs args) => AttachSettings();
        private void SettingChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ToolbarWidgetSettings.Items) || args.PropertyName == nameof(ToolbarWidgetSettings.Groups)) AttachSettings();
            else ScheduleRender();
        }
        private void ScheduleRender()
        {
            if (renderPending) return;
            renderPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                renderPending = false;
                try { RenderToolbar(); }
                catch (Exception ex)
                {
                    LandingPageExtension.logger.Warn(ex, "Unable to render toolbar widget.");
                    Content = new TextBlock { Text = "Toolbar unavailable. Reopen the page or check the Playnite log.", TextWrapping = TextWrapping.Wrap };
                }
            }));
        }
        private StartPageView Host()
        {
            DependencyObject current = this;
            while (current != null && !(current is StartPageView)) current = VisualTreeHelper.GetParent(current);
            return current as StartPageView;
        }

        internal void RenderToolbar()
        {
            if (Settings == null) return;
            if (openNativeMenu != null) openNativeMenu.IsOpen = false;
            var host = Host();
            var model = host?.DataContext as StartPageViewModel;
            var window = Window.GetWindow(this);
            var options = NativeToolbarCatalog.Discover(NativeToolbarCatalog.FindNativeTopPanel(window), window?.DataContext).ToDictionary(option => option.Id);
            unresolvedNativeItems.Clear();
            var groups = Settings.Groups.Count == 0 ? new[] { new ToolbarGroup { Id = "main", Name = "Main" } } : Settings.Groups.ToArray();
            var panels = groups.ToDictionary(group => group.Id, group => new WrapPanel { Orientation = Settings.Orientation,
                HorizontalAlignment = group.Alignment, VerticalAlignment = group.VerticalAlignment });
            var panel = panels[groups[0].Id];
            foreach (var entry in Settings.Items.Where(entry => entry != null))
            {
                var option = NativeToolbarCatalog.Resolve(options.Values, entry.Id);
                if (!string.IsNullOrEmpty(entry.Id) && (entry.Id.StartsWith("native.") || entry.Id.StartsWith("plugin.") || entry.Id.StartsWith("extension.") || entry.Id.StartsWith("sidebar."))
                    && option?.NativeControl == null) unresolvedNativeItems.Add(entry.Id);
                if (option == null && entry.Id?.StartsWith("extension.") == true && reportedMissing.Add(entry.Id))
                    LandingPageExtension.logger?.Debug("StartPage toolbar could not resolve " + entry.Id + "; discovered extension IDs: "
                        + string.Join(", ", options.Keys.Where(id => id.StartsWith("extension.") || id.StartsWith("plugin."))));
                option = option ?? new ToolbarOption { Id = entry.Id, Title = "Unavailable toolbar item", Glyph = "?" };
                var title = string.IsNullOrWhiteSpace(entry.Label) ? option.Title : entry.Label;
                FrameworkElement control;
                if (option.Kind == "separator")
                    control = new Border { Width = Settings.Orientation == Orientation.Horizontal ? 1 : double.NaN,
                        Height = Settings.Orientation == Orientation.Horizontal ? 24 : 1, Background = TryFindResource("TextBrush") as Brush ?? Brushes.Gray, Opacity = 0.4 };
                else if (option.Kind == "pages")
                {
                    var box = new ComboBox { MinWidth = 120, MaxWidth = 260, DisplayMemberPath = "Name", ToolTip = title, FontSize = Settings.FontSize };
                    if (model != null)
                    {
                        box.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(model.Pages)) { Source = model });
                        box.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(model.SelectedPage)) { Source = model, Mode = BindingMode.TwoWay });
                        box.SetBinding(IsEnabledProperty, new Binding(nameof(model.CanChangePages)) { Source = model });
                    }
                    else box.IsEnabled = false;
                    control = box;
                }
                else if (option.Kind == "search")
                {
                    var box = new TextBox { MinWidth = 160, MaxWidth = 300, ToolTip = title, FontSize = Settings.FontSize };
                    var source = option.NativeControl;
                    var property = source == null ? null : DependencyPropertyDescriptor.FromName("Text", source.GetType(), source.GetType())?.DependencyProperty;
                    var binding = property == null ? null : BindingOperations.GetBindingBase(source, property);
                    if (binding != null) box.SetBinding(TextBox.TextProperty, binding); else box.IsEnabled = false;
                    control = box;
                }
                else if (option.Kind == "toggle")
                {
                    var toggle = new ToggleButton { Content = ButtonContent(option, title), ToolTip = title, Padding = new Thickness(8,4,8,4) };
                    var sourceToggle = option.NativeControl as ToggleButton;
                    toggle.IsChecked = sourceToggle?.IsChecked ?? false;
                    // Invoke the real native toggle so its handlers/panel logic also run.
                    toggle.Click += (sender, args) =>
                    {
                        if (sourceToggle != null)
                        {
                            sourceToggle.SetCurrentValue(ToggleButton.IsCheckedProperty, toggle.IsChecked);
                            sourceToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, sourceToggle));
                            if (sourceToggle.Command?.CanExecute(sourceToggle.CommandParameter) == true) sourceToggle.Command.Execute(sourceToggle.CommandParameter);
                        }
                        else if (option.NativeModel != null)
                        {
                            var property = option.NativeModel.GetType().GetProperty(option.Id == "native.filters" ? "FilterPanelVisible" : "NotificationPanelVisible");
                            if (property?.CanWrite == true) property.SetValue(option.NativeModel, toggle.IsChecked == true);
                        }
                    };
                    if (sourceToggle != null) toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(sourceToggle.IsChecked)) { Source = sourceToggle, Mode = BindingMode.OneWay });
                    else if (option.NativeModel != null) toggle.SetBinding(ToggleButton.IsCheckedProperty,
                        new Binding(option.Id == "native.filters" ? "FilterPanelVisible" : "NotificationPanelVisible") { Source = option.NativeModel, Mode = BindingMode.OneWay });
                    else toggle.IsEnabled = false;
                    control = toggle;
                }
                else if (option.Kind == "status")
                {
                    var text = new TextBlock { FontSize = Settings.FontSize, MaxWidth = 300, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = title };
                    if (option.NativeControl is TextBlock source) text.SetBinding(TextBlock.TextProperty, new Binding(nameof(source.Text)) { Source = source });
                    else text.Text = "No active operation";
                    control = text;
                }
                else if (option.Kind == "progress")
                {
                    var progress = new ProgressBar { Width = 150, Height = 16, ToolTip = title };
                    if (option.NativeControl is ProgressBar source)
                    {
                        progress.SetBinding(RangeBase.ValueProperty, new Binding(nameof(source.Value)) { Source = source });
                        progress.SetBinding(RangeBase.MaximumProperty, new Binding(nameof(source.Maximum)) { Source = source });
                        progress.SetBinding(ProgressBar.IsIndeterminateProperty, new Binding(nameof(source.IsIndeterminate)) { Source = source });
                    }
                    else progress.IsEnabled = false;
                    control = progress;
                }
                else
                {
                    var button = new Button { Content = ButtonContent(option, title), ToolTip = title, Padding = new Thickness(8,4,8,4) };
                    if (option.Id == "page.previous") button.Command = model?.PreviousPageCommand;
                    else if (option.Id == "page.next") button.Command = model?.NextPageCommand;
                    else if (option.Id == "page.manage") button.Click += (sender, args) => host?.ShowPages();
                    else if (!entry.IsExtension && option.NativeControl?.ContextMenu != null)
                        button.Click += (sender, args) => OpenMenu(option.NativeControl, button);
                    else if (option.Command != null) { button.Command = option.Command; button.CommandParameter = option.CommandParameter; }

                    else button.IsEnabled = false;
                    if (option.Id?.StartsWith("page.") == true && model == null) button.IsEnabled = false;
                    if (entry.IsExtension)
                    {
                        button.Command = NativeToolbarCatalog.ExtensionCommand(option, entry.OpenMode);
                        button.CommandParameter = null;
                    }
                    control = button;
                }
                control.Margin = new Thickness(Settings.Spacing / 2);
                control.VerticalAlignment = VerticalAlignment.Center;
                WindowChrome.SetIsHitTestVisibleInChrome(control, true);
                if (!control.IsEnabled && !(option.Id?.StartsWith("page.") ?? false))
                { control.ToolTip = title + (entry.IsExtension ? ": no extension page or enabled action is currently available." : ": unavailable in the current Playnite theme/context."); ToolTipService.SetShowOnDisabled(control, true); }
                (panels.TryGetValue(entry.GroupId ?? "main", out var groupPanel) ? groupPanel : panel).Children.Add(control);
            }
            if (Settings.Items.Count == 0) panel.Children.Add(new TextBlock { Text = "Toolbar is empty. Add items in Appearance > Toolbar settings.", TextWrapping = TextWrapping.Wrap });
            var grid = new Grid();
            for (var i = 0; i < groups.Length; ++i)
            {
                var group = groups[i];
                var horizontal = Settings.Orientation == Orientation.Horizontal;
                var sizing = horizontal ? group.WidthSizing : group.HeightSizing;
                var size = horizontal ? group.Width : group.Height;
                var length = sizing == ToolbarGroupSizing.Fixed ? new GridLength(size + 2 * group.Margin)
                    : sizing == ToolbarGroupSizing.Content ? GridLength.Auto : new GridLength(group.Weight, GridUnitType.Star);
                if (horizontal) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = length });
                else grid.RowDefinitions.Add(new RowDefinition { Height = length });
                var border = new Border
                {
                    ClipToBounds = true, Child = panels[group.Id], Padding = new Thickness(group.Padding), Margin = new Thickness(group.Margin),
                    CornerRadius = new CornerRadius(group.CornerRadius), BorderThickness = new Thickness(group.BorderWidth),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(group.BorderColor)),
                    Background = group.ShowBackground ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(group.Background)) { Opacity = group.BackgroundOpacity } : null,
                    HorizontalAlignment = group.WidthSizing == ToolbarGroupSizing.Fill ? HorizontalAlignment.Stretch : group.Alignment,
                    VerticalAlignment = group.HeightSizing == ToolbarGroupSizing.Fill ? VerticalAlignment.Stretch : group.VerticalAlignment
                };
                if (group.WidthSizing == ToolbarGroupSizing.Fixed) border.Width = group.Width;
                if (group.HeightSizing == ToolbarGroupSizing.Fixed) border.Height = group.Height;
                if (horizontal) Grid.SetColumn(border, i); else Grid.SetRow(border, i);
                grid.Children.Add(border);
            }
            FrameworkElement layout = grid;
            layout.SetBinding(MinHeightProperty, new Binding(nameof(ActualHeight)) { Source = this });
            Content = new ScrollViewer { Content = layout, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0) };
        }

        private object ButtonContent(ToolbarOption option, string title)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (Settings.ShowIcons)
            {
                content.Children.Add(new ToolbarIcon { Option = option });
            }
            if (Settings.ShowLabels || content.Children.Count == 0)
                content.Children.Add(new TextBlock { Text = title, FontSize = Settings.FontSize, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(content.Children.Count > 0 ? 6 : 0,0,0,0) });
            return content;
        }

        private void OpenMenu(FrameworkElement source, FrameworkElement anchor)
        {
            var menu = source.ContextMenu;
            if (menu == null) return;
            if (menu.IsOpen) menu.IsOpen = false;
            var previousTarget = menu.ReadLocalValue(ContextMenu.PlacementTargetProperty);
            var previousPlacement = menu.ReadLocalValue(ContextMenu.PlacementProperty);
            var previousContext = menu.ReadLocalValue(DataContextProperty);
            if (previousContext == DependencyProperty.UnsetValue) menu.SetCurrentValue(DataContextProperty, source.DataContext);
            menu.SetCurrentValue(ContextMenu.PlacementTargetProperty, anchor);
            menu.SetCurrentValue(ContextMenu.PlacementProperty, PlacementMode.Bottom);
            // IsOpen also changes when a queued popup is dismissed before it creates
            // a window; ContextMenu.Closed is not raised in that case.
            var isOpen = DependencyPropertyDescriptor.FromProperty(ContextMenu.IsOpenProperty, typeof(ContextMenu));
            EventHandler closed = null;
            closed = (sender, args) =>
            {
                if (menu.IsOpen) return;
                isOpen.RemoveValueChanged(menu, closed);
                Restore(menu, ContextMenu.PlacementTargetProperty, previousTarget);
                Restore(menu, ContextMenu.PlacementProperty, previousPlacement);
                Restore(menu, DataContextProperty, previousContext);
                openNativeMenu = null;
            };
            isOpen.AddValueChanged(menu, closed);
            openNativeMenu = menu;
            menu.IsOpen = true;
        }
        private static void Restore(DependencyObject target, DependencyProperty property, object value)
        {
            if (value == DependencyProperty.UnsetValue) target.ClearValue(property);
            else if (!(value is BindingExpressionBase)) target.SetCurrentValue(property, value);
        }
    }
}
