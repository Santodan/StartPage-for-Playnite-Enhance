using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using LandingPage.Models.GameActivity;
using LandingPage.ViewModels.GameActivity;
using System.Linq;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Data;
using System.Windows.Threading;
using LandingPage.Models;
using LandingPage.Settings;
using StartPage.SDK.Async;
namespace LandingPage.Views
{
    public sealed class LibraryStatisticsView : UserControl, IAsyncStartPageControl, IStartPageViewModel
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings),typeof(LibraryStatisticsSettings),typeof(LibraryStatisticsView),new PropertyMetadata(null,SettingsChanged));
        public LibraryStatisticsSettings Settings { get => (LibraryStatisticsSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty,value); }
        public static readonly DependencyProperty DashboardEditModeProperty = DependencyProperty.Register(nameof(DashboardEditMode),typeof(bool),typeof(LibraryStatisticsView),new PropertyMetadata(false,EditingChanged));
        public bool DashboardEditMode { get => (bool)GetValue(DashboardEditModeProperty); set => SetValue(DashboardEditModeProperty,value); }
        public static readonly DependencyProperty EditingRequestedProperty = DependencyProperty.Register(nameof(EditingRequested),typeof(bool),typeof(LibraryStatisticsView),new PropertyMetadata(false,EditingChanged));
        public bool EditingRequested { get => (bool)GetValue(EditingRequestedProperty); set => SetValue(EditingRequestedProperty,value); }
        private bool IsChartEditing => chartOnly && DashboardEditMode && EditingRequested;
        private bool manipulating, deferredRender, refreshingLabels;
        private int renderCount;
        private readonly Dictionary<string,ImageSource> statisticIcons = new Dictionary<string,ImageSource>();
        private Border editOutline;
        private Thumb resizeGrip;
        private sealed class LabelPlacement { public Slice Slice; public Point Offset; }
        private readonly Dictionary<TextBlock,LabelPlacement> labelPlacements = new Dictionary<TextBlock,LabelPlacement>();
        private UIElement activeManipulation;
        private static void EditingChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)
        {
            var view = (LibraryStatisticsView)sender;
            if(!view.IsChartEditing) { if(view.activeManipulation is Thumb thumb) thumb.CancelDrag(); else view.activeManipulation?.ReleaseMouseCapture(); view.manipulating = false; }
            view.RefreshEditingVisuals();
        }
        private void RefreshEditingVisuals()
        {
            var visibility = IsChartEditing ? Visibility.Visible : Visibility.Collapsed;
            if(editOutline != null) editOutline.Visibility = visibility;
            if(resizeGrip != null) resizeGrip.Visibility = visibility;
            foreach(var text in labelPlacements.Keys)
            {
                if(IsChartEditing) { text.Cursor = Cursors.SizeAll; text.ToolTip = "Drag label to move it"; }
                else { text.ClearValue(CursorProperty); text.ClearValue(ToolTipProperty); }
            }
            ScheduleLayoutLog("editing overlay changed");
        }
        private void RefreshLabelPositions()
        {
            if(manipulating || refreshingLabels) return;
            refreshingLabels = true;
            try
            {
                foreach(var pair in labelPlacements.ToArray())
                {
                    var target = Offset(pair.Value.Slice); var previous = pair.Value.Offset;
                    if(target == previous) continue;
                    var transforms = new TransformGroup(); if(pair.Key.RenderTransform != null) transforms.Children.Add(pair.Key.RenderTransform);
                    transforms.Children.Add(new TranslateTransform(target.X-previous.X,target.Y-previous.Y));
                    pair.Key.RenderTransform = transforms; pair.Value.Offset = target;
                }
            }
            finally { refreshingLabels = false; }
        }
        private void FinishManipulation()
        {
            manipulating = false; activeManipulation = null;
            if(deferredRender) { deferredRender = false; Queue(); }
        }
        private readonly bool chartOnly, allowExtraCharts;
        private ObservableCollection<LibraryStatisticsSettings> observedCharts;
        private ObservableCollection<CounterPlacement> observedPlacements;
        private ObservableCollection<ChartLabelOffset> observedLabelOffsets;
        private readonly List<ChartLabelOffset> labelSubscriptions = new List<ChartLabelOffset>();
        private readonly List<StatisticColors> colorSubscriptions = new List<StatisticColors>();
        private readonly List<CounterPlacement> placementSubscriptions = new List<CounterPlacement>();
        private readonly Dictionary<LibraryStatisticsSettings,LibraryStatisticsView> extraViews = new Dictionary<LibraryStatisticsSettings,LibraryStatisticsView>();
        private LibraryStatisticsSource source;
        private LibraryStatisticsSettings observed;
        private ObservableCollection<StatisticCounterOption> observedCounterOptions;
        private ObservableCollection<StatisticGroupOption> observedGroupOptions;
        private readonly List<System.ComponentModel.INotifyPropertyChanged> statisticOptionSubscriptions = new List<System.ComponentModel.INotifyPropertyChanged>();
        private bool visible, closed, queued;
        private GameActivityViewModel history;
        private ObservableCollection<Activity> observedActivities;
        private PeriodPlaytime periodTotals;
        private bool periodsLoading, periodsDirty = true, historyMissing;
        private DateTime periodMinute;
        private int historyGeneration;
        private readonly DispatcherTimer periodTimer;
        private readonly DispatcherTimer layoutLogTimer;
        private readonly DispatcherTimer optionsTimer;
        private Controls.LibraryChartPlot activePlot;
        private string layoutLogReason;
        private bool NeedsPeriods => !chartOnly && (Settings.ShowTodayPlaytime || Settings.ShowWeekPlaytime || Settings.ShowMonthPlaytime || Settings.ShowYearPlaytime);
        private readonly ScrollViewer viewport = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false, PanningMode = PanningMode.Both };
        public LibraryStatisticsView() : this(false) { }
        internal LibraryStatisticsView(bool chartOnly,bool allowExtraCharts = true)
        {
            this.chartOnly = chartOnly; this.allowExtraCharts = allowExtraCharts;
            periodTimer = new DispatcherTimer(DispatcherPriority.Background,Dispatcher) { Interval = TimeSpan.FromMinutes(1) };
            periodTimer.Tick += (s,e) => { periodsDirty = true; Queue(); };
            WindowChrome.SetIsHitTestVisibleInChrome(viewport,true);
            PreviewMouseWheel += ChartMouseWheel;
            optionsTimer = new DispatcherTimer(DispatcherPriority.Background,Dispatcher) { Interval = TimeSpan.FromMilliseconds(75) };
            optionsTimer.Tick += (sender,args) => { optionsTimer.Stop(); Queue(); };
            layoutLogTimer = new DispatcherTimer(DispatcherPriority.Background,Dispatcher) { Interval = TimeSpan.FromMilliseconds(250) };
            layoutLogTimer.Tick += (sender,args) => { layoutLogTimer.Stop(); LogChartLayout(); };
            viewport.ScrollChanged += (sender,args) => ScheduleLayoutLog("scroll/extent changed");
            HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
            Content = viewport; Settings = chartOnly ? LibraryStatisticsSettings.CreateChartSettings() : new LibraryStatisticsSettings();
            SizeChanged += (s,e) => ScheduleLayoutLog("widget resized without rebuilding");
            SetBinding(SettingsProperty,new Binding("DataContext.GridNode.ViewProperties.LibraryStatisticsSettings") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,typeof(GridNodeView),1), FallbackValue = Settings, TargetNullValue = Settings });
            SetBinding(DashboardEditModeProperty,new Binding("DataContext.EditModeEnabled") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,typeof(GridNodeView),1),FallbackValue = false });
            SetBinding(EditingRequestedProperty,new Binding("DataContext.GridNode.ViewProperties.LibraryStatisticsSettings.EditCharts") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,typeof(GridNodeView),1),FallbackValue = false });
            Focusable = true; PreviewKeyDown += (sender,args) => { if(args.Key == Key.Escape && activeManipulation != null) { activeManipulation.ReleaseMouseCapture(); args.Handled = true; } };
            Loaded += (s,e) => {
                if(allowExtraCharts)
                {
                    DependencyObject ancestor = VisualTreeHelper.GetParent(this);
                    while(ancestor != null && !(ancestor is GridNodeView)) ancestor = VisualTreeHelper.GetParent(ancestor);
                    if(ancestor is GridNodeView node && node.DataContext is ViewModels.Layout.GridNodeViewModel model && model.GridNode.ViewProperties is Models.Layout.ViewProperties properties)
                        SetBinding(SettingsProperty,new Binding(nameof(properties.LibraryStatisticsSettings)) { Source = properties });
                }
                Show(); ScheduleLayoutLog("loaded");
            }; Unloaded += (s,e) => Hide();
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            // Use the parent's measure constraint, never its previous ActualHeight:
            // binding a viewport limit to its own content-sized host can collapse it.
            if(allowExtraCharts) viewport.MaxHeight = availableSize.Height;
            return base.MeasureOverride(availableSize);
        }
        private static bool ScrollViewerBy(ScrollViewer viewer,int delta)
        {
            var target = Math.Max(0,Math.Min(viewer.ScrollableHeight,viewer.VerticalOffset-delta));
            if(Math.Abs(target-viewer.VerticalOffset) < 0.5) return false;
            viewer.ScrollToVerticalOffset(target); return true;
        }
        internal void ChartMouseWheel(object sender,MouseWheelEventArgs args)
        {
            if(!allowExtraCharts) return;
            // A side legend gets first use of the wheel. At its boundary, move the
            // chart collection; at that boundary, hand the wheel to the dashboard.
            DependencyObject current = args.OriginalSource as DependencyObject;
            while(current != null && current != this)
            {
                if(current is ScrollViewer inner && inner != viewport && IsAncestorOf(inner) && ScrollViewerBy(inner,args.Delta)) { args.Handled = true; return; }
                current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
            }
            if(ScrollCharts(args.Delta)) { args.Handled = true; return; }
            current = VisualTreeHelper.GetParent(this);
            while(current != null)
            {
                if(current is ScrollViewer outer && ScrollViewerBy(outer,args.Delta)) { args.Handled = true; ScheduleLayoutLog("wheel handed to dashboard"); return; }
                current = VisualTreeHelper.GetParent(current);
            }
        }
        private void ScheduleLayoutLog(string reason)
        {
            if(!chartOnly || !allowExtraCharts || !visible || LandingPageExtension.Instance?.SettingsViewModel?.Settings?.EnableDashboardDiagnostics != true) return;
            layoutLogReason = reason; layoutLogTimer.Stop(); layoutLogTimer.Start();
        }
        private Task LogChartLayout()
        {
            if(!visible || closed || !chartOnly || !allowExtraCharts) return Task.CompletedTask;
            var details = new List<string>();
            if(viewport.Content is FrameworkElement content)
            {
                var grid = (content as Panel)?.Children.OfType<Grid>().FirstOrDefault(item => item.Name == "ChartGrid");
                if(grid != null) foreach(FrameworkElement chart in grid.Children)
                {
                    var point = chart.TranslatePoint(new Point(),viewport);
                    details.Add($"row={Grid.GetRow(chart)},column={Grid.GetColumn(chart)},top={point.Y:0.##},height={chart.ActualHeight:0.##},desired={chart.DesiredSize.Height:0.##}");
                }
            }
            var outerInfo = "none"; DependencyObject ancestor = VisualTreeHelper.GetParent(this);
            while(ancestor != null)
            {
                if(ancestor is ScrollViewer outer) { outerInfo = $"viewport={outer.ViewportHeight:0.##},extent={outer.ExtentHeight:0.##},offset={outer.VerticalOffset:0.##},scrollable={outer.ScrollableHeight:0.##}"; break; }
                ancestor = VisualTreeHelper.GetParent(ancestor);
            }
            return ExtensionDiagnostics.RecordAsync(LandingPageExtension.Instance,$"Library chart layout: reason={layoutLogReason}; configuredCharts={1+Settings.ExtraCharts.Count}; renderedCharts={details.Count}; columns={Settings.ChartColumns}; widget={ActualWidth:0.##}x{ActualHeight:0.##}; viewport={viewport.ViewportWidth:0.##}x{viewport.ViewportHeight:0.##}; extent={viewport.ExtentHeight:0.##}; offset={viewport.VerticalOffset:0.##}; scrollable={viewport.ScrollableHeight:0.##}; scrollbar={viewport.ComputedVerticalScrollBarVisibility}; dashboardScroll=[{outerInfo}]; charts=[{string.Join("; ",details)}].");
        }
        private static void SettingsChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)
        {
            var view = (LibraryStatisticsView)sender;
            if(view.observed != null) view.observed.PropertyChanged -= view.OptionsChanged;
            view.observed = args.NewValue as LibraryStatisticsSettings;
            if(view.observed != null) view.observed.PropertyChanged += view.OptionsChanged;
            view.Settings?.EnsureStatisticOptions();
            view.WatchCollections(); view.periodsDirty = true; view.Queue();
        }
        private void UnwatchStatisticOptions()
        {
            if(observedCounterOptions != null) observedCounterOptions.CollectionChanged -= LayoutCollectionChanged;
            if(observedGroupOptions != null) observedGroupOptions.CollectionChanged -= LayoutCollectionChanged;
            foreach(var option in statisticOptionSubscriptions) option.PropertyChanged -= PlacementChanged;
            statisticOptionSubscriptions.Clear();
        }
        private void WatchCollections()
        {
            UnwatchStatisticOptions();
            foreach(var color in colorSubscriptions) color.PropertyChanged -= PlacementChanged; colorSubscriptions.Clear();
            observedCounterOptions = Settings?.AdditionalCounters; observedGroupOptions = Settings?.StatisticGroups;
            if(observedCounterOptions != null) { observedCounterOptions.CollectionChanged += LayoutCollectionChanged; foreach(var option in observedCounterOptions.Where(item => item != null)) { option.PropertyChanged += PlacementChanged; statisticOptionSubscriptions.Add(option); } }
            if(observedGroupOptions != null) { observedGroupOptions.CollectionChanged += LayoutCollectionChanged; foreach(var option in observedGroupOptions.Where(item => item != null)) { option.PropertyChanged += PlacementChanged; statisticOptionSubscriptions.Add(option); } }
            if(observedCharts != null) observedCharts.CollectionChanged -= LayoutCollectionChanged;
            if(observedPlacements != null) observedPlacements.CollectionChanged -= LayoutCollectionChanged;
            if(observedLabelOffsets != null) observedLabelOffsets.CollectionChanged -= LayoutCollectionChanged;
            foreach(var label in labelSubscriptions) label.PropertyChanged -= PlacementChanged; labelSubscriptions.Clear();
            foreach(var placement in placementSubscriptions) placement.PropertyChanged -= PlacementChanged;
            placementSubscriptions.Clear(); observedCharts = Settings?.ExtraCharts; observedPlacements = Settings?.CounterPlacements;
            observedLabelOffsets = Settings?.LabelOffsets;
            if(observedLabelOffsets != null) { observedLabelOffsets.CollectionChanged += LayoutCollectionChanged; foreach(var label in observedLabelOffsets) { label.PropertyChanged += PlacementChanged; labelSubscriptions.Add(label); } }
            if(observedCharts != null) observedCharts.CollectionChanged += LayoutCollectionChanged;
            if(observedPlacements != null) { observedPlacements.CollectionChanged += LayoutCollectionChanged; foreach(var placement in observedPlacements.Where(item => item != null)) { placement.PropertyChanged += PlacementChanged; placementSubscriptions.Add(placement); placement.Colors.PropertyChanged += PlacementChanged; colorSubscriptions.Add(placement.Colors); } }
        }
        private void LayoutCollectionChanged(object sender,NotifyCollectionChangedEventArgs args)
        {
            var labels = ReferenceEquals(sender,observedLabelOffsets); WatchCollections();
            if(labels) RefreshLabelPositions(); else Queue();
        }
        private void PlacementChanged(object sender,PropertyChangedEventArgs args)
        {
            if(sender is ChartLabelOffset) { RefreshLabelPositions(); return; }
            if(sender is CounterPlacement && args.PropertyName == nameof(CounterPlacement.Colors)) WatchCollections();
            if(sender is StatisticColors && Mouse.Captured is Thumb) { optionsTimer.Stop(); optionsTimer.Start(); return; }
            Queue();
        }
        private void OptionsChanged(object sender,PropertyChangedEventArgs args)
        {
            if(args.PropertyName == nameof(Settings.ChartName)) return;
            if(args.PropertyName == nameof(Settings.EditCharts)) { RefreshEditingVisuals(); return; }
            if(args.PropertyName == nameof(Settings.LabelOffsets)) { WatchCollections(); RefreshLabelPositions(); return; }
            if(args.PropertyName == nameof(Settings.LabelOffsetX) || args.PropertyName == nameof(Settings.LabelOffsetY)) { RefreshLabelPositions(); return; }
            if(manipulating) { deferredRender = true; return; }
            if(args.PropertyName == nameof(Settings.ChartRotation) && activePlot != null) { activePlot.UpdateRotation(Settings.ChartRotation); return; }
            if(Mouse.Captured is Thumb) { optionsTimer.Stop(); optionsTimer.Start(); return; }
            if(args.PropertyName == nameof(Settings.AdditionalCounters) || args.PropertyName == nameof(Settings.StatisticGroups) || args.PropertyName == nameof(Settings.ExtraCharts) || args.PropertyName == nameof(Settings.CounterPlacements) || args.PropertyName == nameof(Settings.LabelOffsets)) WatchCollections();
            if(args.PropertyName == nameof(Settings.IncludeHidden) || args.PropertyName == nameof(Settings.InstalledOnly) || args.PropertyName == nameof(Settings.WeekStartsOn)) periodsDirty = true;
            Queue();
        }
        private void DataChanged(object sender,EventArgs args) { periodsDirty = true; Queue(); }
        private void Queue()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(Queue)); return; }
            if(!visible || closed || queued) return;
            if(manipulating) { deferredRender = true; return; }
            queued = true; Dispatcher.BeginInvoke(new Action(() => { queued = false; if(!visible || closed) return; if(manipulating) deferredRender = true; else Render(); }),DispatcherPriority.Loaded);
        }
        private void Show()
        {
            if(closed) return;
            if(source == null) { source = LandingPageExtension.Instance.LibraryStatistics; source.Changed += DataChanged; }
            visible = true; Queue();
        }
        private void Hide() { optionsTimer.Stop(); layoutLogTimer.Stop(); if(activeManipulation is Thumb thumb) thumb.CancelDrag(); else activeManipulation?.ReleaseMouseCapture(); manipulating = false; if(chartOnly && allowExtraCharts) Settings.EditCharts = false; foreach(var child in extraViews.Values) child.Hide(); StopHistoryWatching(); visible = false; if(source != null) { source.Changed -= DataChanged; source = null; } }
        private void ObserveHistory()
        {
            if(history == null) return;
            history.PropertyChanged -= HistoryChanged; history.PropertyChanged += HistoryChanged;
            if(observedActivities != null) observedActivities.CollectionChanged -= SessionsChanged;
            observedActivities = history.Activities;
            if(observedActivities != null) observedActivities.CollectionChanged += SessionsChanged;
        }
        private void HistoryChanged(object sender,PropertyChangedEventArgs args)
        { if(args.PropertyName == nameof(GameActivityViewModel.Activities)) { ObserveHistory(); periodsDirty = true; Queue(); } }
        private void SessionsChanged(object sender,NotifyCollectionChangedEventArgs args) { periodsDirty = true; Queue(); }
        private void StopHistoryWatching()
        {
            periodTimer.Stop(); historyGeneration++;
            if(history != null) history.PropertyChanged -= HistoryChanged;
            if(observedActivities != null) observedActivities.CollectionChanged -= SessionsChanged;
            observedActivities = null;
        }
        private async void RefreshPeriods()
        {
            if(!NeedsPeriods) { StopHistoryWatching(); return; }
            if(!periodTimer.IsEnabled) periodTimer.Start();
            if(history != null && observedActivities == null) { ObserveHistory(); periodsDirty = true; }
            var now = DateTime.Now; var minute = new DateTime(now.Year,now.Month,now.Day,now.Hour,now.Minute,0);
            if(periodsLoading || (!periodsDirty && periodMinute == minute)) return;
            periodsLoading = true; periodsDirty = false; var generation = historyGeneration;
            try
            {
                history = history ?? LandingPageExtension.Instance.gameActivityViewModel?.Value;
                historyMissing = history == null;
                if(historyMissing) { periodTotals = null; periodMinute = minute; return; }
                ObserveHistory(); await history.Initialize();
                if(closed || !visible || generation != historyGeneration) return;
                // Initial parsing may replace the collection. That snapshot is included below.
                periodsDirty = false;
                var allowed = source.IncludedGameIds(Settings); var weekStarts = Settings.WeekStartsOn;
                List<Session> sessions;
                history.ActivityLock.EnterReadLock();
                try { sessions = history.Activities.Where(activity => allowed.Contains(activity.Id)).SelectMany(activity => activity.Items ?? new List<Session>()).Select(session => new Session { DateSession = session.DateSession,ElapsedSeconds = session.ElapsedSeconds }).ToList(); }
                finally { history.ActivityLock.ExitReadLock(); }
                var result = await Task.Run(() => PeriodPlaytime.Calculate(sessions,now,weekStarts));
                if(!closed && visible && generation == historyGeneration && !periodsDirty) { periodTotals = result; periodMinute = minute; }
            }
            catch(Exception ex) { historyMissing = true; periodTotals = null; periodMinute = minute; LandingPageExtension.logger.Warn(ex,"Unable to calculate period playtime from GameActivity history."); }
            finally { periodsLoading = false; if(visible && !closed) Queue(); }
        }
        internal bool ScrollCharts(int delta)
        {
            return allowExtraCharts && ScrollViewerBy(viewport,delta);
        }
        private void Render()
        {
            if(source == null || Settings == null) return;
            ++renderCount; labelPlacements.Clear();
            var savedOffset = viewport.VerticalOffset;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var totals = source.Calculate(Settings,true);
                        Panel panel;
            if(!chartOnly && Settings.UseCounterGrid)
            {
                var grid = new Grid { Name = "CounterGrid" };
                for(var row = 0; row < Settings.CounterRows; row++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for(var column = 0; column < Settings.CounterColumns; column++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
                panel = grid;
            }
            else panel = Settings.Rows ? (Panel)new StackPanel() : new WrapPanel();
            if(!chartOnly && Settings.ShowTotal) Add(panel,"Total games",totals.Total.ToString("N0"),StatisticsCounter.Total);
            if(!chartOnly && Settings.ShowInstalled) Add(panel,"Installed",totals.Installed.ToString("N0"),StatisticsCounter.Installed);
            if(!chartOnly && Settings.ShowUnplayed) Add(panel,"Unplayed",totals.Unplayed.ToString("N0"),StatisticsCounter.Unplayed);
            if(!chartOnly && Settings.ShowCompleted) Add(panel,"Completed",totals.Completed.ToString("N0"),StatisticsCounter.Completed);
            if(!chartOnly && Settings.ShowPlaytime) Add(panel,"Total playtime",totals.Playtime,StatisticsCounter.TotalPlaytime);
            if(!chartOnly && Settings.ShowAveragePlaytime) Add(panel,"Average playtime (played games)",totals.AveragePlaytime,StatisticsCounter.AveragePlaytime);
            if(!chartOnly && Settings.ShowPlayedGames) Add(panel,"Games with playtime",totals.GamesWithPlaytime.ToString("N0"),StatisticsCounter.PlayedGames);
            if(!chartOnly)
            {
                foreach(var option in Settings.AdditionalCounters.Where(item => item != null && item.Enabled)) Add(panel,option.Label,AdditionalValue(option.Counter,totals),option.Counter);
                foreach(var group in Settings.StatisticGroups.Where(item => item != null && item.Enabled)) AddStatisticGroup(panel,group,totals);
            }
            RefreshPeriods();
            if(NeedsPeriods)
            {
                if(Settings.ShowTodayPlaytime) Add(panel,"Playtime today",periodTotals == null ? "\u2014" : PeriodPlaytime.Hours(periodTotals.TodaySeconds),StatisticsCounter.Today);
                if(Settings.ShowWeekPlaytime) Add(panel,"Playtime this week",periodTotals == null ? "\u2014" : PeriodPlaytime.Hours(periodTotals.WeekSeconds),StatisticsCounter.Week);
                if(Settings.ShowMonthPlaytime) Add(panel,"Playtime this month",periodTotals == null ? "\u2014" : PeriodPlaytime.Hours(periodTotals.MonthSeconds),StatisticsCounter.Month);
                if(Settings.ShowYearPlaytime) Add(panel,"Playtime this year",periodTotals == null ? "\u2014" : PeriodPlaytime.Hours(periodTotals.YearSeconds),StatisticsCounter.Year);
            }
            var content = new StackPanel(); content.Children.Add(panel);
            if(NeedsPeriods && historyMissing) content.Children.Add(Text("Day/week/month/year playtime requires GameActivity session history. Lifetime playtime cannot reconstruct past sessions.",Settings.LabelSize));
            else if(NeedsPeriods && periodTotals == null) content.Children.Add(Text("Loading recorded session history...",Settings.LabelSize));
            if(chartOnly)
            {
                var charts = allowExtraCharts ? Settings.ExtraCharts.Where(item => item != null && !ReferenceEquals(item,Settings)).Distinct().ToList() : new List<LibraryStatisticsSettings>();
                foreach(var removed in extraViews.Keys.Where(item => !charts.Contains(item)).ToList()) { extraViews[removed].OnViewClosed(); extraViews.Remove(removed); }
                var chartGrid = new Grid { Name = "ChartGrid" }; var columns = Math.Min(Settings.ChartColumns,charts.Count+1);
                for(var col = 0; col < columns; col++) chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
                for(var row = 0; row < (charts.Count+columns)/columns; row++) chartGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var primary = new StackPanel { Margin = new Thickness(Settings.Spacing/2) }; RenderChart(primary,totals); chartGrid.Children.Add(primary);
                for(var index = 0; index < charts.Count; index++)
                {
                    var options = charts[index];
                    if(!extraViews.TryGetValue(options,out var child)) { child = new LibraryStatisticsView(true,false) { Settings = options }; extraViews[options] = child; }
                    if(child.Parent is Panel oldParent) oldParent.Children.Remove(child);
                    child.Margin = new Thickness(Settings.Spacing/2); Grid.SetRow(child,(index+1)/columns); Grid.SetColumn(child,(index+1)%columns); chartGrid.Children.Add(child); if(visible && !child.visible) child.Show();
                }
                content.Children.Add(chartGrid);
            }
            if(panel.Children.Count == 0 && !chartOnly) Add(panel,"Library statistics","Choose counters in Appearance");
            if(chartOnly && !allowExtraCharts) Content = content;
            else { viewport.Content = content; viewport.UpdateLayout(); viewport.ScrollToVerticalOffset(savedOffset); }
            ScheduleLayoutLog("render completed");
            ExtensionDiagnostics.RecordAsync(LandingPageExtension.Instance,"Library statistics rendered: chart="+Settings.ChartName+", root="+allowExtraCharts+", renderCount="+renderCount+", games="+totals.Total+", durationMs="+watch.ElapsedMilliseconds+".");
        }
        private sealed class Slice { public string Name, Key; public double Value; }
        private List<Slice> Group(Dictionary<Guid,int> counts,Dictionary<Guid,decimal> times,Func<Guid,string> name)
            => Settings.Metric == StatisticsMetric.Playtime
                ? times.Select(pair => new Slice { Key = pair.Key.ToString(), Name = name(pair.Key), Value = (double)pair.Value }).ToList()
                : counts.Select(pair => new Slice { Key = pair.Key.ToString(), Name = name(pair.Key), Value = pair.Value }).ToList();
        private void RenderChart(StackPanel parent,LibraryStatisticsTotals totals)
        {
            var time = Settings.Metric == StatisticsMetric.Playtime;
            var slices = new List<Slice>(); string title;
            var database = LandingPageExtension.Instance.PlayniteApi.Database;
            switch(Settings.Breakdown)
            {
                case StatisticsBreakdown.Played:
                    title = "Played / unplayed"; slices.Add(new Slice { Name = "Played", Value = time ? (double)totals.PlaytimeSeconds : totals.Total - totals.Unplayed }); slices.Add(new Slice { Name = "Unplayed", Value = time ? 0 : totals.Unplayed }); break;
                case StatisticsBreakdown.Completed:
                    title = "Completion";
                    slices.Add(new Slice { Name = "Completed", Value = time ? (double)totals.CompletedPlaytime : totals.Completed }); slices.Add(new Slice { Name = "Other games", Value = time ? (double)(totals.PlaytimeSeconds - totals.CompletedPlaytime) : totals.Total - totals.Completed }); break;
                case StatisticsBreakdown.Sources:
                    title = "By source"; slices = Group(totals.Sources,totals.SourcePlaytime,id => id == Guid.Empty ? "Playnite / no source" : database.Sources?.Get(id)?.Name ?? "Unknown source"); break;
                case StatisticsBreakdown.Platforms:
                    title = "By platform"; slices = Group(totals.Platforms,totals.PlatformPlaytime,id => id == Guid.Empty ? "No platform" : database.Platforms?.Get(id)?.Name ?? "Unknown platform"); break;
                case StatisticsBreakdown.Genres:
                    title = "By genre"; slices = Group(totals.Genres,totals.GenrePlaytime,id => id == Guid.Empty ? "No genre" : database.Genres?.Get(id)?.Name ?? "Unknown genre"); break;
                case StatisticsBreakdown.CompletionStatuses:
                    title = "By completion status"; slices = Group(totals.CompletionStatuses,totals.CompletionPlaytime,id => id == Guid.Empty ? "No completion status" : database.CompletionStatuses?.Get(id)?.Name ?? "Unknown status"); break;
                case StatisticsBreakdown.Games:
                    title = "By game"; slices = totals.GamePlaytime.Select(pair => new Slice { Key = pair.Key.ToString(), Name = totals.GameNames[pair.Key] ?? "Unnamed game", Value = time ? (double)pair.Value : 1 }).ToList(); break;
                case StatisticsBreakdown.PlaytimeRanges:
                    title = "By recorded playtime";
                    var ranges = new[] { "No playtime", "Under 1 hour", "1 to under 10 hours", "10 to under 50 hours", "50 to under 100 hours", "100+ hours" };
                    for(var i = 0; i < ranges.Length; i++) slices.Add(new Slice { Name = ranges[i], Value = time ? (double)totals.PlaytimeRangeSeconds[i] : totals.PlaytimeRangeCounts[i] }); break;
                default:
                    title = "Installed / not installed"; slices.Add(new Slice { Name = "Installed", Value = time ? (double)totals.InstalledPlaytime : totals.Installed }); slices.Add(new Slice { Name = "Not installed", Value = time ? (double)(totals.PlaytimeSeconds - totals.InstalledPlaytime) : totals.Total - totals.Installed }); break;
            }
            slices = slices.Where(item => item.Value > 0).OrderByDescending(item => item.Value).ThenBy(item => item.Name).ToList();
            if(Settings.GroupLimit > 0 && slices.Count > Settings.GroupLimit)
            {
                var rest = slices.Skip(Settings.GroupLimit).Sum(item => item.Value);
                slices = slices.Take(Settings.GroupLimit).ToList(); slices.Add(new Slice { Name = "Other", Value = rest });
            }
            var heading = Text(string.IsNullOrWhiteSpace(Settings.ChartTitle) ? (time ? "Playtime: " : "Games: ")+title : Settings.ChartTitle,Settings.LabelSize + 2); heading.Margin = new Thickness(0,Settings.Spacing,0,Settings.Spacing); parent.Children.Add(heading);
            if(slices.Count == 0) { parent.Children.Add(Text(time ? "No recorded playtime for matching games" : "No matching games",Settings.LabelSize)); return; }
            double sum = slices.Sum(item => item.Value);
            var colors = new[] { Settings.ChartColor1,Settings.ChartColor2,Settings.ChartColor3,Settings.ChartColor4,Settings.ChartColor5,Settings.ChartColor6 };
            var defaultColors = new[] { "#42A5F5", "#66BB6A", "#FFA726", "#AB47BC", "#EF5350", "#26C6DA" };
            Func<int,Brush> brush = index => (Brush)new BrushConverter().ConvertFromString((Settings.CustomChartColors ? colors : defaultColors)[index % colors.Length]);
            foreach(var slice in slices) LabelOffset(slice);
            var plot = new Controls.LibraryChartPlot(Settings,slices.Select(slice => slice.Name).ToList(),
                slices.Select(slice => time ? slice.Value/3600 : slice.Value).ToList(),brush,index => Label(slices[index],sum),
                index => LabelText(string.Empty),(text,index) => { var offset = Offset(slices[index]); text.RenderTransform = new TranslateTransform(offset.X,offset.Y); DraggableLabel(text,slices[index]); })
            { Height = Settings.ChartHeight*Settings.ChartScale, HorizontalAlignment = HorizontalAlignment.Stretch };
            if(Settings.ChartWidth > 0) plot.Width = Settings.ChartWidth*Settings.ChartScale;
            activePlot = plot;
            plot.Name = "StatisticsPlot";
            FrameworkElement framedPlot = ResizeFrame(plot);
            if(Settings.ShowLegend && Settings.LabelPosition == StatisticsLabelPosition.Legend)
            {
                var side = Settings.LegendPosition == StatisticsLegendPosition.Left || Settings.LegendPosition == StatisticsLegendPosition.Right;
                var legend = new WrapPanel { Name = "ChartLegend", Orientation = side || !Settings.LegendHorizontal ? Orientation.Vertical : Orientation.Horizontal,
                    HorizontalAlignment = Settings.LegendAlignment, VerticalAlignment = Settings.LegendVerticalAlignment, Margin = new Thickness(Settings.Spacing) };
                Grid.SetIsSharedSizeScope(legend,true);
                for(var i = 0; i < slices.Count; i++)
                {
                    var index = i; var slice = slices[i];
                    var entry = new Grid { Margin = new Thickness(4,5,4,5), ToolTip = Label(slice,sum), Background = Brushes.Transparent };
                    entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto,SharedSizeGroup = "SeriesName" });
                    entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto,SharedSizeGroup = "SeriesValue" });
                    entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto,SharedSizeGroup = "SeriesPercent" });
                    var swatch = new Rectangle { Width = 10,Height = 10,Fill = brush(i),Margin = new Thickness(0,0,8,0),VerticalAlignment = VerticalAlignment.Center }; entry.Children.Add(swatch);
                    var text = LabelText(slice.Name); text.TextAlignment = TextAlignment.Left; text.MaxWidth = Settings.LabelWidth; text.Margin = new Thickness(0,0,12,0);
                    var offset = Offset(slice); text.RenderTransform = new TranslateTransform(offset.X,offset.Y); DraggableLabel(text,slice); Grid.SetColumn(text,1); entry.Children.Add(text);
                    var value = LabelText(time ? (slice.Value/3600).ToString("N1")+" h" : slice.Value.ToString("N0")); Grid.SetColumn(value,2); value.TextAlignment = TextAlignment.Right; entry.Children.Add(value);
                    if(Settings.ShowPercentages) { var percent = LabelText((slice.Value/sum).ToString("P1")); percent.Margin = new Thickness(12,0,0,0); Grid.SetColumn(percent,3); percent.TextAlignment = TextAlignment.Right; entry.Children.Add(percent); }
                    entry.MouseEnter += (sender,args) => plot.Highlight(index,true); entry.MouseLeave += (sender,args) => plot.Highlight(index,false);
                    legend.Children.Add(entry);
                }
                FrameworkElement legendHost = legend;
                if(side) legendHost = new ScrollViewer { Content = legend,MaxHeight = Settings.ChartHeight*Settings.ChartScale,VerticalScrollBarVisibility = ScrollBarVisibility.Auto,HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                var dock = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(legendHost,Settings.LegendPosition == StatisticsLegendPosition.Top ? Dock.Top : Settings.LegendPosition == StatisticsLegendPosition.Left ? Dock.Left : Settings.LegendPosition == StatisticsLegendPosition.Right ? Dock.Right : Dock.Bottom);
                dock.Children.Add(legendHost); dock.Children.Add(framedPlot); parent.Children.Add(dock);
            }
            else parent.Children.Add(framedPlot);
            if(Settings.Breakdown == StatisticsBreakdown.Platforms || Settings.Breakdown == StatisticsBreakdown.Genres) parent.Children.Add(Text("Games can belong to multiple groups; percentages use group memberships.",Settings.LabelSize));
        }
        private ChartLabelOffset LabelOffset(Slice slice)
        {
            var key = Settings.Breakdown+":"+(slice.Key ?? slice.Name);
            var offset = Settings.LabelOffsets.FirstOrDefault(item => item.Key == key);
            if(offset == null) { offset = new ChartLabelOffset { Key = key,Name = slice.Name }; Settings.LabelOffsets.Add(offset); }
            return offset;
        }
        private Point Offset(Slice slice) { var point = LabelOffset(slice); return new Point(Settings.LabelOffsetX+point.X,Settings.LabelOffsetY+point.Y); }
        private void DraggableLabel(TextBlock text,Slice slice)
        {
            if(labelPlacements.ContainsKey(text)) return;
            labelPlacements[text] = new LabelPlacement { Slice = slice,Offset = Offset(slice) };
            WindowChrome.SetIsHitTestVisibleInChrome(text,true); RefreshEditingVisuals();
            bool dragging = false; Point start = new Point(); double initialX = 0,initialY = 0; Transform initialTransform = null;
            var watch = new System.Diagnostics.Stopwatch(); int startingRenderCount = 0;
            Action finish = () => {
                if(!dragging) return;
                dragging = false; text.ReleaseMouseCapture(); FinishManipulation(); watch.Stop();
                ExtensionDiagnostics.RecordAsync(LandingPageExtension.Instance,$"Chart label drag completed: chart={Settings.ChartName}; label={slice.Name}; elapsedMs={watch.ElapsedMilliseconds}; chartRebuilds={renderCount-startingRenderCount}; deferredRebuild={queued}.");
            };
            text.PreviewMouseLeftButtonDown += (sender,args) => {
                if(!IsChartEditing) return;
                startingRenderCount = renderCount; watch.Restart();
                manipulating = dragging = true; activeManipulation = text; var offset = LabelOffset(slice); initialX = offset.X; initialY = offset.Y;
                start = args.GetPosition(this); initialTransform = text.RenderTransform?.CloneCurrentValue(); text.CaptureMouse(); args.Handled = true;
            };
            text.PreviewMouseMove += (sender,args) => {
                if(!dragging) return;
                if(args.LeftButton != MouseButtonState.Pressed) { finish(); return; }
                var point = args.GetPosition(this); var dx = point.X-start.X; var dy = point.Y-start.Y; var offset = LabelOffset(slice); offset.X = initialX+dx; offset.Y = initialY+dy;
                var transform = new TransformGroup(); if(initialTransform != null) transform.Children.Add(initialTransform); transform.Children.Add(new TranslateTransform(dx,dy)); text.RenderTransform = transform;
                if(labelPlacements.TryGetValue(text,out var placement)) placement.Offset = Offset(slice);
                args.Handled = true;
            };
            text.PreviewMouseLeftButtonUp += (sender,args) => { if(dragging) { finish(); args.Handled = true; } };
            text.LostMouseCapture += (sender,args) => finish();
        }
        private FrameworkElement ResizeFrame(FrameworkElement plot)
        {
            var frame = new Grid { HorizontalAlignment = double.IsNaN(plot.Width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,Width = plot.Width,Height = plot.Height }; frame.Children.Add(plot);
            var outline = new Border { BorderBrush = Brushes.DodgerBlue,BorderThickness = new Thickness(1),IsHitTestVisible = false }; frame.Children.Add(outline);
            var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetValue(TextBlock.TextProperty,"\u25e2"); text.SetValue(TextBlock.ForegroundProperty,Brushes.White); text.SetValue(TextBlock.BackgroundProperty,Brushes.DodgerBlue);
            var grip = new Thumb { Name = "ChartResizeHandle",Width = 20,Height = 20,HorizontalAlignment = HorizontalAlignment.Right,VerticalAlignment = VerticalAlignment.Bottom,Cursor = Cursors.SizeNWSE,ToolTip = "Drag to resize chart",Template = new ControlTemplate(typeof(Thumb)) { VisualTree = text } };
            editOutline = outline; resizeGrip = grip; RefreshEditingVisuals();
            WindowChrome.SetIsHitTestVisibleInChrome(grip,true); frame.Children.Add(grip); Panel.SetZIndex(grip,20);
            double width = 0,height = 0,dx = 0,dy = 0; Point pointerStart = new Point();
            grip.DragStarted += (sender,args) => { manipulating = true; activeManipulation = grip; width = Settings.ChartWidth > 0 ? Settings.ChartWidth : Math.Max(40,plot.ActualWidth/Settings.ChartScale); height = Settings.ChartHeight; dx = dy = 0; pointerStart = Mouse.GetPosition(this); };
            grip.DragDelta += (sender,args) => {
                if(grip.IsMouseCaptured) { var pointer = Mouse.GetPosition(this); dx = 2*(pointer.X-pointerStart.X); dy = pointer.Y-pointerStart.Y; } else { dx += args.HorizontalChange; dy += args.VerticalChange; }
                Settings.ChartWidth = Math.Max(40,width+dx/Settings.ChartScale); Settings.ChartHeight = Math.Max(60,height+dy/Settings.ChartScale);
                frame.Width = Settings.ChartWidth*Settings.ChartScale; frame.Height = Settings.ChartHeight*Settings.ChartScale;
                plot.RenderTransform = new ScaleTransform(Settings.ChartWidth/Math.Max(1,width),Settings.ChartHeight/Math.Max(1,height)); plot.RenderTransformOrigin = new Point(0,0);
            };
            grip.DragCompleted += (sender,args) => { FinishManipulation(); Queue(); };
            return frame;
        }
        private static int Missing(Dictionary<Guid,int> counts) => counts.TryGetValue(Guid.Empty,out var value) ? value : 0;
        private static int DistinctCount(Dictionary<Guid,int> counts) => counts.Keys.Count(id => id != Guid.Empty);
        private static string Percent(decimal count,int total) => (total == 0 ? 0 : count/total).ToString("P1");
        private string AdditionalValue(StatisticsCounter counter,LibraryStatisticsTotals totals)
        {
            decimal number;
            switch(counter)
            {
                case StatisticsCounter.NotInstalled: number = totals.Total-totals.Installed; break;
                case StatisticsCounter.Hidden: number = totals.Hidden; break;
                case StatisticsCounter.Favorites: number = totals.Favorites; break;
                case StatisticsCounter.SourceCount: number = DistinctCount(totals.Sources); break;
                case StatisticsCounter.PlatformCount: number = DistinctCount(totals.Platforms); break;
                case StatisticsCounter.GenreCount: number = DistinctCount(totals.Genres); break;
                case StatisticsCounter.CategoryCount: number = DistinctCount(totals.Categories); break;
                case StatisticsCounter.TagCount: number = DistinctCount(totals.Tags); break;
                case StatisticsCounter.FeatureCount: number = DistinctCount(totals.Features); break;
                case StatisticsCounter.DeveloperCount: number = DistinctCount(totals.Developers); break;
                case StatisticsCounter.PublisherCount: number = DistinctCount(totals.Publishers); break;
                case StatisticsCounter.StatusCount: number = DistinctCount(totals.CompletionStatuses); break;
                case StatisticsCounter.PlayedIncludingDates: number = totals.Total-totals.Unplayed; break;
                case StatisticsCounter.NeverLaunched: number = totals.NeverLaunched; break;
                case StatisticsCounter.TotalLaunches: number = totals.TotalLaunches; break;
                case StatisticsCounter.NoSource: number = Missing(totals.Sources); break;
                case StatisticsCounter.NoPlatform: number = Missing(totals.Platforms); break;
                case StatisticsCounter.NoGenre: number = Missing(totals.Genres); break;
                case StatisticsCounter.NoCategory: number = Missing(totals.Categories); break;
                case StatisticsCounter.NoTags: number = Missing(totals.Tags); break;
                case StatisticsCounter.NoFeatures: number = Missing(totals.Features); break;
                case StatisticsCounter.NoCompletionStatus: number = Missing(totals.CompletionStatuses); break;
                case StatisticsCounter.MissingCover: number = totals.MissingCover; break;
                case StatisticsCounter.MissingBackground: number = totals.MissingBackground; break;
                case StatisticsCounter.MissingIcon: number = totals.MissingIcon; break;
                case StatisticsCounter.MissingDescription: number = totals.MissingDescription; break;
                case StatisticsCounter.NoReleaseDate: number = totals.NoReleaseDate; break;
                case StatisticsCounter.InstalledPercent: return Percent(totals.Installed,totals.Total);
                case StatisticsCounter.PlayedPercent: return Percent(totals.Total-totals.Unplayed,totals.Total);
                case StatisticsCounter.CompletedPercent: return Percent(totals.Completed,totals.Total);
                case StatisticsCounter.FavoritesPercent: return Percent(totals.Favorites,totals.Total);
                case StatisticsCounter.MedianPlaytime: return PeriodPlaytime.Hours(totals.MedianPlaytimeSeconds);
                case StatisticsCounter.LongestPlaytime: return PeriodPlaytime.Hours(totals.LongestPlaytimeSeconds);
                case StatisticsCounter.AverageAllPlaytime: return PeriodPlaytime.Hours(totals.Total == 0 ? 0 : totals.PlaytimeSeconds/totals.Total);
                case StatisticsCounter.InstalledPlaytime: return PeriodPlaytime.Hours(totals.InstalledPlaytime);
                case StatisticsCounter.CompletedPlaytime: return PeriodPlaytime.Hours(totals.CompletedPlaytime);
                case StatisticsCounter.UninstalledPlaytime: return PeriodPlaytime.Hours(totals.PlaytimeSeconds-totals.InstalledPlaytime);
                case StatisticsCounter.MostPlayedGame: return totals.LongestPlaytimeSeconds == 0 ? "No recorded playtime" : totals.MostPlayedGameName ?? "Unnamed game";
                case StatisticsCounter.LargestSource:
                    var largest = totals.Sources.Where(pair => pair.Key != Guid.Empty).OrderByDescending(pair => pair.Value).ThenBy(pair => LandingPageExtension.Instance.PlayniteApi.Database.Sources?.Get(pair.Key)?.Name).FirstOrDefault();
                    return largest.Key == Guid.Empty ? "No named sources" : (LandingPageExtension.Instance.PlayniteApi.Database.Sources?.Get(largest.Key)?.Name ?? "Unknown source")+" ("+largest.Value.ToString("N0")+")";
                default: return "Unavailable";
            }
            return number.ToString("N0");
        }
        private void AddStatisticGroup(Panel parent,StatisticGroupOption option,LibraryStatisticsTotals totals)
        {
            Dictionary<Guid,int> counts; Dictionary<Guid,decimal> times; Func<Guid,string> name;
            var database = LandingPageExtension.Instance.PlayniteApi.Database;
            switch(option.Group)
            {
                case StatisticGroup.Sources: counts = totals.Sources; times = totals.SourcePlaytime; name = id => id == Guid.Empty ? "Playnite / no source" : database.Sources?.Get(id)?.Name ?? "Unknown sources"; break;
                case StatisticGroup.Platforms: counts = totals.Platforms; times = totals.PlatformPlaytime; name = id => id == Guid.Empty ? "No platform" : database.Platforms?.Get(id)?.Name ?? "Unknown platforms"; break;
                case StatisticGroup.Genres: counts = totals.Genres; times = totals.GenrePlaytime; name = id => id == Guid.Empty ? "No genre" : database.Genres?.Get(id)?.Name ?? "Unknown genres"; break;
                case StatisticGroup.Categories: counts = totals.Categories; times = totals.CategoryPlaytime; name = id => id == Guid.Empty ? "No category" : database.Categories?.Get(id)?.Name ?? "Unknown categories"; break;
                case StatisticGroup.Tags: counts = totals.Tags; times = totals.TagPlaytime; name = id => id == Guid.Empty ? "No tags" : database.Tags?.Get(id)?.Name ?? "Unknown tags"; break;
                case StatisticGroup.Features: counts = totals.Features; times = totals.FeaturePlaytime; name = id => id == Guid.Empty ? "No features" : database.Features?.Get(id)?.Name ?? "Unknown features"; break;
                case StatisticGroup.Developers: counts = totals.Developers; times = totals.DeveloperPlaytime; name = id => id == Guid.Empty ? "No developer" : database.Companies?.Get(id)?.Name ?? "Unknown developers"; break;
                case StatisticGroup.Publishers: counts = totals.Publishers; times = totals.PublisherPlaytime; name = id => id == Guid.Empty ? "No publisher" : database.Companies?.Get(id)?.Name ?? "Unknown publishers"; break;
                case StatisticGroup.CompletionStatuses: counts = totals.CompletionStatuses; times = totals.CompletionPlaytime; name = id => id == Guid.Empty ? "No completion status" : database.CompletionStatuses?.Get(id)?.Name ?? "Unknown completionstatuses"; break;
                default: return;
            }
            var time = option.Metric == StatisticsMetric.Playtime;
            var selected = option.SupportsSelection && option.SelectedOnly;
            var ids = selected ? new[] { option.SelectedId } : counts.Keys.Where(id => option.IncludeUnassigned || id != Guid.Empty);
            var rows = ids.Select(id => new { Id = id,Name = name(id),Value = time ? (times.TryGetValue(id,out var seconds) ? seconds : 0) : (counts.TryGetValue(id,out var count) ? count : 0) }).Where(item => selected || item.Value > 0).OrderByDescending(item => item.Value).ThenBy(item => item.Name).ToList();
            var total = counts.Keys.Where(id => selected || option.IncludeUnassigned || id != Guid.Empty).Sum(id => time ? times[id] : counts[id]);
            var block = new StackPanel { Margin = new Thickness(Settings.Spacing/2),Tag = option.Counter };
            var heading = LabelText((time ? "Playtime " : "Games ")+option.Label.ToLowerInvariant()); heading.FontWeight = FontWeights.Bold; heading.Margin = new Thickness(0,0,0,Settings.Spacing/2); if(option.ShowHeading) { ApplyCounterColors(option.Counter,null,heading); block.Children.Add(heading); }
            Panel entries = Settings.Rows ? (Panel)new StackPanel() : new WrapPanel(); block.Children.Add(entries);
            if(!(parent is Grid)) block.Width = Math.Max(Settings.TileWidth,Math.Min(ActualWidth > 0 ? ActualWidth-24 : Settings.TileWidth*3,Settings.TileWidth*3+Settings.Spacing*3));
            foreach(var row in option.Limit > 0 ? rows.Take(option.Limit) : rows)
            {
                Add(entries,row.Name,(time ? PeriodPlaytime.Hours(row.Value) : row.Value.ToString("N0"))+(option.ShowPercentages && total > 0 ? " ("+(row.Value/total).ToString("P1")+")" : ""),colorCounter: option.Counter);
                var tile = (StackPanel)entries.Children[entries.Children.Count-1]; tile.ToolTip = row.Name;
                var caption = tile.Children.OfType<TextBlock>().First(item => item.Name == "StatisticLabel");
                caption.Visibility = option.ShowName ? Visibility.Visible : Visibility.Collapsed;
                if(option.SupportsSelection && option.ShowIcon)
                {
                    var badge = new StackPanel { Name = "StatisticSourceBadge",Orientation = Orientation.Horizontal,HorizontalAlignment = Settings.Alignment == TextAlignment.Left ? HorizontalAlignment.Left : Settings.Alignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Center,ToolTip = row.Name };
                    var index = tile.Children.IndexOf(caption); tile.Children.Remove(caption);
                    var image = StatisticIcon(option.Group,row.Id,row.Name,totals);
                    if(image != null) badge.Children.Add(new Image { Source = image,Width = option.IconSize,Height = option.IconSize,Stretch = Stretch.Uniform,VerticalAlignment = VerticalAlignment.Center,Margin = new Thickness(0,0,option.ShowName ? 6 : 0,0) });
                    else { var fallback = LabelText("\u25a3"); fallback.FontSize = option.IconSize; fallback.ToolTip = row.Name+" (no icon available)"; badge.Children.Add(fallback); }
                    badge.Children.Add(caption); tile.Children.Insert(index,badge);
                }
            }
            if(rows.Count == 0) block.Children.Add(LabelText(time ? "No recorded playtime" : "No matching groups"));
            PlaceCounter(parent,block,option.Counter); parent.Children.Add(block);
        }
        private ImageSource StatisticIcon(StatisticGroup group,Guid id,string name,LibraryStatisticsTotals totals)
        {
            try
            {
                var api = LandingPageExtension.Instance.PlayniteApi;
                if(group == StatisticGroup.Sources && id == Guid.Empty) return ShelfGameInfo.LoadPlayniteIcon(api.Paths.ApplicationPath);
                string path = null;
                if(group == StatisticGroup.Platforms) path = api.Database.Platforms?.Get(id)?.Icon;
                else
                {
                    totals.SourcePluginIds.TryGetValue(id,out var pluginId);
                    var libraries = api.Addons?.Plugins?.OfType<Playnite.SDK.Plugins.LibraryPlugin>();
                    var library = libraries?.FirstOrDefault(item => pluginId != Guid.Empty && item.Id == pluginId)
                        ?? libraries?.FirstOrDefault(item => !string.IsNullOrWhiteSpace(name) && item.Name?.IndexOf(name,StringComparison.OrdinalIgnoreCase) >= 0);
                    path = library?.LibraryIcon;
                }
                if(string.IsNullOrWhiteSpace(path)) return null;
                if(statisticIcons.TryGetValue(path,out var cached)) return cached;
                var source = new Converters.UriToBitmapImageConverter().Convert(path,typeof(ImageSource),"128",System.Globalization.CultureInfo.CurrentCulture) as ImageSource;
                if(source != null) statisticIcons[path] = source;
                return source;
            }
            catch(Exception ex) { LandingPageExtension.logger.Debug(ex,"Unable to load a statistic source/platform icon."); return null; }
        }
        private string Label(Slice slice,double sum) => slice.Name + "  " + (Settings.Metric == StatisticsMetric.Playtime ? (slice.Value/3600).ToString("N1")+" h" : slice.Value.ToString("N0")) + (Settings.ShowPercentages ? " ("+(slice.Value/sum).ToString("P1")+")" : string.Empty);
        private void Add(Panel parent,string label,string value,StatisticsCounter? counter = null,StatisticsCounter? colorCounter = null)
        {
            var inline = Settings.Rows && !(parent is Grid);
            var tile = new StackPanel { Margin = new Thickness(Settings.Spacing/2), Orientation = inline ? Orientation.Horizontal : Orientation.Vertical };
            if(!Settings.Rows && !(parent is Grid)) tile.Width = Settings.TileWidth;
            var number = Text(value,Settings.ValueSize); var caption = LabelText(label);
            number.Name = "StatisticValue"; caption.Name = "StatisticLabel";
            ApplySizePreset(number,Settings.ValueSizePreset);
            if(!string.IsNullOrWhiteSpace(Settings.ValueFontFamily)) number.FontFamily = new FontFamily(Settings.ValueFontFamily);
            if(Settings.ValueBold) number.FontWeight = FontWeights.Bold;
            if(Settings.ValueItalic) number.FontStyle = FontStyles.Italic;
            SetDecorations(number,Settings.ValueUnderline,Settings.ValueStrikeThrough);
            number.Opacity = Settings.ValueOpacity;
            var customValueColor = Settings.ValueColorMode == StatisticsValueColor.Custom || (Settings.ValueColorMode == StatisticsValueColor.MatchLabels && Settings.CustomLabelColor);
            if(customValueColor) { number.Foreground = (Brush)new BrushConverter().ConvertFromString(Settings.ValueColorMode == StatisticsValueColor.Custom ? Settings.ValueColor : Settings.LabelColor); Controls.WidgetPalette.SetPreserveTextColor(number,true); }
            ApplyCounterColors(counter ?? colorCounter,number,caption);
            number.TextWrapping = caption.TextWrapping = Settings.WrapCounterText ? TextWrapping.Wrap : TextWrapping.NoWrap;
            number.TextTrimming = caption.TextTrimming = Settings.CounterTextTrimming;
            if(inline) { caption.Width = Settings.TileWidth; caption.VerticalAlignment = VerticalAlignment.Center; tile.Children.Add(caption); tile.Children.Add(number); }
            else { tile.Children.Add(number); tile.Children.Add(caption); }
            PlaceCounter(parent,tile,counter);
            parent.Children.Add(tile);
        }
        private void ApplyCounterColors(StatisticsCounter? counter,TextBlock value,TextBlock label)
        {
            var colors = counter.HasValue ? Settings.CounterPlacements.FirstOrDefault(item => item?.Counter == counter.Value)?.Colors : null;
            if(colors == null) return;
            if(label != null && !colors.UseGlobalLabelColor) { label.Foreground = (Brush)new BrushConverter().ConvertFromString(colors.LabelColor); Controls.WidgetPalette.SetPreserveTextColor(label,true); }
            if(value == null) return;
            var custom = !colors.UseGlobalValueColor;
            var matchLocalLabel = Settings.ValueColorMode == StatisticsValueColor.MatchLabels && !colors.UseGlobalLabelColor;
            if(custom || matchLocalLabel) { value.Foreground = (Brush)new BrushConverter().ConvertFromString(custom ? colors.ValueColor : colors.LabelColor); Controls.WidgetPalette.SetPreserveTextColor(value,true); }
        }
        private void PlaceCounter(Panel parent,FrameworkElement tile,StatisticsCounter? counter)
        {
            if(parent is Grid grid && counter.HasValue)
            {
                var placement = Settings.CounterPlacements.FirstOrDefault(item => item?.Counter == counter.Value);
                var row = (placement?.Row ?? 1)-1; var col = (placement?.Column ?? 1)-1; var rowSpan = placement?.RowSpan ?? 1; var colSpan = placement?.ColumnSpan ?? 1;
                while(grid.RowDefinitions.Count < row+rowSpan) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                while(grid.ColumnDefinitions.Count < col+colSpan) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
                Grid.SetRow(tile,row); Grid.SetColumn(tile,col); Grid.SetRowSpan(tile,rowSpan); Grid.SetColumnSpan(tile,colSpan); tile.Tag = counter.Value;
            }
        }
        private TextBlock LabelText(string value)
        {
            var text = Text(value,Settings.LabelSize);
            if(!string.IsNullOrWhiteSpace(Settings.LabelFontFamily)) text.FontFamily = new FontFamily(Settings.LabelFontFamily);
            text.FontWeight = Settings.LabelBold ? FontWeights.Bold : FontWeights.Normal;
            text.FontStyle = Settings.LabelItalic ? FontStyles.Italic : FontStyles.Normal;
            SetDecorations(text,Settings.LabelUnderline,!chartOnly && Settings.LabelStrikeThrough);
            if(!chartOnly)
            {
                ApplySizePreset(text,Settings.LabelSizePreset); text.Opacity = Settings.LabelOpacity;
                if(Settings.LabelLineHeight > 0) { text.LineHeight = Settings.LabelLineHeight; text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight; }
            }
            if(Settings.CustomLabelColor) { text.Foreground = (Brush)new BrushConverter().ConvertFromString(Settings.LabelColor); Controls.WidgetPalette.SetPreserveTextColor(text,true); }
            return text;
        }
        private static void SetDecorations(TextBlock text,bool underline,bool strikeThrough)
        {
            if(!underline && !strikeThrough) return;
            var decorations = new TextDecorationCollection();
            if(underline) foreach(var item in TextDecorations.Underline) decorations.Add(item);
            if(strikeThrough) foreach(var item in TextDecorations.Strikethrough) decorations.Add(item);
            text.TextDecorations = decorations;
        }
        private static void ApplySizePreset(TextBlock text,StatisticsTextSize preset)
        {
            var key = preset == StatisticsTextSize.ThemeSmall ? "FontSizeSmall" : preset == StatisticsTextSize.ThemeNormal ? "FontSize" : preset == StatisticsTextSize.ThemeLarge ? "FontSizeLarge" : preset == StatisticsTextSize.ThemeLarger ? "FontSizeLarger" : preset == StatisticsTextSize.ThemeLargest ? "FontSizeLargest" : null;
            if(key != null) text.SetResourceReference(TextBlock.FontSizeProperty,key);
        }
        private TextBlock Text(string value,double size)
        {
            var text = new TextBlock { Text = value, FontSize = size, TextAlignment = Settings.Alignment, TextWrapping = TextWrapping.Wrap };
            text.SetResourceReference(StyleProperty,"BaseTextBlockStyle"); text.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            if(!chartOnly && !Settings.InheritTextRendering) { TextOptions.SetTextFormattingMode(text,Settings.TextFormatting); TextOptions.SetTextRenderingMode(text,Settings.TextRendering); }
            return text;
        }
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnViewShownAsync() { Show(); return Task.CompletedTask; }
        public Task OnViewHiddenAsync() { Hide(); return Task.CompletedTask; }
        public Task OnDayChangedAsync(DateTime time) { periodsDirty = true; Queue(); return Task.CompletedTask; }
        public void OnViewClosed()
        {
            closed = true; Hide(); UnwatchStatisticOptions();
            foreach(var color in colorSubscriptions) color.PropertyChanged -= PlacementChanged; colorSubscriptions.Clear(); statisticIcons.Clear(); foreach(var child in extraViews.Values) child.OnViewClosed(); extraViews.Clear();
            if(observed != null) observed.PropertyChanged -= OptionsChanged;
            if(observedCharts != null) observedCharts.CollectionChanged -= LayoutCollectionChanged;
            if(observedPlacements != null) observedPlacements.CollectionChanged -= LayoutCollectionChanged;
            foreach(var placement in placementSubscriptions) placement.PropertyChanged -= PlacementChanged; placementSubscriptions.Clear();
            if(observedLabelOffsets != null) observedLabelOffsets.CollectionChanged -= LayoutCollectionChanged;
            foreach(var label in labelSubscriptions) label.PropertyChanged -= PlacementChanged; labelSubscriptions.Clear();
        }
    }
}
