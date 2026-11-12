using PlayniteCommon.Extensions;
using LandingPage.Models;
using Playnite.SDK;
using Playnite.SDK.Models;
using StartPage.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StartPage.SDK.Async;
using System.Diagnostics;

namespace LandingPage.ViewModels
{
    public class ShelvesViewModel : BusyObservableObject, IAsyncStartPageControl, IStartPageViewModel
    {
        internal ObservableCollection<ShelveViewModel> shelveViewModels = new ObservableCollection<ShelveViewModel>();
        public ObservableCollection<ShelveViewModel> ShelveViewModels { get => shelveViewModels; set => SetValue(ref shelveViewModels, value); }

        public ICommand AddShelveCommand { get; set; }

        public ICommand RemoveShelveCommand { get; set; }

        public ICommand ExtendShelveCommand { get; set; }

        public ICommand MoveShelveUpCommand { get; set; }
        public ICommand MoveShelveDownCommand { get; set;}

        private bool showDetails;
        public bool ShowDetails { get => showDetails; set => SetValue(ref showDetails, value); }

        public Game LastSelectedGame { get => plugin.startPageViewModel.BackgroundViewModel.LastSelectedGame; set => plugin.startPageViewModel.BackgroundViewModel.LastSelectedGame = value; }

        public Game LastHoveredGame { get => plugin.startPageViewModel.BackgroundViewModel.LastHoveredGame; set => plugin.startPageViewModel.BackgroundViewModel.LastHoveredGame = value; }

        public Game CurrentlyHoveredGame
        {
            get => plugin.startPageViewModel.BackgroundViewModel.CurrentlyHoveredGame;
            set
            {
                if (value is Game && plugin.startPageViewModel.BackgroundViewModel.LastHoveredGame != value)
                {
                    plugin.startPageViewModel.BackgroundViewModel.LastHoveredGame = value;
                }
            }
        }

        private GameModel _currentlyHoveredModel;
        public GameModel CurrentlyHoveredGameModel
        {
            get => _currentlyHoveredModel;
            set => SetValue(ref _currentlyHoveredModel, value);
        }

        private FrameworkElement popupTarget;
        public FrameworkElement PopupTarget { get => popupTarget; set => SetValue(ref popupTarget, value); }

        internal IPlayniteAPI playniteAPI;
        internal LandingPageExtension plugin;

        internal LandingPageSettingsViewModel settings;
        public LandingPageSettingsViewModel Settings => settings;

        internal DispatcherTimer dispatcherTimer;

        private Guid instanceId;
        public Guid InstanceId { get => instanceId; set => SetValue(ref instanceId, value); }

        private LandingPage.Settings.ShelvesSettings shelves;
        

        public LandingPage.Settings.ShelvesSettings Shelves
        {
            get => shelves;
            set
            {
                if (ReferenceEquals(shelves, value)) return;
                if (shelves != null) shelves.PropertyChanged -= Settings_PropertyChangedAsync;
                SetValue(ref shelves, value);
                if (shelves != null) shelves.PropertyChanged += Settings_PropertyChangedAsync;
            }
        }

        public ShelvesViewModel(
            IPlayniteAPI playniteAPI,
            LandingPageExtension landingPage,
            LandingPageSettingsViewModel settings,
            Guid instanceId)
        {
            this.playniteAPI = playniteAPI;
            this.plugin = landingPage;
            this.settings = settings;
            this.instanceId = instanceId;

            dispatcherTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            dispatcherTimer.Tick += DispatcherTimer_TickAsync;
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);

            //Settings.Settings.PropertyChanged += Settings_PropertyChangedAsync;
            Settings.PropertyChanged += Settings_PropertyChanged1Async;
            observedGlobalSettings = Settings.Settings;
            observedGlobalSettings.PropertyChanged += GlobalShelfSettingsChanged;

