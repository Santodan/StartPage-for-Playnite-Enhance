using LandingPage.Converters;
using PlayniteCommon.Extensions;
using LandingPage.Models;
using LandingPage.Models.GameActivity;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using StartPage.SDK.Async;

namespace LandingPage.ViewModels.GameActivity
{
    public class MostPlayedViewModel : ObservableObject, IStartPageViewModel, IAsyncStartPageControl
    {
        private static readonly EnumDescriptionTypeConverter converter = new EnumDescriptionTypeConverter(typeof(Timeframe));

        public GameActivityViewModel GameActivityViewModel { get; set; }

        internal ObservableCollection<GameGroup> specialGames = new ObservableCollection<GameGroup>();
        public ObservableCollection<GameGroup> SpecialGames => specialGames;

        internal IPlayniteAPI playniteAPI;
        internal LandingPageSettingsViewModel settings;
        public LandingPageSettingsViewModel Settings => settings;

        public MostPlayedViewModel(IPlayniteAPI playniteApi, LandingPageSettingsViewModel settingsViewModel, GameActivityViewModel gameActivityViewModel)
        {
            GameActivityViewModel = gameActivityViewModel;
            this.playniteAPI = playniteApi;
            this.settings = settingsViewModel;
            settingsViewModel.PropertyChanged += SettingsViewModel_PropertyChangedAsync;
            BindSettings();
            observedActivities = gameActivityViewModel.Activities;
            observedActivities.CollectionChanged += Activities_CollectionChanged;
            gameActivityViewModel.PropertyChanged += GameActivityViewModel_PropertyChangedAsync;
            FillWithPlaceholders();
            if (GameActivityViewModel.Activities.Count > 0)
            {
                // UpdateMostPlayedGame();
            }
        }

        private LandingPageSettings observedSettings;
        private ObservableCollection<MostPlayedOptions> observedOptions;
        private readonly List<MostPlayedOptions> observedOptionItems = new List<MostPlayedOptions>();
        private ObservableCollection<Activity> observedActivities;
        private volatile bool closed;
        private int settingsRevision;

