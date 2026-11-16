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
    public sealed class DashboardTransitionEventArgs : EventArgs
    {
        public bool Forward { get; set; }
    }

    public partial class StartPageViewModel
    {
        public event EventHandler<DashboardTransitionEventArgs> PageTransitionStarting;
        public event Func<Task> PageTransitionFinishing;
        public event Func<Task> PageLayoutReady;
        public event Action<DashboardPage> QuickPageDeletionRequested;

        private readonly Dictionary<Guid, GridNodeViewModel> pageRoots = new Dictionary<Guid, GridNodeViewModel>();
        private readonly HashSet<Guid> initializedPages = new HashSet<Guid>();
        private DashboardPage selectedPage;
        public ObservableCollection<DashboardPage> Pages => Settings.Settings.DashboardPages;
        public DashboardPage SelectedPage
        {
            get => selectedPage;
            set { if (value != null && value != selectedPage) _ = SwitchPageAsync(value); }
        }
        public Guid SelectedPageId
        {
            get => selectedPage?.Id ?? Guid.Empty;
            set
            {
                var page = Pages.FirstOrDefault(item => item.Id == value);
                if (page != null && page != selectedPage) SelectedPage = page;
            }
        }
        private bool isSwitchingPage;
        public bool IsSwitchingPage
        {
            get => isSwitchingPage;
            private set { SetValue(ref isSwitchingPage, value); OnPropertyChanged(nameof(CanChangePages)); }
        }
        public bool CanChangePages => !IsSwitchingPage && !IsLoading;
        private Dictionary<Guid,string> draftPageNames;
        private LandingPageSettings pageEditSnapshot;
        private string pageEditBaseline;
        public string EditedPageName
        {
            get => SelectedPage == null ? string.Empty : draftPageNames != null && draftPageNames.TryGetValue(SelectedPage.Id,out var name) ? name : SelectedPage.Name;
            set { if(SelectedPage != null && draftPageNames != null) { draftPageNames[SelectedPage.Id] = value; OnPropertyChanged(nameof(EditedPageName)); } }
        }
        public void BeginPageEditing()
        {
            if(draftPageNames != null) return;
            SetPagesSelectedForDeletion(Enumerable.Empty<DashboardPage>());
            CaptureCurrentPage(); navigationSaveTimer?.Stop();
            LandingPageExtension.logger.Info($"Dashboard page editor begin: active={SelectedPage?.Id}; pages={Pages.Count}.");
            pageEditSnapshot = DashboardProfiles.Clone(Settings.Settings);
            pageEditBaseline = Newtonsoft.Json.JsonConvert.SerializeObject(Settings.Settings);
            draftPageNames = Pages.ToDictionary(page => page.Id,page => page.Name);
            PageSelectedForOrdering = Pages.FirstOrDefault(page => page.Id == PageSelectedForOrdering?.Id) ?? SelectedPage;
            RefreshPageDeletionCommands();
            OnPropertyChanged(nameof(EditedPageName));
        }
        public void EndPageNameEditing(bool save)
        {
            if(draftPageNames == null) return;
            if(save) foreach(var page in Pages) if(draftPageNames.TryGetValue(page.Id,out var name)) page.Name = name;
            draftPageNames = null; OnPropertyChanged(nameof(EditedPageName));
        }
        public async Task EndPageEditingAsync(bool save)
        {
            var snapshot = pageEditSnapshot;
            var changed = !save && snapshot != null && Newtonsoft.Json.JsonConvert.SerializeObject(Settings.Settings) != pageEditBaseline;
            LandingPageExtension.logger.Info($"Dashboard page editor finish requested: save={save}; active={SelectedPage?.Id}; pages={Pages.Count}; draft={snapshot != null}.");
            if (save)
            {
                var pendingNames = draftPageNames;
                try
                {
                    EndPageNameEditing(true);
                    CaptureCurrentPage();
                    // Commit explicitly while retaining the rollback snapshot until the disk write succeeds.
                    plugin.SavePluginSettings(Settings.Settings);
                    var configPath = System.IO.Path.Combine(playniteAPI.Paths.ExtensionsDataPath, plugin.Id.ToString(), "config.json");
                    var saved = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(configPath));
                    var storedPages = saved["DashboardPages"] as Newtonsoft.Json.Linq.JArray;
                    if (storedPages == null || storedPages.Count != Pages.Count
                        || Pages.Any(page => !storedPages.Any(stored => (Guid?)stored["Id"] == page.Id))
                        || (Guid?)saved["ActiveDashboardPageId"] != SelectedPage?.Id)
                        throw new InvalidOperationException("The saved configuration does not contain the current dashboard pages.");
                    ExtensionDiagnostics.Record(plugin, $"Dashboard page save verified on disk: {configPath}; pages={storedPages.Count}.");
                    pageEditSnapshot = null; pageEditBaseline = null;
                    RefreshPageDeletionCommands();
                    navigationSaveTimer?.Stop();
                    LandingPageExtension.logger.Info($"Dashboard page editor saved: active={SelectedPage?.Id}; pages={Pages.Count}; layouts=[{string.Join("; ", Pages.Select(page => page.Id + ":" + string.Join(",", page.Layout.Enumerate().Where(node => node.ViewProperties != null).Select(node => node.ViewProperties.ViewId))))}].");
                }
                catch (Exception ex)
                {
                    draftPageNames = pendingNames;
                    LandingPageExtension.logger.Error(ex, "Dashboard page editor save failed; pending pages retained for retry.");
                    throw;
                }
                return;
            }
            EndPageNameEditing(false); pageEditSnapshot = null; pageEditBaseline = null;
            RefreshPageDeletionCommands();
            navigationSaveTimer?.Stop();
            if(!changed) return;
            LandingPageExtension.logger.Info($"Dashboard page editor cancelling pending changes: restoring {snapshot.DashboardPages.Count} pages.");
            var target = snapshot.DashboardPages.FirstOrDefault(page => page.Id == SelectedPage?.Id)
                ?? snapshot.DashboardPages.First(page => page.Id == snapshot.ActiveDashboardPageId);
            foreach(var added in Pages.Where(page => !snapshot.DashboardPages.Any(original => original.Id == page.Id)).ToList())
            { GridNodeViewModel.OnNodeRemoved(added.Layout); pageRoots.Remove(added.Id); initializedPages.Remove(added.Id); }
            var oldRoot = RootNodeViewModel;
            if(!pageRoots.TryGetValue(target.Id,out var restoredRoot)) pageRoots[target.Id] = restoredRoot = new GridNodeViewModel(target.Layout);
            if(oldRoot != restoredRoot) await HidePageAsync(false);
            selectedPage = target; snapshot.ActiveDashboardPageId = target.Id; target.Background.Apply(snapshot); snapshot.GridLayout = restoredRoot.GridNode;
            RootNodeViewModel = restoredRoot;
            Settings.Settings = snapshot;
            ApplyPageBackground(SelectedPage);
            OnPropertyChanged(nameof(Pages)); OnPropertyChanged(nameof(SelectedPage));
            if(oldRoot != restoredRoot)
            {
                if(!initializedPages.Contains(target.Id)) { await restoredRoot.InititalizeGrid(); await InitializeAndOpenViews(); initializedPages.Add(target.Id); }
                else if(isPageVisible) await OpenPageAsync();
            }
        }
        private string newPageName = "New page";
        public string NewPageName { get => newPageName; set => SetValue(ref newPageName, value); }
        private bool confirmPageDeletion;
        public bool ConfirmPageDeletion { get => confirmPageDeletion; set => SetValue(ref confirmPageDeletion, value); }
        public ICommand PreviousPageCommand { get; private set; }
        public ICommand NextPageCommand { get; private set; }
        public ICommand AddPageCommand { get; private set; }
        public ICommand QuickAddPageCommand { get; private set; }
        public ICommand QuickRemovePageCommand { get; private set; }
        public ICommand MovePageUpCommand { get; private set; }
        public ICommand MovePageDownCommand { get; private set; }
        private DashboardPage pageSelectedForOrdering;
        public DashboardPage PageSelectedForOrdering
        {
            get => pageSelectedForOrdering;
            set { SetValue(ref pageSelectedForOrdering, value); RefreshPageDeletionCommands(); }
        }
        public ICommand MoveOrderedPageUpCommand { get; private set; }
        public ICommand MoveOrderedPageDownCommand { get; private set; }
        public ICommand MoveOrderedPageFirstCommand { get; private set; }
        public ICommand MoveOrderedPageLastCommand { get; private set; }
        private bool CanOrderPage => CanChangePages && PageSelectedForOrdering != null && Pages.Contains(PageSelectedForOrdering);
        private void MoveOrderedPage(int destination)
        {
            if (!CanOrderPage || destination < 0 || destination >= Pages.Count) return;
            var page = PageSelectedForOrdering;
            Pages.Move(Pages.IndexOf(page), destination);
            PageSelectedForOrdering = page;
            SaveCurrentPage();
        }
        private sealed class PageDeletionCommand : ICommand
        {
            private readonly Action execute;
            private readonly Func<bool> canExecute;
            public PageDeletionCommand(Action execute, Func<bool> canExecute) { this.execute = execute; this.canExecute = canExecute; }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter) => canExecute();
            public void Execute(object parameter) { if (CanExecute(parameter)) execute(); }
            public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        private void RefreshPageDeletionCommands()
        {
            (RequestDeleteSelectedPagesCommand as PageDeletionCommand)?.Refresh();
            (DeleteSelectedPagesCommand as PageDeletionCommand)?.Refresh();
            (QuickAddPageCommand as PageDeletionCommand)?.Refresh();
            (QuickRemovePageCommand as PageDeletionCommand)?.Refresh();
            (MoveOrderedPageUpCommand as PageDeletionCommand)?.Refresh();
            (MoveOrderedPageDownCommand as PageDeletionCommand)?.Refresh();
            (MoveOrderedPageFirstCommand as PageDeletionCommand)?.Refresh();
            (MoveOrderedPageLastCommand as PageDeletionCommand)?.Refresh();
        }
        public ObservableCollection<DashboardPage> PagesSelectedForDeletion { get; } = new ObservableCollection<DashboardPage>();
        public string PageDeletionSummary => PagesSelectedForDeletion.Count == 0 ? "Select pages to delete. At least one page must remain."
            : PagesSelectedForDeletion.Count >= Pages.Count ? "Keep at least one page: deselect a page to continue."
            : $"Delete {PagesSelectedForDeletion.Count} selected page(s)? Changes are saved when you press Done.";
        public void SetPagesSelectedForDeletion(IEnumerable<DashboardPage> pages)
        {
            var ids = new HashSet<Guid>((pages ?? Enumerable.Empty<DashboardPage>()).Select(page => page.Id));
            var selected = Pages.Where(page => ids.Contains(page.Id)).ToArray();
            PagesSelectedForDeletion.Clear();
            foreach (var page in selected) PagesSelectedForDeletion.Add(page);
            ConfirmPageDeletion = false;
            OnPropertyChanged(nameof(PageDeletionSummary));
            RefreshPageDeletionCommands();
            CommandManager.InvalidateRequerySuggested();
        }
        private bool CanDeleteSelectedPages => CanChangePages && PagesSelectedForDeletion.Count > 0
            && PagesSelectedForDeletion.Count < Pages.Count;
        public ICommand RequestDeleteSelectedPagesCommand { get; private set; }
        public ICommand DeleteSelectedPagesCommand { get; private set; }
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
                if (args.PropertyName == nameof(SelectedPage)) { OnPropertyChanged(nameof(SelectedPageId)); OnPropertyChanged(nameof(EditedPageName)); }
                if (args.PropertyName == nameof(CanChangePages) || args.PropertyName == nameof(ConfirmPageDeletion)
                    || args.PropertyName == nameof(Pages)) RefreshPageDeletionCommands();
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
                LandingPageExtension.logger.Info($"Dashboard page created: id={page.Id}; panels={page.Layout.Children.Count}; widgets={string.Join(",", page.Layout.Enumerate().Where(node => node.ViewProperties != null).Select(node => node.ViewProperties.ViewId))}; editorPending={pageEditSnapshot != null}.");
                page.Background = SelectedPage.Background.Copy();
                await SwitchPageAsync(page);
            }, () => CanChangePages);
            QuickAddPageCommand = new PageDeletionCommand(async () =>
            {
                try
                {
                    CaptureCurrentPage();
                    var page = DashboardPages.Add(Settings.Settings, "New page");
                    page.Background = SelectedPage.Background.Copy();
                    await SwitchPageAsync(page);
                    SaveCurrentPage();
                    LandingPageExtension.logger.Info($"Quick dashboard page added: id={page.Id}.");
                }
                catch (Exception ex)
                {
                    LandingPageExtension.logger.Error(ex, "Unable to quick-add a dashboard page.");
                    playniteAPI.Dialogs?.ShowErrorMessage("The page could not be added. Please check the debug log.", "Dashboard pages");
                }
            }, () => CanChangePages && pageEditSnapshot == null);
            QuickRemovePageCommand = new PageDeletionCommand(() => QuickPageDeletionRequested?.Invoke(SelectedPage),
                () => CanChangePages && pageEditSnapshot == null && Pages.Count > 1);
            MoveOrderedPageUpCommand = new PageDeletionCommand(() => MoveOrderedPage(Pages.IndexOf(PageSelectedForOrdering) - 1),
                () => CanOrderPage && Pages.IndexOf(PageSelectedForOrdering) > 0);
            MoveOrderedPageDownCommand = new PageDeletionCommand(() => MoveOrderedPage(Pages.IndexOf(PageSelectedForOrdering) + 1),
                () => CanOrderPage && Pages.IndexOf(PageSelectedForOrdering) < Pages.Count - 1);
            MoveOrderedPageFirstCommand = new PageDeletionCommand(() => MoveOrderedPage(0),
                () => CanOrderPage && Pages.IndexOf(PageSelectedForOrdering) > 0);
            MoveOrderedPageLastCommand = new PageDeletionCommand(() => MoveOrderedPage(Pages.Count - 1),
                () => CanOrderPage && Pages.IndexOf(PageSelectedForOrdering) < Pages.Count - 1);
            MovePageUpCommand = new RelayCommand(() => MovePage(-1), () => CanChangePages && Pages.IndexOf(SelectedPage) > 0);
            MovePageDownCommand = new RelayCommand(() => MovePage(1), () => CanChangePages && Pages.IndexOf(SelectedPage) < Pages.Count - 1);
            RequestDeletePageCommand = new RelayCommand(() => ConfirmPageDeletion = true, () => CanChangePages && Pages.Count > 1);
            CancelDeletePageCommand = new RelayCommand(() => ConfirmPageDeletion = false);
            DeletePageCommand = new RelayCommand(async () =>
            {
                if (CanChangePages && Pages.Count > 1 && ConfirmPageDeletion)
                    await DeletePagesAsync(new[] { SelectedPage });
            }, () => CanChangePages && Pages.Count > 1 && ConfirmPageDeletion);
            RequestDeleteSelectedPagesCommand = new PageDeletionCommand(() => ConfirmPageDeletion = true, () => CanDeleteSelectedPages);
            DeleteSelectedPagesCommand = new PageDeletionCommand(async () =>
            {
                if (CanDeleteSelectedPages && ConfirmPageDeletion)
                    await DeletePagesAsync(PagesSelectedForDeletion.ToArray());
            }, () => CanDeleteSelectedPages && ConfirmPageDeletion);
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

        public async Task RemovePageQuickAsync(DashboardPage page)
        {
            if (!CanChangePages || pageEditSnapshot != null || Pages.Count <= 1 || SelectedPage != page) return;
            await DeletePagesAsync(new[] { page });
        }

        private async Task DeletePagesAsync(IEnumerable<DashboardPage> selection)
        {
            var ids = new HashSet<Guid>(selection.Select(page => page.Id));
            var removed = Pages.Where(page => ids.Contains(page.Id)).ToArray();
            if (!CanChangePages || removed.Length == 0 || removed.Length >= Pages.Count) return;
            var active = SelectedPage;
            if (ids.Contains(active.Id))
            {
                var index = Pages.IndexOf(active);
                var target = Pages.Take(index).LastOrDefault(page => !ids.Contains(page.Id))
                    ?? Pages.First(page => !ids.Contains(page.Id));
                await SwitchPageAsync(target);
                if (SelectedPage != target) return;
            }
            foreach (var page in removed)
            {
                GridNodeViewModel.OnNodeRemoved(page.Layout);
                Pages.Remove(page);
                pageRoots.Remove(page.Id);
                initializedPages.Remove(page.Id);
            }
            SetPagesSelectedForDeletion(Enumerable.Empty<DashboardPage>());
            LandingPageExtension.logger.Info($"Dashboard pages deleted: count={removed.Length}; active={SelectedPage.Id}; remaining={Pages.Count}.");
            SaveCurrentPage();
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
            if(pageEditSnapshot == null)
            {
                plugin.SavePluginSettings(Settings.Settings);
                ExtensionDiagnostics.Record(plugin, $"Dashboard pages saved: active={SelectedPage?.Id}; count={Pages.Count}.");
            }
            else ExtensionDiagnostics.Record(plugin, "Dashboard save deferred: page editor is waiting for Done or Cancel.");
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
            navigationSaveTimer?.Stop();
            IsSwitchingPage = true;
            IsLoading = true;
            var previous = SelectedPage;
            var previousRoot = RootNodeViewModel;
            var switchWatch = System.Diagnostics.Stopwatch.StartNew();
            ExtensionDiagnostics.Record(plugin, $"Switch start: '{previous?.Name}' -> '{page.Name}', cached layout: {initializedPages.Contains(page.Id)}.");
            long backgroundMilliseconds = 0;
            try
            {
                PageTransitionStarting?.Invoke(this, new DashboardTransitionEventArgs { Forward = Pages.IndexOf(page) > Pages.IndexOf(previous) });
                // Capture the outgoing projection before installing the next background.
                // Neither widget shutdown nor settings serialization should hold the old
                // page's artwork on screen while the destination is loading.
                CaptureCurrentPage();
                ApplyPageBackground(page);
                backgroundMilliseconds = switchWatch.ElapsedMilliseconds;
                // Do not wait at Background priority with the old dashboard attached:
                // rendering/input from that page can starve the switch continuation.
                ExtensionDiagnostics.Record(plugin, $"Background applied: {switchWatch.ElapsedMilliseconds} ms.");
                await HidePageAsync(false);
                ExtensionDiagnostics.Record(plugin, $"Outgoing widgets hidden: {switchWatch.ElapsedMilliseconds} ms.");
                ConfirmPageDeletion = false;
                selectedPage = page;
                Settings.Settings.ActiveDashboardPageId = page.Id;
                Settings.Settings.GridLayout = page.Layout;
                if (!pageRoots.TryGetValue(page.Id, out var root))
                    pageRoots[page.Id] = root = new GridNodeViewModel(page.Layout);
                RootNodeViewModel = root;
                OnPropertyChanged(nameof(SelectedPage));
                ExtensionDiagnostics.Record(plugin, $"Destination layout attached: {switchWatch.ElapsedMilliseconds} ms.");
                if (initializedPages.Contains(page.Id))
                {
                    if (isPageVisible) await OpenPageAsync();
                }
                else
                {
                    await root.InititalizeGrid();
                    ExtensionDiagnostics.Record(plugin, $"Destination layout initialized: {switchWatch.ElapsedMilliseconds} ms.");
                    await InitializeAndOpenViews();
                    initializedPages.Add(page.Id);
                    if (!isPageVisible) await HidePageAsync();
                }
                if (PageLayoutReady != null)
                    foreach (Func<Task> arrange in PageLayoutReady.GetInvocationList()) await arrange();
                // Let the destination render before serializing profiles/settings;
                // rapid navigation only needs to persist the final active page.
                var timing = $"Dashboard switch '{page.Name}': background selected {backgroundMilliseconds} ms; ready {switchWatch.ElapsedMilliseconds} ms.";
                LandingPageExtension.logger?.Debug(timing);
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
                if(playniteAPI.Dialogs != null) playniteAPI.Dialogs.ShowErrorMessage("Unable to switch dashboard page. Your previous page has been restored.", "Dashboard pages");
                else throw;
            }
            finally
            {
                try
                {
                    if (PageTransitionFinishing != null)
                        foreach (Func<Task> finish in PageTransitionFinishing.GetInvocationList()) await finish();
                }
                finally
                {
                    IsLoading = false; IsSwitchingPage = false;
                    ExtensionDiagnostics.Record(plugin, $"Switch complete (including transition): {switchWatch.ElapsedMilliseconds} ms.");
                    QueueNavigationSave();
                    var displayedRoot = RootNodeViewModel;
                    if (Settings.Settings.EnableDashboardDiagnostics) _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                    {
                        if (displayedRoot != RootNodeViewModel) return;
                        ExtensionDiagnostics.Record(plugin, $"Dashboard rendered: page={SelectedPage?.Id}; root={displayedRoot.View.ActualWidth:0.#}x{displayedRoot.View.ActualHeight:0.#}; nodes=[{string.Join("; ", displayedRoot.AllNodes.Select(node => (node.GridNode.ViewProperties?.ViewId ?? "panel") + ":" + node.View.ActualWidth.ToString("0.#") + "x" + node.View.ActualHeight.ToString("0.#") + ":visible=" + node.View.IsVisible + ":children=" + node.View.Children.Count))}].");
                    }));
                }
            }
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
        {
            OnPropertyChanged(nameof(PageDeletionSummary));
            RefreshPageDeletionCommands();
            CommandManager.InvalidateRequerySuggested();
        }
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
            LandingPageExtension.logger.Info($"Dashboard settings object replaced: pages={Pages?.Count ?? 0}; active={Settings.Settings.ActiveDashboardPageId}; editorPending={pageEditSnapshot != null}.");
            WatchPageInputs();
            PageSelectedForOrdering = Pages.FirstOrDefault(page => page.Id == PageSelectedForOrdering?.Id);
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