            if (!Settings.Settings.ShelveInstanceSettings.ContainsKey(InstanceId))
            {
                if (Settings.Settings.ShelveInstanceSettings.Count == 0)
                {
                    Settings.Settings.ShelveInstanceSettings.Add(InstanceId, new LandingPage.Settings.ShelvesSettings(Settings.Settings) { ShelveProperties = Settings.Settings.ShelveProperties });
                } else
                {
                    Settings.Settings.ShelveInstanceSettings.Add(InstanceId, new LandingPage.Settings.ShelvesSettings(Settings.Settings));
                }
            }

            Shelves = Settings.Settings.ShelveInstanceSettings[instanceId];

            foreach (var shelveProperties in Shelves.ShelveProperties)
            {
                ShelveViewModels.Add(new ShelveViewModel(shelveProperties, Shelves, playniteAPI, ShelveViewModels) { ParentViewModel = this });
            }

            AddShelveCommand = new RelayCommand(async () =>
            {
                ShelveViewModel item = new ShelveViewModel(ShelveProperties.RecentlyPlayed, Shelves, playniteAPI, ShelveViewModels) { ParentViewModel = this };
                Shelves.ShelveProperties.Add(item.ShelveProperties);
                ShelveViewModels.Add(item);
                await UpdateShelvesAsync();
            });

            RemoveShelveCommand = new RelayCommand<ShelveViewModel>(async svm =>
            {
                if (svm != null)
                {
                    Shelves.ShelveProperties.Remove(svm.ShelveProperties);
                    ShelveViewModels.Remove(svm);
                    await UpdateShelvesAsync();
                }
            });


            ExtendShelveCommand = new RelayCommand<ShelveViewModel>(async svm =>
            {
                var idx = ShelveViewModels.IndexOf(svm);
                if (idx > -1)
                {
                    var properties = svm.ShelveProperties.Copy();
                    properties.SkippedGames = svm.ShelveProperties.NumberOfGames + svm.ShelveProperties.SkippedGames;
                    properties.Name = string.Empty;
                    ShelveViewModel item = new ShelveViewModel(properties, Shelves, playniteAPI, ShelveViewModels) { ParentViewModel = this };
                    Shelves.ShelveProperties.Insert(idx + 1, item.ShelveProperties);
                    ShelveViewModels.Insert(idx + 1, item);
                    await UpdateShelvesAsync();
                }
            });
            MoveShelveUpCommand = new RelayCommand<ShelveViewModel>(async svm =>
            {
                var idx = ShelveViewModels.IndexOf(svm);
                if (idx > 0)
                {
                    ShelveViewModels.Move(idx, idx - 1);
                    Shelves.ShelveProperties.Move(idx, idx - 1);
                }
                await UpdateShelvesAsync();
            }, svm => ShelveViewModels.IndexOf(svm) > 0);

            MoveShelveDownCommand = new RelayCommand<ShelveViewModel>(async svm =>
            {
                var idx = ShelveViewModels.IndexOf(svm);
                if (idx < ShelveViewModels.Count - 1)
                {
                    ShelveViewModels.Move(idx, idx + 1);
                    Shelves.ShelveProperties.Move(idx, idx + 1);
                }
                await UpdateShelvesAsync();
            }, svm => ShelveViewModels.IndexOf(svm) < ShelveViewModels.Count - 1);
        }

        public Task UpdateShelves()
        {
            return Task.Run(() =>
            {
                IsBusy = true;
                foreach(var shelve in ShelveViewModels)
                {
                    shelve.UpdateGames(shelve.ShelveProperties);
                }
                IsBusy = false;
            });
        }

        private bool closed;
        private DateTime refreshedDate;
        private LandingPageSettings observedGlobalSettings;
        private bool isVisible;
        private bool subscribed;
        private bool hasLoaded;
        private long libraryRevision;
        private long refreshedRevision = -1;
        private readonly System.Threading.SemaphoreSlim refreshGate = new System.Threading.SemaphoreSlim(1, 1);

