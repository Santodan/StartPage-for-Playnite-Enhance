using System;
using System.Windows.Media;

namespace LandingPage.Settings
{
    public class ImageWidgetSettings : ObservableObjectExt
    {
        private string imagePath = string.Empty;
        public string ImagePath { get => imagePath; set => SetValue(ref imagePath, value ?? string.Empty); }
        private Stretch stretch = Stretch.Uniform;
        public Stretch Stretch { get => stretch; set => SetValue(ref stretch, value); }
        private double opacity = 1;
        public double Opacity { get => opacity; set => SetValue(ref opacity,
            double.IsNaN(value) || double.IsInfinity(value) ? 1 : Math.Max(0, Math.Min(1, value))); }
        // Playback for GIF and video files (still images ignore these). Widgets start muted so a dashboard
        // never starts making sound on its own; older layouts load with these defaults.
        private bool playMedia = true;
        public bool PlayMedia { get => playMedia; set => SetValue(ref playMedia, value); }
        private bool loopMedia = true;
        public bool LoopMedia { get => loopMedia; set => SetValue(ref loopMedia, value); }
        private double playbackSpeed = 1;
        public double PlaybackSpeed { get => playbackSpeed; set => SetValue(ref playbackSpeed, double.IsNaN(value) || double.IsInfinity(value) ? 1 : Math.Max(.25, Math.Min(2, value))); }
        private bool videoMuted = true;
        public bool VideoMuted { get => videoMuted; set => SetValue(ref videoMuted, value); }
        private double videoVolume = .5;
        public double VideoVolume { get => videoVolume; set => SetValue(ref videoVolume, double.IsNaN(value) || double.IsInfinity(value) ? .5 : Math.Max(0, Math.Min(1, value))); }
        private bool pauseWhenInactive = true;
        public bool PauseWhenInactive { get => pauseWhenInactive; set => SetValue(ref pauseWhenInactive, value); }
        private int gifFrameRate = 24;
        public int GifFrameRate { get => gifFrameRate; set => SetValue(ref gifFrameRate, Math.Max(1, Math.Min(60, value))); }
        // Longest decoded GIF side in pixels (0 = original size). Lower values save memory in small panels.
        private int gifMaxDimension = 1280;
        public int GifMaxDimension { get => gifMaxDimension; set => SetValue(ref gifMaxDimension, Math.Max(0, Math.Min(3840, value))); }
        public static BackgroundFit FitFor(Stretch stretch) => stretch == Stretch.UniformToFill ? BackgroundFit.Fill
            : stretch == Stretch.Fill ? BackgroundFit.Stretch : stretch == Stretch.None ? BackgroundFit.OriginalSize : BackgroundFit.Fit;
        // Framing/playback options for the shared media player, mirroring this widget's settings.
        public void ApplyTo(BackgroundAppearanceSettings media)
        {
            media.Fit = FitFor(Stretch); media.ImageOpacity = Opacity; media.PlayMedia = PlayMedia; media.LoopMedia = LoopMedia;
            media.PlaybackSpeed = PlaybackSpeed; media.VideoMuted = VideoMuted; media.VideoVolume = VideoVolume;
            media.PauseMediaWhenInactive = PauseWhenInactive; media.GifFrameRate = GifFrameRate; media.GifMaxDimension = GifMaxDimension;
        }
        // Asks the widget to play its GIF/video from the beginning (editor "Restart" button).
        public void RestartMedia() => NotifyPropertyChanged(nameof(RestartMedia), null, null);
        private string link = string.Empty;
        public string Link
        {
            get => link;
            set
            {
                var candidate = (value ?? string.Empty).Trim();
                if (candidate.Length > 0 && (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
                    throw new ArgumentException("Enter a website address starting with https:// or http://.");
                SetValue(ref link, candidate);
            }
        }
    }
}
