using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace LandingPage.Settings
{
    public enum IntroMediaSource { File, RandomFromFolder }
    // Whole Playnite window (sidebar and top bar included) or only the StartPage area.
    public enum IntroCoverage { PlayniteWindow, StartPageArea }
    public enum IntroLength { UntilMediaEnds, FixedTime, UntilDashboardLoaded }
    public enum IntroExit { Fade, ZoomIn, ZoomOut, Spotlight, Curtain, Cut }
    // Global intro: once = when Playnite starts, every = every time StartPage opens.
    // Page intro: once = first switch to the page each session, every = every switch to the page.
    public enum IntroRepeat { OncePerSession, EveryTime }
    // What a non-looping GIF/video shows once it has reached its end while the intro or transition is still up
    // (still loading, minimum/fixed time, waiting for Playnite): its last frame, or a repeating part of it.
    public enum MediaEndBehavior { HoldLastFrame, RepeatStart, RepeatEnd }

    // A startup intro: an image, GIF or video shown over Playnite or StartPage before the dashboard appears.
    // Used globally (LandingPageSettings.StartupIntro) and per dashboard page (DashboardPage.Intro).
    public sealed class StartupIntroSettings : ObservableObjectExt
    {
        private static int Limit(int value,int min,int max) => Math.Max(min,Math.Min(max,value));

        private bool enabled;
        public bool Enabled { get => enabled; set => SetValue(ref enabled,value); }
        private IntroMediaSource source = IntroMediaSource.File;
        public IntroMediaSource Source { get => source; set => SetValue(ref source,value,nameof(Source),nameof(UsesFolder)); }
        private string mediaPath;
        public string MediaPath { get => mediaPath; set => SetValue(ref mediaPath,string.IsNullOrWhiteSpace(value) ? null : value.Trim()); }
        private string folderPath;
        public string FolderPath { get => folderPath; set => SetValue(ref folderPath,string.IsNullOrWhiteSpace(value) ? null : value.Trim()); }
        private IntroCoverage coverage = IntroCoverage.PlayniteWindow;
        public IntroCoverage Coverage { get => coverage; set => SetValue(ref coverage,value); }
        private IntroRepeat repeat = IntroRepeat.OncePerSession;
        public IntroRepeat Repeat { get => repeat; set => SetValue(ref repeat,value); }
        // Page intros only: also play when StartPage opens on this page (the global intro, when enabled, plays first).
        private bool playWhenStartPageOpens;
        public bool PlayWhenStartPageOpens { get => playWhenStartPageOpens; set => SetValue(ref playWhenStartPageOpens,value); }
        // Global whole-window intro played when Playnite starts: open it while Playnite is still loading, in its own
        // window over Playnite's saved window position (or the whole monitor), instead of after the window appears.
        private bool playWhileLoading = true;
        public bool PlayWhileLoading { get => playWhileLoading; set => SetValue(ref playWhileLoading,value); }
        private bool launchFullScreen;
        public bool LaunchFullScreen { get => launchFullScreen; set => SetValue(ref launchFullScreen,value); }
        // The launch intro window normally stays above Playnite's own windows only, so other apps can be used while
        // Playnite loads. On: it stays above every app until it ends (the original behavior).
        private bool launchAboveOtherApps;
        public bool LaunchAboveOtherApps { get => launchAboveOtherApps; set => SetValue(ref launchAboveOtherApps,value); }
        private IntroLength length = IntroLength.UntilMediaEnds;
        public IntroLength Length { get => length; set => SetValue(ref length,value,nameof(Length),nameof(UsesFixedTime)); }
        private int fixedMs = 3000;
        public int FixedMs { get => fixedMs; set => SetValue(ref fixedMs,Limit(value,500,30000)); }
        private int minimumMs = 1000;
        public int MinimumMs { get => minimumMs; set => SetValue(ref minimumMs,Limit(value,0,30000)); }
        private int maximumMs = 12000;
        public int MaximumMs { get => maximumMs; set => SetValue(ref maximumMs,Limit(value,1000,60000)); }
        // After a non-looping GIF/video ends while the intro is still shown: hold the last frame or repeat its first/last part.
        private MediaEndBehavior afterEnd = MediaEndBehavior.HoldLastFrame;
        public MediaEndBehavior AfterEnd { get => afterEnd; set => SetValue(ref afterEnd,value,nameof(AfterEnd),nameof(RepeatsAfterEnd)); }
        private int afterEndRepeatMs = 1000;
        public int AfterEndRepeatMs { get => afterEndRepeatMs; set => SetValue(ref afterEndRepeatMs,Limit(value,1,10000)); }
        [JsonIgnore] public bool RepeatsAfterEnd => AfterEnd != MediaEndBehavior.HoldLastFrame;
        private int fadeInMs = 300;
        public int FadeInMs { get => fadeInMs; set => SetValue(ref fadeInMs,Limit(value,0,3000)); }
        private IntroExit exit = IntroExit.Fade;
        public IntroExit Exit { get => exit; set => SetValue(ref exit,value); }
        private int exitMs = 600;
        public int ExitMs { get => exitMs; set => SetValue(ref exitMs,Limit(value,0,3000)); }
        private string backgroundColor = "#FF000000";
        public string BackgroundColor { get => backgroundColor; set => SetValue(ref backgroundColor,string.IsNullOrWhiteSpace(value) ? "#FF000000" : value.Trim()); }
        private bool skipOnInput = true;
        public bool SkipOnInput { get => skipOnInput; set => SetValue(ref skipOnInput,value); }
        private bool showSkipHint = true;
        public bool ShowSkipHint { get => showSkipHint; set => SetValue(ref showSkipHint,value); }
        // Framing (fit, zoom, position, flips, opacity) and playback (loop, speed, mute, volume, GIF limits)
        // reuse the background media options. Intros default to fitting the whole image, with sound, no loop.
        private BackgroundAppearanceSettings media;
        public BackgroundAppearanceSettings Media { get => media ?? (media = DefaultMedia()); set => SetValue(ref media,value ?? DefaultMedia()); }

        public static BackgroundAppearanceSettings DefaultMedia() => new BackgroundAppearanceSettings
            { Fit = BackgroundFit.Fit, LoopMedia = false, PlayMedia = true, PauseMediaWhenInactive = false, VideoMuted = false, VideoVolume = .6 };

        [JsonIgnore] public bool UsesFolder => Source == IntroMediaSource.RandomFromFolder;
        [JsonIgnore] public bool UsesFixedTime => Length == IntroLength.FixedTime;

        // The media file to play now: the chosen file, or a random supported file from the folder. Null when nothing usable exists.
        public string ResolveMedia(Random random = null) => ResolveMedia(Source,MediaPath,FolderPath,random);

        // Shared with transition media: the chosen file, or a random supported file from the folder.
        public static string ResolveMedia(IntroMediaSource source,string mediaPath,string folderPath,Random random = null)
        {
            try
            {
                if (source == IntroMediaSource.File) return !string.IsNullOrEmpty(mediaPath) && File.Exists(mediaPath) && Utils.BackgroundMediaFiles.IsSupportedFile(mediaPath) ? mediaPath : null;
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return null;
                var files = Directory.EnumerateFiles(folderPath).Where(Utils.BackgroundMediaFiles.IsSupportedFile).OrderBy(file => file,StringComparer.OrdinalIgnoreCase).ToList();
                return files.Count == 0 ? null : files[(random ?? new Random()).Next(files.Count)];
            }
            catch (Exception) { return null; }
        }

        public void ResetOptions()
        {
            Coverage = IntroCoverage.PlayniteWindow; Repeat = IntroRepeat.OncePerSession; PlayWhenStartPageOpens = false; PlayWhileLoading = true; LaunchFullScreen = false; LaunchAboveOtherApps = false; Length = IntroLength.UntilMediaEnds;
            FixedMs = 3000; MinimumMs = 1000; MaximumMs = 12000; FadeInMs = 300; Exit = IntroExit.Fade; ExitMs = 600; BackgroundColor = "#FF000000";
            SkipOnInput = true; ShowSkipHint = true; Media = DefaultMedia(); AfterEnd = MediaEndBehavior.HoldLastFrame; AfterEndRepeatMs = 1000;
        }
    }
}
