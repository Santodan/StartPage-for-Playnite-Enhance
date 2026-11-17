using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LandingPage.Settings;
using LandingPage.Utils;
using LandingPage.ViewModels.Layout;

namespace LandingPage.Views
{
    public sealed class ShortcutWidgetView : UserControl
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings),typeof(ShortcutWidgetSettings),typeof(ShortcutWidgetView),new PropertyMetadata(null,(s,e) => ((ShortcutWidgetView)s).Attach()));
        public ShortcutWidgetSettings Settings { get => (ShortcutWidgetSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty,value); }
        private ShortcutWidgetSettings observed;
        private ObservableCollection<ShortcutEntry> items;
        private readonly HashSet<ShortcutEntry> entries = new HashSet<ShortcutEntry>();
        private readonly Dictionary<string,ImageSource> icons = new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
        private bool pending;
        private WebsiteIconCache websiteIcons;
        private CancellationTokenSource iconCancellation;
        public ShortcutWidgetView()
        {
            SetResourceReference(ForegroundProperty,"TextBrush"); Settings = new ShortcutWidgetSettings();
            SetBinding(SettingsProperty,new Binding("DataContext.GridNode.ViewProperties.ShortcutSettings") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,typeof(GridNodeView),1),FallbackValue = Settings,TargetNullValue = Settings });
            Loaded += (s,e) => { Attach(); Render(); }; Unloaded += (s,e) => { Detach(); icons.Clear(); iconCancellation?.Cancel(); websiteIcons?.Dispose(); websiteIcons = null; iconCancellation?.Dispose(); iconCancellation = null; };
        }
        private void Detach()
        {
            if(observed != null) observed.PropertyChanged -= Changed;
            if(items != null) items.CollectionChanged -= CollectionChanged;
            foreach(var item in entries) item.PropertyChanged -= Changed;
            entries.Clear(); observed = null; items = null;
        }
        private void Attach()
        {
            Detach(); if(!IsLoaded || Settings == null) return;
            observed = Settings; observed.PropertyChanged += Changed; items = observed.Items; items.CollectionChanged += CollectionChanged;
            foreach(var item in items) if(item != null && entries.Add(item)) item.PropertyChanged += Changed;
            Schedule();
        }
        private void CollectionChanged(object sender,NotifyCollectionChangedEventArgs e) { Attach(); icons.Clear(); }
        private void Changed(object sender,PropertyChangedEventArgs e)
        {
            if(sender is ShortcutWidgetSettings && e.PropertyName == nameof(ShortcutWidgetSettings.Items)) Attach();
            if(sender is ShortcutEntry) icons.Clear(); Schedule();
        }
        private void Schedule()
        {
            if(!IsLoaded || pending) return; pending = true;
            Dispatcher.BeginInvoke(new Action(() => { pending = false; if(IsLoaded) Render(); }),DispatcherPriority.Background);
        }
        private static Brush Brush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        private void Render()
        {
            var options = Settings; if(options == null) return;
            Panel panel = options.Layout == ShortcutLayout.Wrap ? (Panel)new WrapPanel() : new StackPanel { Orientation = options.Layout == ShortcutLayout.Horizontal ? Orientation.Horizontal : Orientation.Vertical };
            panel.HorizontalAlignment = options.Alignment;
            foreach(var entry in options.Items) {
                if(entry == null) continue;
                var game = entry.Kind == ShortcutKind.Game ? LandingPageExtension.Instance?.PlayniteApi.Database.Games.Get(entry.GameId) : null;
                var title = string.IsNullOrWhiteSpace(entry.Label) ? game?.Name ?? entry.DisplayName : entry.Label;
                var button = new Button { Margin = new Thickness(options.Spacing/2),Padding = new Thickness(options.Padding),HorizontalContentAlignment = HorizontalAlignment.Center,VerticalContentAlignment = VerticalAlignment.Center,ToolTip = title,Tag = entry };
                if(options.ButtonWidth > 0) button.Width = Math.Max(40,options.ButtonWidth);
                var valid = ShortcutLauncher.TryCreateStartInfo(entry,out var unused,out var error);
                if(entry.Kind == ShortcutKind.Game && game == null) { valid = false; error = "This game is not in the current library. Choose a game in settings."; }
                button.IsEnabled = valid;
                if(!valid) { button.ToolTip = title+" - "+error; ToolTipService.SetShowOnDisabled(button,true); }
                var row = new StackPanel { Orientation = options.IconAbove ? Orientation.Vertical : Orientation.Horizontal };
                if(options.ShowIcons) {
                    var icon = Icon(entry,game?.Icon);
                    FrameworkElement art;
                    if(entry.Kind == ShortcutKind.Website && string.IsNullOrWhiteSpace(entry.IconPath)) {
                        var image = new Image { Source = icon ?? ShortcutIcons.Website,Stretch = Stretch.Uniform };
                        art = image;
                        if(valid) _ = LoadWebsiteIcon(image,entry);
                    } else art = icon == null ? (FrameworkElement)new TextBlock { Text = entry.Kind == ShortcutKind.Game ? "\u25B6" : entry.Kind == ShortcutKind.Folder ? "\u25A3" : entry.Kind == ShortcutKind.Website ? "\u25CE" : "\u25A4",FontSize = options.IconSize*.8,FontFamily = new FontFamily("Segoe UI Symbol"),TextAlignment = TextAlignment.Center,VerticalAlignment = VerticalAlignment.Center } : new Image { Source = icon,Stretch = Stretch.Uniform };
                    art.Width = art.Height = options.IconSize; art.Margin = new Thickness(0,0,options.ShowLabels && !options.IconAbove ? 8 : 0,options.ShowLabels && options.IconAbove ? 6 : 0); row.Children.Add(art);
                }
                var textBrush = options.CustomTextColor ? Brush(options.TextColor) : null;
                if(options.ShowLabels || !options.ShowIcons) {
                    var text = new TextBlock { Text = title,FontSize = options.FontSize,FontWeight = options.Bold ? FontWeights.Bold : FontWeights.Normal,FontStyle = options.Italic ? FontStyles.Italic : FontStyles.Normal,TextWrapping = TextWrapping.Wrap,TextAlignment = TextAlignment.Center,VerticalAlignment = VerticalAlignment.Center };
                    row.Children.Add(text);
                }
                foreach(var child in row.Children) if(child is TextBlock text) { if(textBrush != null) text.Foreground = textBrush; else text.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); }
                button.Content = row;
                if(options.CustomBackground || options.CustomBorder) {
                    button.Content = null;
                    var frame = new Border { Padding = button.Padding,CornerRadius = new CornerRadius(options.CornerRadius),BorderThickness = new Thickness(options.BorderWidth),Child = row };
                    if(options.CustomBackground) frame.Background = Brush(options.Background); else frame.SetResourceReference(Border.BackgroundProperty,"ButtonBackgroundBrush");
                    if(options.CustomBorder) frame.BorderBrush = Brush(options.BorderColor); else frame.SetResourceReference(Border.BorderBrushProperty,"ButtonBorderBrush");
                    button.Content = frame; button.Padding = new Thickness(0);
                }
                button.Click += (s,e) => Activate(entry); panel.Children.Add(button);
            }
            if(panel.Children.Count == 0) { var hint = new TextBlock { Text = "Add shortcuts using this widget's settings gear.",TextWrapping = TextWrapping.Wrap,Margin = new Thickness(8) }; hint.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); panel.Children.Add(hint); }
            Content = new ScrollViewer { Content = panel,VerticalScrollBarVisibility = options.Layout == ShortcutLayout.Horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,HorizontalScrollBarVisibility = options.Layout == ShortcutLayout.Horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled };
        }
        private ImageSource Icon(ShortcutEntry entry,string gameIcon)
        {
            var path = Environment.ExpandEnvironmentVariables(entry.IconPath ?? "").Trim();
            if(path.Length == 0 && entry.Kind == ShortcutKind.Folder) return ShortcutIcons.Folder;
            if(path.Length == 0 && entry.Kind == ShortcutKind.Website) return ShortcutIcons.Website;
            if(path.Length == 0 && entry.Kind == ShortcutKind.Game && !string.IsNullOrEmpty(gameIcon)) path = LandingPageExtension.Instance.PlayniteApi.Database.GetFullFilePath(gameIcon);
            if(path.Length == 0 && entry.Kind == ShortcutKind.Application) path = Environment.ExpandEnvironmentVariables(entry.Target ?? "");
            if(path.Length == 0 || !File.Exists(path)) return null;
            if(icons.TryGetValue(path,out var cached)) return cached;
            try {
                ImageSource image;
                if(Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".lnk",StringComparison.OrdinalIgnoreCase)) {
                    using(var icon = System.Drawing.Icon.ExtractAssociatedIcon(path)) { if(icon == null) return null; image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions()); }
                } else {
                    using(var stream = File.OpenRead(path)) { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 256; bitmap.StreamSource = stream; bitmap.EndInit(); image = bitmap; }
                }
                image.Freeze(); icons[path] = image; return image;
            } catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is System.Runtime.InteropServices.COMException || ex is System.IO.FileFormatException || ex is InvalidOperationException) { icons[path] = null; return null; }
        }
        private async Task LoadWebsiteIcon(Image image,ShortcutEntry entry)
        {
            if(websiteIcons == null) {
                var plugin = LandingPageExtension.Instance;
                var folder = plugin == null ? null : Path.Combine(plugin.PlayniteApi.Paths.ExtensionsDataPath,plugin.Id.ToString(),"cache","shortcut-icons");
                websiteIcons = new WebsiteIconCache(folder); iconCancellation = new CancellationTokenSource();
            }
            var service = websiteIcons; var token = iconCancellation.Token; var target = entry.Target;
            try {
                var icon = await service.GetAsync(target,token);
                if(icon != null && !token.IsCancellationRequested && IsLoaded && entry.Kind == ShortcutKind.Website && entry.Target == target && string.IsNullOrWhiteSpace(entry.IconPath)) image.Source = icon;
            }
            catch(OperationCanceledException) { }
            catch(Exception error) { LandingPageExtension.logger.Debug("Shortcut website icon unavailable: "+error.GetType().Name); }
        }
        private void Activate(ShortcutEntry entry)
        {
            DependencyObject parent = this;
            while(parent != null && !(parent is GridNodeView)) parent = VisualTreeHelper.GetParent(parent);
            if((parent as FrameworkElement)?.DataContext is GridNodeViewModel model && model.EditModeEnabled) return;
            if(!ShortcutLauncher.TryCreateStartInfo(entry,out var info,out var error)) return;
            try {
                if(entry.Kind == ShortcutKind.Game) { var api = LandingPageExtension.Instance.PlayniteApi; var game = api.Database.Games.Get(entry.GameId); if(game == null) return; if(game.IsInstalled) api.StartGame(game.Id); else api.InstallGame(game.Id); }
                else Process.Start(info);
            } catch(Exception ex) { LandingPageExtension.logger.Error(ex,"Shortcut activation failed."); LandingPageExtension.Instance.PlayniteApi.Dialogs.ShowErrorMessage("This shortcut could not be opened. Check its target and arguments in widget settings.","StartPage shortcut"); }
        }
    }
}
