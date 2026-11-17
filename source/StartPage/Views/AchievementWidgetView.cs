using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LandingPage.Models;
using LandingPage.Settings;
using StartPage.SDK.Async;

namespace LandingPage.Views
{
    public sealed class AchievementWidgetView : UserControl, IAsyncStartPageControl, IStartPageViewModel
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings),typeof(AchievementWidgetSettings),typeof(AchievementWidgetView),new PropertyMetadata(null,SettingsChanged));
        public AchievementWidgetSettings Settings { get => (AchievementWidgetSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty,value); }
        private readonly string widgetId;
        private readonly RecentAchievementsView recentView;
        private bool IsRecent => widgetId == "RecentAchivements";
        private bool IsList => widgetId == "GameAchievementList" || IsRecent;
        private long listRequest, styleRevision;
        private string listSignature;
        private readonly ScrollViewer viewport = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        private readonly DispatcherTimer timer;
        private readonly DispatcherTimer editTimer;
        private AchievementWidgetSettings observed;
        private LandingPageExtension context;
        private System.Collections.ObjectModel.ObservableCollection<AchievementGoal> observedCollection;
        private readonly List<AchievementTextStyle> observedTextStyles = new List<AchievementTextStyle>();
        private readonly List<AchievementGoal> observedGoals = new List<AchievementGoal>();
        private bool visible, closed;
        private Task pending = Task.CompletedTask;
        private Dictionary<Guid,ShelfAchievementProgress> progress = new Dictionary<Guid,ShelfAchievementProgress>();
        private List<Playnite.SDK.Models.Game> games = new List<Playnite.SDK.Models.Game>();
        private string fingerprint;
        private readonly Dictionary<string,ImageSource> images = new Dictionary<string,ImageSource>();
        public AchievementWidgetView(string id)
        {
            widgetId = id;
            if (IsRecent) { recentView = new RecentAchievementsView(); Content = recentView; } else Content = viewport;
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(this,true);
            timer = new DispatcherTimer(DispatcherPriority.Background,Dispatcher) { Interval = TimeSpan.FromSeconds(10) };
            timer.Tick += async (s,e) => await Refresh();
            editTimer = new DispatcherTimer(DispatcherPriority.Background,Dispatcher) { Interval = TimeSpan.FromMilliseconds(60) };
            editTimer.Tick += (s,e) => { editTimer.Stop(); Render(); };
            Settings = AchievementWidgetSettings.Create(id);
            SetBinding(SettingsProperty,new Binding("DataContext.GridNode.ViewProperties.AchievementSettings") {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,typeof(GridNodeView),1), FallbackValue = Settings, TargetNullValue = Settings });
            Loaded += async (s,e) => { if (!closed) { visible = true; Observe(); timer.Start(); await Refresh(); } };
            Unloaded += (s,e) => { visible = false; listRequest++; timer.Stop(); editTimer.Stop(); Unobserve(); };
        }
        private static void SettingsChanged(DependencyObject sender,DependencyPropertyChangedEventArgs e)
        {
            var view = (AchievementWidgetView)sender;
            view.styleRevision++;
            view.Unobserve();
            if (view.visible) { view.Observe(); view.Render(); }
        }
        private void Observe()
        {
            Unobserve(); observed = Settings;
            if (observed == null) return;
            if (widgetId == "AchievementGoals" || (IsList && !IsRecent)) {
                context = LandingPageExtension.Instance;
                if (context != null) { context.FeaturedSelectionChanged += GameContextChanged; context.FeaturedHoverChanged += GameContextChanged; context.FeaturedFallbackChanged += GameContextChanged; }
            }
            observed.PropertyChanged += Changed;
            foreach (var style in observed.TextStyles.Values) { style.PropertyChanged += Changed; observedTextStyles.Add(style); }
            observedCollection = observed.Goals; observedCollection.CollectionChanged += GoalsChanged;
            foreach (var goal in observed.Goals) { goal.PropertyChanged += Changed; observedGoals.Add(goal); }
        }
        private void Unobserve()
        {
            if (context != null) { context.FeaturedSelectionChanged -= GameContextChanged; context.FeaturedHoverChanged -= GameContextChanged; context.FeaturedFallbackChanged -= GameContextChanged; context = null; }
            if (observed != null) observed.PropertyChanged -= Changed;
            if (observedCollection != null) observedCollection.CollectionChanged -= GoalsChanged; observedCollection = null;
            foreach (var style in observedTextStyles) style.PropertyChanged -= Changed; observedTextStyles.Clear();
            foreach (var goal in observedGoals) goal.PropertyChanged -= Changed;
            observedGoals.Clear(); observed = null;
        }
        private Guid? ResolveContextGame(LandingPageExtension plugin)
        {
            var hovered = plugin?.FeaturedHoveredGame;
            if (hovered == null && Settings.KeepLastHoveredGame
                && (Settings.GameSource == AchievementGoalSource.HoveredGame || Settings.GameSource == AchievementGoalSource.HoveredOrSelectedGame))
                hovered = plugin?.LastFeaturedHoveredGame;
            return Settings.ResolveGoalGame(plugin?.FeaturedSelectedGame?.Id ?? plugin?.PlayniteApi.MainView?.SelectedGames?.FirstOrDefault()?.Id,hovered?.Id);
        }
        private void GameContextChanged(object sender,EventArgs e)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => GameContextChanged(sender,e))); return; }
            if (Settings.GameSource != AchievementGoalSource.PinnedGames) ScheduleRender();
        }
        private void GoalsChanged(object sender,NotifyCollectionChangedEventArgs e) { styleRevision++; Observe(); ScheduleRender(); }
        private void Changed(object sender,PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => Changed(sender,e))); return; }
            styleRevision++;
            if (e.PropertyName == nameof(AchievementWidgetSettings.Goals) || e.PropertyName == nameof(AchievementWidgetSettings.TextStyles)) Observe();
            ScheduleRender();
        }
        private void ScheduleRender() { if (visible && !closed) { editTimer.Stop(); editTimer.Start(); } }
        private Task Refresh()
        {
            if (!visible || closed) return Task.CompletedTask;
            if (!pending.IsCompleted) return pending;
            return pending = RefreshCore();
        }
        private async Task RefreshCore()
        {
            try {
                var plugin = LandingPageExtension.Instance;
                if (IsList) { await RefreshList(); return; }
                var data = await ShelfAchievements.GetSnapshotAsync(plugin.Settings.SelectedAchievementsProvider,plugin.PlayniteApi.Paths.ExtensionsDataPath);
                if (!visible || closed) return;
                var library = plugin.PlayniteApi.Database.Games.ToList();
                var key = plugin.Settings.SelectedAchievementsProvider + "|" + string.Join(";",library.Select(game =>
                    game.Id + ":" + game.Name + ":" + game.Hidden + ":" + game.IsInstalled + ":" + game.Favorite + ":" + game.LastActivity + ":" + game.CoverImage))
                    + "|" + string.Join(";",data.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value.Unlocked + "/" + pair.Value.Total));
                progress = data; games = library;
                if (fingerprint != key) { fingerprint = key; Render(); }
            } catch (Exception ex) {
                LandingPageExtension.logger?.Warn(ex,"Unable to update achievement widget.");
                fingerprint = null;
                if (visible && !closed) viewport.Content = Text("Achievement data could not be read. Check the selected provider in StartPage settings.",false);
            }
        }
        private void Render()
        {
            if (Settings == null || closed || !visible) return;
            if (IsList) { _ = RefreshList(); return; }
            var options = Settings;
            var candidates = games.Where(game => (options.IncludeHidden || !game.Hidden) && (!options.InstalledOnly || game.IsInstalled)
                && (!options.FavoritesOnly || game.Favorite));
            if (widgetId == "AchievementGoals" || (IsList && !IsRecent)) {
                if (options.GameSource == AchievementGoalSource.PinnedGames) candidates = candidates.Where(game => options.Goals.Any(goal => goal.GameId == game.Id));
                else {
                    var plugin = LandingPageExtension.Instance;
                    var selected = ResolveContextGame(plugin);
                    if (!selected.HasValue && plugin != null) selected = plugin.GetPreviewFallback(new FeaturedGameSettings {
                        IncludeHidden = options.IncludeHidden, InstalledOnly = options.InstalledOnly })?.Id;
                    candidates = candidates.Where(game => game.Id == selected);
                }
            }
            candidates = candidates.Where(game => progress.TryGetValue(game.Id,out var data) && data.Total > 0
                && ((widgetId != "AchievementFinishNext" && options.IncludeCompleted) || data.Unlocked < data.Total)
                && 100.0*data.Unlocked/data.Total >= options.MinimumPercent
                && (options.MaximumRemaining == 0 || data.Total-data.Unlocked <= options.MaximumRemaining));
            switch (options.Order) {
                case AchievementGameOrder.RemainingAscending: candidates = candidates.OrderBy(game => progress[game.Id].Total-progress[game.Id].Unlocked).ThenBy(game => game.Name); break;
                case AchievementGameOrder.Name: candidates = candidates.OrderBy(game => game.Name); break;
                case AchievementGameOrder.LastPlayed: candidates = candidates.OrderByDescending(game => game.LastActivity).ThenBy(game => game.Name); break;
                default: candidates = candidates.OrderByDescending(game => (double)progress[game.Id].Unlocked/progress[game.Id].Total).ThenBy(game => game.Name); break;
            }
            if (options.Limit > 0) candidates = candidates.Take(options.Limit);
            Panel panel = options.Layout == AchievementCardLayout.Cards ? (Panel)new WrapPanel() : new StackPanel();
            var count = 0;
            foreach (var game in candidates) { panel.Children.Add(Card(game,progress[game.Id])); count++; }
            if (count == 0) panel.Children.Add(Text(widgetId == "AchievementGoals" && options.GameSource == AchievementGoalSource.PinnedGames && options.Goals.Count == 0
                ? "Add games and their targets in Appearance > Achievement widget settings > Goals."
                : "No matching achievement data. Check the provider in StartPage settings and this widget's filters.",false));
            var offset = viewport.VerticalOffset; viewport.Content = panel; viewport.ScrollToVerticalOffset(offset);
        }
        private static bool MatchesText(string text,string filter) => string.IsNullOrWhiteSpace(filter) || (text ?? "").IndexOf(filter.Trim(),StringComparison.CurrentCultureIgnoreCase) >= 0;
        private sealed class AchievementListRow
        {
            public Guid GameId;
            public string GameName;
            public GameAchievementEntry Entry;
        }
        private sealed class RowConverter : IValueConverter
        {
            private readonly AchievementWidgetView owner;
            internal RowConverter(AchievementWidgetView owner) { this.owner = owner; }
            public object Convert(object value,Type targetType,object parameter,CultureInfo culture) => value is AchievementListRow row ? (row.Entry == null ? (object)owner.Text(row.GameName,true) : owner.AchievementRow(row.GameName,row.Entry)) : null;
            public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture) => throw new NotSupportedException();
        }
        private ListBox CreateVirtualList(List<AchievementListRow> rows)
        {
            var list = new ListBox { ItemsSource = rows, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0) };
            ScrollViewer.SetCanContentScroll(list,true);
            ScrollViewer.SetVerticalScrollBarVisibility(list,ScrollBarVisibility.Auto);
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
            VirtualizingPanel.SetIsVirtualizing(list,true);
            VirtualizingPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
            list.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
            var row = new FrameworkElementFactory(typeof(ContentControl));
            row.SetValue(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch);
            row.SetBinding(ContentControl.ContentProperty,new Binding { Converter = new RowConverter(this) });
            list.ItemTemplate = new DataTemplate { VisualTree = row };
            return list;
        }
        private async Task RefreshList()
        {
            if (!visible || closed || Settings == null) return;
            var request = ++listRequest;
            try {
                var plugin = LandingPageExtension.Instance;
                if (plugin == null) return;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var o = Settings;
                var ids = !IsRecent && o.GameSource == AchievementGoalSource.PinnedGames ? o.Goals.Select(goal => goal.GameId).Distinct().ToList() : new List<Guid>();
                if (!IsRecent && o.GameSource != AchievementGoalSource.PinnedGames) {
                    var id = ResolveContextGame(plugin)
                        ?? plugin.GetPreviewFallback(new FeaturedGameSettings { IncludeHidden = o.IncludeHidden, InstalledOnly = o.InstalledOnly })?.Id;
                    if (id.HasValue) ids.Add(id.Value);
                }
                var selected = (IsRecent ? plugin.PlayniteApi.Database.Games : ids.Select(id => plugin.PlayniteApi.Database.Games.Get(id))).Where(game => game != null
                    && (o.IncludeHidden || !game.Hidden) && (!o.InstalledOnly || game.IsInstalled) && (!o.FavoritesOnly || game.Favorite)
                    && (!IsRecent || (MatchesText(game.Name,o.GameNameFilter) && MatchesText(game.Source?.Name ?? "Playnite",o.SourceNameFilter)
                        && MatchesText(string.Join(", ",game.Platforms?.Select(platform => platform.Name) ?? Enumerable.Empty<string>()),o.PlatformNameFilter)))).ToList();
                List<GameAchievementEntry>[] results;
                if (IsRecent) {
                    var recent = await GameAchievementList.GetRecentAsync(plugin.PlayniteApi.Paths.ExtensionsDataPath,plugin.Settings.SelectedAchievementsProvider,
                        selected.Select(game => game.Id.ToString()).ToArray(),o.Limit,o.PerGameLimit,o.RecentDays);
                    var byGame = recent.ToLookup(entry => entry.GameId,StringComparer.OrdinalIgnoreCase);
                    results = selected.Select(game => byGame[game.Id.ToString()].ToList()).ToArray();
                } else {
                    var tasks = selected.Select(game => GameAchievementList.GetAsync(plugin.PlayniteApi.Paths.ExtensionsDataPath,plugin.Settings.SelectedAchievementsProvider,game.Id)).ToList();
                    results = await Task.WhenAll(tasks);
                }
                var readMs = watch.ElapsedMilliseconds;
                if (!visible || closed || request != listRequest) return;
                var signature = styleRevision + "|" + string.Join(";",selected.Select(game => game.Id + game.Name)) + "|" +
                    string.Join(";",results.SelectMany(entries => entries).Select(row => row.Name + row.Description + row.Unlocked + row.UnlockedOn + row.UnlockedIconPath + row.LockedIconPath));
                if (listSignature == signature) return;
                if (IsRecent) {
                    var summary = await ShelfAchievements.GetSnapshotAsync(plugin.Settings.SelectedAchievementsProvider,plugin.PlayniteApi.Paths.ExtensionsDataPath);
                    if (!visible || closed || request != listRequest) return;
                    listSignature = signature;
                    recentView.SetFeed(selected,results,summary,o); Content = recentView;
                    _ = ExtensionDiagnostics.RecordAsync(plugin,"Recent achievements loaded: entries=" + results.Sum(items => items.Count) + "; readMs=" + readMs + "; readyMs=" + watch.ElapsedMilliseconds + ".");
                    return;
                }
                listSignature = signature;
                var rows = new List<AchievementListRow>();
                var total = 0;
                for (var index = 0; index < selected.Count; index++) {
                    var entries = FilterEntries(results[index],o);
                    foreach (var entry in entries) {
                        if (o.Limit > 0 && total >= o.Limit) break;
                        rows.Add(new AchievementListRow { GameId = selected[index].Id, GameName = selected[index].Name, Entry = entry }); total++;
                    }
                }
                if (total > 0 && o.Layout == AchievementCardLayout.Rows) Content = CreateVirtualList(rows);
                else {
                    Panel panel = new WrapPanel();
                    foreach (var row in rows) panel.Children.Add(AchievementRow(row.GameName,row.Entry));
                    if (total == 0) panel.Children.Add(Text(!IsRecent && o.GameSource == AchievementGoalSource.PinnedGames && ids.Count == 0
                        ? "Choose pinned games in Appearance > Achievement widget settings > Pinned games."
                        : IsRecent ? "No recent achievements match this widget's filters." : "No matching achievements available for this game and filter.",false));
                    var offset = viewport.VerticalOffset; viewport.Content = panel; Content = viewport; viewport.ScrollToVerticalOffset(offset);
                }
                _ = ExtensionDiagnostics.RecordAsync(plugin,(IsRecent ? "Recent achievements loaded: scopeGames=" + selected.Count : "Achievement list loaded: games=" + string.Join(",",selected.Select(game => game.Id)))
                    + "; entries=" + total + "; readMs=" + readMs + "; buildMs=" + (watch.ElapsedMilliseconds-readMs) + "; source=" + o.GameSource + "; keepHover=" + o.KeepLastHoveredGame + ".");
            } catch (Exception ex) {
                if (!visible || closed || request != listRequest) return;
                listSignature = null;
                LandingPageExtension.logger?.Warn(ex,"Unable to read game achievement list.");
                Content = viewport;
                viewport.Content = Text("Achievement list could not be read. Check your selected provider and Playnite log.",false);
            }
        }
        internal static IEnumerable<GameAchievementEntry> FilterEntries(IEnumerable<GameAchievementEntry> entries,AchievementWidgetSettings o)
        {
            entries = entries.Where(entry => o.ListFilter == AchievementListFilter.All || (o.ListFilter == AchievementListFilter.Unlocked ? entry.Unlocked != 0 : entry.Unlocked == 0));
            switch (o.ListOrder) {
                case AchievementListOrder.Name: return entries.OrderBy(entry => entry.Name);
                case AchievementListOrder.RecentUnlock: return entries.OrderByDescending(entry => entry.UnlockedOn).ThenBy(entry => entry.Name);
                case AchievementListOrder.LockedFirst: return entries.OrderBy(entry => entry.Unlocked != 0).ThenBy(entry => entry.Name);
                case AchievementListOrder.UnlockedFirst: return entries.OrderByDescending(entry => entry.Unlocked != 0).ThenBy(entry => entry.Name);
                default: return entries;
            }
        }
        private Border AchievementRow(string gameName,GameAchievementEntry entry)
        {
            var o = Settings;
            var row = new DockPanel();
            if (o.ShowArtwork) {
                var image = new Image { Width = o.ArtworkSize, Height = o.ArtworkSize, Stretch = Stretch.Uniform, Margin = new Thickness(0,0,10,0) };
                var path = entry.Unlocked != 0 ? entry.UnlockedIconPath : entry.LockedIconPath;
                if (string.IsNullOrWhiteSpace(path)) path = entry.UnlockedIconPath;
                try {
                    if (!string.IsNullOrWhiteSpace(path) && Uri.TryCreate(path,UriKind.Absolute,out var uri)) {
                        var key = path + "|achievement|" + (int)o.ArtworkSize;
                        if (!images.TryGetValue(key,out var source)) {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage(); bitmap.BeginInit(); bitmap.DecodePixelWidth = (int)o.ArtworkSize*2;
                            bitmap.UriSource = uri; if (uri.IsFile) bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit(); if (bitmap.CanFreeze) bitmap.Freeze();
                            if (images.Count >= 256) images.Clear(); images[key] = source = bitmap;
                        }
                        image.Source = source;
                    }
                } catch { }
                if (image.Source != null) { DockPanel.SetDock(image,Dock.Left); row.Children.Add(image); }
            }
            var content = new StackPanel();
            if (o.ShowTitle) content.Children.Add(Text(entry.Name ?? "Achievement",true));
            if (o.ShowDescription && !string.IsNullOrEmpty(entry.Description)) content.Children.Add(Text(entry.Description,false,AchievementTextRole.Description));
            if (o.ShowCounts) content.Children.Add(Text(gameName,false,AchievementTextRole.GameName));
            if (o.ShowUnlockState) {
                var state = Text(entry.Unlocked != 0 ? "Unlocked" : "Locked",false,AchievementTextRole.UnlockState);
                if (o.TextStyles[AchievementTextRole.UnlockState.ToString()].UseDefaults || !o.TextStyles[AchievementTextRole.UnlockState.ToString()].UseCustomColor) state.Foreground = Brush(entry.Unlocked != 0 ? o.CompleteColor : o.ProgressColor); content.Children.Add(state);
            }
            if (o.ShowUnlockDate && entry.Unlocked != 0 && entry.UnlockedOn.HasValue) content.Children.Add(Text(entry.UnlockedOn.Value.ToLocalTime().ToString("g"),false,AchievementTextRole.UnlockDate));
            row.Children.Add(content);
            var background = Brush(o.CardColor); background.Opacity = o.BackgroundOpacity;
            var border = new Border { Child = row, Background = background, Padding = new Thickness(o.Padding), Margin = new Thickness(o.Spacing/2),
                BorderBrush = Brush(o.BorderColor), BorderThickness = new Thickness(o.BorderWidth), CornerRadius = new CornerRadius(o.CornerRadius) };
            if (o.Layout == AchievementCardLayout.Cards) border.Width = o.CardWidth;
            return border;
        }
        private Border Card(Playnite.SDK.Models.Game game,ShelfAchievementProgress data)
        {
            var o = Settings;
            var percent = 100.0 * data.Unlocked/data.Total;
            var goal = widgetId == "AchievementGoals" ? o.Goals.FirstOrDefault(item => item.GameId == game.Id) : null;
            var target = goal?.TargetPercent ?? o.TargetPercent;
            var reached = percent >= (widgetId == "AchievementGoals" ? target : 100);
            var accent = Brush(reached ? o.CompleteColor : o.ProgressColor);
            var body = new StackPanel();
            var header = new DockPanel();
            if (o.ShowArtwork) {
                var image = new Image { Width = o.ArtworkSize, Height = o.ArtworkSize, Stretch = Stretch.Uniform,
                    Margin = new Thickness(0,0,10,4), IsHitTestVisible = false };
                try {
                    var path = LandingPageExtension.Instance.PlayniteApi.Database.GetFullFilePath(game.CoverImage);
                    if (!string.IsNullOrEmpty(path)) {
                        var key = path + "|" + (int)o.ArtworkSize;
                        if (!images.TryGetValue(key,out var source)) {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.DecodePixelWidth = (int)o.ArtworkSize*2; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
                            if (images.Count >= 256) images.Clear(); images[key] = source = bitmap;
                        }
                        image.Source = source;
                    }
                } catch { }
                DockPanel.SetDock(image,Dock.Left); header.Children.Add(image);
            }
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (o.ShowTitle) text.Children.Add(Text(game.Name,true));
            var parts = new List<Tuple<string,AchievementTextRole>>();
            if (o.ShowCounts) parts.Add(Tuple.Create(data.Unlocked + " / " + data.Total,AchievementTextRole.Counts));
            if (o.ShowPercent) parts.Add(Tuple.Create(percent.ToString("0.#",CultureInfo.CurrentCulture) + "%",AchievementTextRole.Percentage));
            if (o.ShowRemaining) parts.Add(Tuple.Create((data.Total-data.Unlocked) + " remaining",AchievementTextRole.Remaining));
            if (parts.Count > 0) {
                var line = Text("",false);
                foreach (var part in parts) {
                    if (line.Inlines.Count > 0) line.Inlines.Add(new System.Windows.Documents.Run("  |  "));
                    var style = Text(part.Item1,false,part.Item2);
                    line.Inlines.Add(new System.Windows.Documents.Run(part.Item1) { FontSize = style.FontSize, FontWeight = style.FontWeight, FontStyle = style.FontStyle, Foreground = style.Foreground });
                }
                text.Children.Add(line);
            }
            if (widgetId == "AchievementGoals") text.Children.Add(Text("Target: " + target.ToString("0.#") + "%" + (reached ? " - reached" : ""),false,AchievementTextRole.Goal));
            header.Children.Add(text); body.Children.Add(header);
            if (o.ProgressStyle == AchievementProgressStyle.Ring || o.ProgressStyle == AchievementProgressStyle.Both) {
                var ring = new Grid { Width = o.RingSize, Height = o.RingSize, Margin = new Thickness(0,6,0,6) };
                ring.Children.Add(new Ellipse { Stroke = Brush(o.TrackColor), StrokeThickness = Math.Min(o.BarHeight,o.RingSize/4) });
                var thickness = Math.Min(o.BarHeight,o.RingSize/4);
                var radius = (o.RingSize-thickness)/2;
                if (percent >= 100) ring.Children.Add(new Ellipse { Stroke = accent, StrokeThickness = thickness });
                else if (percent > 0) {
                    var center = o.RingSize/2; var angle = percent/100 * Math.PI*2;
                    var figure = new PathFigure { StartPoint = new Point(center,center-radius) };
                    figure.Segments.Add(new ArcSegment(new Point(center+radius*Math.Sin(angle),center-radius*Math.Cos(angle)),new Size(radius,radius),0,percent>50,SweepDirection.Clockwise,true));
                    ring.Children.Add(new Path { Data = new PathGeometry(new[] { figure }), Stroke = accent, StrokeThickness = thickness });
                }
                var label = Text(percent.ToString("0.#") + "%",false,AchievementTextRole.Percentage); label.TextAlignment = TextAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center;
                if (o.ShowPercent) ring.Children.Add(label); body.Children.Add(ring);
            }
            if (o.ProgressStyle == AchievementProgressStyle.Bar || o.ProgressStyle == AchievementProgressStyle.Both) {
                var bar = new Grid { Height = o.BarHeight, Margin = new Thickness(0,8,0,0), Background = Brush(o.TrackColor) };
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(.00001,percent),GridUnitType.Star) });
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(.00001,100-percent),GridUnitType.Star) });
                if (percent > 0) bar.Children.Add(new Border { Background = accent });
                body.Children.Add(bar);
            }
            var background = Brush(o.CardColor); background.Opacity = o.BackgroundOpacity;
            var card = new Border { Child = body, Padding = new Thickness(o.Padding), Margin = new Thickness(o.Spacing/2),
                CornerRadius = new CornerRadius(o.CornerRadius), BorderThickness = new Thickness(o.BorderWidth),
                BorderBrush = Brush(o.BorderColor), Background = background, ToolTip = game.Name };
            if (o.Layout == AchievementCardLayout.Cards) card.Width = o.CardWidth;
            card.MouseLeftButtonUp += (s,e) => {
                var ancestor = (DependencyObject)this;
                while (ancestor != null) { if ((ancestor as FrameworkElement)?.DataContext is ViewModels.Layout.GridNodeViewModel node && node.EditModeEnabled) return; ancestor = VisualTreeHelper.GetParent(ancestor); }
                LandingPageExtension.Instance.PlayniteApi.MainView.SelectGame(game.Id);
            };
            return card;
        }
        private TextBlock Text(string value,bool title,AchievementTextRole? role = null)
        {
            var o = Settings;
            var block = new TextBlock { Text = value, FontSize = title ? o.TitleSize : o.DetailSize, TextWrapping = TextWrapping.Wrap,
                TextAlignment = o.Alignment, FontWeight = (title ? o.Bold : o.DetailBold) ? FontWeights.Bold : FontWeights.Normal, FontStyle = (title ? o.Italic : o.DetailItalic) ? FontStyles.Italic : FontStyles.Normal };
            if (o.CustomTextColors) block.Foreground = Brush(title ? o.TitleColor : o.DetailColor);
            else block.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            if (role.HasValue && o.TextStyles.TryGetValue(role.Value.ToString(),out var custom) && !custom.UseDefaults) {
                block.FontSize = custom.Size; block.FontWeight = custom.FontWeight; block.FontStyle = custom.FontStyle;
                if (custom.UseCustomColor) block.Foreground = Brush(custom.Color);
            }
            return block;
        }
        private static SolidColorBrush Brush(string value) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnViewShownAsync() { if (closed) return Task.CompletedTask; visible = true; Observe(); timer.Start(); Render(); return Refresh(); }
        public Task OnViewHiddenAsync() { visible = false; listRequest++; timer.Stop(); editTimer.Stop(); Unobserve(); return Task.CompletedTask; }
        public Task OnDayChangedAsync(DateTime time) => Refresh();
        public void OnViewClosed() { closed = true; visible = false; listRequest++; timer.Stop(); editTimer.Stop(); Unobserve(); images.Clear(); }
    }
}