        public async Task UpdateShelvesAsync()
        {
            if (closed) return;
            var requestRevision = System.Threading.Interlocked.Increment(ref libraryRevision);
            await refreshGate.WaitAsync();
            IsBusy = true;
            var revision = System.Threading.Interlocked.Read(ref libraryRevision);
            var refreshWatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (closed) return;
                if (hasLoaded && refreshedRevision >= requestRevision)
                {
                    ExtensionDiagnostics.Record(plugin, $"Shelf refresh coalesced: widget {InstanceId}.");
                    return;
                }
                var snapshotWatch = System.Diagnostics.Stopwatch.StartNew();
                var library = await Task.Run(() =>
                {
                    var worker = System.Diagnostics.Stopwatch.StartNew();
                    var snapshot = playniteAPI.Database.Games.ToList();
                    ExtensionDiagnostics.Record(plugin, $"Library snapshot worker: widget {InstanceId}, {worker.ElapsedMilliseconds} ms.");
                    return snapshot;
                });
                ExtensionDiagnostics.Record(plugin, $"Library snapshot available: widget {InstanceId}, {snapshotWatch.ElapsedMilliseconds} ms (includes scheduling/UI wait).");
                foreach (var shelve in ShelveViewModels.ToArray())
                {
                    if (closed) return;
                    await shelve.UpdateGamesAsync(shelve.ShelveProperties, false, library);
                }
                hasLoaded = true; refreshedRevision = revision; refreshedDate = DateTime.Today;
                ExtensionDiagnostics.Record(plugin, $"Shelves refreshed: {ShelveViewModels.Count} shelves, {library.Count} library games, {refreshWatch.ElapsedMilliseconds} ms.");
            }
            finally { IsBusy = false; refreshGate.Release(); }
        }

        private async void Games_ItemUpdatedAsync(object sender, ItemUpdatedEventArgs<Game> e)
        {
            if (e.UpdatedItems.Count > 0)
            {
                System.Threading.Interlocked.Increment(ref libraryRevision);
                if (!isVisible) return;
                dispatcherTimer.Stop();
                if(e.UpdatedItems.Any(u => u.NewData.LastActivity != u.OldData.LastActivity))
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await UpdateShelvesAsync();
                    });
                } else
                {
                    dispatcherTimer.Start();
                }
            }
        }

        private async void DispatcherTimer_TickAsync(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();
            dispatcherTimer.Tick -= DispatcherTimer_TickAsync;
            await UpdateShelvesAsync();
            dispatcherTimer.Tick += DispatcherTimer_TickAsync;
        }

        private void Games_ItemCollectionChanged(object sender, ItemCollectionChangedEventArgs<Game> e)
        {
            if (e.RemovedItems.Count + e.AddedItems.Count > 0)
            {
                System.Threading.Interlocked.Increment(ref libraryRevision);
                if (!isVisible) return;
                dispatcherTimer.Stop();
                dispatcherTimer.Start();
            }
        }


        private static bool IsRelevantUpdate(ItemUpdateEvent<Game> update)
        {
            var old = update.OldData;
            var updated = update.NewData;
            if (old.Name != updated.Name) return true;
            if (old.LastActivity != updated.LastActivity) return true;
            if (old.Added != updated.Added) return true;
            if (old.Playtime != updated.Playtime) return true;
            if (old.Hidden != updated.Hidden) return true;
            if (old.Favorite != updated.Favorite) return true;
            if (old.IsInstalled != updated.IsInstalled) return true;
            var oldPlatformIds = old.PlatformIds ?? new List<Guid>();
            var updatedPlatformIds = updated.PlatformIds ?? new List<Guid>();
            if (!(oldPlatformIds.All(id => updatedPlatformIds.Contains(id)) && oldPlatformIds.Count == updatedPlatformIds.Count)) return true;
            var oldFeatureIds = old.FeatureIds ?? new List<Guid>();
            var updatedFeatureIds = updated.FeatureIds ?? new List<Guid>();
            if (!(oldFeatureIds.All(id => updatedFeatureIds.Contains(id)) && oldFeatureIds.Count == updatedFeatureIds.Count)) return true;
            return false;
        }

        public void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            playniteAPI.Database.Games.ItemUpdated += Games_ItemUpdatedAsync;
            playniteAPI.Database.Games.ItemCollectionChanged += Games_ItemCollectionChanged;
            Application.Current.MainWindow.Deactivated += MainWindow_Deactivated;
        }

        public void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            playniteAPI.Database.Games.ItemUpdated -= Games_ItemUpdatedAsync;
            playniteAPI.Database.Games.ItemCollectionChanged -= Games_ItemCollectionChanged;
            Application.Current.MainWindow.Deactivated -= MainWindow_Deactivated;
        }

        public void UnsubscribeSettings()
        {
            Settings.PropertyChanged -= Settings_PropertyChanged1Async;
            if (Shelves != null) Shelves.PropertyChanged -= Settings_PropertyChangedAsync;
            if (observedGlobalSettings != null) observedGlobalSettings.PropertyChanged -= GlobalShelfSettingsChanged;
        }

        private async void GlobalShelfSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (closed || e.PropertyName != nameof(LandingPageSettings.IgnoreTagId)) return;
            System.Threading.Interlocked.Increment(ref libraryRevision);
            if (isVisible) await UpdateShelvesAsync();
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            ShowDetails = false;
            CurrentlyHoveredGame = null;
        }

        private async void Settings_PropertyChangedAsync(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LandingPage.Settings.ShelvesSettings.SkipGamesInPreviousShelves))
            {
                System.Threading.Interlocked.Increment(ref libraryRevision);
                if (isVisible) await UpdateShelvesAsync();
            }
        }

        private async void Settings_PropertyChanged1Async(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LandingPageSettingsViewModel.Settings))
            {
                if (observedGlobalSettings != null) observedGlobalSettings.PropertyChanged -= GlobalShelfSettingsChanged;
                observedGlobalSettings = Settings.Settings;
                observedGlobalSettings.PropertyChanged += GlobalShelfSettingsChanged;
                System.Threading.Interlocked.Increment(ref libraryRevision);
                if (Settings.Settings.ShelveInstanceSettings.ContainsKey(InstanceId))
                {
                    Shelves = Settings.Settings.ShelveInstanceSettings[InstanceId];
                    for (int i = 0; i < Shelves.ShelveProperties.Count; ++i)
                    {
                        ShelveViewModels[i].ShelveProperties = Shelves.ShelveProperties[i];
                        ShelveViewModels[i].ShelveSettings = Shelves;
                    }
                    if (isVisible) await UpdateShelvesAsync();
                } else
                {
                    Settings.Settings.ShelveInstanceSettings.Add(InstanceId, Shelves);
                }
                // Settings.Settings.PropertyChanged += Settings_PropertyChangedAsync;
            }
        }

        public async Task OnViewShownAsync()
        {
            if (closed) return;
            isVisible = true;
            Subscribe();
            if (!hasLoaded || refreshedRevision != System.Threading.Interlocked.Read(ref libraryRevision) || refreshedDate != DateTime.Today) await UpdateShelvesAsync();
            else ExtensionDiagnostics.Record(plugin, $"Shelves cache hit: {ShelveViewModels.Count} shelves.");
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task OnViewHiddenAsync()
        {
            isVisible = false;
            plugin.ClearFeaturedHoveredGame(this);
            dispatcherTimer.Stop();
            ShowDetails = false;
            // Keep lightweight database notifications: hidden pages only become dirty,
            // and returning to an unchanged page reuses its already loaded shelves.
            return Task.CompletedTask;
        }

        public async Task OnDayChangedAsync(DateTime newTime)
        {
            System.Threading.Interlocked.Increment(ref libraryRevision);
            if (!isVisible) return;
            await UpdateShelvesAsync();
            foreach(var shelve in ShelveViewModels)
            {
                shelve.CollectionViewSource.View.Refresh();
            }
        }

        public void OnViewClosed()
        {
            closed = true;
            plugin.ClearFeaturedHoveredGame(this);
            isVisible = false;
            dispatcherTimer.Stop();
            Unsubscribe();
            UnsubscribeSettings();
            ShelveViewModels.ForEach(m => m.Games.Clear());
            ShelveViewModels.ForEach(m => m.OnViewClosed());
            ShelveViewModels.Clear();
        }

    }
}
