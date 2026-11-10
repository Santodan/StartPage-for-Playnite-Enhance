using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using LandingPage.Models;

namespace LandingPage.Views
{
    public class ShelfGameInfo : UserControl
    {
        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
            nameof(Model), typeof(GameModel), typeof(ShelfGameInfo), new PropertyMetadata(null, Changed));
        public GameModel Model { get => (GameModel)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
            nameof(Settings), typeof(ShelveProperties), typeof(ShelfGameInfo), new PropertyMetadata(null, Changed));
        public ShelveProperties Settings { get => (ShelveProperties)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
        public static readonly DependencyProperty IncludeTitleProperty = DependencyProperty.Register(
            nameof(IncludeTitle), typeof(bool), typeof(ShelfGameInfo), new PropertyMetadata(true, Changed));
        public bool IncludeTitle { get => (bool)GetValue(IncludeTitleProperty); set => SetValue(IncludeTitleProperty, value); }

        public static readonly DependencyProperty DefaultTitleVisibleProperty = DependencyProperty.Register(
            nameof(DefaultTitleVisible), typeof(bool), typeof(ShelfGameInfo), new PropertyMetadata(true, Changed));
        public bool DefaultTitleVisible { get => (bool)GetValue(DefaultTitleVisibleProperty); set => SetValue(DefaultTitleVisibleProperty, value); }

        private readonly StackPanel panel = new StackPanel();
        private GameModel observedModel;
        private Game observedGame;
        private ShelveProperties observedSettings;
        private LandingPageSettings globalSettings;
        private int generation;
        private readonly DispatcherTimer achievementTimer;

        public ShelfGameInfo()
        {
            Content = panel;
            achievementTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(10) };
            achievementTimer.Tick += (sender, args) => Refresh();
            Loaded += (sender, args) => Rebind();
            Unloaded += (sender, args) => { generation++; achievementTimer.Stop(); Unsubscribe(); };
        }

