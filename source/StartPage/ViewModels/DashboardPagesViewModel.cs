using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using LandingPage.Models;
using LandingPage.Models.Layout;
using LandingPage.ViewModels.Layout;
using Playnite.SDK;

namespace LandingPage.ViewModels
{
    public partial class StartPageViewModel
    {
        private readonly Dictionary<Guid, GridNodeViewModel> pageRoots = new Dictionary<Guid, GridNodeViewModel>();
        private readonly HashSet<Guid> initializedPages = new HashSet<Guid>();
        private DashboardPage selectedPage;
        public ObservableCollection<DashboardPage> Pages => Settings.Settings.DashboardPages;
        public DashboardPage SelectedPage
        {
            get => selectedPage;
            set { if (value != null && value != selectedPage) _ = SwitchPageAsync(value); }
        }
        private bool isSwitchingPage;
        public bool IsSwitchingPage
        {
            get => isSwitchingPage;
            private set { SetValue(ref isSwitchingPage, value); OnPropertyChanged(nameof(CanChangePages)); }
        }
        public bool CanChangePages => !IsSwitchingPage && !IsLoading;
        private string newPageName = "New page";
        public string NewPageName { get => newPageName; set => SetValue(ref newPageName, value); }
        private bool confirmPageDeletion;
        public bool ConfirmPageDeletion { get => confirmPageDeletion; set => SetValue(ref confirmPageDeletion, value); }
        public ICommand PreviousPageCommand { get; private set; }
        public ICommand NextPageCommand { get; private set; }
        public ICommand AddPageCommand { get; private set; }
        public ICommand MovePageUpCommand { get; private set; }
        public ICommand MovePageDownCommand { get; private set; }
        public ICommand RequestDeletePageCommand { get; private set; }
        public ICommand CancelDeletePageCommand { get; private set; }
        public ICommand DeletePageCommand { get; private set; }
        public ICommand BrowsePageImageCommand { get; private set; }
        public ICommand BrowsePageFolderCommand { get; private set; }
        public ICommand ClearPageImageCommand { get; private set; }

