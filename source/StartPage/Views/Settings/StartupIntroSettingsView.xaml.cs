using System;
using System.Windows;
using System.Windows.Controls;
using LandingPage.Settings;
using LandingPage.Utils;

namespace LandingPage.Views.Settings
{
    // Editor for a StartupIntroSettings: the global intro (extension settings) or a page intro (page manager).
    public partial class StartupIntroSettingsView : UserControl
    {
        public static readonly DependencyProperty IsPageIntroProperty = DependencyProperty.Register(nameof(IsPageIntro),typeof(bool),typeof(StartupIntroSettingsView),
            new PropertyMetadata(false,(sender,args) => ((StartupIntroSettingsView)sender).UpdateMode()));
        public bool IsPageIntro { get => (bool)GetValue(IsPageIntroProperty); set => SetValue(IsPageIntroProperty,value); }

        public StartupIntroSettingsView()
        {
            InitializeComponent(); UpdateMode();
            Unloaded += (sender,args) => StopPreview();
            DataContextChanged += (sender,args) =>
            {
                if (args.OldValue is StartupIntroSettings old) old.PropertyChanged -= OptionChanged;
                if (args.NewValue is StartupIntroSettings current) current.PropertyChanged += OptionChanged;
                UpdateMode();
            };
        }

        private void OptionChanged(object sender,System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(StartupIntroSettings.Coverage) || args.PropertyName == nameof(StartupIntroSettings.Repeat)) UpdateMode();
        }

        private StartupIntroSettings Options => DataContext as StartupIntroSettings;
        private bool previewing;

        private void UpdateMode()
        {
            EnabledToggle.Content = IsPageIntro ? "Play an intro when switching to this page" : "Play a startup intro";
            Summary.Text = IsPageIntro
                ? "Shows an image, GIF or video while this page opens. It is saved with the page, so dashboard profiles carry it."
                : "Shows your image, GIF or video over Playnite before the dashboard appears. Widgets keep loading behind it.";
            RepeatOnce.Content = IsPageIntro ? "First switch to this page each session" : "When Playnite starts";
            RepeatEvery.Content = IsPageIntro ? "Every switch to this page" : "Every time StartPage opens";
            PlayOnOpenToggle.Visibility = IsPageIntro ? Visibility.Visible : Visibility.Collapsed;
            // Launch options apply to the global whole-window intro that plays when Playnite starts.
            var options = Options;
            LaunchOptions.Visibility = !IsPageIntro && options != null && options.Coverage == IntroCoverage.PlayniteWindow && options.Repeat == IntroRepeat.OncePerSession
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BrowseFile_Click(object sender,RoutedEventArgs e)
        {
            if (Options == null) return;
            var path = LandingPageExtension.Instance?.PlayniteApi?.Dialogs.SelectFile(BackgroundMediaFiles.Filter);
            if (!string.IsNullOrEmpty(path)) Options.MediaPath = path;
        }

        private void BrowseFolder_Click(object sender,RoutedEventArgs e)
        {
            if (Options == null) return;
            var path = LandingPageExtension.Instance?.PlayniteApi?.Dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(path)) Options.FolderPath = path;
        }

        // Plays the intro inside the preview box, so it also works from the modal settings window.
        internal async void Preview_Click(object sender,RoutedEventArgs e)
        {
            var options = Options; if (options == null) return;
            if (IntroHost.Current != null) { PreviewStatus.Text = "An intro is already playing."; return; }
            var copy = Models.DashboardProfiles.Clone(options); copy.Enabled = true;
            if (copy.ResolveMedia() == null && copy.Length == IntroLength.UntilMediaEnds) { PreviewStatus.Text = "Choose an existing image, GIF or video (or a folder that contains one) to preview."; return; }
            PreviewPlaceholder.Visibility = Visibility.Collapsed; PreviewStatus.Text = "Playing preview..."; previewing = true;
            try
            {
                var played = await IntroHost.PlayAsync(PreviewArea,copy,() => true);
                PreviewStatus.Text = played ? "Preview finished." : "The preview could not start.";
            }
            catch (Exception ex) { PreviewStatus.Text = ex.Message; }
            finally { previewing = false; PreviewPlaceholder.Visibility = Visibility.Visible; }
        }

        private void StopPreview_Click(object sender,RoutedEventArgs e) => StopPreview();
        private void StopPreview() { if (previewing) IntroHost.SkipCurrent(); }
        private void Reset_Click(object sender,RoutedEventArgs e) => Options?.ResetOptions();
    }
}
