using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shell;
using LandingPage.Models;
using LandingPage.Controls;
using LandingPage.Settings;
using Playnite.SDK;
using Playnite.SDK.Models;
using StartPage.SDK.Async;

namespace LandingPage.Views
{
    public class FeaturedGameView : UserControl, IAsyncStartPageControl, IStartPageViewModel
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(FeaturedGameSettings), typeof(FeaturedGameView), new PropertyMetadata(null, Changed));
        public FeaturedGameSettings Settings { get => (FeaturedGameSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
        public static readonly DependencyProperty DashboardEditModeProperty = DependencyProperty.Register(nameof(DashboardEditMode),typeof(bool),typeof(FeaturedGameView),new PropertyMetadata(false,DashboardEditChanged));
        public bool DashboardEditMode { get => (bool)GetValue(DashboardEditModeProperty); set => SetValue(DashboardEditModeProperty,value); }
        private static void DashboardEditChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var view = (FeaturedGameView)sender;
            if (!(bool)args.NewValue && view.Settings?.EditSectionLayout == true) view.Settings.EditSectionLayout = false;
        }
        private readonly GameDetailWidgetDefinition standalone;
        private bool IsStandalone => standalone != null;
        private bool CustomSectionLayout => !IsStandalone && Settings.UseCustomSectionLayout;
        private bool Includes(FeaturedGameSection section) => !IsStandalone || standalone.Section == section;
        private FeaturedGameSettings observed;
        private readonly Grid surface = new Grid { ClipToBounds = true, MinHeight = 180 };
        private readonly Image artwork = new Image { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
        private readonly Border shade = new Border { IsHitTestVisible = false };
        private readonly StackPanel details = new StackPanel { Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Bottom };
        private readonly ScrollViewer detailsViewport = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        private readonly FeaturedSectionCanvas sectionCanvas = new FeaturedSectionCanvas();
        private readonly Dictionary<FeaturedGameSection,StackPanel> sectionPanels = new Dictionary<FeaturedGameSection,StackPanel>();
        private StackPanel currentSection;
        private Panel ContentPanel => currentSection ?? details;
        private bool visible, closed, dirty = true, subscribed, shuffle, renderPending, refreshQueued, forceRender = true, usingFallback;
        private long previewRequestedAt;
        private Game game;
        private string artworkPath;
        private readonly System.Windows.Threading.DispatcherTimer hoverTimer;
        private bool UsesHover => Settings.Source == FeaturedGameSource.HoveredGame || Settings.Source == FeaturedGameSource.HoveredOrSelectedGame;
        private Task refreshTask = Task.CompletedTask;
        public Game FeaturedGame => game;
        private IPlayniteAPI Api => LandingPageExtension.Instance.PlayniteApi;

        public FeaturedGameView() : this(null) { }
        internal FeaturedGameView(GameDetailWidgetDefinition standalone)
        {
            this.standalone = standalone;
            if (IsStandalone) { surface.MinHeight = 0; details.Margin = new Thickness(8); }
            hoverTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Loaded,Dispatcher);
            hoverTimer.Tick += async (sender,args) => { hoverTimer.Stop(); if (visible && !closed) await RefreshSafely(); };
            surface.Children.Add(artwork); surface.Children.Add(shade); detailsViewport.Content = details; surface.Children.Add(detailsViewport); Content = surface;
            Focusable = true;
            Settings = IsStandalone ? GameDetailWidgets.CreateSettings(standalone.Id) : new FeaturedGameSettings();
            SetBinding(DashboardEditModeProperty,new Binding("DataContext.EditModeEnabled") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,typeof(GridNodeView),1), FallbackValue = false });
            SetBinding(SettingsProperty, new Binding(IsStandalone ? "DataContext.GridNode.ViewProperties.GameDetailSettings" : "DataContext.GridNode.ViewProperties.FeaturedGameSettings")
            { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(GridNodeView), 1), FallbackValue = Settings, TargetNullValue = Settings });
            Loaded += async (sender,args) => { if (!closed) { visible = true; Subscribe(); await RefreshSafely(); } };
            Unloaded += (sender,args) => { visible = false; hoverTimer.Stop(); UpdateAutoScrollActivity(); if (Settings.EditSectionLayout) Settings.EditSectionLayout = false; };
            PreviewKeyDown += (sender,args) => {
                if (args.Key == System.Windows.Input.Key.Escape && Settings.EditSectionLayout)
                { Settings.EditSectionLayout = false; args.Handled = true; }
            };
        }
        private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var view = (FeaturedGameView)sender;
            if (view.observed != null) view.observed.PropertyChanged -= view.SettingChanged;
            view.observed = args.NewValue as FeaturedGameSettings;
            if (view.observed != null) view.observed.PropertyChanged += view.SettingChanged;
            view.dirty = true;
            if (view.visible) view.QueueRefresh();
        }
        // Database/import events may arrive on workers. Defer before accessing any
        // dependency property, timer, bound state or WPF visual; never block imports.
        private bool DeferToDispatcher(Action callback)
        {
            if(Dispatcher.CheckAccess()) return closed;
            if(!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                Dispatcher.BeginInvoke(new Action(() => { if(!closed) callback(); }),System.Windows.Threading.DispatcherPriority.Loaded);
            return true;
        }
        private void SettingChanged(object sender, PropertyChangedEventArgs args)
        {
            if(DeferToDispatcher(() => SettingChanged(sender,args))) return;
            if (args.PropertyName == nameof(FeaturedGameSettings.HasCustomSectionPositions)) return;
            if (args.PropertyName == nameof(FeaturedGameSettings.UseCustomSectionLayout) && CustomSectionLayout && !Settings.HasCustomSectionPositions)
            {
                foreach (var pair in sectionPanels)
                {
                    var layout = Settings.GetSectionLayout(pair.Key); var element = pair.Value.Parent as SectionAppearanceBorder ?? (FrameworkElement)pair.Value;
                    if (element.ActualWidth <= 0 || element.ActualHeight <= 0) continue;
                    var point = element.TranslatePoint(new Point(),details);
                    layout.X = point.X+24; layout.Y = point.Y+24;
                    layout.Width = element.ActualWidth; layout.Height = element.ActualHeight;
                }
                Settings.HasCustomSectionPositions = true;
            }
            if (args.PropertyName == nameof(FeaturedGameSettings.Source) || args.PropertyName == nameof(FeaturedGameSettings.PinnedGameId)
                || args.PropertyName == nameof(FeaturedGameSettings.KeepLastHoveredGame) || args.PropertyName == nameof(FeaturedGameSettings.HoverDelayMilliseconds)
                || args.PropertyName == nameof(FeaturedGameSettings.InstalledOnly) || args.PropertyName == nameof(FeaturedGameSettings.IncludeHidden))
            {
                if (args.PropertyName == nameof(FeaturedGameSettings.Source)
                    && (Settings.Source == FeaturedGameSource.Random || Settings.Source == FeaturedGameSource.RandomFavorite)) shuffle = true;
                previewRequestedAt = System.Diagnostics.Stopwatch.GetTimestamp(); forceRender = true; dirty = true; QueueRefresh();
            }
            else if (visible) RenderGame();
            else renderPending = true;
        }
        internal void FocusSectionEditor() => Focus();
        private void Subscribe()
        {
            if (subscribed || closed) return;
            subscribed = true;
            Api.Database.Games.ItemUpdated += GamesUpdated;
            Api.Database.Games.ItemCollectionChanged += GamesChanged;
            LandingPageExtension.Instance.FeaturedSelectionChanged += SelectionChanged;
            LandingPageExtension.Instance.FeaturedHoverChanged += HoverChanged;
            LandingPageExtension.Instance.FeaturedFallbackChanged += FallbackChanged;
        }
        private void GamesUpdated(object sender, ItemUpdatedEventArgs<Game> args)
        {
            if(DeferToDispatcher(() => GamesUpdated(sender,args))) return;
            if (IsStandalone)
            {
                var context = LandingPageExtension.Instance;
                if (!args.UpdatedItems.Any(item => item.NewData?.Id == game?.Id || item.NewData?.Id == context.FeaturedSelectedGame?.Id
                    || item.NewData?.Id == context.FeaturedHoveredGame?.Id || (UsesHover && item.NewData?.Id == context.LastFeaturedHoveredGame?.Id)
                    || (Settings.Source == FeaturedGameSource.Pinned && item.NewData?.Id == Settings.PinnedGameId))) return;
            }
            previewRequestedAt = System.Diagnostics.Stopwatch.GetTimestamp(); forceRender = true; dirty = true; QueueRefresh();
        }
        private void GamesChanged(object sender, ItemCollectionChangedEventArgs<Game> args) { if(DeferToDispatcher(() => GamesChanged(sender,args))) return; previewRequestedAt = System.Diagnostics.Stopwatch.GetTimestamp(); forceRender = true; dirty = true; QueueRefresh(); }
        private void SelectionChanged(object sender, EventArgs args)
        {
            if(DeferToDispatcher(() => SelectionChanged(sender,args))) return;
            if (Settings.Source == FeaturedGameSource.SelectedGame || Settings.Source == FeaturedGameSource.HoveredOrSelectedGame)
            { previewRequestedAt = System.Diagnostics.Stopwatch.GetTimestamp(); dirty = true; if (!hoverTimer.IsEnabled) QueueRefresh(); }
        }
        private void HoverChanged(object sender, EventArgs args)
        {
            if(DeferToDispatcher(() => HoverChanged(sender,args))) return;
            if (!UsesHover) return;
            previewRequestedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            dirty = true; hoverTimer.Stop();
            if (!visible || closed) return;
            if (Settings.HoverDelayMilliseconds == 0) QueueRefresh();
            else { hoverTimer.Interval = TimeSpan.FromMilliseconds(Settings.HoverDelayMilliseconds); hoverTimer.Start(); }
        }
        private void FallbackChanged(object sender,EventArgs args)
        {
            if(DeferToDispatcher(() => FallbackChanged(sender,args))) return;
            if(Settings.Source == FeaturedGameSource.Pinned || (!usingFallback && !UsesHover && Settings.Source != FeaturedGameSource.SelectedGame)) return;
            dirty = true; QueueRefresh();
        }
        private void QueueRefresh()
        {
            if(DeferToDispatcher(QueueRefresh)) return;
            if (!visible || closed || refreshQueued) return;
            refreshQueued = true;
            Dispatcher.BeginInvoke(new Action(async () => {
                refreshQueued = false;
                if (visible && !closed) await RefreshSafely();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        private async Task RefreshSafely()
        {
            try { await Refresh(); }
            catch (Exception ex) { LandingPageExtension.logger.Warn(ex,"Unable to refresh featured game widget."); details.Children.Clear(); currentSection = null; detailsViewport.Content = details; AddText("Unable to load a featured game. Check the StartPage debug log."); }
        }
        private Task Refresh()
        {
            if (closed || !visible) return Task.CompletedTask;
            if (!refreshTask.IsCompleted) return refreshTask;
            refreshTask = RefreshCore(); return refreshTask;
        }
        private async Task RefreshCore()
        {
            while (dirty && visible && !closed)
            {
                dirty = false;
                var mustRender = forceRender || renderPending; forceRender = false;
                var debug = LandingPageExtension.Instance.Settings.EnableDashboardDiagnostics;
                var watch = debug ? System.Diagnostics.Stopwatch.StartNew() : null;
                var queueMs = previewRequestedAt == 0 ? 0 : (System.Diagnostics.Stopwatch.GetTimestamp()-previewRequestedAt)*1000d/System.Diagnostics.Stopwatch.Frequency;
                var options = Settings;
                var selectedId = LandingPageExtension.Instance.FeaturedSelectedGame?.Id ?? Api.MainView?.SelectedGames?.FirstOrDefault()?.Id;
                var hoverGame = LandingPageExtension.Instance.FeaturedHoveredGame;
                if (hoverGame == null && options.Source == FeaturedGameSource.HoveredGame && options.KeepLastHoveredGame) hoverGame = LandingPageExtension.Instance.LastFeaturedHoveredGame;
                var hoveredId = hoverGame?.Id;
                Game next;
                if (options.Source == FeaturedGameSource.Pinned) next = Api.Database.Games.Get(options.PinnedGameId);
                else if (options.Source == FeaturedGameSource.SelectedGame || options.Source == FeaturedGameSource.HoveredGame || options.Source == FeaturedGameSource.HoveredOrSelectedGame)
                {
                    // These are indexed in-memory lookups. Keep them on the dispatcher:
                    // cloning every setting and scheduling a worker adds latency per widget.
                    var candidates = new List<Game>();
                    if (selectedId.HasValue && Api.Database.Games.Get(selectedId.Value) is Game selected) candidates.Add(selected);
                    if (hoveredId.HasValue && hoveredId != selectedId && Api.Database.Games.Get(hoveredId.Value) is Game hovered) candidates.Add(hovered);
                    next = FeaturedGameSelection.Select(candidates,options,game?.Id,false,selectedId,hoveredId);
                }
                else
                {
                    var snapshot = DashboardProfiles.Clone(options);
                    var currentId = game?.Id; var doShuffle = shuffle; shuffle = false;
                    next = await Task.Run(() => FeaturedGameSelection.Select(Api.Database.Games.ToList(),snapshot,currentId,doShuffle,selectedId));
                }
                usingFallback = next == null && options.Source != FeaturedGameSource.Pinned;
                if(usingFallback) next = LandingPageExtension.Instance.GetPreviewFallback(options);
                if (closed) return;
                if (dirty) { forceRender |= mustRender; continue; }
                var lookupMs = watch?.Elapsed.TotalMilliseconds ?? 0;
                var changed = !ReferenceEquals(game,next);
                if (game?.Id != next?.Id) detailsViewport.ScrollToTop();
                game = next;
                if (visible && (changed || mustRender)) RenderGame();
                else if (!visible) renderPending = true;
                if (debug)
                    _ = ExtensionDiagnostics.RecordAsync(LandingPageExtension.Instance,$"Game preview update: widget={standalone?.Id ?? "FeaturedGame"}, source={options.Source}, game={game?.Id}, requestedToStartMs={queueMs:F1}, configuredHoverDelayMs={options.HoverDelayMilliseconds}, lookupMs={lookupMs:F2}, renderMs={(watch.Elapsed.TotalMilliseconds-lookupMs):F2}, reused={!(changed || mustRender)}.");
                previewRequestedAt = 0;
            }
        }
        private TextBlock AddText(string text, bool title = false)
        {
            var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = title ? Settings.TitleSize : Settings.TextSize,
                FontWeight = title ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0,0,0,10) };
            block.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            if (Settings.UseCustomTextColor) block.Foreground = Brush(title ? Settings.TitleColor : Settings.TextColor);
            ContentPanel.Children.Add(block); return block;
        }
        private static Brush Brush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        private void RenderGame()
        {
            if (closed) return;
            renderPending = false;
            details.Children.Clear(); sectionCanvas.Children.Clear(); sectionPanels.Clear(); currentSection = null;
            detailsViewport.Content = CustomSectionLayout ? (UIElement)sectionCanvas : details;
            details.MaxWidth = Settings.ContentWidth; details.HorizontalAlignment = Settings.Alignment; details.VerticalAlignment = Settings.VerticalAlignment;
            detailsViewport.HorizontalAlignment = Settings.Alignment; detailsViewport.VerticalAlignment = Settings.VerticalAlignment; detailsViewport.MaxWidth = Settings.ContentWidth + 48;
            if (CustomSectionLayout)
            {
                detailsViewport.HorizontalAlignment = HorizontalAlignment.Stretch; detailsViewport.VerticalAlignment = VerticalAlignment.Stretch;
                detailsViewport.MaxWidth = double.PositiveInfinity; detailsViewport.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else detailsViewport.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            ConfigureAutoScroll(detailsViewport,!CustomSectionLayout);
            artwork.Stretch = Settings.ArtworkStretch;
            artwork.StretchDirection = Settings.ArtworkShrinkOnly ? StretchDirection.DownOnly : StretchDirection.Both;
            var showArtwork = Settings.ShowArtwork && (!IsStandalone || standalone.IsArtwork);
            artwork.Opacity = showArtwork ? Settings.ArtworkOpacity : 0;
            shade.Background = Brush(Settings.OverlayColor); shade.Opacity = !IsStandalone || standalone.IsArtwork ? Settings.OverlayOpacity : 0;
            if (game == null)
            {
                artwork.Source = null; artworkPath = null; currentSection = null; detailsViewport.Content = details;
                if(Settings.Source == FeaturedGameSource.Pinned) AddText("Choose a pinned game in this widget's Appearance settings.");
                return;
            }
            var path = showArtwork ? (Settings.UseCoverArtwork ? game.CoverImage : game.BackgroundImage) : null;
            if (showArtwork && string.IsNullOrWhiteSpace(path)) path = game.CoverImage;
            if (path != artworkPath)
            {
                artworkPath = path; artwork.Source = null;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var fullPath = Api.Database.GetFullFilePath(path);
                    if (Uri.TryCreate(fullPath,UriKind.Absolute,out var uri) && Converters.DashboardBackgroundCache.Load(uri) is ImageSource image) artwork.Source = image;
                    else artwork.Source = new Converters.UriToBitmapImageConverter().Convert(path,typeof(ImageSource),null,System.Globalization.CultureInfo.CurrentCulture) as ImageSource;
                }
            }
            if (Includes(FeaturedGameSection.Title) && Settings.ShowTitle) { StartSection(FeaturedGameSection.Title); AddText(game.Name,true); }
            if (Includes(FeaturedGameSection.Description) && Settings.ShowDescription) RenderDescription();
            var metadataSize = Settings.MatchMetadataTextSize ? Settings.TextSize : Settings.MetadataTextSize;
            var metadataColor = Settings.MatchMetadataTextColor
                ? (Settings.UseCustomTextColor ? Brush(Settings.TextColor) : null)
                : (Settings.UseCustomMetadataColor ? Brush(Settings.MetadataTextColor) : null);
            if (Includes(FeaturedGameSection.Activity) && (Settings.ShowPlaytime || Settings.ShowLastPlayed || Settings.ShowSource || Settings.ShowAchievements))
            {
                StartSection(FeaturedGameSection.Activity);
                ContentPanel.Children.Add(new ShelfGameInfo { Model = new GameModel(game), IncludeTitle = false,
                    TextSizeOverride = metadataSize, TextBrushOverride = metadataColor, Settings = new ShelveProperties
                {
                    ShowPlaytime = Settings.ShowPlaytime, ShowLastPlayed = Settings.ShowLastPlayed, ShowSource = Settings.ShowSource, ShowSourceIcon = Settings.ShowSource,
                    ShowAchievementProgress = Settings.ShowAchievements, ShowAchievementTrophy = true, TitleDisplay = ShelfTitleDisplay.Hide,
                    MetadataIconSize = Settings.MatchMetadataIconSize ? metadataSize : Settings.MetadataIconSize
                } });
            }
            RenderSections();
            if (Includes(FeaturedGameSection.Actions) && (Settings.ShowPlayButton || Settings.ShowLibraryButton || Settings.Source == FeaturedGameSource.Random || Settings.Source == FeaturedGameSource.RandomFavorite))
            {
                StartSection(FeaturedGameSection.Actions);
                var actions = new WrapPanel { Margin = new Thickness(0,10,0,0) }; ContentPanel.Children.Add(actions);
                if (Settings.ShowPlayButton) AddButton(actions,game.IsInstalled ? "Play" : "Install",() => { if (game.IsInstalled) Api.StartGame(game.Id); else Api.InstallGame(game.Id); });
                if (Settings.ShowLibraryButton) AddButton(actions,"Open in library",() => { Api.MainView.SwitchToLibraryView(); Api.MainView.SelectGame(game.Id); });
                if (Settings.Source == FeaturedGameSource.Random || Settings.Source == FeaturedGameSource.RandomFavorite)
                    AddButton(actions,"Another game",() => { shuffle = true; dirty = true; QueueRefresh(); });
            }
        }
        private void StartSection(FeaturedGameSection section)
        {
            currentSection = new StackPanel(); sectionPanels[section] = currentSection;
            if (!CustomSectionLayout)
            {
                details.Children.Add(new SectionAppearanceBorder(Settings.GetSectionLayout(section),Settings,currentSection) { Child = currentSection });
                return;
            }
            var frame = new FeaturedSectionFrame(Settings.GetSectionLayout(section),Settings,currentSection);
            frame.SetBinding(FeaturedSectionFrame.EditingProperty,new Binding(nameof(FeaturedGameSettings.EditSectionLayout)) { Source = Settings });
            ConfigureAutoScroll(frame.Viewport,true);
            sectionCanvas.Children.Add(frame);
        }
        private void UpdateAutoScrollActivity()
        {
            ContentAutoScroll.SetEnabled(detailsViewport,visible && !closed && !CustomSectionLayout && Settings.AutoScrollContent);
            foreach (FeaturedSectionFrame frame in sectionCanvas.Children)
                ContentAutoScroll.SetEnabled(frame.Viewport,visible && !closed && Settings.AutoScrollContent);
        }
        private void ConfigureAutoScroll(ScrollViewer viewport, bool enabled)
        {
            ContentAutoScroll.SetEnabled(viewport,visible && enabled && Settings.AutoScrollContent);
            viewport.CanContentScroll = false;
            ContentAutoScroll.SetSpeed(viewport,Settings.AutoScrollSpeed);
            ContentAutoScroll.SetPauseOnHover(viewport,Settings.PauseAutoScrollOnHover);
            BindingOperations.SetBinding(viewport,ContentAutoScroll.PausedProperty,new Binding(nameof(FeaturedGameSettings.EditSectionLayout)) { Source = Settings });
        }
        private void RenderDescription()
        {
            if (Settings.PreserveDescriptionFormatting && !string.IsNullOrWhiteSpace(game.Description))
            {
                var html = FeaturedDescription.Html(game.Description,Settings.DescriptionLength,Settings.ShowDescriptionImages);
                var formatted = LibraryDescriptionRenderer.Create(html,Settings.TextSize,
                    TryFindResource("FontFamily") as FontFamily ?? FontFamily,
                    Settings.UseCustomTextColor ? Brush(Settings.TextColor) : null);
                if (formatted != null)
                {
                    StartSection(FeaturedGameSection.Description); ContentPanel.Children.Add(formatted);
                    ExtensionDiagnostics.RecordAsync(LandingPageExtension.Instance,$"Description renderer: library HTML, game={game.Id}, imagesEnabled={Settings.ShowDescriptionImages}, characterLimit={Settings.DescriptionLength}.");
                    return;
                }
            }
            var parts = FeaturedDescription.Parse(game.Description,Settings.DescriptionLength,Settings.ShowDescriptionImages);
            ExtensionDiagnostics.RecordAsync(LandingPageExtension.Instance,$"Description content: game={game.Id}, imagesEnabled={Settings.ShowDescriptionImages}, parsedImages={parts.Count(part => part.ImageUri != null)}, characterLimit={Settings.DescriptionLength}.");
            if (parts.Count == 0) return;
            StartSection(FeaturedGameSection.Description);
            foreach (var part in parts)
            {
                if (part.ImageUri == null) { if (!string.IsNullOrWhiteSpace(part.Text)) AddText(part.Text); continue; }
                var image = new Image { HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(0,0,0,10) };
                image.ImageFailed += (sender,args) => {
                    image.Visibility = Visibility.Collapsed; args.Handled = true;
                    ExtensionDiagnostics.RecordAsync(LandingPageExtension.Instance,$"Description image failed: game={game?.Id}, source={part.ImageUri}, error={args.ErrorException?.Message}.");
                };
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit(); bitmap.UriSource = part.ImageUri;
                    bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.EndInit();
                    // Preserve the source's natural dimensions; only shrink to fit the section.
                    Action sizeImage = () => { if (bitmap.Width > 0) image.MaxWidth = bitmap.Width; };
                    if (bitmap.IsDownloading) bitmap.DownloadCompleted += (sender,args) => sizeImage();
                    else sizeImage();
                    image.Source = bitmap; ContentPanel.Children.Add(image);
                }
                catch (Exception ex) { LandingPageExtension.logger.Debug(ex,"Unable to display a description image."); }
            }
        }
        private static string Names<T>(IEnumerable<T> values) where T : DatabaseObject
            => values == null ? null : string.Join(", ",values.Select(item => item?.Name).Where(name => !string.IsNullOrWhiteSpace(name)));
        private TextBlock AddMetadataText(string text, bool heading = false)
        {
            var block = AddText(text);
            block.FontSize = Settings.MatchMetadataTextSize ? Settings.TextSize : Settings.MetadataTextSize;
            block.Margin = new Thickness(0,heading ? 10 : 0,0,heading ? 6 : 4);
            block.FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal;
            block.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            if (Settings.MatchMetadataTextColor && Settings.UseCustomTextColor) block.Foreground = Brush(Settings.TextColor);
            else if (!Settings.MatchMetadataTextColor && Settings.UseCustomMetadataColor) block.Foreground = Brush(Settings.MetadataTextColor);
            return block;
        }
        private void AddSection(string title, Dictionary<string,string> fields)
        {
            var rows = fields.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).ToList();
            if (rows.Count == 0) return;
            StartSection(title == "Game details" ? FeaturedGameSection.GameDetails : title == "Scores" ? FeaturedGameSection.Scores : FeaturedGameSection.LibraryInformation);
            if (Settings.ShowSectionHeadings) AddMetadataText(title,true);
            foreach (var row in rows) AddMetadataText(row.Key + ": " + row.Value);
        }
        private void RenderSections()
        {
            if (Includes(FeaturedGameSection.GameDetails) && Settings.ShowGameDetails) AddSection("Game details",new Dictionary<string,string> {
                ["Platforms"] = Settings.ShowPlatforms ? Names(game.Platforms) : null,
                ["Genres"] = Settings.ShowGenres ? Names(game.Genres) : null,
                ["Release date"] = Settings.ShowReleaseDate ? game.ReleaseDate?.ToString() : null,
                ["Developers"] = Settings.ShowDevelopers ? Names(game.Developers) : null,
                ["Publishers"] = Settings.ShowPublishers ? Names(game.Publishers) : null
            });
            if (Includes(FeaturedGameSection.LibraryInformation) && Settings.ShowLibraryDetails) AddSection("Library information",new Dictionary<string,string> {
                ["Completion status"] = Settings.ShowCompletionStatus ? game.CompletionStatus?.Name : null,
                ["Categories"] = Settings.ShowCategories ? Names(game.Categories) : null,
                ["Tags"] = Settings.ShowTags ? Names(game.Tags) : null,
                ["Features"] = Settings.ShowFeatures ? Names(game.Features) : null,
                ["Added"] = Settings.ShowDateAdded ? game.Added?.ToLocalTime().ToString("d") : null,
                ["Installation"] = Settings.ShowInstallationStatus ? (game.IsInstalled ? "Installed" : "Not installed") : null,
                ["Version"] = Settings.ShowVersion ? game.Version : null
            });
            if (Includes(FeaturedGameSection.Scores) && Settings.ShowScores) AddSection("Scores",new Dictionary<string,string> {
                ["User score"] = Settings.ShowUserScore ? game.UserScore?.ToString(CultureInfo.CurrentCulture) : null,
                ["Critic score"] = Settings.ShowCriticScore ? game.CriticScore?.ToString(CultureInfo.CurrentCulture) : null,
                ["Community score"] = Settings.ShowCommunityScore ? game.CommunityScore?.ToString(CultureInfo.CurrentCulture) : null
            });
            if (Includes(FeaturedGameSection.Links) && Settings.ShowLinks && game.Links != null)
            {
                var links = game.Links.Where(link => Uri.TryCreate(link.Url,UriKind.Absolute,out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)).ToList();
                if (links.Count > 0)
                {
                    StartSection(FeaturedGameSection.Links);
                    if (Settings.ShowSectionHeadings) AddMetadataText("Links",true);
                    var buttons = new WrapPanel(); ContentPanel.Children.Add(buttons);
                    foreach (var link in links) AddButton(buttons,string.IsNullOrWhiteSpace(link.Name) ? link.Url : link.Name,
                        () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.Url) { UseShellExecute = true }));
                }
            }
        }
        private void AddButton(Panel parent, string text, Action action)
        {
            var button = new Button { Content = text, Padding = new Thickness(12,6,12,6), Margin = new Thickness(0,0,8,4) };
            if (IsStandalone)
            {
                button.FontSize = Settings.TextSize;
                if (Settings.UseCustomTextColor) button.Foreground = Brush(Settings.TextColor);
            }
            WindowChrome.SetIsHitTestVisibleInChrome(button,true);
            button.Click += (sender,args) =>
            {
                if (Settings.EditSectionLayout) return;
                DependencyObject ancestor = this;
                while (ancestor != null && !(ancestor is GridNodeView)) ancestor = VisualTreeHelper.GetParent(ancestor);
                if ((ancestor as FrameworkElement)?.DataContext is ViewModels.Layout.GridNodeViewModel model && model.EditModeEnabled) return;
                try { action(); } catch (Exception ex) { LandingPageExtension.logger.Error(ex,"Featured game action failed."); }
            };
            parent.Children.Add(button);
        }
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnViewShownAsync() { if (closed) return Task.CompletedTask; visible = true; Subscribe(); UpdateAutoScrollActivity(); if (renderPending && !dirty) RenderGame(); return RefreshSafely(); }
        public Task OnViewHiddenAsync() { visible = false; hoverTimer.Stop(); UpdateAutoScrollActivity(); if (Settings.EditSectionLayout) Settings.EditSectionLayout = false; return Task.CompletedTask; }
        public Task OnDayChangedAsync(DateTime time) { dirty = true; return visible ? RefreshSafely() : Task.CompletedTask; }
        public void OnViewClosed()
        {
            closed = true; visible = false; hoverTimer.Stop(); UpdateAutoScrollActivity();
            if (observed != null) observed.PropertyChanged -= SettingChanged;
            if (subscribed)
            {
                Api.Database.Games.ItemUpdated -= GamesUpdated; Api.Database.Games.ItemCollectionChanged -= GamesChanged;
                LandingPageExtension.Instance.FeaturedSelectionChanged -= SelectionChanged;
                LandingPageExtension.Instance.FeaturedHoverChanged -= HoverChanged;
                LandingPageExtension.Instance.FeaturedFallbackChanged -= FallbackChanged;
                subscribed = false;
            }
        }
    }
}
