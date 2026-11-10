using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
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
            };
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

        public void SaveCurrentPage()
        {
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
            try
            {
                await HidePageAsync();
                plugin.SavePluginSettings(Settings.Settings);
                ConfirmPageDeletion = false;
                selectedPage = page;
                Settings.Settings.ActiveDashboardPageId = page.Id;
                Settings.Settings.GridLayout = page.Layout;
                if (!pageRoots.TryGetValue(page.Id, out var root))
                    pageRoots[page.Id] = root = new GridNodeViewModel(page.Layout);
                RootNodeViewModel = root;
                OnPropertyChanged(nameof(SelectedPage));
                ApplyPageBackground(page);
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
                SaveCurrentPage();
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

        private void PageSettingsReplaced(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(LandingPageSettingsViewModel.Settings)) return;
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
