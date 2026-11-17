using LandingPage.Models;
using LandingPage.Models.SuccessStory;
using System;
using System.Collections.Generic;
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
using static LandingPage.ViewModels.SuccessStory.SuccessStoryViewModel;

namespace LandingPage.Views
{
    /// <summary>
    /// Interaktionslogik für RecentAchievementsView.xaml
    /// </summary>
    public partial class RecentAchievementsView : UserControl
    {
        public RecentAchievementsView()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings),typeof(LandingPage.Settings.AchievementWidgetSettings),typeof(RecentAchievementsView));
        public LandingPage.Settings.AchievementWidgetSettings Settings { get => (LandingPage.Settings.AchievementWidgetSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty,value); }
        private sealed class GameInfo : IGameAchievementsInfo
        {
            public int TotalAchievements { get; set; }
            public int UnlockedAchievements { get; set; }
            public double Progress => TotalAchievements > 0 ? (double)UnlockedAchievements/TotalAchievements : 0;
            public string GameName { get; set; }
            public Guid GameId { get; set; }
            public DateTime? LastUnlock { get; set; }
        }
        private sealed class RecentItem : IAchievement
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public bool IsUnlocked => true;
            public Uri Icon { get; set; }
            public DateTime? UnlockedOn { get; set; }
            public IGameAchievementsInfo GameInfo { get; set; }
            public IGameAchievementsInfo GroupInfo { get; set; }
        }
        internal void SetFeed(IList<Playnite.SDK.Models.Game> games,IList<GameAchievementEntry>[] entries,
            IDictionary<Guid,ShelfAchievementProgress> progress,LandingPage.Settings.AchievementWidgetSettings options)
        {
            Settings = options;
            var items = new List<RecentItem>(); var groups = new Dictionary<string,GameInfo>();
            for (var index = 0; index < games.Count; index++) {
                var game = games[index]; progress.TryGetValue(game.Id,out var counts);
                var info = new GameInfo { GameId = game.Id, GameName = game.Name, TotalAchievements = counts?.Total ?? 0,
                    UnlockedAchievements = counts?.Unlocked ?? 0, LastUnlock = entries[index].Select(entry => entry.UnlockedOn).Max() };
                foreach (var entry in entries[index]) {
                    var group = info;
                    if (options.RecentGrouping != LandingPage.Settings.RecentAchievementGrouping.Game) {
                        var title = options.RecentGrouping == LandingPage.Settings.RecentAchievementGrouping.UnlockDay ? entry.UnlockedOn?.ToLocalTime().ToString("D")
                            : options.RecentGrouping == LandingPage.Settings.RecentAchievementGrouping.Source ? game.Source?.Name ?? "Playnite"
                            : string.Join(", ",game.Platforms?.Select(platform => platform.Name) ?? Enumerable.Empty<string>());
                        title = string.IsNullOrEmpty(title) ? "No platform" : title;
                        if (!groups.TryGetValue(title,out group)) groups[title] = group = new GameInfo { GameName = title, LastUnlock = entry.UnlockedOn };
                        if (entry.UnlockedOn > group.LastUnlock) group.LastUnlock = entry.UnlockedOn;
                    }
                    Uri.TryCreate(entry.UnlockedIconPath,UriKind.Absolute,out var icon);
                    items.Add(new RecentItem { Name = entry.Name, Description = entry.Description, UnlockedOn = entry.UnlockedOn,
                        Icon = icon, GameInfo = info, GroupInfo = group });
                }
            }
            var view = new ListCollectionView(items);
            if (options.RecentGrouping != LandingPage.Settings.RecentAchievementGrouping.None) {
                var grouping = new PropertyGroupDescription("GroupInfo");
                grouping.SortDescriptions.Add(new System.ComponentModel.SortDescription(options.RecentGroupOrder == LandingPage.Settings.RecentAchievementGroupOrder.Name ? "Name.GameName" : "Name.LastUnlock",
                    options.RecentGroupOrder == LandingPage.Settings.RecentAchievementGroupOrder.Name ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending));
                view.GroupDescriptions.Add(grouping);
            }
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription(options.ListOrder == LandingPage.Settings.AchievementListOrder.Name ? "Name" : "UnlockedOn",
                options.ListOrder == LandingPage.Settings.AchievementListOrder.Name ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending));
            DataContext = new { Inner = new { LatestAchievementsView = view } };
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button bt)
            {
                if (bt.DataContext is CollectionViewGroup group)
                {
                    if (group.Name is IGameAchievementsInfo info && info.GameId != Guid.Empty)
                    {
                        Playnite.SDK.API.Instance.MainView.SwitchToLibraryView();
                        Playnite.SDK.API.Instance.MainView.SelectGame(info.GameId);
                    }
                }
            }
        }
    }
}
