using LandingPage.ViewModels;
using LandingPage.Views;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using LandingPage.Models;
using StartPage.SDK;
using LandingPage.Models.Layout;
using LandingPage.ViewModels.Layout;
using LandingPage.ViewModels.GameActivity;
using Newtonsoft.Json;
using PlayniteCommon.UI;
using System.Windows.Data;
using LandingPage.ViewModels.PlayniteAchievements;

namespace LandingPage
{
    public partial class LandingPageExtension : GenericPlugin, IStartPageExtension
    {
#pragma warning disable IDE0052 // Ungelesene private Member entfernen
        internal static readonly ILogger logger = new ExtensionDebugLogger(LogManager.GetLogger());
#pragma warning restore IDE0052 // Ungelesene private Member entfernen

        public static LandingPageExtension Instance { get; private set; } = null;

        internal LandingPageSettingsViewModel SettingsViewModel { get; set; }
        internal LandingPageSettings Settings => SettingsViewModel.Settings;

        private Utils.HostWindowDiagnostics hostWindowDiagnostics;
        private Models.LibraryStatisticsSource libraryStatistics;
        internal Models.LibraryStatisticsSource LibraryStatistics => libraryStatistics ?? (libraryStatistics = new Models.LibraryStatisticsSource(PlayniteApi));
        private readonly Dictionary<Guid,LibraryStatisticsView> libraryStatisticsViews = new Dictionary<Guid,LibraryStatisticsView>();
        private Dictionary<Guid, FeaturedGameView> featuredGameViews = new Dictionary<Guid, FeaturedGameView>();
        internal Dictionary<Guid, ShelvesViewModel> shelvesViewModels = new Dictionary<Guid, ShelvesViewModel>();

        public Dictionary<string, List<StartPageViewArgs>> AllAvailableViews { get; set; } = new Dictionary<string, List<StartPageViewArgs>>();

        public override Guid Id { get; } = Guid.Parse("a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5");

        internal Lazy<GameActivityViewModel> gameActivityViewModel;

        private BackgroundViewModel backgroundViewModel = null;
        public BackgroundViewModel BackgroundViewModel { get => backgroundViewModel ?? (backgroundViewModel = new BackgroundViewModel(PlayniteApi, this)); }

        internal Uri lastBackgroundUri = null;
        internal double lastBlur = 0;
        internal double lastBackgroundScale = 1;
        internal double lastAnimationDuration = 0;

        internal LandingPageView view = null;
        internal LandingPageView View
        {
            get
            {
                if (view == null)
                {
                    view = new LandingPageView(ViewModel);
                    view.Resources.Add("SettingsModel", SettingsViewModel);
                }
                return view;
            }
        }

        public List<Game> RunningGames { get; } = new List<Game>();

        internal LandingPageViewModel viewModel = null;
        internal LandingPageViewModel ViewModel
        {
            get
            {
                if (viewModel == null)
                {
                    ViewModels.SuccessStory.SuccessStoryViewModel successStory = null;
                    ViewModels.GameActivity.GameActivityViewModel gameActivity = null;
                    var successStoryPath = Directory.GetDirectories(PlayniteApi.Paths.ExtensionsDataPath, "SuccessStory", SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrEmpty(successStoryPath))
                    {
                        successStory = new ViewModels.SuccessStory.SuccessStoryViewModel(successStoryPath, PlayniteApi, SettingsViewModel);
                        successStory.ParseAllAchievements();
                    }
                    else
                    {
                        successStory = new ViewModels.SuccessStory.SuccessStoryViewModel(null, PlayniteApi, SettingsViewModel);
                    }
                    var gameActivityPath = Directory.GetDirectories(PlayniteApi.Paths.ExtensionsDataPath, "GameActivity", SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrEmpty(gameActivityPath))
                    {
                        gameActivity = new ViewModels.GameActivity.GameActivityViewModel(gameActivityPath, PlayniteApi, SettingsViewModel);
                        gameActivity.ParseAllActivites();
                    }
                    else
                    {
                        gameActivity = new ViewModels.GameActivity.GameActivityViewModel(null, PlayniteApi, SettingsViewModel);
                    }
                    viewModel = new LandingPageViewModel(PlayniteApi, this, SettingsViewModel, successStory, gameActivity);
                    foreach (var shelve in Settings.ShelveProperties)
                    {
                        //viewModel.ShelveViewModels.Add(new ShelveViewModel(shelve, PlayniteApi, viewModel));
                    }
                    viewModel.Update(true);
                }
                else
                {
                    viewModel.Update(false);
                }
                return viewModel;
            }
        }

        ProgressBar progressBar = null;
        TextBlock progressText = null;
        Button cancelButton = null;

        public string PlaceholderCoverPath { get; private set; }

