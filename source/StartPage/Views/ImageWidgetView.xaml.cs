using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LandingPage.Settings;
using LandingPage.Utils;
using LandingPage.ViewModels.Layout;

namespace LandingPage.Views
{
    public partial class ImageWidgetView : UserControl
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
            nameof(Settings), typeof(ImageWidgetSettings), typeof(ImageWidgetView),
            new PropertyMetadata(null, (sender, args) => ((ImageWidgetView)sender).SettingsChanged(args.OldValue as ImageWidgetSettings)));
        public ImageWidgetSettings Settings
        {
            get => (ImageWidgetSettings)GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }
        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
            nameof(ImageSource), typeof(ImageSource), typeof(ImageWidgetView));
        public ImageSource ImageSource { get => (ImageSource)GetValue(ImageSourceProperty); private set => SetValue(ImageSourceProperty, value); }
        public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
            nameof(Status), typeof(string), typeof(ImageWidgetView), new PropertyMetadata(string.Empty));
        public string Status { get => (string)GetValue(StatusProperty); private set => SetValue(StatusProperty, value); }
        public static readonly DependencyProperty HasLinkProperty = DependencyProperty.Register(
            nameof(HasLink), typeof(bool), typeof(ImageWidgetView), new PropertyMetadata(false));
        public bool HasLink { get => (bool)GetValue(HasLinkProperty); private set => SetValue(HasLinkProperty, value); }

        // Framing/playback handed to the media player for GIF and video files.
        private readonly BackgroundAppearanceSettings media = new BackgroundAppearanceSettings();
        // True while a still image or an animated file is shown (the link only opens on content).
        public bool HasDisplayedMedia => ImageSource != null || WidgetMedia?.MediaUri != null;
        public ImageWidgetView()
        {
            Settings = new ImageWidgetSettings();
            InitializeComponent();
            WidgetMedia.Appearance = media;
            SetBinding(SettingsProperty, new Binding("DataContext.GridNode.ViewProperties.ImageSettings")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(GridNodeView), 1),
                FallbackValue = Settings, TargetNullValue = Settings
            });
            Loaded += (sender, args) => { Subscribe(); ReloadImage(); };
            Unloaded += (sender, args) => Unsubscribe(Settings);
        }

        private void Subscribe()
        {
            if (Settings == null) return;
            Settings.PropertyChanged -= SettingChanged;
            Settings.PropertyChanged += SettingChanged;
        }

        private void Unsubscribe(ImageWidgetSettings settings)
        {
            if (settings != null) settings.PropertyChanged -= SettingChanged;
        }

        private void SettingsChanged(ImageWidgetSettings previous)
        {
            Unsubscribe(previous);
            if (IsLoaded) Subscribe();
            ReloadImage();
        }

        private void SettingChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ImageWidgetSettings.ImagePath)) ReloadImage();
            else if (args.PropertyName == nameof(ImageWidgetSettings.Link)) HasLink = !string.IsNullOrEmpty(Settings?.Link);
            else if (args.PropertyName == nameof(ImageWidgetSettings.RestartMedia)) media.RestartMedia();
            else Settings?.ApplyTo(media);
        }

        private void ReloadImage()
        {
            ImageSource = null;
            if (WidgetMedia != null) { WidgetMedia.MediaUri = null; WidgetMedia.Visibility = Visibility.Collapsed; }
            HasLink = !string.IsNullOrEmpty(Settings?.Link);
            Status = "Choose a local image, GIF or video in Appearance > Image settings.";
            if (string.IsNullOrWhiteSpace(Settings?.ImagePath)) return;
            try
            {
                if (!File.Exists(Settings.ImagePath)) { Status = "Image file not found."; return; }
                // GIFs and videos play through the shared media player (same engine as animated backgrounds).
                if (Uri.TryCreate(Path.GetFullPath(Settings.ImagePath), UriKind.Absolute, out var mediaUri) && BackgroundMediaFiles.IsAnimated(mediaUri) && WidgetMedia != null)
                {
                    Settings.ApplyTo(media);
                    WidgetMedia.MediaUri = mediaUri; WidgetMedia.Visibility = Visibility.Visible;
                    Status = string.Empty;
                    return;
                }
                // Decode fully before closing the stream, so the widget does not lock the image file.
                using (var stream = new FileStream(Settings.ImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    ImageSource = bitmap;
                }
                Status = string.Empty;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException
                || ex is ArgumentException || ex is System.IO.FileFormatException || ex is InvalidOperationException
                || ex is System.Runtime.InteropServices.COMException || ex is System.Security.SecurityException)
            {
                Status = "This image could not be loaded. Choose a supported image file.";
            }
        }

        private void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            DependencyObject parent = this;
            while (parent != null && !(parent is GridNodeView)) parent = VisualTreeHelper.GetParent(parent);
            if ((parent as FrameworkElement)?.DataContext is GridNodeViewModel model && model.EditModeEnabled) return;
            if (!HasDisplayedMedia || !Uri.TryCreate(Settings?.Link, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return;
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                LandingPageExtension.logger.Error(ex, "Unable to open image widget link.");
                LandingPageExtension.Instance.PlayniteApi.Dialogs.ShowErrorMessage("Unable to open this link.", "Image widget");
            }
            e.Handled = true;
        }
    }
}
