using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            => new[] { Build("minimal", context, builtinPluginId), Build("achievements", context, builtinPluginId), Build("library", context, builtinPluginId) };

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
                var shelf = Shelves(settings, pluginId, "Recently played", Rule("Recently played", SortingField.LastActivity, 12, ShelfDisplayStyle.CoverRow));
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
                games.ShowSourceIcon = true; games.MetadataPosition = ShelfMetadataPosition.OverArtwork;
                var shelf = Shelves(settings, pluginId, "Achievement progress", games);
                AddPage(settings, "Achievements", Stack(Orientation.Vertical, Toolbar(pluginId), Height(Heading("Achievements", "Your recent unlocks and games to return to.", pluginId), 76),
                    Split(Orientation.Horizontal, 4, Weight(recent, 1.2), Weight(shelf, 2.8))));
            }
            else
            {
                result.Name = "Library";
                result.Description = "Two pages: recent and favorite games in Library, plus an unplayed grid and installed list in Backlog.";
                result.Notes = "Uses built-in shelves and portable filters, with no library-specific tags or file paths. Both pages include toolbar navigation.";
                var recent = Rule("Continue playing", SortingField.LastActivity, 12, ShelfDisplayStyle.CoverRow); recent.ShowLastPlayed = true; recent.ShowPlaytime = true; recent.ShowSourceIcon = true;
                var added = Rule("Recently added", SortingField.DateAdded, 10, ShelfDisplayStyle.WideCards); added.CardWidth = 260; added.ShowSourceIcon = true; added.ShowInstallationStatus = true;
                var favorites = Rule("Favorites", SortingField.Name, 14, ShelfDisplayStyle.CompactList); favorites.FavoritesOnly = true; favorites.ShowFavoriteBadge = true;
                AddPage(settings, "Library", Stack(Orientation.Vertical, Toolbar(pluginId), Height(Heading("My library", "Continue playing, explore recent additions, or revisit a favorite.", pluginId), 76),
                    Split(Orientation.Horizontal, 4, Weight(Shelves(settings, pluginId, "Your games", recent, added), 3), Weight(Shelves(settings, pluginId, "Favorites", favorites), 1.4))));
                var unplayed = Rule("Never played", SortingField.DateAdded, 30, ShelfDisplayStyle.CoverGrid); unplayed.NeverPlayedOnly = true; unplayed.ShowSourceIcon = true; unplayed.ShowInstallationStatus = true;
                var installed = Rule("Ready to play", SortingField.Name, 20, ShelfDisplayStyle.CompactList); installed.InstalledOnly = true; installed.ShowPlaytime = true;
                AddPage(settings, "Backlog", Stack(Orientation.Vertical, Toolbar(pluginId), Height(Heading("Find your next game", "Unplayed games and installed titles, ready when you are.", pluginId), 76),
                    Split(Orientation.Horizontal, 4, Weight(Shelves(settings, pluginId, "Backlog", unplayed), 3), Weight(Shelves(settings, pluginId, "Installed", installed), 1.4))));
            }
            settings.ActiveDashboardPageId = settings.DashboardPages[0].Id;
            settings.GridLayout = settings.DashboardPages[0].Layout;
            result.Profile = DashboardProfiles.Capture(settings, result.Name);
            return result;
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
            var node = Widget("ToolbarWidget", pluginId); node.Size = new GridLength(56);
            node.ViewProperties.ToolbarSettings.ShowLabels = false;
            node.ViewProperties.ToolbarSettings.Items.Add(new ToolbarEntry { Id = "profile.manage" });
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
