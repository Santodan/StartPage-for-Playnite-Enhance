using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LandingPage.Models.Layout;
using LandingPage.Settings;

namespace LandingPage.Models
{
    public class DashboardStarterTemplate
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
        public DashboardProfile Profile { get; set; }
    }

    public static class DashboardStarterTemplates
    {
        public static IReadOnlyList<DashboardStarterTemplate> Create(LandingPageSettings context, Guid builtinPluginId)
            => new[] { Build("minimal", context, builtinPluginId), Build("achievements", context, builtinPluginId), Build("library", context, builtinPluginId),
                Build("spotlight", context, builtinPluginId), Build("inspector", context, builtinPluginId), Build("insights", context, builtinPluginId), Build("achievement-cards", context, builtinPluginId) };

        // Use only views advertised by one enabled provider; no dependency on its assemblies or private settings.
        public static IReadOnlyList<DashboardStarterTemplate> Create(LandingPageSettings context, Guid builtinPluginId,
            IEnumerable<StartPageViewArgs> availableViews)
        {
            var templates = Create(context, builtinPluginId).ToList();
            var required = new[] { "PlayniteAchievements_GameSummariesGrid", "PlayniteAchievements_RecentUnlocksGrid",
                "PlayniteAchievements_Showcase_Statistics", "PlayniteAchievements_Showcase_ActivityCalendar" };
            var provider = (availableViews ?? Enumerable.Empty<StartPageViewArgs>())
                .Where(view => view != null && view.PluginId != Guid.Empty && view.PluginId != builtinPluginId)
                .GroupBy(view => view.PluginId)
                .FirstOrDefault(group => required.All(id => group.Any(view => view.ViewId == id && view.AllowMultipleInstances)));
            if (provider == null) return templates;

            var template = Build("minimal", context, builtinPluginId);
            template.Key = "provider-achievements";
            template.Name = "PlayniteAchievements dashboard";
            template.Description = "PlayniteAchievements game summaries, recent unlocks, statistics and activity calendar.";
            template.Notes = "Requires the enabled PlayniteAchievements provider. Configure each external widget through its Settings menu. " +
                "Choose Finish next or completed games in Game summaries; use game/achievement collections for pinned goals. " +
                "Profiles save placement and panel appearance, not the provider's private widget settings.";
            var snapshot = template.Profile.Snapshot;
            snapshot.Shelves.Clear();
            Func<string, string, GridNode> external = (id, title) =>
            {
                var node = Widget(id, provider.Key);
                Decorate(node, title);
                return node;
            };
            snapshot.Pages[0].Name = "Achievements";
            snapshot.Pages[0].Layout = Stack(Orientation.Vertical, Toolbar(builtinPluginId),
                Split(Orientation.Horizontal, 1,
                    Weight(external(required[0], "Game progress"), 1.6),
                    Weight(Stack(Orientation.Vertical, external(required[1], "Recent unlocks"),
                        external(required[2], "Achievement statistics"), external(required[3], "Activity calendar")), 1)));
            template.Profile.Name = template.Name;
            templates.Add(template);
            return templates;
        }

        private static DashboardStarterTemplate Build(string key, LandingPageSettings context, Guid pluginId)
        {
            var settings = new LandingPageSettings
            {
                Padding = 12, FixGridSize = false, SelectedAchievementsProvider = context.SelectedAchievementsProvider,
                IgnoreTagId = context.IgnoreTagId, IgnoreMostPlayedTagId = context.IgnoreMostPlayedTagId,
                EnableGlobalBackground = context.EnableGlobalBackground, GlobalBackgroundOpacity = context.GlobalBackgroundOpacity,
                EnableNotifications = context.EnableNotifications, EnableGlobalProgressBar = context.EnableGlobalProgressBar,
                BlurAmount = 24, OverlayOpacity = 0.28, NoiseOpacity = 0.04,
                ShelveInstanceSettings = new Dictionary<Guid, ShelvesSettings>()
            };
            var result = new DashboardStarterTemplate { Key = key };
            Func<string, GridNode> widget = id => Widget(id, pluginId);
            if (key == "minimal")
            {
                result.Name = "Minimal";
                result.Description = "A clean home page with page navigation, a clock, and a recently played shelf.";
                result.Notes = "Uses StartPage widgets only. Colors follow your theme; the background uses game artwork.";
                var clock = widget("DigitalClock"); clock.ViewProperties.ClockSettings.TimeSize = 100;
                var welcome = Heading("Welcome back", "Pick a game and enjoy your next session.", pluginId);
                var recentRule = Rule("Recently played", SortingField.LastActivity, 12, ShelfDisplayStyle.CoverRow);
                recentRule.HoverEffect = ShelfHoverEffect.Dock; recentRule.HoverScale = 1.15; recentRule.ShowSourceIcon = true;
                var shelf = Shelves(settings, pluginId, "Recently played", recentRule);
                AddPage(settings, "Home", Stack(Orientation.Vertical, Toolbar(pluginId), Split(Orientation.Horizontal, 1.2, clock, welcome), Weight(shelf, 3.2)));
            }
            else if (key == "achievements")
            {
                result.Name = "Achievements";
                result.Description = "Recent unlocks beside game cards with achievement progress and trophy badges.";
                result.Notes = "Uses your selected achievement provider. Progress and recent unlocks can be empty until achievement data is available. No provider settings or caches are changed.";
                settings.MaxNumberRecentAchievements = 10; settings.MaxNumberRecentAchievementsPerGame = 3;
                var recent = widget("RecentAchivements"); Decorate(recent, "Recent unlocks");
                var games = Rule("Continue your progress", SortingField.LastActivity, 12, ShelfDisplayStyle.CoverGrid);
                games.ShowAchievementProgress = true; games.ShowAchievementTrophy = true; games.ShowPlaytime = true;
                games.HoverEffect = ShelfHoverEffect.Lift;
                games.ShowSourceIcon = true; games.MetadataPosition = ShelfMetadataPosition.OverArtwork;
                var shelf = Shelves(settings, pluginId, "Achievement progress", games);
                AddPage(settings, "Achievements", Stack(Orientation.Vertical, Toolbar(pluginId), Height(Heading("Achievements", "Your recent unlocks and games to return to.", pluginId), 76),
                    Split(Orientation.Horizontal, 4, Weight(recent, 1.2), Weight(shelf, 2.8))));
            }
            else if (key == "achievement-cards")
            {
                result.Name = "Achievement dashboard";
                result.Description = "StartPage progress cards and finish-next games with customizable rings, bars, text and card styling.";
                result.Notes = "Reads your selected achievement provider. Add the separate Achievement goals widget to track pinned games and percentage targets. Customize every instance in Appearance > Achievement widget settings.";
                var progress = widget("AchievementProgress"); Decorate(progress,"Achievement progress");
                var next = widget("AchievementFinishNext"); Decorate(next,"Finish next");
                AddPage(settings,"Achievement dashboard",Stack(Orientation.Vertical,Toolbar(pluginId),
                    Split(Orientation.Horizontal,1,Weight(progress,1.7),Weight(next,1))));
            }
            else if (key == "library")
            {
                result.Name = "Library";
                result.Description = "Two pages: recent and favorite games in Library, plus an unplayed grid and installed list in Backlog.";
                result.Notes = "Uses built-in shelves and portable filters, with no library-specific tags or file paths. Both pages include toolbar navigation.";
                var recent = Rule("Continue playing", SortingField.LastActivity, 12, ShelfDisplayStyle.CoverRow); recent.ShowLastPlayed = true; recent.ShowPlaytime = true; recent.ShowSourceIcon = true;
                var added = Rule("Recently added", SortingField.DateAdded, 10, ShelfDisplayStyle.WideCards); added.CardWidth = 260; added.ShowSourceIcon = true; added.ShowInstallationStatus = true;
                var favorites = Rule("Favorites", SortingField.Name, 14, ShelfDisplayStyle.CompactList); favorites.FavoritesOnly = true; favorites.ShowFavoriteBadge = true;
                var summary = Statistics(pluginId, false); summary.ViewProperties.LibraryStatisticsSettings.ShowPlaytime = false;
                AddPage(settings, "Library", Stack(Orientation.Vertical, Toolbar(pluginId), Height(summary, 110),
                    Split(Orientation.Horizontal, 4, Weight(Shelves(settings, pluginId, "Your games", recent, added), 3), Weight(Shelves(settings, pluginId, "Favorites", favorites), 1.4))));
                var unplayed = Rule("Never played", SortingField.DateAdded, 30, ShelfDisplayStyle.CoverGrid); unplayed.NeverPlayedOnly = true; unplayed.ShowSourceIcon = true; unplayed.ShowInstallationStatus = true;
                var installed = Rule("Ready to play", SortingField.Name, 20, ShelfDisplayStyle.CompactList); installed.InstalledOnly = true; installed.ShowPlaytime = true;
                AddPage(settings, "Backlog", Stack(Orientation.Vertical, Toolbar(pluginId), Height(Heading("Find your next game", "Unplayed games and installed titles, ready when you are.", pluginId), 76),
                    Split(Orientation.Horizontal, 4, Weight(Shelves(settings, pluginId, "Backlog", unplayed), 3), Weight(Shelves(settings, pluginId, "Installed", installed), 1.4))));
            }
            else if (key == "spotlight")
            {
                result.Name = "Game spotlight";
                result.Description = "A large featured game follows the hovered or selected game, with a dock-style recent-games shelf below.";
                result.Notes = "Uses built-in widgets. Without a selection, the featured game starts with the first shelf game. Artwork follows the available game background.";
                var feature = widget("FeaturedGame"); Decorate(feature, "Game spotlight");
                feature.ViewProperties.FeaturedGameSettings.Source = FeaturedGameSource.HoveredOrSelectedGame;
                feature.ViewProperties.FeaturedGameSettings.ArtworkSizing = FeaturedArtworkSizing.Fit;
                feature.ViewProperties.FeaturedGameSettings.DescriptionLength = 600;
                var recent = Rule("Explore your games", SortingField.LastActivity, 16, ShelfDisplayStyle.CoverRow);
                recent.HoverEffect = ShelfHoverEffect.Dock; recent.HoverScale = 1.2; recent.ShowSourceIcon = true; recent.ShowPlaytime = true;
                AddPage(settings, "Spotlight", Stack(Orientation.Vertical, Toolbar(pluginId), Weight(feature, 3),
                    Weight(Shelves(settings, pluginId, "Explore", recent), 1.2)));
            }
            else if (key == "inspector")
            {
                result.Name = "Game inspector";
                result.Description = "Independent artwork, title, HTML description, activity and action widgets follow the same hovered or selected game.";
                result.Notes = "Hover a game in the top shelf to update every game section. The description shows the complete text and images, with scrolling. Achievement activity needs your selected provider.";
                var recent = Rule("Choose a game", SortingField.LastActivity, 12, ShelfDisplayStyle.CoverRow);
                recent.HoverEffect = ShelfHoverEffect.Lift; recent.HoverDirection = ShelfHoverDirection.Down; recent.ShowSourceIcon = true;
                var artwork = widget("GameDetailArtwork"); Decorate(artwork, "Artwork");
                var description = widget("GameDetailDescription"); Decorate(description, "Description");
                description.ViewProperties.GameDetailSettings.DescriptionLength = 0;
                description.ViewProperties.GameDetailSettings.ShowDescriptionImages = true;
                var title = widget("GameDetailTitle");
                var activity = widget("GameDetailActivity"); Decorate(activity, "Activity");
                var actions = widget("GameDetailActions");
                AddPage(settings, "Game inspector", Stack(Orientation.Vertical, Toolbar(pluginId),
                    Weight(Shelves(settings, pluginId, "Choose a game", recent), 1.1),
                    Split(Orientation.Horizontal, 3, Weight(artwork, 1), Weight(Stack(Orientation.Vertical,
                        Height(title, 70), Weight(description, 3), Weight(activity, 1.2), Height(actions, 54)), 2.3))));
            }
            else if (key == "insights")
            {
                result.Name = "Library insights";
                result.Description = "Library totals and recorded playtime alongside three charts: games by source, playtime by source and installation status.";
                result.Notes = "Source groups are detected from your library. Period playtime counters need GameActivity session history; source playtime charts use Playnite lifetime totals. Charts start with animations disabled.";
                var counters = Statistics(pluginId, true);
                var chart = widget("LibraryChart"); Decorate(chart, "Library charts");
                chart.ViewProperties.LibraryStatisticsSettings = Chart(StatisticsBreakdown.Sources, StatisticsMetric.GameCount, StatisticsChartStyle.Doughnut);
                var charts = chart.ViewProperties.LibraryStatisticsSettings;
                charts.ExtraCharts.Add(Chart(StatisticsBreakdown.Sources, StatisticsMetric.Playtime, StatisticsChartStyle.Bars));
                charts.ExtraCharts.Add(Chart(StatisticsBreakdown.Installation, StatisticsMetric.GameCount, StatisticsChartStyle.VerticalBars));
                AddPage(settings, "Library insights", Stack(Orientation.Vertical, Toolbar(pluginId),
                    Split(Orientation.Horizontal, 1, Weight(counters, 1.2), Weight(chart, 2))));
            }
            settings.ActiveDashboardPageId = settings.DashboardPages[0].Id;
            settings.GridLayout = settings.DashboardPages[0].Layout;
            result.Profile = DashboardProfiles.Capture(settings, result.Name);
            return result;
        }

        private static GridNode Statistics(Guid pluginId, bool periods)
        {
            var node = Widget("LibraryStatistics", pluginId); Decorate(node, "Library overview");
            var options = node.ViewProperties.LibraryStatisticsSettings;
            options.ShowChart = false; options.UseCounterGrid = true; options.CounterColumns = 3;
            options.ShowTotal = true; options.ShowInstalled = true; options.ShowUnplayed = true;
            options.ShowCompleted = periods; options.ShowPlaytime = true; options.ShowAveragePlaytime = periods;
            options.ShowTodayPlaytime = periods; options.ShowWeekPlaytime = periods;
            options.ShowMonthPlaytime = periods; options.ShowYearPlaytime = periods;
            return node;
        }
        private static LibraryStatisticsSettings Chart(StatisticsBreakdown breakdown, StatisticsMetric metric, StatisticsChartStyle style)
        {
            var options = LibraryStatisticsSettings.CreateChartSettings();
            options.Breakdown = breakdown; options.Metric = metric; options.ChartStyle = style;
            options.ChartHeight = 240; options.ChartAnimations = false; options.GlossySeries = false;
            return options;
        }
        private static void AddPage(LandingPageSettings settings, string name, GridNode layout)
            => settings.DashboardPages.Add(new DashboardPage { Name = name, Layout = layout, Background = DashboardBackground.Capture(settings) });
        private static GridNode Widget(string id, Guid pluginId) => new GridNode { Padding = 4,
            ViewProperties = new ViewProperties { ViewId = id, PluginId = pluginId } };
        private static void Decorate(GridNode node, string title)
        {
            var properties = node.ViewProperties;
            properties.ShowLabel = !string.IsNullOrEmpty(title);
            properties.Appearance = new PanelAppearance { Enabled = true, Title = title, Background = "#16191F", BackgroundOpacity = 0.48,
                CornerRadius = 12, Padding = 10, Margin = 4 };
        }
        private static GridNode Toolbar(Guid pluginId)
        {
            var node = Widget("ToolbarWidget", pluginId); node.Size = GridLength.Auto;
            node.ViewProperties.ToolbarSettings.ShowLabels = false;
            return node;
        }
        private static GridNode Heading(string title, string subtitle, Guid pluginId)
        {
            var node = Widget("TextWidget", pluginId);
            node.ViewProperties.TextSettings.Text = title + "\n" + subtitle;
            node.ViewProperties.TextSettings.FontSize = 22; node.ViewProperties.TextSettings.VerticalAlignment = VerticalAlignment.Center;
            return node;
        }
        private static ShelveProperties Rule(string name, SortingField sorting, int count, ShelfDisplayStyle style)
            => new ShelveProperties { Name = name, SortBy = sorting, Order = sorting == SortingField.Name ? Order.Ascending : Order.Descending,
                NumberOfGames = count, DisplayStyle = style, TitleDisplay = ShelfTitleDisplay.Show, CardWidth = 150 };
        private static GridNode Shelves(LandingPageSettings settings, Guid pluginId, string title, params ShelveProperties[] rules)
        {
            var node = Widget("GameShelves", pluginId); Decorate(node, title);
            settings.ShelveInstanceSettings[node.ViewProperties.InstanceId] = new ShelvesSettings(settings)
                { ShelveProperties = new ObservableCollection<ShelveProperties>(rules), ShowDetails = false };
            return node;
        }
        private static GridNode Stack(Orientation orientation, params GridNode[] children) => new GridNode { Orientation = orientation, Children = new ObservableCollection<GridNode>(children) };
        private static GridNode Split(Orientation orientation, double weight, params GridNode[] children) => Weight(Stack(orientation, children), weight);
        private static GridNode Weight(GridNode node, double value) { node.Size = new GridLength(value, GridUnitType.Star); return node; }
        private static GridNode Height(GridNode node, double value) { node.Size = new GridLength(value); return node; }
    }
}