        private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var view = (ShelfGameInfo)sender;
            if (view.IsLoaded) view.Rebind();
        }

        private void Rebind()
        {
            Unsubscribe();
            observedSettings = Settings;
            observedModel = Model;
            observedGame = Model?.Game;
            globalSettings = LandingPageExtension.Instance?.Settings;
            if (observedSettings != null) observedSettings.PropertyChanged += Updated;
            if (observedModel != null) observedModel.PropertyChanged += ModelUpdated;
            if (observedGame != null) observedGame.PropertyChanged += Updated;
            if (globalSettings != null) globalSettings.PropertyChanged += ProviderUpdated;
            Refresh();
        }

        private void ModelUpdated(object sender, PropertyChangedEventArgs args)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => { if (IsLoaded) Rebind(); })); return; }
            if (IsLoaded) Rebind();
        }
        private void Updated(object sender, PropertyChangedEventArgs args)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => { if (IsLoaded) Refresh(); })); return; }
            if (IsLoaded) Refresh();
        }
        private void ProviderUpdated(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(LandingPageSettings.SelectedAchievementsProvider)) Updated(sender, args);
        }

        private void Unsubscribe()
        {
            if (observedSettings != null) observedSettings.PropertyChanged -= Updated;
            if (observedModel != null) observedModel.PropertyChanged -= ModelUpdated;
            if (observedGame != null) observedGame.PropertyChanged -= Updated;
            if (globalSettings != null) globalSettings.PropertyChanged -= ProviderUpdated;
            observedSettings = null; observedModel = null; observedGame = null; globalSettings = null;
        }

        private TextBlock AddText(string value, bool title = false, bool attach = true)
        {
            var text = new TextBlock { Text = value, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(2, title ? 6 : 2, 2, 0), ToolTip = value };
            text.SetResourceReference(StyleProperty, "BaseTextBlockStyle");
            text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            if (!title) text.FontSize = 12;
            if (Settings.CustomizeMetadataText)
            {
                text.FontSize = title ? Settings.MetadataTitleSize : Settings.MetadataTextSize;
                text.FontWeight = (title ? Settings.MetadataTitleBold : Settings.MetadataTextBold) ? FontWeights.Bold : FontWeights.Normal;
                text.FontStyle = Settings.MetadataItalic ? FontStyles.Italic : FontStyles.Normal;
                text.TextAlignment = Settings.MetadataTextAlignment;
                text.TextWrapping = Settings.MetadataWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
                text.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(title ? Settings.MetadataTitleColor : Settings.MetadataTextColor));
            }
            if (attach) panel.Children.Add(text);
            Visibility = Visibility.Visible;
            return text;
        }

        private void AddPlatforms(IEnumerable<Platform> platforms)
        {
            if (!Settings.ShowPlatformIcons)
            {
                if (Settings.ShowPlatform) AddText(string.Join(", ", platforms.Select(p => p.Name)));
                return;
            }
            var row = new WrapPanel { Margin = new Thickness(2, 2, 2, 0) };
            if (Settings.CustomizeMetadataText)
                row.HorizontalAlignment = Settings.MetadataTextAlignment == TextAlignment.Center ? HorizontalAlignment.Center
                    : Settings.MetadataTextAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            foreach (var platform in platforms)
            {
                ImageSource source = null;
                if (!string.IsNullOrWhiteSpace(platform.Icon))
                {
                    try
                    {
                        source = new Converters.UriToBitmapImageConverter().Convert(platform.Icon, typeof(ImageSource), null,
                            CultureInfo.CurrentCulture) as ImageSource;
                    }
                    catch (Exception ex) { LandingPageExtension.logger.Debug(ex, "Unable to load a platform metadata icon."); }
                }
                var entry = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 2), ToolTip = platform.Name };
                if (source != null) entry.Children.Add(new Image { Source = source, Width = Settings.MetadataIconSize,
                    Height = Settings.MetadataIconSize, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, Settings.ShowPlatform ? 4 : 0, 0), ToolTip = platform.Name });
                if (Settings.ShowPlatform || source == null)
                {
                    var label = AddText(platform.Name, attach: false);
                    label.Margin = new Thickness(0);
                    label.VerticalAlignment = VerticalAlignment.Center;
                    entry.Children.Add(label);
                }
                row.Children.Add(entry);
            }
            if (row.Children.Count > 0) { panel.Children.Add(row); Visibility = Visibility.Visible; }
        }

        private void AddAchievementProgress(ShelfAchievementProgress progress)
        {
            var percentage = 100d * progress.Unlocked / progress.Total;
            var label = AddText(string.Format(CultureInfo.CurrentCulture, "Achievements: {0}/{1} ({2:0}%)",
                progress.Unlocked, progress.Total, percentage), attach: !Settings.ShowAchievementTrophy);
            if (Settings.ShowAchievementTrophy)
            {
                var row = new DockPanel { Margin = new Thickness(2, 2, 2, 0), ToolTip = label.Text };
                var trophy = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse("M4,2 L16,2 16,7 C16,11 13,14 10,14 C7,14 4,11 4,7 Z M4,4 L1,4 1,7 C1,10 3,11 6,11 L6,9 C4,9 3,8 3,6 L4,6 Z M16,4 L19,4 19,7 C19,10 17,11 14,11 L14,9 C16,9 17,8 17,6 L16,6 Z M9,14 L11,14 11,17 15,17 15,19 5,19 5,17 9,17 Z"),
                    Width = Settings.MetadataIconSize, Height = Settings.MetadataIconSize, Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0)
                };
                trophy.SetBinding(Shape.FillProperty, new Binding(nameof(TextBlock.Foreground)) { Source = label });
                row.Children.Add(trophy);
                label.Margin = new Thickness(0);
                label.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(label);
                panel.Children.Add(row);
            }
            panel.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = percentage,
                Height = 5, Margin = new Thickness(2, 3, 2, 2), IsHitTestVisible = false });
        }

        private void AddSource(Game game)
        {
            var name = Model.Source.Name;
            ImageSource icon = null;
            try
            {
                var libraries = LandingPageExtension.Instance?.PlayniteApi?.Addons.Plugins.OfType<LibraryPlugin>().ToList();
                var library = libraries?.FirstOrDefault(p => p.Id == game.PluginId)
                    ?? libraries?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(name) && name != "Playnite"
                        && p.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                if (library != null)
                {
                    if (name == "Playnite") name = library.Name;
                    if (Settings.ShowSourceIcon && !string.IsNullOrWhiteSpace(library.LibraryIcon))
                        icon = new Converters.UriToBitmapImageConverter().Convert(library.LibraryIcon,
                            typeof(ImageSource), null, CultureInfo.CurrentCulture) as ImageSource;
                }
            }
            catch (Exception ex) { LandingPageExtension.logger.Debug(ex, "Unable to load a library metadata icon."); }
            AddSourceBadge(name, icon);
        }

        private void AddSourceBadge(string name, ImageSource icon)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (icon == null && Settings.ShowSourceIcon && string.Equals(name, "Playnite", StringComparison.OrdinalIgnoreCase))
                icon = LoadPlayniteIcon(LandingPageExtension.Instance?.PlayniteApi?.Paths.ApplicationPath);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 2, 2, 0), ToolTip = name };
            if (Settings.CustomizeMetadataText)
                row.HorizontalAlignment = Settings.MetadataTextAlignment == TextAlignment.Center ? HorizontalAlignment.Center
                    : Settings.MetadataTextAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            if (Settings.ShowSourceIcon && icon != null)
                row.Children.Add(new Image { Source = icon, Width = Settings.MetadataIconSize, Height = Settings.MetadataIconSize,
                    Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, ToolTip = name,
                    Margin = new Thickness(0, 0, Settings.ShowSource ? 4 : 0, 0) });
            if (Settings.ShowSource || !Settings.ShowSourceIcon || icon == null)
            {
                var label = AddText(name, attach: false);
                label.Margin = new Thickness(0);
                label.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(label);
            }
            panel.Children.Add(row);
            Visibility = Visibility.Visible;
        }

        private static ImageSource playniteIcon;
        internal static ImageSource LoadPlayniteIcon(string applicationPath)
        {
            if (playniteIcon != null) return playniteIcon;
            if (string.IsNullOrWhiteSpace(applicationPath)) return null;
            try
            {
                var executable = Directory.Exists(applicationPath) ? System.IO.Path.Combine(applicationPath, "Playnite.DesktopApp.exe") : applicationPath;
                if (!File.Exists(executable)) return null;
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(executable))
                {
                    if (icon == null) return null;
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle,
                        Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    playniteIcon = source;
                    return source;
                }
            }
            catch (Exception ex) { LandingPageExtension.logger.Debug(ex, "Unable to load the default Playnite source icon."); return null; }
        }

        private async void Refresh()
        {
            var version = ++generation;
            panel.Children.Clear();
            Visibility = Visibility.Collapsed;
            var game = Model?.Game;
            var settings = Settings;
            if (IsLoaded && settings?.ShowAchievementProgress == true) achievementTimer.Start();
            else achievementTimer.Stop();
            if (game == null || settings == null || string.IsNullOrEmpty(game.Name)) return;
            if (IncludeTitle && settings.TitleDisplay != ShelfTitleDisplay.Hide
                && (DefaultTitleVisible || settings.TitleDisplay == ShelfTitleDisplay.Show)) AddText(game.Name, true);
            if (settings.ShowPlaytime) AddText(string.Format(CultureInfo.CurrentCulture, "Playtime: {0:0.#} h", game.Playtime / 3600d));
            if (settings.ShowLastPlayed) AddText("Last played: " + (game.LastActivity?.ToLocalTime().ToString("d") ?? "Never"));
            if (settings.ShowSource || settings.ShowSourceIcon) AddSource(game);
            if ((settings.ShowPlatform || settings.ShowPlatformIcons) && game.Platforms?.Any() == true) AddPlatforms(game.Platforms);
            if (settings.ShowFavoriteBadge && game.Favorite) AddText("★ Favorite");
            if (settings.ShowInstallationStatus) AddText(game.IsInstalled ? "Installed" : "Not installed");
            if (!settings.ShowAchievementProgress) return;
            try
            {
                var plugin = LandingPageExtension.Instance;
                var progress = await ShelfAchievements.GetAsync(game.Id,
                    plugin.Settings.SelectedAchievementsProvider, plugin.PlayniteApi?.Paths.ExtensionsDataPath);
                if (!IsLoaded || version != generation || progress == null) return;
                AddAchievementProgress(progress);
            }
            catch (Exception ex) { LandingPageExtension.logger.Debug(ex, "Unable to show optional shelf achievement progress."); }
        }
    }
}
