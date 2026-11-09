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
            Unloaded += (sender, args) => StopObservingShelf();
            DataContextChanged += (sender, args) => { StopObservingShelf(); if (IsLoaded) ObserveShelf(); };
        }

        private ShelveProperties observedShelf;
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
        }

        private void ApplyDisplayStyle()
        {
            if (GameListBox == null || !(DataContext is ShelveViewModel model)) return;
            var style = model.ShelveProperties.DisplayStyle;
            var original = style == ShelfDisplayStyle.CoverRow;
            if (Template.FindName("ShelveDockPanel", this) is DockPanel dock)
            {
                dock.LastChildFill = !original;
                dock.HorizontalAlignment = original ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
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
            lastContraint = null;
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

        protected Size? lastContraint = null;

        protected override Size MeasureOverride(Size constraint)
        {
            if (constraint != lastContraint)
            {
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    lastContraint = ApplyWidthUpdate(this) ? constraint : (Size?)null;
                }), DispatcherPriority.Background);
            }

            return base.MeasureOverride(constraint);
        }

        private bool ApplyWidthUpdate(object sender)
        {
            if (DataContext is ShelveViewModel model && model.ShelveProperties.DisplayStyle != ShelfDisplayStyle.CoverRow)
                return true;
            var updated = 0;
            var notUpdated = 0;
            var newWidth = ActualWidth;
            var listBoxes = new[] { GameListBox };
            foreach (var listBox in listBoxes)
            {
                if (listBox is ListBox)
                {
                    var itemCount = listBox.ItemsSource?.Cast<object>().Count() ?? 0;
                    if (listBox.IsVisible && itemCount > 0)
                    {
                        FrameworkElement container = null;
                        for (int i = 0; i < itemCount; ++i)
                        {
                            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement element)
                            {
                                container = element;
                                break;
                            }
                        }
                        if (container != null)
                        {
                            var desiredWidth = listBox.DesiredSize.Width;
                            var itemWidth = container.ActualWidth + container.Margin.Left + container.Margin.Right;
                            var scrollViewer = UiHelper.FindVisualChildren<ScrollViewer>(listBox).FirstOrDefault();
                            // itemWidth = desiredWidth / itemCount;
                            var availableWidth = newWidth - 60;
                            FrameworkElement panel = VisualTreeHelper.GetParent(this) as FrameworkElement;
                            while (panel != null && !(panel is GridNodeView))
                            {
                                panel = VisualTreeHelper.GetParent(panel) as FrameworkElement;
                            }
                            if (panel != null) availableWidth = panel.ActualWidth - 60;
                            var newListWidth = Math.Floor(availableWidth / Math.Max(itemWidth, 1)) * itemWidth;
                            if (listBox.MaxWidth != newListWidth)
                            {
                                listBox.MaxWidth = Math.Max(0, newListWidth);
                                ++updated;
                                continue;
                            }
                        }
                    }
                }

                ++notUpdated;
            }
            return notUpdated == 0 && updated > 0;
        }

        private void StackPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is GameModel model && model.Game != ShelveViewModel.DummyGame)
            {
                if (DataContext is ShelveViewModel viewModel && viewModel.ParentViewModel is ShelvesViewModel)
                {
                    if (UiHelper.FindVisualChildren<Grid>(element, "ImageGrid").FirstOrDefault() is Grid imageGrid)
                    {
                        Dispatcher.Invoke(() => {
                            viewModel.ParentViewModel.PopupTarget = element;
                        }, System.Windows.Threading.DispatcherPriority.Normal);
                    }

                    viewModel.ParentViewModel.CurrentlyHoveredGame = model.Game;
                    viewModel.ParentViewModel.CurrentlyHoveredGameModel = model;
                    viewModel.ParentViewModel.ShowDetails = viewModel.ShelveSettings.ShowDetails;
                }
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
