using LandingPage.Models;
using LandingPage.ViewModels;
using PlayniteCommon.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LandingPage.Views
{
    /// <summary>
    /// Interaktionslogik für ShelveView.xaml
    /// </summary>
    public partial class ShelveView : UserControl
    {
        public ShelveView()
        {
            InitializeComponent();
            Loaded += (sender, args) => ObserveShelf();
            Unloaded += (sender, args) => { CloseShelfSettings(); StopObservingShelf(); };
            DataContextChanged += (sender, args) => { StopObservingShelf(); if (IsLoaded) ObserveShelf(); };
        }

        private ShelveProperties observedShelf;

        private void CloseShelfSettings()
        {
            if (Template?.FindName("ShelveDockPanel", this) is DockPanel dock
                && dock.Tag is System.Windows.Controls.Primitives.ToggleButton toggle)
                toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
            if (Template?.FindName("SettingsPopup", this) is System.Windows.Controls.Primitives.Popup popup)
                popup.SetCurrentValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty, false);
        }

        private Window settingsOwner;

        private void ShelfSettings_Opened(object sender, EventArgs e)
        {
            ShelfSettings_Closed(sender, e);
            settingsOwner = Window.GetWindow(this);
            settingsOwner?.AddHandler(Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler(SettingsOwner_PreviewMouseDown), true);
        }

        private void ShelfSettings_Closed(object sender, EventArgs e)
        {
            settingsOwner?.RemoveHandler(Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler(SettingsOwner_PreviewMouseDown));
            settingsOwner = null;
        }

        private void SettingsOwner_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(Template?.FindName("SettingsPopup", this) is System.Windows.Controls.Primitives.Popup popup)
                || !popup.IsOpen) return;
            // Popup content has its own presentation source; only dismiss clicks in the owner.
            if (e.OriginalSource is Visual visual
                && PresentationSource.FromVisual(visual) == PresentationSource.FromVisual(settingsOwner)
                && !(popup.PlacementTarget?.IsMouseOver ?? false))
                CloseShelfSettings();
        }

        private void ResetShelfHover_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ShelveViewModel model) model.ShelveProperties.ResetHoverAppearance();
        }

        private void CloseShelfSettings_Click(object sender, RoutedEventArgs e) => CloseShelfSettings();

        private void ShelfSettings_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { CloseShelfSettings(); e.Handled = true; }
        }
        private ItemsPanelTemplate originalPanel;
        private ItemsPanelTemplate originalGroupPanel;
        private DataTemplate originalItemTemplate;

        private void ObserveShelf()
        {
            StopObservingShelf();
            observedShelf = (DataContext as ShelveViewModel)?.ShelveProperties;
            if (observedShelf != null) observedShelf.PropertyChanged += ShelfLayoutChanged;
            ApplyDisplayStyle();
        }

        private void StopObservingShelf()
        {
            if (observedShelf != null) observedShelf.PropertyChanged -= ShelfLayoutChanged;
            observedShelf = null;
        }

        private void ShelfLayoutChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ShelveProperties.DisplayStyle)) ApplyDisplayStyle();
            if (args.PropertyName == nameof(ShelveProperties.HoverShowDetails) && observedShelf?.HoverShowDetails == false
                && DataContext is ShelveViewModel shelf && shelf.ParentViewModel is ShelvesViewModel)
                shelf.ParentViewModel.ShowDetails = false;
        }

        private ListBox styledList;
        private ShelfDisplayStyle? appliedStyle;

        private void ApplyDisplayStyle()
        {
            if (GameListBox == null || !(DataContext is ShelveViewModel model)) return;
            var style = model.ShelveProperties.DisplayStyle;
            if (ReferenceEquals(styledList, GameListBox) && appliedStyle == style) return;
            styledList = GameListBox; appliedStyle = style;
            var original = style == ShelfDisplayStyle.CoverRow;
            if (Template.FindName("ShelveDockPanel", this) is DockPanel dock)
            {
                dock.LastChildFill = true;
                dock.HorizontalAlignment = HorizontalAlignment.Stretch;
                dock.VerticalAlignment = original ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            }
            GameListBox.VerticalAlignment = original ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            GameListBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            GameListBox.ClearValue(MaxWidthProperty);
            if (original)
            {
                GameListBox.ItemsPanel = originalPanel;
                GameListBox.ItemTemplate = originalItemTemplate;
                if (GameListBox.GroupStyle.Count > 0) GameListBox.GroupStyle[0].Panel = originalGroupPanel;
                GameListBox.ClearValue(ScrollViewer.HorizontalScrollBarVisibilityProperty);
                GameListBox.ClearValue(ScrollViewer.VerticalScrollBarVisibilityProperty);
                GameListBox.ClearValue(ScrollViewerHelper.UseHorizontalScrollingProperty);
            }
            else
            {
                var grid = style == ShelfDisplayStyle.CoverGrid;
                var compact = style == ShelfDisplayStyle.CompactList;
                var panel = new FrameworkElementFactory(grid ? typeof(WrapPanel) : typeof(StackPanel));
                panel.SetValue(grid ? WrapPanel.OrientationProperty : StackPanel.OrientationProperty,
                    compact ? Orientation.Vertical : Orientation.Horizontal);
                var itemsPanel = new ItemsPanelTemplate(panel);
                GameListBox.ItemsPanel = itemsPanel;
                if (GameListBox.GroupStyle.Count > 0) GameListBox.GroupStyle[0].Panel = itemsPanel;
                GameListBox.ItemTemplate = (DataTemplate)Resources[compact ? "ShelfCompactTemplate" : grid ? "ShelfGridTemplate" : "ShelfWideTemplate"];
                GameListBox.MaxWidth = double.PositiveInfinity;
                ScrollViewer.SetHorizontalScrollBarVisibility(GameListBox, grid || compact ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
                ScrollViewer.SetVerticalScrollBarVisibility(GameListBox, grid || compact ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
                GameListBox.SetValue(ScrollViewerHelper.UseHorizontalScrollingProperty, !grid && !compact);
            }
            InvalidateMeasure();
        }

        private static DispatcherTimer dispatcherTimer = null;
        private static GameModel clickedModel = null;

        public FrameworkElement HoveredElement
        {
            get { return (FrameworkElement)GetValue(HoveredElementProperty); }
            set 
            { 
                SetValue(HoveredElementProperty, value);
                HoveredElementChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HoveredElementChanged)));
            }
        }

        public ListBox GameListBox { get; private set; }

        // Using a DependencyProperty as the backing store for HoveredElement.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HoveredElementProperty =
            DependencyProperty.Register(nameof(HoveredElement), typeof(FrameworkElement), typeof(ShelveView), new PropertyMetadata(default(FrameworkElement)));

        public event EventHandler<PropertyChangedEventArgs> HoveredElementChanged;

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();
            clickedModel?.OpenCommand?.Execute(null);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            GameListBox = Template?.FindName("PART_GamesListBox", this) as ListBox;
            if (GameListBox != null)
            {
                originalPanel = GameListBox.ItemsPanel;
                originalItemTemplate = GameListBox.ItemTemplate;
                originalGroupPanel = GameListBox.GroupStyle.FirstOrDefault()?.Panel;
                ApplyDisplayStyle();
            }
        }

        private void StackPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            var diagnostics = LandingPageExtension.Instance?.SettingsViewModel?.Settings?.EnableDashboardDiagnostics == true;
            var watch = diagnostics ? System.Diagnostics.Stopwatch.StartNew() : null;
            if (sender is FrameworkElement element && element.DataContext is GameModel model && model.Game != ShelveViewModel.DummyGame)
            {
                if (DataContext is ShelveViewModel viewModel && viewModel.ParentViewModel is ShelvesViewModel)
                {
                    var showDetails = viewModel.ShelveSettings.ShowDetails && viewModel.ShelveProperties.HoverShowDetails;
                    // A hidden details panel should not rebuild its target or game model.
                    if (showDetails)
                    {
                        viewModel.ParentViewModel.PopupTarget = element;
                        viewModel.ParentViewModel.CurrentlyHoveredGameModel = model;
                    }
                    viewModel.ParentViewModel.CurrentlyHoveredGame = model.Game;
                    LandingPageExtension.Instance?.SetFeaturedHoveredGame(model.Game,element,viewModel.ParentViewModel);
                    viewModel.ParentViewModel.ShowDetails = showDetails;
                }
            }
            if (watch != null)
            {
                var details = (DataContext as ShelveViewModel)?.ParentViewModel?.ShowDetails;
                var message = $"Shelf hover enter: details={details}, updateMs={watch.Elapsed.TotalMilliseconds:F2}.";
                var plugin = LandingPageExtension.Instance;
                ExtensionDiagnostics.RecordAsync(plugin,message);
            }
        }

        private void StackPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is GameModel model)
            {
                if (UiHelper.FindVisualChildren<Grid>(element, "ImageGrid").FirstOrDefault() is Grid imageGrid)
                {
                    Dispatcher.Invoke(() => {
                        HoveredElement = null;
                    }, System.Windows.Threading.DispatcherPriority.Normal);
                }

                if (DataContext is ShelveViewModel viewModel && viewModel.ParentViewModel is ShelvesViewModel)
                {
                    viewModel.ParentViewModel.ShowDetails = false;
                    viewModel.ParentViewModel.CurrentlyHoveredGame = null;
                    LandingPageExtension.Instance?.SetFeaturedHoveredGame(null,element,viewModel.ParentViewModel);
                }
            }
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item)
            {
                if (item.DataContext is GameModel model)
                {
                    dispatcherTimer?.Stop();
                    model.StartCommand?.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void ListBoxItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && (!(e.OriginalSource is TextBlock)
                || (DataContext as ShelveViewModel)?.ShelveProperties.DisplayStyle != ShelfDisplayStyle.CoverRow))
            {
                if (dispatcherTimer == null)
                {
                    dispatcherTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
                    dispatcherTimer.Interval = TimeSpan.FromMilliseconds(250);
                    dispatcherTimer.Tick += DispatcherTimer_Tick;
                }
                dispatcherTimer.Start();

                if (item.DataContext is GameModel model)
                {
                    clickedModel = model;
                    //model.OpenCommand?.Execute(null);
                }
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button bt && bt.DataContext is GameModel game)
            {
                game.StartCommand?.Execute(null);
            }
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button bt && bt.DataContext is GameModel game)
            {
                game.OpenCommand?.Execute(null);
            }
        }
    }
}
