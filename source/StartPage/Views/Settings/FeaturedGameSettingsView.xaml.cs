using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LandingPage.Settings;
using Playnite.SDK;

namespace LandingPage.Views.Settings
{
    public partial class FeaturedGameSettingsView : UserControl
    {
        public FeaturedGameSettingsView() { InitializeComponent(); }
        private sealed class GameChoice : GenericItemOption { public Guid GameId { get; set; } }
        private void ChooseGame_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is FeaturedGameSettings settings)) return;
            var api = LandingPageExtension.Instance.PlayniteApi;
            Func<string,List<GenericItemOption>> search = text => api.Database.Games
                .Where(game => string.IsNullOrWhiteSpace(text) || (game.Name ?? string.Empty).IndexOf(text,StringComparison.CurrentCultureIgnoreCase) >= 0)
                .OrderBy(game => game.Name).Take(100).Select(game => (GenericItemOption)new GameChoice
                { GameId = game.Id, Name = game.Name, Description = game.IsInstalled ? "Installed" : "Not installed" }).ToList();
            var selected = api.Dialogs.ChooseItemWithSearch(search(string.Empty),search,"Choose featured game","Search your library");
            if (selected is GameChoice choice)
            { settings.PinnedGameId = choice.GameId; settings.PinnedGameName = choice.Name; settings.Source = FeaturedGameSource.Pinned; }
        }
    }
}
