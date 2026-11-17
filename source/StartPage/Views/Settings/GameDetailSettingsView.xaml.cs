using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LandingPage.Models;
using LandingPage.Settings;
using Playnite.SDK;
namespace LandingPage.Views.Settings
{
    public partial class GameDetailSettingsView : UserControl
    {
        public static readonly DependencyProperty WidgetIdProperty = DependencyProperty.Register(nameof(WidgetId),typeof(string),typeof(GameDetailSettingsView),new PropertyMetadata(null,Changed));
        public string WidgetId { get => (string)GetValue(WidgetIdProperty); set => SetValue(WidgetIdProperty,value); }
        private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((GameDetailSettingsView)sender).UpdatePanels();
        public GameDetailSettingsView() { InitializeComponent(); Loaded += (sender,args) => UpdatePanels(); }
        private void UpdatePanels()
        {
            if (TitleOptions == null) return;
            var item = GameDetailWidgets.Find(WidgetId);
            GameTitleTextEntry.IsAvailable = item?.Section == FeaturedGameSection.Title;
            GameBodyTextEntry.IsAvailable = item?.Section != FeaturedGameSection.Title;
            TitleOptions.Visibility = item?.Section == FeaturedGameSection.Title ? Visibility.Visible : Visibility.Collapsed;
            DescriptionOptions.Visibility = item?.Section == FeaturedGameSection.Description ? Visibility.Visible : Visibility.Collapsed;
            ArtworkOptions.Visibility = item?.IsArtwork == true ? Visibility.Visible : Visibility.Collapsed;
            ActivityOptions.Visibility = item?.Section == FeaturedGameSection.Activity ? Visibility.Visible : Visibility.Collapsed;
            DetailsOptions.Visibility = item?.Section == FeaturedGameSection.GameDetails ? Visibility.Visible : Visibility.Collapsed;
            LibraryOptions.Visibility = item?.Section == FeaturedGameSection.LibraryInformation ? Visibility.Visible : Visibility.Collapsed;
            ScoresOptions.Visibility = item?.Section == FeaturedGameSection.Scores ? Visibility.Visible : Visibility.Collapsed;
            LinksOptions.Visibility = item?.Section == FeaturedGameSection.Links ? Visibility.Visible : Visibility.Collapsed;
            ActionsOptions.Visibility = item?.Section == FeaturedGameSection.Actions ? Visibility.Visible : Visibility.Collapsed;
            FormattingOptions.Visibility = ScrollingOptions.Visibility = item?.IsArtwork == true ? Visibility.Collapsed : Visibility.Visible;
        }
        private sealed class Choice : GenericItemOption { public Guid GameId { get; set; } }
        private void ChooseGame_Click(object sender, RoutedEventArgs args)
        {
            if (!(DataContext is FeaturedGameSettings settings)) return;
            var api = LandingPageExtension.Instance.PlayniteApi;
            Func<string,List<GenericItemOption>> search = text => api.Database.Games
                .Where(game => string.IsNullOrWhiteSpace(text) || (game.Name ?? string.Empty).IndexOf(text,StringComparison.CurrentCultureIgnoreCase) >= 0)
                .OrderBy(game => game.Name).Take(100).Select(game => (GenericItemOption)new Choice { GameId = game.Id, Name = game.Name }).ToList();
            var selected = api.Dialogs.ChooseItemWithSearch(search(string.Empty),search,"Choose game","Search your library");
            if (selected is Choice choice) { settings.PinnedGameId = choice.GameId; settings.PinnedGameName = choice.Name; settings.Source = FeaturedGameSource.Pinned; }
        }
    }
}
