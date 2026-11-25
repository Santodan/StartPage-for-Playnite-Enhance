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
            var plan = PlanPreview(copy, out var holdUntil);
            PreviewPlaceholder.Visibility = Visibility.Collapsed; PreviewStatus.Text = "Playing preview" + plan + "..."; previewing = true;
            try
            {
                var played = await IntroHost.PlayAsync(PreviewArea,copy,() => true,holdUntil);
                PreviewStatus.Text = played ? "Preview finished" + plan + "." : "The preview could not start.";
            }
            catch (Exception ex) { PreviewStatus.Text = ex.Message; }
            finally { previewing = false; PreviewPlaceholder.Visibility = Visibility.Visible; }
        }

        // The preview has no Playnite or dashboard to wait for. A GIF/video plays to its end (instead of ending at once in
        // "until the dashboard has loaded"), and with "repeat its first/last part" the part then repeats PreviewRepeats times,
        // so the whole clip and the repeat can be checked. Returns a description for the status line.
        internal const int PreviewRepeats = 3;
        internal static string PlanPreview(StartupIntroSettings copy,out Func<bool> holdUntil)
        {
            holdUntil = null;
            var media = copy.ResolveMedia();
            var clip = media != null && BackgroundMediaFiles.IsAnimated(new Uri(media,UriKind.Absolute)) && !copy.Media.LoopMedia;
            if (!clip) return "";
            if (copy.RepeatsAfterEnd)
            {
                copy.Length = IntroLength.UntilMediaEnds; copy.MinimumMs = 0; copy.MaximumMs = 60000;
                holdUntil = () => (IntroHost.Current?.Artwork.RepeatCount ?? PreviewRepeats) >= PreviewRepeats;
                return ": the whole clip, then its " + (copy.AfterEnd == MediaEndBehavior.RepeatStart ? "first" : "last") + " " + copy.AfterEndRepeatMs + " ms " + PreviewRepeats + " times";
            }
            if (copy.Length == IntroLength.UntilDashboardLoaded) { copy.Length = IntroLength.UntilMediaEnds; copy.MinimumMs = 0; return ": the whole clip"; }
            return "";
        }

        private void StopPreview_Click(object sender,RoutedEventArgs e) => StopPreview();
        private void StopPreview() { if (previewing) IntroHost.SkipCurrent(); }
        private void Reset_Click(object sender,RoutedEventArgs e) => Options?.ResetOptions();
    }
}