        public LandingPageExtension(IPlayniteAPI api) : base(api)
        {
            Instance = this;
            SettingsViewModel = new LandingPageSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            gameActivityViewModel = new Lazy<GameActivityViewModel>(() =>
            {
                string gameActivityPath = null;
                string path = Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, "afbb1a0d-04a1-4d0c-9afa-c6e42ca855b4", "GameActivity");
                if (Directory.Exists(path))
                {
                    gameActivityPath = path;
                }

                if (!string.IsNullOrEmpty(gameActivityPath))
                {
                    var model = new GameActivityViewModel(gameActivityPath, PlayniteApi, SettingsViewModel);
                    return model;
                }
                return null;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        internal StartPageView startPageView;
        internal StartPageViewModel startPageViewModel;
        private MostPlayedViewModel mostPlayedViewModel;

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            if (args.Games.Count == 1 && !string.IsNullOrEmpty(args.Games[0].BackgroundImage))
            {
                Action<GameMenuItemActionArgs> action = null;
                var path = args.Games[0].BackgroundImage;
                if (path.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)
                    || File.Exists(path))
                {
                    if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var url))
                    {
                        action = a => { Settings.BackgroundImageUri = url; Settings.BackgroundImagePath = a.Games[0].Name; };
                    }
                    ;
                }
                else if (PlayniteApi.Database.GetFullFilePath(path) is string databasePath)
                {
                    if (Uri.TryCreate(databasePath, UriKind.RelativeOrAbsolute, out var url))
                    {
                        action = a => { Settings.BackgroundImageUri = url; Settings.BackgroundImagePath = a.Games[0].Name; };
                    }
                    ;
                }
                if (action != null)
                {
                    yield return new GameMenuItem
                    {
                        Action = action,
                        Description = ResourceProvider.GetString("LOC_SPG_SetGameAsBackground")
                    };
                }
            }
        }


        private static Image CreateSidebarIcon()
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 64;
            bitmap.UriSource = new Uri(Path.Combine(Path.GetDirectoryName(typeof(LandingPageExtension).Assembly.Location), "icon.png"));
            bitmap.EndInit();
            bitmap.Freeze();
            return new Image { Source = bitmap, Width = 24, Height = 24, Stretch = Stretch.Uniform };
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            var items = new List<SidebarItem>();



            items.Add(new SidebarItem
            {
                Type = SiderbarItemType.View,
                Title = "Start Page",
                Visible = true,
                Opened = ViewOpened,
                Closed = ViewClosed,
                Icon = new TextBlock { Text = "", FontFamily = ResourceProvider.GetResource<FontFamily>("FontIcoFont") }
            });

            return items;
        }

        private Control ViewOpened()
        {
            Task initTask = Task.CompletedTask;
            if (startPageView == null)
            {
                DashboardPages.Ensure(Settings);
                startPageViewModel = new StartPageViewModel(PlayniteApi, this, SettingsViewModel, new GridNodeViewModel(Settings.GridLayout), BackgroundViewModel);

                Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await startPageViewModel.Inititalize();
                }), System.Windows.Threading.DispatcherPriority.Background);


                var view = new StartPageView
                {
                    DataContext = startPageViewModel
                };

                BindDashboardProgress(view);

                startPageView = view;
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await Task.Delay(100);
                    await startPageViewModel.Opened();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (globalBackground is BackgroundView)
                {
                    UpdateGlobalBackgroundVisibility();
                }
            });

            isViewOpen = true;
            UpdateGlobalBackgroundVisibility();

            return startPageView;
        }

        private async void ViewClosed()
        {
            isViewOpen = false;
            UpdateGlobalBackgroundVisibility();
            if (startPageView?.DataContext is StartPageViewModel model)
            {
                // async void: an exception here would reach Playnite's dispatcher and crash it.
                try { await model.Closed(); }
                catch (Exception ex) { logger.Error(ex, "Unable to close the StartPage dashboard cleanly."); }
            }


            GC.Collect();
        }

        private Dictionary<string,Guid> previewFallbackGames;
        internal event EventHandler FeaturedFallbackChanged;
        internal void NotifyPreviewFallbackChanged() => FeaturedFallbackChanged?.Invoke(this,EventArgs.Empty);
        internal Game GetPreviewFallback(Settings.FeaturedGameSettings options)
        {
            Func<Game,bool> matches = candidate => candidate != null && (options.IncludeHidden || !candidate.Hidden) && (!options.InstalledOnly || candidate.IsInstalled)
                && (options.Source != LandingPage.Settings.FeaturedGameSource.RandomFavorite || candidate.Favorite);
            var layout = Settings.GridLayout;
            if(layout != null && shelvesViewModels != null)
            {
                foreach(var node in layout.Enumerate())
                {
                    var properties = node.ViewProperties;
                    if(properties?.ViewId != "GameShelves" || !shelvesViewModels.TryGetValue(properties.InstanceId,out var shelves)) continue;
                    foreach(var shelf in shelves.ShelveViewModels)
                    {
                        var models = shelf.CollectionViewSource?.View?.Cast<object>().OfType<Models.GameModel>() ?? shelf.Games;
                        foreach(var model in models)
                        {
                            if(model.Game == null) continue;
                            var candidate = PlayniteApi.Database.Games.Get(model.Game.Id);
                            if(matches(candidate)) return candidate;
                        }
                    }
                }
            }
            if(previewFallbackGames == null) previewFallbackGames = new Dictionary<string,Guid>();
            var key = Settings.ActiveDashboardPageId+":"+options.IncludeHidden+":"+options.InstalledOnly+":"+(options.Source == LandingPage.Settings.FeaturedGameSource.RandomFavorite);
            if(previewFallbackGames.TryGetValue(key,out var id) && matches(PlayniteApi.Database.Games.Get(id))) return PlayniteApi.Database.Games.Get(id);
            var randomOptions = new Settings.FeaturedGameSettings { Source = options.Source == LandingPage.Settings.FeaturedGameSource.RandomFavorite ? LandingPage.Settings.FeaturedGameSource.RandomFavorite : LandingPage.Settings.FeaturedGameSource.Random, IncludeHidden = options.IncludeHidden, InstalledOnly = options.InstalledOnly };
            var random = Models.FeaturedGameSelection.Select(PlayniteApi.Database.Games,randomOptions,null,false);
            if(random != null) previewFallbackGames[key] = random.Id;
            return random;
        }
        internal event EventHandler FeaturedSelectionChanged;
        internal event EventHandler FeaturedHoverChanged;
        internal Game FeaturedSelectedGame { get; private set; }
        internal Game FeaturedHoveredGame { get; private set; }
        internal Game LastFeaturedHoveredGame { get; private set; }
        private object featuredHoverOwner, featuredHoverGroup;
        internal void SetFeaturedHoveredGame(Game game, object owner, object group)
        {
            if (game == null && !ReferenceEquals(owner,featuredHoverOwner)) return;
            featuredHoverOwner = game == null ? null : owner;
            featuredHoverGroup = game == null ? null : group;
            if (game != null) LastFeaturedHoveredGame = game;
            if (ReferenceEquals(FeaturedHoveredGame,game)) return;
            FeaturedHoveredGame = game;
            ExtensionDiagnostics.RecordAsync(this,$"Game preview hover context: game={game?.Id}, last={LastFeaturedHoveredGame?.Id}.");
            FeaturedHoverChanged?.Invoke(this,EventArgs.Empty);
        }
        internal void ClearFeaturedHoveredGame(object group)
        {
            if (ReferenceEquals(group,featuredHoverGroup)) SetFeaturedHoveredGame(null,featuredHoverOwner,group);
        }


        public override void OnGameSelected(OnGameSelectedEventArgs args)
        {
            ExtensionDiagnostics.Record(this, "Lifecycle: OnGameSelected");
            FeaturedSelectedGame = args.NewValue?.FirstOrDefault();
            FeaturedSelectionChanged?.Invoke(this, EventArgs.Empty);
            if (viewModel != null)
            {
                if (args.NewValue?.FirstOrDefault() is Game last)
                {
                    viewModel.LastSelectedGame = last;
                }
            }
            if (backgroundViewModel is BackgroundViewModel model)
            {
                if (args.NewValue?.FirstOrDefault() is Game last)
                {
                    model.LastSelectedGame = last;
                }
            }
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            ExtensionDiagnostics.Record(this, "Lifecycle: OnGameInstalled");
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            ExtensionDiagnostics.Record(this, "Lifecycle: OnGameStarted");
            if (RunningGames.Count == 0)
            {
                if (startPageViewModel != null && Settings.ShowCurrentlyPlayedBackground)
                {
                    lastBackgroundUri = startPageViewModel.BackgroundViewModel.BackgroundImagePath;
                    lastBlur = Settings.BlurAmount;
                    lastBackgroundScale = Settings.RenderScale;
                    lastAnimationDuration = Settings.AnimationDuration;
                    Settings.AnimationDuration = 0;
                    if (Uri.TryCreate(PlayniteApi.Database.GetFullFilePath(args.Game.BackgroundImage), UriKind.RelativeOrAbsolute, out var uri))
                    {
                        startPageViewModel.BackgroundViewModel.BackgroundImagePath = uri;
                    }
                    if (Settings.DisableBlurForCurrentlyPlayed)
                    {
                        Settings.BlurAmount = 0;
                        Settings.RenderScale = 1;
                    }
                }
            }
            RunningGames.AddMissing(args.Game);
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            ExtensionDiagnostics.Record(this, "Lifecycle: OnGameStarting");
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            ExtensionDiagnostics.Record(this, "Lifecycle: OnGameStopped");
            RunningGames.Remove(args.Game);
            if (startPageViewModel == null)
            {
                return;
            }
            if (RunningGames.Count == 0)
            {
                if (lastBackgroundUri != null)
                {
                    startPageViewModel.BackgroundViewModel.BackgroundImagePath = lastBackgroundUri;
                    Settings.BlurAmount = lastBlur;
                    Settings.RenderScale = lastBackgroundScale;
                    Settings.AnimationDuration = lastAnimationDuration;
                    lastBackgroundUri = null;
                }
            }
            else
            {
                if (Uri.TryCreate(PlayniteApi.Database.GetFullFilePath(RunningGames.Last().BackgroundImage), UriKind.RelativeOrAbsolute, out var uri))
                {
                    startPageViewModel.BackgroundViewModel.BackgroundImagePath = uri;
                }
            }
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            ExtensionDiagnostics.Record(this, "Lifecycle: OnGameUninstalled");
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            ExtensionDiagnostics.Record(this, "Lifecycle: OnApplicationStarted");
            StartUpdateChecks();
            Application.Current.Deactivated += OnApplicattionDeactivated;
            // set cover path
            var extensionPath = typeof(LandingPageExtension).Assembly.Location;
            extensionPath = Path.GetDirectoryName(extensionPath);
            PlaceholderCoverPath = Path.Combine(PlayniteApi.Paths.ConfigurationPath, extensionPath, "PlaceholderCover.png");

            // create tags
            if (Settings.EnableTagCreation)
            {
                if (Settings.IgnoreTagId == Guid.Empty || PlayniteApi.Database.Tags.Get(Settings.IgnoreTagId) == null)
                {
                    Settings.IgnoreTagId = PlayniteApi.Database.Tags.Add("[SPG] Ignored").Id;
                }
                if (Settings.IgnoreMostPlayedTagId == Guid.Empty || PlayniteApi.Database.Tags.Get(Settings.IgnoreMostPlayedTagId) == null)
                {
                    Settings.IgnoreMostPlayedTagId = PlayniteApi.Database.Tags.Add("[SPG] Most Played Ignored").Id;
                }
            }
            //Settings.IgnoreTagId = PlayniteApi.Database.Tags.Add("[SPG] Ignored").Id;
            //Settings.IgnoreMostPlayedTagId = PlayniteApi.Database.Tags.Add("[SPG] Most Played Ignored").Id;
            var switchWithLowPrio = false;
            var parentWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.Name == "WindowMain");
            parentWindow?.Dispatcher.Invoke(() =>
            {
                hostWindowDiagnostics?.Dispose();
                hostWindowDiagnostics = new Utils.HostWindowDiagnostics(this,parentWindow);
                if (UiHelper.FindVisualChildren<ProgressBar>(parentWindow, "PART_ProgressGlobal").FirstOrDefault() is ProgressBar bar)
                {
                    progressBar = bar;
                }
                if (UiHelper.FindVisualChildren<TextBlock>(parentWindow, "PART_TextProgressText").FirstOrDefault() is TextBlock info)
                {
                    progressText = info;
                }
                if (UiHelper.FindVisualChildren<Button>(parentWindow, "PART_ButtonProgressCancel").FirstOrDefault() is Button bt)
                {
                    cancelButton = bt;
                }
            });
            //Settings.SwitchWithLowPriority = true;
            //SavePluginSettings(Settings);
            if (Settings.EnableStartupOverride || Settings.MoveToTopOfList)
            {
                logger.Debug("Trying to execute unsupported actions...");
                var mainWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Name == "WindowMain");
                if (mainWindow == null)
                    logger.Debug("MainWindow could not be found.");
                if (mainWindow is Window)
                {
                    mainWindow.Dispatcher.InvokeAsync(new Action(async () =>
                    {
                        try
                        {
                            if (UiHelper.FindVisualChildren<StackPanel>(mainWindow, "PART_PanelSideBarItems").FirstOrDefault() is StackPanel panel)
                            {
                                IEnumerable<FrameworkElement> children = UiHelper.FindVisualChildren(panel);
                                int retries = 20;
                                IEnumerable<Button> sideBarButtons = panel.Children.OfType<Button>();
                                int delay = 50;
                                while (sideBarButtons.FirstOrDefault(child => child.ToolTip?.ToString() == Settings.StartPage) == null && retries > 0)
                                {
                                    logger.Debug($"Could not find any side bar items. Waiting {delay}ms and retrying. Remaining retries: {retries}.");
                                    await Task.Delay(delay);
                                    sideBarButtons = panel.Children.OfType<Button>();
                                    retries--;
                                }
                                logger.Debug($"Found side bar items:\n{string.Join("\n", sideBarButtons.Select(a => (a.ToolTip?.ToString())).OfType<string>())}");
                                if (sideBarButtons.FirstOrDefault(child => child.ToolTip?.ToString() == Settings.StartPage) is Button element)
                                {
                                    var childIndex = -1;
                                    if (Settings.MoveToTopOfList)
                                    {
                                        childIndex = panel.Children.Cast<FrameworkElement>().ToList().FindIndex(c => c.ToolTip?.ToString() == "Start Page");
                                        if (childIndex == -1)
                                            logger.Debug("Could not find StartPage side bar element.");
                                    }

                                    await panel.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        if (childIndex > -1)
                                        {
                                            var child = panel.Children[childIndex];
                                            panel.Children.RemoveAt(childIndex);
                                            panel.Children.Insert(0, child);
                                        }

                                        if (Settings.EnableStartupOverride)
                                        {
                                            if (element.Command.CanExecute(element))
                                            {
                                                element.Command.Execute(element);
                                            }
                                        }
                                    }), switchWithLowPrio ? System.Windows.Threading.DispatcherPriority.ApplicationIdle : System.Windows.Threading.DispatcherPriority.DataBind);
                                }
                                else
                                {
                                    logger.Debug("Could not find StartPage side bar button.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Debug(ex, "Failed to move Start Page icon or open StartPage.");
                        }
                    }), switchWithLowPrio ? System.Windows.Threading.DispatcherPriority.ApplicationIdle : System.Windows.Threading.DispatcherPriority.DataBind);
                }
            }
            foreach (var plugin in PlayniteApi.Addons.Plugins)
            {
                if (plugin is IStartPageExtension extension)
                {
                    if (extension.GetAvailableStartPageViews() is StartPageExtensionArgs extensionArgs
                        && extensionArgs.Views != null
                        && extensionArgs.Views.Any())
                    {
                        AllAvailableViews.Add(extensionArgs.ExtensionName, extensionArgs.Views.Select(v => new StartPageViewArgs(v, plugin.Id)).ToList());
                    }
                }
            }

            SetGlobalBackground();

            SettingsViewModel.SettingsChanged += SettingsViewModel_SettingsChanged;
        }

        private void SettingsViewModel_SettingsChanged(object sender, LandingPageSettings e)
        {
            ExtensionDiagnostics.Record(this, "Settings saved.");
            SetGlobalBackground();
        }

        private void BindDashboardProgress(StartPageView view)
        {
            if (!Settings.EnableGlobalProgressBar)
            {
                BindingOperations.ClearBinding(view.ProgressbarGrid, UIElement.VisibilityProperty);
                view.ProgressbarGrid.Visibility = Visibility.Collapsed;
                return;
            }
            if (progressText is TextBlock && progressBar is ProgressBar && cancelButton is Button)
            {
                view.ProgressbarGrid.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(progressBar.Visibility)) { Source = progressBar });
                view.ProgressText.SetBinding(TextBlock.TextProperty, new Binding(nameof(progressText.Text)) { Source = progressText });
                view.ProgressBar.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(progressBar.Value)) { Source = progressBar });
                view.ProgressBar.SetBinding(ProgressBar.MaximumProperty, new Binding(nameof(progressBar.Maximum)) { Source = progressBar });
                view.ProgressCancelButton.Command = cancelButton.Command;
            }
        }

        internal void RefreshDashboardPresentation()
        {
            if (Application.Current.Windows.OfType<Window>().Any(window => window.Name == "WindowMain"))
            {
                SetGlobalBackground();
            }
            if (startPageView is StartPageView view) BindDashboardProgress(view);
        }

        internal void UpdateGlobalBackgroundVisibility()
        {
            if (globalBackground == null) return;
            globalBackground.Visibility = (Settings.EnableGlobalBackground || Settings.BackgroundAppearance.ShowSidebarLine || Settings.BackgroundAppearance.ShowTopBarLine)
                && (isViewOpen || Settings.ShowBackgroundOutsideStartPage) ? Visibility.Visible : Visibility.Collapsed;
            if (isViewOpen) globalBackground.Opacity = 1;
            else globalBackground.SetBinding(UIElement.OpacityProperty,
                new Binding("Settings.GlobalBackgroundOpacity") { Source = SettingsViewModel });
        }
        private void SetGlobalBackground()
        {
            logger.Debug("Trying to execute unsupported actions...");
            var mainWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Name == "WindowMain");
            if (mainWindow == null)
                logger.Debug("MainWindow could not be found.");
            if (mainWindow is Window)
            {
                mainWindow.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (UiHelper.FindVisualChildren<Grid>(mainWindow).FirstOrDefault() is Grid grid)
                        {
                            if (!grid.Children.OfType<BackgroundView>().Any())
                            {
                                globalBackground = new BackgroundView() { IsGlobalBackground = true, DataContext = BackgroundViewModel };
                                if (!isViewOpen)
                                {
                                    globalBackground.SetBinding(UIElement.OpacityProperty, new Binding("Settings.GlobalBackgroundOpacity") { Source = SettingsViewModel });
                                }

                                grid.Children.Insert(0, globalBackground);
                            }
                            UpdateGlobalBackgroundVisibility();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "Failed to find background image control.");
                    }
                });
            }
        }

        private void UnsetGlobalBackground()
        {
            logger.Debug("Trying to execute unsupported actions...");
            var mainWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Name == "WindowMain");
            if (mainWindow == null)
                logger.Debug("MainWindow could not be found.");
            if (mainWindow is Window)
            {
                mainWindow.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (UiHelper.FindVisualChildren<Grid>(mainWindow).FirstOrDefault() is Grid grid)
                        {
                            if (grid.Children.OfType<BackgroundView>().FirstOrDefault() is BackgroundView view)
                            {
                                BindingOperations.ClearAllBindings(view);
                                grid.Children.Remove(view);
                                globalBackground = null;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "Failed to remove background image control.");
                    }
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private void OnApplicattionDeactivated(object sender, EventArgs e)
        {
            ExtensionDiagnostics.Record(this,"Application deactivated; dashboard edit state preserved.");
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            ExtensionDiagnostics.Record(this, "Lifecycle: OnApplicationStopped");
            StopUpdateChecks();
            hostWindowDiagnostics?.Dispose(); hostWindowDiagnostics = null;
            foreach(var stats in libraryStatisticsViews.Values) stats.OnViewClosed();
            libraryStatistics?.Dispose();
            if (featuredGameViews != null) { foreach (var featured in featuredGameViews.Values) featured.OnViewClosed(); featuredGameViews.Clear(); }
            // Add code to be executed when Playnite is shutting down.
            if (viewModel is LandingPageViewModel)
            {
                // Settings.ShelveProperties = viewModel.ShelveViewModels.Select(svm => svm.ShelveProperties).ToList();
            }
            if (startPageView?.DataContext is StartPageViewModel startPageViewModel)
            {
                if (lastBackgroundUri != null)
                {
                    if (startPageViewModel.BackgroundViewModel != null)
                        startPageViewModel.BackgroundViewModel.BackgroundImagePath = lastBackgroundUri;
                    Settings.BlurAmount = lastBlur;
                    Settings.RenderScale = lastBackgroundScale;
                    Settings.AnimationDuration = lastAnimationDuration;
                }
                startPageViewModel.CaptureCurrentPage();
                Settings.GridLayout = startPageViewModel.RootNodeViewModel.GridNode;
                foreach (var vm in shelvesViewModels)
                {
                    vm.Value.Shelves.TTL = 5;
                    Settings.ShelveInstanceSettings[vm.Key] = vm.Value.Shelves;
                }
                foreach (var page in Settings.DashboardPages)
                    foreach (var node in page.Layout.Enumerate())
                        if (node.ViewProperties is ViewProperties properties
                            && Settings.ShelveInstanceSettings.TryGetValue(properties.InstanceId, out var savedShelf)) savedShelf.TTL = 5;
                if (shelvesViewModels.Count > 0)
                {
                    Settings.ShelveInstanceSettings = Settings.ShelveInstanceSettings
                        .Where(item => item.Value.TTL > 0)
                        .ToDictionary(item => item.Key, item => item.Value);
                    foreach (var settings in Settings.ShelveInstanceSettings)
                    {
                        settings.Value.TTL--;
                    }
                }
            }


            // collapse layout
            if (Settings.GridLayout != null)
            {
                GridNode.Minimize(Settings.GridLayout, null);
            }
            SavePluginSettings(Settings);
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            ExtensionDiagnostics.Record(this, "Lifecycle: OnLibraryUpdated");
            // Add code to be executed when library is updated.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return SettingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            ExtensionDiagnostics.Record(this, "Settings view opened.");
            return new LandingPageSettingsView();
        }

        public StartPageExtensionArgs GetAvailableStartPageViews()
        {
            var views = new List<StartPageViewArgsBase>();
            var args = new StartPageExtensionArgs() { ExtensionName = "StartPage", Views = views };

            views.Add(new StartPageViewArgsBase
            {
                ViewId = "RecentAchivements",
                Name = ResourceProvider.GetString("LOC_SPG_RecentAchievementsView"),
                Description = ResourceProvider.GetString("LOC_SPG_RecentAchievementsDescription"),
                HasSettings = false,
            });

            string gameActivityPath = null;
            string path2 = Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, "afbb1a0d-04a1-4d0c-9afa-c6e42ca855b4", "GameActivity");
            if (Directory.Exists(path2))
            {
                gameActivityPath = path2;
            }
            if (!string.IsNullOrEmpty(gameActivityPath))
            {
                views.Add(new StartPageViewArgsBase
                {
                    ViewId = "WeeklyActivity",
                    Name = ResourceProvider.GetString("LOC_SPG_WeeklyActivityView"),
                    Description = ResourceProvider.GetString("LOC_SPG_WeeklyActivityDescription")
                });
            }

            views.Add(new StartPageViewArgsBase
            {
                ViewId = "GameShelves",
                Name = ResourceProvider.GetString("LOC_SPG_ShelvesView"),
                Description = ResourceProvider.GetString("LOC_SPG_ShelvesViewDescription"),
                HasSettings = true,
                AllowMultipleInstances = true
            });
            views.Add(new StartPageViewArgsBase
            {
                ViewId = "MostPlayed",
                Name = ResourceProvider.GetString("LOC_SPG_MostPlayedView"),
                Description = ResourceProvider.GetString("LOC_SPG_MostPlayedDescription"),
                HasSettings = true
            });
            views.Add(new StartPageViewArgsBase
            {
                ViewId = "DigitalClock",
                Name = ResourceProvider.GetString("LOC_SPG_ClockView"),
                Description = ResourceProvider.GetString("LOC_SPG_ClockViewDescription"),
                AllowMultipleInstances = true,
            });
            views.Add(new StartPageViewArgsBase
            {
                ViewId = "TextWidget",
                Name = "Text",
                Description = "Custom headings and notes. Configure text and formatting in Appearance.",
                AllowMultipleInstances = true
            });
            views.Add(new StartPageViewArgsBase
            {
                ViewId = "ToolbarWidget", Name = "Toolbar",
                Description = "Configurable Playnite toolbar actions and dashboard page navigation. Configure in Appearance.",
                AllowMultipleInstances = true
            });
            views.Add(new StartPageViewArgsBase
            {
                ViewId = "ImageWidget",
                Name = "Image",
                Description = "Local images, banners, and logos with an optional website link. Configure in Appearance.",
                AllowMultipleInstances = true
            });
            views.Add(new StartPageViewArgsBase { ViewId = "DividerWidget", Name = "Divider / spacer", Description = "Configurable horizontal or vertical separator lines and empty space.", AllowMultipleInstances = true });
            views.Add(new StartPageViewArgsBase { ViewId = "ShortcutWidget", Name = "Shortcuts", Description = "Game, application, folder and website buttons with configurable labels and icons.", AllowMultipleInstances = true });
            foreach (var entry in new[] { new[] { "AchievementProgress", "Achievement progress cards" }, new[] { "AchievementFinishNext", "Achievement finish next" }, new[] { "AchievementGoals", "Achievement goals" }, new[] { "GameAchievementList", "Game achievements list" } })
                views.Add(new StartPageViewArgsBase { ViewId = entry[0], Name = entry[1], Description = entry[0] == "GameAchievementList" ? "Individual achievements for the selected, hovered or pinned game. Configure in Appearance." : "Customizable achievement cards, rings, bars and game targets. Configure in Appearance.", AllowMultipleInstances = true });
            views.Add(new StartPageViewArgsBase { ViewId = "LibraryChart", Name = "Library chart", Description = "An independent library graph for game counts or playtime. Add multiple charts and configure each in Appearance.", AllowMultipleInstances = true });
            views.Add(new StartPageViewArgsBase { ViewId = "LibraryStatistics", Name = "Library statistics", Description = "Configurable game counters and total playtime. Configure in Appearance.", AllowMultipleInstances = true });
            foreach (var definition in Models.GameDetailWidgets.Definitions)
                views.Add(new StartPageViewArgsBase { ViewId = definition.Id, Name = definition.Name,
                    Description = "An independent game detail widget. Follow the selected or hovered game and configure fields in Appearance.", AllowMultipleInstances = true });
            views.Add(new StartPageViewArgsBase { ViewId = "FeaturedGame", Name = "Featured game",
                Description = "Pinned or automatic featured game with artwork, description and actions. Configure in Appearance.", AllowMultipleInstances = true });
            return args;
        }

        private Dictionary<Guid, AchievementWidgetView> achievementWidgetViews = new Dictionary<Guid, AchievementWidgetView>();
        private Task gameActivityTask = Task.CompletedTask;
        private BackgroundView globalBackground;
        internal BackgroundView GlobalBackgroundView => globalBackground as BackgroundView;
        private bool isViewOpen;

        public object GetStartPageView(string id, Guid instanceId)
        {
            if (LandingPage.Settings.AchievementWidgetSettings.IsWidget(id)) { if (achievementWidgetViews == null) achievementWidgetViews = new Dictionary<Guid, AchievementWidgetView>(); var achievement = new AchievementWidgetView(id); achievementWidgetViews[instanceId] = achievement; return achievement; }
            if(id == "LibraryStatistics" || id == "LibraryChart") { var stats = new LibraryStatisticsView(id == "LibraryChart"); libraryStatisticsViews[instanceId] = stats; return stats; }
            var detail = Models.GameDetailWidgets.Find(id);
            if (id == "FeaturedGame" || detail != null)
            {
                if (featuredGameViews == null) featuredGameViews = new Dictionary<Guid, FeaturedGameView>();
                var featured = new FeaturedGameView(detail); featuredGameViews[instanceId] = featured;
                return featured;
            }
            if (id == "GameShelves")
            {
                var viewModel = new ShelvesViewModel(PlayniteApi, this, SettingsViewModel, instanceId);
                // A recreated instance replaces its stale view model instead of failing on a duplicate key.
                if (shelvesViewModels.TryGetValue(instanceId, out var stale)) stale.OnViewClosed();
                shelvesViewModels[instanceId] = viewModel;
                return new ShelvesView { DataContext = viewModel };
            }
            if (id == "MostPlayed")
            {
                string gameActivityPath = null;
                string path = Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, "afbb1a0d-04a1-4d0c-9afa-c6e42ca855b4", "GameActivity");
                if (Directory.Exists(path))
                {
                    gameActivityPath = path;
                }

                if (!string.IsNullOrEmpty(gameActivityPath))
                {
                    mostPlayedViewModel = new MostPlayedViewModel(PlayniteApi, SettingsViewModel, gameActivityViewModel.Value);
                    var view = new MostPlayedView() { DataContext = mostPlayedViewModel };

                    return view;
                }
            }
            if (id == "ToolbarWidget") return new ToolbarWidgetView();
            if (id == "DividerWidget") return new DividerWidgetView();
            if (id == "ShortcutWidget") return new ShortcutWidgetView();
            if (id == "ImageWidget")
            {
                return new ImageWidgetView();
            }
            if (id == "TextWidget")
            {
                return new TextWidgetView();
            }
            if (id == "DigitalClock")
            {
                return new ClockView();
            }
            if (id == "WeeklyActivity")
            {
                string gameActivityPath = null;
                string path = Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, "afbb1a0d-04a1-4d0c-9afa-c6e42ca855b4", "GameActivity");
                if (Directory.Exists(path))
                {
                    gameActivityPath = path;
                }
                if (!string.IsNullOrEmpty(gameActivityPath))
                {
                    var view = new GameActivityView() { DataContext = gameActivityViewModel.Value };
                    return view;
                }
            }
            return null;
        }

        public Control GetStartPageViewSettings(string id, Guid instanceId)
        {
            if (id == "MostPlayed")
            {
                return new Views.Settings.MostPlayedSettingsView() { DataContext = SettingsViewModel };
            }
            if (id == "GameShelves")
            {
                return new Views.Settings.ShelvesSettingsView() { DataContext = new ViewModels.ShelvesSettingsViewModel(SettingsViewModel, Settings.ShelveInstanceSettings[instanceId]) };
            }
            if (id == "RecentAchivements")
            {
                return new Views.Settings.AchievementsSettingsView() { DataContext = new AchievementsSettingsViewModel(Settings) };
            }
            return null;
        }

        public void OnViewRemoved(string viewId, Guid instanceId)
        {
            if (achievementWidgetViews?.TryGetValue(instanceId,out var achievement) == true) { achievement.OnViewClosed(); achievementWidgetViews.Remove(instanceId); }
            if((viewId == "LibraryStatistics" || viewId == "LibraryChart") && libraryStatisticsViews.TryGetValue(instanceId,out var stats)) { stats.OnViewClosed(); libraryStatisticsViews.Remove(instanceId); }
            if ((viewId == "FeaturedGame" || Models.GameDetailWidgets.Find(viewId) != null) && featuredGameViews?.TryGetValue(instanceId, out var featured) == true)
            { featured.OnViewClosed(); featuredGameViews.Remove(instanceId); }
            if (viewId == "GameShelves")
            {
                // Shelves on pages that were never opened have no view model; still drop their settings.
                if (shelvesViewModels.TryGetValue(instanceId, out var shelves)) shelves.OnViewClosed();
                shelvesViewModels.Remove(instanceId);
                Settings.ShelveInstanceSettings.Remove(instanceId);
            }
            if (viewId == "MostPlayed")
            {
                mostPlayedViewModel?.OnViewClosed();
                mostPlayedViewModel = null;
            }
            if (viewId == "WeeklyActivity")
            {

            }
            GC.Collect();
            GC.Collect(1);
            GC.Collect(2);
        }
    }
}