        private void UnbindSettings()
        {
            if (observedSettings != null) observedSettings.PropertyChanged -= Settings_PropertyChangedAsync;
            if (observedOptions != null) observedOptions.CollectionChanged -= MostPlayedOptions_CollectionChanged;
            foreach (var option in observedOptionItems) option.PropertyChanged -= Options_PropertyChangedAsync;
            observedOptionItems.Clear(); observedSettings = null; observedOptions = null;
        }
        private void BindSettings()
        {
            UnbindSettings();
            ++settingsRevision;
            if (closed || Settings?.Settings == null) return;
            observedSettings = Settings.Settings;
            observedOptions = observedSettings.MostPlayedOptions;
            observedSettings.PropertyChanged += Settings_PropertyChangedAsync;
            observedOptions.CollectionChanged += MostPlayedOptions_CollectionChanged;
            foreach (var option in observedOptions.Distinct()) { option.PropertyChanged += Options_PropertyChangedAsync; observedOptionItems.Add(option); }
        }
        private async Task RefreshSafelyAsync()
        {
            if (closed) return;
            try { await UpdateMostPlayedAsync(); }
            catch (Exception ex) { if (!closed) LandingPageExtension.logger?.Warn(ex, "Unable to refresh Most Played after a settings/activity update."); }
        }
        private async void GameActivityViewModel_PropertyChangedAsync(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (closed || e.PropertyName != nameof(GameActivityViewModel.Activities)) return;
            if (observedActivities != null) observedActivities.CollectionChanged -= Activities_CollectionChanged;
            observedActivities = GameActivityViewModel.Activities;
            if (observedActivities != null) observedActivities.CollectionChanged += Activities_CollectionChanged;
            await RefreshSafelyAsync();
        }
        private async void Options_PropertyChangedAsync(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (closed) return;
            ++settingsRevision;
            await RefreshSafelyAsync();
        }
        private async void SettingsViewModel_PropertyChangedAsync(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (closed || e.PropertyName != nameof(LandingPageSettingsViewModel.Settings)) return;
            BindSettings();
            await RefreshSafelyAsync();
        }
        private async void Settings_PropertyChangedAsync(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (closed) return;
            if (e.PropertyName == nameof(LandingPageSettings.MostPlayedOptions))
            {
                BindSettings();
                await RefreshSafelyAsync();
            }
            else if (e.PropertyName == nameof(LandingPageSettings.SkipGamesInPreviousMostPlayed) || e.PropertyName == nameof(LandingPageSettings.ShowHiddenInMostPlayed) || e.PropertyName == nameof(LandingPageSettings.IgnoreMostPlayedTagId))
            {
                ++settingsRevision;
                await RefreshSafelyAsync();
            }
        }
        private void MostPlayedOptions_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!closed) BindSettings();
        }

        private DispatcherTimer timer = null;

        private void Activities_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (closed) return;
            if (timer == null)
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                timer.Tick += async (s, a) =>
                {
                    if (s is DispatcherTimer dt)
                    {
                        dt.Stop();
                    }
                    await RefreshSafelyAsync();
                };
            }
            timer.Stop();
            timer.Start();
        }

        Task backgroundTask = Task.CompletedTask;

        public async Task UpdateMostPlayedAsync()
        {
            if (closed || Settings?.Settings == null) return;
            for (int i = Settings.Settings.MostPlayedOptions.Count - 1; i > 0; --i)
            {
                if (Settings.Settings.MostPlayedOptions[i].Timeframe == Timeframe.None &&
                    Settings.Settings.MostPlayedOptions[i - 1].Timeframe == Timeframe.None)
                {
                    Settings.Settings.MostPlayedOptions.RemoveAt(i);
                    continue;
                }
                break;
            }

            if (Settings.Settings.MostPlayedOptions.LastOrDefault() is MostPlayedOptions option &&
                option.Timeframe != Timeframe.None)
            {
                Settings.Settings.MostPlayedOptions.Add(new MostPlayedOptions { Timeframe = Timeframe.None });
            }

            if (Settings.Settings.MostPlayedOptions.Count == 0)
            {
                Settings.Settings.MostPlayedOptions.Add(new MostPlayedOptions { Timeframe = Timeframe.None });
            }

            var revision = settingsRevision;
            var groups = await GetMostPlayedGroupsAsync();
            if (closed || revision != settingsRevision) return;

            for (int i = 0; i < groups.Count; i++)
            {
                if (specialGames.Count > i)
                {
                    specialGames[i].Label = groups[i].Label;
                    UpdateGroupGames(specialGames[i].Games, groups[i].Games);
                }
                else
                {
                    specialGames.Add(groups[i]);
                }
            }

            int j = specialGames.Count - 1;
            while (specialGames.Count > groups.Count)
            {
                specialGames.RemoveAt(j--);
            }
        }

        private static void UpdateGroupGames(ObservableCollection<GameModel> current, ObservableCollection<GameModel> incoming)
        {
            var ids = new HashSet<Guid>(incoming.Select(model => model.Game.Id));
            for (var i = current.Count - 1; i >= 0; --i)
                if (!ids.Contains(current[i].Game.Id)) current.RemoveAt(i);
            for (var i = 0; i < incoming.Count; ++i)
            {
                var next = incoming[i];
                var existing = current.FirstOrDefault(model => model.Game.Id == next.Game.Id);
                if (existing == null) current.Insert(i, next);
                else
                {
                    if (!ReferenceEquals(existing.Game, next.Game)) existing.Game = next.Game;
                    var index = current.IndexOf(existing);
                    if (index != i) current.Move(index, i);
                }
            }
        }

        public void UpdateMostPlayedGame()
        {
            backgroundTask = backgroundTask.ContinueWith(t =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    for (int i = Settings.Settings.MostPlayedOptions.Count - 1; i > 0; --i)
                    {
                        if (Settings.Settings.MostPlayedOptions[i].Timeframe == Timeframe.None &&
                            Settings.Settings.MostPlayedOptions[i - 1].Timeframe == Timeframe.None)
                        {
                            Settings.Settings.MostPlayedOptions.RemoveAt(i);
                            continue;
                        }
                        break;
                    }

                    if (Settings.Settings.MostPlayedOptions.LastOrDefault() is MostPlayedOptions option &&
                        option.Timeframe != Timeframe.None)
                    {
                        Settings.Settings.MostPlayedOptions.Add(new MostPlayedOptions { Timeframe = Timeframe.None });
                    }

                    if (Settings.Settings.MostPlayedOptions.Count == 0)
                    {
                        Settings.Settings.MostPlayedOptions.Add(new MostPlayedOptions { Timeframe = Timeframe.None });
                    }
                });
                t?.Dispose();
                return GetMostPlayedGroups();
            }).ContinueWith(t =>
            {
                var groups = t.Result;
                for (int i = 0; i < groups.Count; i++)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (specialGames.Count > i)
                        {
                            specialGames[i].Label = groups[i].Label;
                            specialGames[i].Games[0].Game = groups[i].Games[0].Game;
                        }
                        else
                        {
                            specialGames.Add(groups[i]);
                        }
                    });
                }

                int j = specialGames.Count - 1;
                while(specialGames.Count > groups.Count)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        specialGames.RemoveAt(j--);
                    });
                }

                t?.Dispose();
            });
        }

        private async Task<List<GameGroup>> GetMostPlayedGroupsAsync()
        {
            return await Task.Run(GetMostPlayedGroups);
        }

        private List<GameGroup> GetMostPlayedGroups()
        {
            var groups = new List<GameGroup>();
            var tagId = Settings.Settings.IgnoreMostPlayedTagId;

            foreach (var options in Settings.Settings.MostPlayedOptions.ToArray())
            {
                if (options.Timeframe == Timeframe.None)
                {
                    continue;
                }
                if (options.Timeframe == Timeframe.AllTime)
                {
                    var candidates = playniteAPI.Database.Games
                        .Where(g => !g.Hidden || settings.Settings.ShowHiddenInMostPlayed)
                        .Where(g => !(g.TagIds?.Contains(tagId) ?? false));
                    candidates = candidates
                        .OrderByDescending(a => a.Playtime)
                        .Skip(Math.Max(0, Math.Min(options.SkippedGames, candidates.Count())));
                    candidates = candidates.Where(g => !settings.Settings.SkipGamesInPreviousMostPlayed || !groups.Any(group => group.Games.Any(m => m.Game == g)));
                    if (candidates.FirstOrDefault() is Game game)
                    {
                        var group = new GameGroup();
                        group.Games.Add(new GameModel(game));
                        group.Label = converter.ConvertToString(options.Timeframe);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            groups.Add(group);
                        });
                    }
                }
                else
                {
                    GameActivityViewModel.ActivityLock.EnterReadLock();

                    var thisWeek = GameActivityViewModel.Activities
                        .Select(a => new { Game = playniteAPI.Database.Games.Get(a.Id), Items = a.Items.Where(i => options.Timeframe == Timeframe.AllTime || i.DateSession.Add(options.TimeSpan) >= DateTime.Today).ToList() })
                        .Where(a => a.Game is Game && a.Items?.Count > 0)
                        .Where(a => !a.Game.Hidden || settings.Settings.ShowHiddenInMostPlayed)
                        .Where(a => !(a.Game.TagIds?.Contains(tagId) ?? false))
                        .Select(a => new { Game = a.Game, Playtime = a.Items.Sum(i => (long)i.ElapsedSeconds) });
                    thisWeek = thisWeek
                        .OrderByDescending(a => a.Playtime)
                        .Skip(Math.Max(0, Math.Min(options.SkippedGames, thisWeek.Count())));

                    thisWeek = thisWeek.Where(a => !settings.Settings.SkipGamesInPreviousMostPlayed || !groups.Any(group => group.Games.Any(m => m.Game == a.Game)));
                    var mostPlayedThisWeek = thisWeek.FirstOrDefault();

                    GameActivityViewModel.ActivityLock.ExitReadLock();

                    if (mostPlayedThisWeek?.Game is Game)
                    {
                        var group = groups.LastOrDefault();
                        var groupLabel = converter.ConvertToString(options.Timeframe);
                        if (groupLabel != group?.Label)
                        {
                            group = new GameGroup() { Label = groupLabel };
                        }
                        group.Games.Add(new GameModel(mostPlayedThisWeek.Game));
                        if (!groups.Contains(group))
                        {
                            groups.Add(group);
                        }
                    }
                }
            }
            return groups;
        }

        public void FillWithPlaceholders()
        {
            var specialGames = SpecialGames;
            var groups = GetPlaceholderGroups();
            for (int i = 0; i < groups.Count; i++)
            {
                if (specialGames.Count > i)
                {
                    specialGames[i].Label = groups[i].Label;
                    specialGames[i].Games[0].Game = groups[i].Games[0].Game;
                }
                else
                {
                    specialGames.Add(groups[i]);
                }
            }

            int j = specialGames.Count - 1;
            while (specialGames.Count > groups.Count)
            {
                specialGames.RemoveAt(j--);
            }
        }

        private List<GameGroup> GetPlaceholderGroups()
        {
            var groups = new List<GameGroup>();
            var tagId = Settings.Settings.IgnoreMostPlayedTagId;

            foreach (var options in Settings.Settings.MostPlayedOptions.ToArray())
            {
                if (options.Timeframe == Timeframe.None)
                {
                    continue;
                }
               var group = new GameGroup();
                group.Games.Add(new GameModel(ShelveViewModel.DummyGame));
                group.Label = converter.ConvertToString(options.Timeframe);
                groups.Add(group);
            }
            return groups;
        }

        public void OnViewClosed()
        {
            if (closed) return;
            closed = true; ++settingsRevision;
            timer?.Stop();
            UnbindSettings();
            settings.PropertyChanged -= SettingsViewModel_PropertyChangedAsync;
            if (observedActivities != null) observedActivities.CollectionChanged -= Activities_CollectionChanged;
            GameActivityViewModel.PropertyChanged -= GameActivityViewModel_PropertyChangedAsync;
        }

        public Task OnViewShownAsync()
        {
            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            await GameActivityViewModel.Initialize();
            await UpdateMostPlayedAsync();
        }

        public Task OnViewHiddenAsync()
        {
            return Task.CompletedTask;
        }

        public Task OnDayChangedAsync(DateTime newTime)
        {
            return UpdateMostPlayedAsync();
        }
    }
}