        private void InitializePages()
        {
            selectedPage = DashboardPages.Ensure(Settings.Settings);
            InitializeProfiles();
            selectedPage.Layout = RootNodeViewModel.GridNode;
            pageRoots[selectedPage.Id] = RootNodeViewModel;
            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(IsLoading)) OnPropertyChanged(nameof(CanChangePages));
                // WPF otherwise waits for mouse/keyboard input to requery RelayCommand.
                // Re-enable navigation as soon as switching/loading completes.
                if (args.PropertyName == nameof(CanChangePages) || args.PropertyName == nameof(SelectedPage)
                    || args.PropertyName == nameof(Pages) || args.PropertyName == nameof(SelectedProfile)
                    || args.PropertyName == nameof(ConfirmPageDeletion) || args.PropertyName == nameof(ConfirmDeleteProfile))
                    CommandManager.InvalidateRequerySuggested();
            };
            WatchPageInputs();
            Settings.PropertyChanged += PageSettingsReplaced;
            PreviousPageCommand = new RelayCommand(async () =>
            {
                var index = Pages.IndexOf(SelectedPage);
                if (CanChangePages && index > 0) await SwitchPageAsync(Pages[index - 1]);
            }, () => CanChangePages && Pages.IndexOf(SelectedPage) > 0);
            NextPageCommand = new RelayCommand(async () =>
            {
                var index = Pages.IndexOf(SelectedPage);
                if (CanChangePages && index < Pages.Count - 1) await SwitchPageAsync(Pages[index + 1]);
            }, () => CanChangePages && Pages.IndexOf(SelectedPage) < Pages.Count - 1);
            AddPageCommand = new RelayCommand(async () =>
            {
                if (!CanChangePages) return;
                CaptureCurrentPage();
                var page = DashboardPages.Add(Settings.Settings, NewPageName);
                page.Background = SelectedPage.Background.Copy();
                await SwitchPageAsync(page);
            }, () => CanChangePages);
            MovePageUpCommand = new RelayCommand(() => MovePage(-1), () => CanChangePages && Pages.IndexOf(SelectedPage) > 0);
            MovePageDownCommand = new RelayCommand(() => MovePage(1), () => CanChangePages && Pages.IndexOf(SelectedPage) < Pages.Count - 1);
            RequestDeletePageCommand = new RelayCommand(() => ConfirmPageDeletion = true, () => CanChangePages && Pages.Count > 1);
            CancelDeletePageCommand = new RelayCommand(() => ConfirmPageDeletion = false);
            DeletePageCommand = new RelayCommand(async () =>
            {
                if (!CanChangePages || Pages.Count < 2 || !ConfirmPageDeletion) return;
                var removed = SelectedPage;
                var target = Pages[Pages.IndexOf(removed) == 0 ? 1 : Pages.IndexOf(removed) - 1];
                await SwitchPageAsync(target);
                if (SelectedPage != target) return;
                GridNodeViewModel.OnNodeRemoved(removed.Layout);
                Pages.Remove(removed);
                pageRoots.Remove(removed.Id);
                initializedPages.Remove(removed.Id);
                SaveCurrentPage();
            }, () => CanChangePages && Pages.Count > 1 && ConfirmPageDeletion);
            BrowsePageImageCommand = new RelayCommand(() =>
            {
                var path = playniteAPI.Dialogs.SelectFile("Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.webp|All files|*.*");
                if (!string.IsNullOrEmpty(path)) SetPageImage(path);
            }, () => CanChangePages);
            BrowsePageFolderCommand = new RelayCommand(() =>
            {
                var path = playniteAPI.Dialogs.SelectFolder();
                if (!string.IsNullOrEmpty(path)) SetPageImage(path);
            }, () => CanChangePages);
            ClearPageImageCommand = new RelayCommand(() => SetPageImage(null), () => CanChangePages);
        }

        public void SetPageImage(string path)
        {
            BackgroundViewModel.IsApplyingPage = true;
            try
            {
                Settings.Settings.BackgroundImagePath = path;
                Settings.Settings.BackgroundImageUri = System.IO.File.Exists(path)
                    ? new Uri(System.IO.Path.GetFullPath(path)) : null;
            }
            finally { BackgroundViewModel.IsApplyingPage = false; }
            BackgroundViewModel.RefreshPageBackground();
            SaveCurrentPage();
        }

        private void MovePage(int offset)
        {
            var index = Pages.IndexOf(SelectedPage);
            var target = index + offset;
            if (!CanChangePages || index < 0 || target < 0 || target >= Pages.Count) return;
            Pages.Move(index, target);
            SaveCurrentPage();
        }

        public void CaptureCurrentPage()
        {
            if (SelectedPage == null) return;
            SelectedPage.Layout = RootNodeViewModel.GridNode;
            SelectedPage.Background = DashboardBackground.Capture(Settings.Settings);
            if (plugin.lastBackgroundUri != null)
            {
                // The playing-game background is temporary, not a page customization.
                SelectedPage.Background.Blur = plugin.lastBlur;
                SelectedPage.Background.RenderScale = plugin.lastBackgroundScale;
                SelectedPage.Background.AnimationDuration = plugin.lastAnimationDuration;
            }
            Settings.Settings.GridLayout = SelectedPage.Layout;
            Settings.Settings.ActiveDashboardPageId = SelectedPage.Id;
        }

        private DispatcherTimer navigationSaveTimer;

        private void QueueNavigationSave()
        {
            CaptureCurrentPage();
            if (navigationSaveTimer == null)
            {
                navigationSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
                { Interval = TimeSpan.FromMilliseconds(750) };
                navigationSaveTimer.Tick += (sender, args) =>
                {
                    try { SaveCurrentPage(); }
                    catch (Exception ex) { LandingPageExtension.logger?.Warn(ex, "Unable to save dashboard navigation."); }
                };
            }
            navigationSaveTimer.Stop(); navigationSaveTimer.Start();
        }

        public void SaveCurrentPage()
        {
            navigationSaveTimer?.Stop();
            CaptureCurrentPage();
            plugin.SavePluginSettings(Settings.Settings);
        }

        private void ApplyPageBackground(DashboardPage page)
        {
            BackgroundViewModel.IsApplyingPage = true;
            try { page.Background.Apply(Settings.Settings); }
            finally { BackgroundViewModel.IsApplyingPage = false; }
            BackgroundViewModel.RefreshPageBackground();
            if (plugin.lastBackgroundUri != null)
            {
                plugin.lastBackgroundUri = BackgroundViewModel.BackgroundImagePath;
                plugin.lastBlur = Settings.Settings.BlurAmount;
                plugin.lastBackgroundScale = Settings.Settings.RenderScale;
                plugin.lastAnimationDuration = Settings.Settings.AnimationDuration;
            }
        }

        public async Task SwitchPageAsync(DashboardPage page)
        {
            if (page == null || page == SelectedPage || !Pages.Contains(page) || !CanChangePages)
            { OnPropertyChanged(nameof(SelectedPage)); return; }
            IsSwitchingPage = true;
            IsLoading = true;
            var previous = SelectedPage;
            var previousRoot = RootNodeViewModel;
            var switchWatch = System.Diagnostics.Stopwatch.StartNew();
            long backgroundMilliseconds = 0;
            try
            {
                // Capture the outgoing projection before installing the next background.
                // Neither widget shutdown nor settings serialization should hold the old
                // page's artwork on screen while the destination is loading.
                CaptureCurrentPage();
                ApplyPageBackground(page);
                backgroundMilliseconds = switchWatch.ElapsedMilliseconds;
                await Dispatcher.Yield(DispatcherPriority.Background);
                await HidePageAsync(false);
                ConfirmPageDeletion = false;
                selectedPage = page;
                Settings.Settings.ActiveDashboardPageId = page.Id;
                Settings.Settings.GridLayout = page.Layout;
                if (!pageRoots.TryGetValue(page.Id, out var root))
                    pageRoots[page.Id] = root = new GridNodeViewModel(page.Layout);
                RootNodeViewModel = root;
                OnPropertyChanged(nameof(SelectedPage));
                if (initializedPages.Contains(page.Id))
                {
                    if (isPageVisible) await OpenPageAsync();
                }
                else
                {
                    await root.InititalizeGrid();
                    await InitializeAndOpenViews();
                    initializedPages.Add(page.Id);
                    if (!isPageVisible) await HidePageAsync();
                }
                // Let the destination render before serializing profiles/settings;
                // rapid navigation only needs to persist the final active page.
                QueueNavigationSave();
                LandingPageExtension.logger?.Debug($"Dashboard switch '{page.Name}': background selected {backgroundMilliseconds} ms; ready {switchWatch.ElapsedMilliseconds} ms.");
            }
            catch (Exception ex)
            {
                LandingPageExtension.logger.Error(ex, "Unable to switch dashboard page.");
                selectedPage = previous;
                RootNodeViewModel = previousRoot;
                Settings.Settings.ActiveDashboardPageId = previous.Id;
                Settings.Settings.GridLayout = previous.Layout;
                OnPropertyChanged(nameof(SelectedPage));
                ApplyPageBackground(previous);
                if (isPageVisible) await OpenPageAsync();
                playniteAPI.Dialogs.ShowErrorMessage("Unable to switch dashboard page. Your previous page has been restored.", "Dashboard pages");
            }
            finally { IsLoading = false; IsSwitchingPage = false; }
        }

        private LandingPageSettings observedPageSettings;
        private ObservableCollection<DashboardPage> observedPages;

        private void WatchPageInputs()
        {
            if (observedPageSettings != null) observedPageSettings.PropertyChanged -= PageInputChanged;
            if (observedPages != null) observedPages.CollectionChanged -= PageOrderChanged;
            observedPageSettings = Settings.Settings;
            observedPages = observedPageSettings.DashboardPages;
            observedPageSettings.PropertyChanged += PageInputChanged;
            if (observedPages != null) observedPages.CollectionChanged += PageOrderChanged;
            CommandManager.InvalidateRequerySuggested();
        }
        private void PageOrderChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
            => CommandManager.InvalidateRequerySuggested();
        private void PageInputChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(LandingPageSettings.DashboardPages)) WatchPageInputs();
            if (args.PropertyName == nameof(LandingPageSettings.BackgroundImageSource)
                || args.PropertyName == nameof(LandingPageSettings.BackgroundImagePath)
                || args.PropertyName == nameof(LandingPageSettings.BackgroundImageUri)) CommandManager.InvalidateRequerySuggested();
        }

        private void PageSettingsReplaced(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(LandingPageSettingsViewModel.Settings)) return;
            WatchPageInputs();
            // Canceling the settings dialog supplies cloned models. Keep cached live
            // layouts attached to those models without resetting inactive backgrounds.
            foreach (var page in Settings.Settings.DashboardPages)
                if (pageRoots.TryGetValue(page.Id, out var root)) page.Layout = root.GridNode;
            var replacement = Pages.FirstOrDefault(page => page.Id == selectedPage.Id);
            if (replacement != null) selectedPage = replacement;
            CaptureCurrentPage();
            OnPropertyChanged(nameof(Pages));
            OnPropertyChanged(nameof(SelectedPage));
            SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == SelectedProfile?.Id) ?? Profiles.FirstOrDefault();
            OnPropertyChanged(nameof(Profiles));
        }
    }
}
