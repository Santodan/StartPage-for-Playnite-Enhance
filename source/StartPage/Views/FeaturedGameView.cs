using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shell;
using LandingPage.Models;
using LandingPage.Settings;
using Playnite.SDK;
using Playnite.SDK.Models;
using StartPage.SDK.Async;

namespace LandingPage.Views
{
    public class FeaturedGameView : UserControl, IAsyncStartPageControl, IStartPageViewModel
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(FeaturedGameSettings), typeof(FeaturedGameView), new PropertyMetadata(null, Changed));
        public FeaturedGameSettings Settings { get => (FeaturedGameSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
        private FeaturedGameSettings observed;
        private readonly Grid surface = new Grid { ClipToBounds = true, MinHeight = 180 };
        private readonly Image artwork = new Image { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
        private readonly Border shade = new Border { IsHitTestVisible = false };
        private readonly StackPanel details = new StackPanel { Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Bottom };
        private bool visible, closed, dirty = true, subscribed, shuffle, renderPending;
        private Game game;
        private string artworkPath;
        private Task refreshTask = Task.CompletedTask;
        public Game FeaturedGame => game;
        private IPlayniteAPI Api => LandingPageExtension.Instance.PlayniteApi;

        public FeaturedGameView()
        {
            surface.Children.Add(artwork); surface.Children.Add(shade); surface.Children.Add(details); Content = surface;
            Settings = new FeaturedGameSettings();
            SetBinding(SettingsProperty, new Binding("DataContext.GridNode.ViewProperties.FeaturedGameSettings")
            { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(GridNodeView), 1), FallbackValue = Settings, TargetNullValue = Settings });
            Loaded += async (sender,args) => { if (!closed) { visible = true; Subscribe(); await RefreshSafely(); } };
            Unloaded += (sender,args) => visible = false;
        }
        private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var view = (FeaturedGameView)sender;
            if (view.observed != null) view.observed.PropertyChanged -= view.SettingChanged;
            view.observed = args.NewValue as FeaturedGameSettings;
            if (view.observed != null) view.observed.PropertyChanged += view.SettingChanged;
            view.dirty = true;
            if (view.visible) view.QueueRefresh();
        }
        private void SettingChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(FeaturedGameSettings.Source) || args.PropertyName == nameof(FeaturedGameSettings.PinnedGameId)
                || args.PropertyName == nameof(FeaturedGameSettings.InstalledOnly) || args.PropertyName == nameof(FeaturedGameSettings.IncludeHidden))
            {
                if (args.PropertyName == nameof(FeaturedGameSettings.Source)
                    && (Settings.Source == FeaturedGameSource.Random || Settings.Source == FeaturedGameSource.RandomFavorite)) shuffle = true;
                dirty = true; QueueRefresh();
            }
            else if (visible) RenderGame();
            else renderPending = true;
        }
        private void Subscribe()
        {
            if (subscribed || closed) return;
            subscribed = true;
            Api.Database.Games.ItemUpdated += GamesUpdated;
            Api.Database.Games.ItemCollectionChanged += GamesChanged;
            LandingPageExtension.Instance.FeaturedSelectionChanged += SelectionChanged;
        }
        private void GamesUpdated(object sender, ItemUpdatedEventArgs<Game> args) { dirty = true; QueueRefresh(); }
        private void GamesChanged(object sender, ItemCollectionChangedEventArgs<Game> args) { dirty = true; QueueRefresh(); }
        private void SelectionChanged(object sender, EventArgs args)
        { if (Settings.Source == FeaturedGameSource.SelectedGame) { dirty = true; QueueRefresh(); } }
        private void QueueRefresh()
        {
            if (!visible || closed) return;
            Dispatcher.BeginInvoke(new Action(async () => { if (visible && !closed) await RefreshSafely(); }), System.Windows.Threading.DispatcherPriority.Background);
        }
        private async Task RefreshSafely()
        {
            try { await Refresh(); }
            catch (Exception ex) { LandingPageExtension.logger.Warn(ex,"Unable to refresh featured game widget."); details.Children.Clear(); AddText("Unable to load a featured game. Check the StartPage debug log."); }
        }
        private Task Refresh()
        {
            if (closed || !visible) return Task.CompletedTask;
            if (!refreshTask.IsCompleted) return refreshTask;
            refreshTask = RefreshCore(); return refreshTask;
        }
        private async Task RefreshCore()
        {
            while (dirty && visible && !closed)
            {
                dirty = false;
                var options = DashboardProfiles.Clone(Settings);
                var selectedId = Api.MainView?.SelectedGames?.FirstOrDefault()?.Id;
                var currentId = game?.Id; var doShuffle = shuffle; shuffle = false;
                var next = await Task.Run(() => options.Source == FeaturedGameSource.Pinned ? Api.Database.Games.Get(options.PinnedGameId)
                    : FeaturedGameSelection.Select(Api.Database.Games.ToList(), options, currentId, doShuffle, selectedId));
                if (closed) return;
                if (dirty) continue;
                game = next;
                if (visible) RenderGame();
                else renderPending = true;
            }
        }
        private TextBlock AddText(string text, bool title = false)
        {
            var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = title ? Settings.TitleSize : Settings.TextSize,
                FontWeight = title ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0,0,0,10) };
            block.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            if (Settings.UseCustomTextColor) block.Foreground = Brush(title ? Settings.TitleColor : Settings.TextColor);
            details.Children.Add(block); return block;
        }
        private static Brush Brush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        private void RenderGame()
        {
            if (closed) return;
            renderPending = false;
            details.Children.Clear(); details.MaxWidth = Settings.ContentWidth; details.HorizontalAlignment = Settings.Alignment; details.VerticalAlignment = Settings.VerticalAlignment;
            artwork.Opacity = Settings.ShowArtwork ? Settings.ArtworkOpacity : 0;
            shade.Background = Brush(Settings.OverlayColor); shade.Opacity = Settings.OverlayOpacity;
            if (game == null)
            {
                artwork.Source = null; artworkPath = null;
                AddText(Settings.Source == FeaturedGameSource.Pinned ? "Choose a pinned game in Appearance > Featured game settings." : "No matching game is available. Change the selection or filters in Featured game settings.");
                return;
            }
            var path = Settings.ShowArtwork ? (Settings.UseCoverArtwork ? game.CoverImage : game.BackgroundImage) : null;
            if (Settings.ShowArtwork && string.IsNullOrWhiteSpace(path)) path = game.CoverImage;
            if (path != artworkPath)
            {
                artworkPath = path; artwork.Source = null;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var fullPath = Api.Database.GetFullFilePath(path);
                    if (Uri.TryCreate(fullPath,UriKind.Absolute,out var uri) && Converters.DashboardBackgroundCache.Load(uri) is ImageSource image) artwork.Source = image;
                    else artwork.Source = new Converters.UriToBitmapImageConverter().Convert(path,typeof(ImageSource),null,System.Globalization.CultureInfo.CurrentCulture) as ImageSource;
                }
            }
            AddText(game.Name,true);
            if (Settings.ShowDescription) AddText(FeaturedGameSelection.Description(game.Description,Settings.DescriptionLength));
            var metadataSize = Settings.MatchMetadataTextSize ? Settings.TextSize : Settings.MetadataTextSize;
            var metadataColor = Settings.MatchMetadataTextColor
                ? (Settings.UseCustomTextColor ? Brush(Settings.TextColor) : null)
                : (Settings.UseCustomMetadataColor ? Brush(Settings.MetadataTextColor) : null);
            details.Children.Add(new ShelfGameInfo { Model = new GameModel(game), IncludeTitle = false,
                TextSizeOverride = metadataSize, TextBrushOverride = metadataColor, Settings = new ShelveProperties
            {
                ShowPlaytime = Settings.ShowPlaytime, ShowLastPlayed = Settings.ShowLastPlayed, ShowSource = Settings.ShowSource, ShowSourceIcon = Settings.ShowSource,
                ShowAchievementProgress = Settings.ShowAchievements, ShowAchievementTrophy = true, TitleDisplay = ShelfTitleDisplay.Hide,
                MetadataIconSize = Settings.MatchMetadataIconSize ? metadataSize : Settings.MetadataIconSize
            } });
            var actions = new WrapPanel { Margin = new Thickness(0,10,0,0) }; details.Children.Add(actions);
            if (Settings.ShowPlayButton) AddButton(actions,game.IsInstalled ? "Play" : "Install",() => { if (game.IsInstalled) Api.StartGame(game.Id); else Api.InstallGame(game.Id); });
            if (Settings.ShowLibraryButton) AddButton(actions,"Open in library",() => { Api.MainView.SwitchToLibraryView(); Api.MainView.SelectGame(game.Id); });
            if (Settings.Source == FeaturedGameSource.Random || Settings.Source == FeaturedGameSource.RandomFavorite)
                AddButton(actions,"Another game",() => { shuffle = true; dirty = true; QueueRefresh(); });
        }
        private void AddButton(Panel parent, string text, Action action)
        {
            var button = new Button { Content = text, Padding = new Thickness(12,6,12,6), Margin = new Thickness(0,0,8,4) };
            WindowChrome.SetIsHitTestVisibleInChrome(button,true);
            button.Click += (sender,args) =>
            {
                DependencyObject ancestor = this;
                while (ancestor != null && !(ancestor is GridNodeView)) ancestor = VisualTreeHelper.GetParent(ancestor);
                if ((ancestor as FrameworkElement)?.DataContext is ViewModels.Layout.GridNodeViewModel model && model.EditModeEnabled) return;
                try { action(); } catch (Exception ex) { LandingPageExtension.logger.Error(ex,"Featured game action failed."); }
            };
            parent.Children.Add(button);
        }
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnViewShownAsync() { if (closed) return Task.CompletedTask; visible = true; Subscribe(); if (renderPending && !dirty) RenderGame(); return RefreshSafely(); }
        public Task OnViewHiddenAsync() { visible = false; return Task.CompletedTask; }
        public Task OnDayChangedAsync(DateTime time) { dirty = true; return visible ? RefreshSafely() : Task.CompletedTask; }
        public void OnViewClosed()
        {
            closed = true; visible = false;
            if (observed != null) observed.PropertyChanged -= SettingChanged;
            if (subscribed)
            {
                Api.Database.Games.ItemUpdated -= GamesUpdated; Api.Database.Games.ItemCollectionChanged -= GamesChanged;
                LandingPageExtension.Instance.FeaturedSelectionChanged -= SelectionChanged;
                subscribed = false;
            }
        }
    }
}
