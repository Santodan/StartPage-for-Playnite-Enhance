using System;
using Newtonsoft.Json;

namespace LandingPage.Settings
{
    // How the GIF/video takes part in a dashboard page transition.
    //   Instead:          the media is the transition: the old page fades into it, it plays, then it fades into the new page.
    //   RevealedByEffect: the media sits between the pages: the chosen effect uncovers it (e.g. it appears along the wipe
    //                     line or as the old page fades), it plays, then the same effect uncovers the new page.
    //   OverEffect:       the media plays on top while the effect runs underneath (best with transparent GIFs or lower opacity).
    public enum TransitionMediaPlacement { Instead, RevealedByEffect, OverEffect }
    public enum TransitionMediaLength { UntilMediaEnds, FixedTime }

    // Optional image/GIF/video played during dashboard page transitions (LandingPageSettings.DashboardTransitionMedia).
    // Global like the other transition options, so it is not part of dashboard profiles.
    public sealed class TransitionMediaSettings : ObservableObjectExt
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
        private TransitionMediaPlacement placement = TransitionMediaPlacement.RevealedByEffect;
        public TransitionMediaPlacement Placement { get => placement; set => SetValue(ref placement,value); }
        private TransitionMediaLength length = TransitionMediaLength.UntilMediaEnds;
        public TransitionMediaLength Length { get => length; set => SetValue(ref length,value); }
        // Used by "Fixed time", and for still images and looping media.
        private int fixedMs = 1200;
        public int FixedMs { get => fixedMs; set => SetValue(ref fixedMs,Limit(value,200,10000)); }
        // Upper bound for "Until the video or GIF ends", so a long file cannot hold the page switch.
        private int maximumMs = 4000;
        public int MaximumMs { get => maximumMs; set => SetValue(ref maximumMs,Limit(value,500,15000)); }
        // Fade into and out of the media ("Instead" and "Over"; also the exit when the transition is None).
        private int fadeMs = 250;
        public int FadeMs { get => fadeMs; set => SetValue(ref fadeMs,Limit(value,0,2000)); }
        // After a non-looping GIF/video ends while the transition still shows it (e.g. a fixed time longer than the clip):
        // hold the last frame or repeat its first/last part.
        private MediaEndBehavior afterEnd = MediaEndBehavior.HoldLastFrame;
        public MediaEndBehavior AfterEnd { get => afterEnd; set => SetValue(ref afterEnd,value,nameof(AfterEnd),nameof(RepeatsAfterEnd)); }
        private int afterEndRepeatMs = 1000;
        public int AfterEndRepeatMs { get => afterEndRepeatMs; set => SetValue(ref afterEndRepeatMs,Limit(value,1,10000)); }
        [JsonIgnore] public bool RepeatsAfterEnd => AfterEnd != MediaEndBehavior.HoldLastFrame;
        private string backgroundColor = "#FF000000";
        public string BackgroundColor { get => backgroundColor; set => SetValue(ref backgroundColor,string.IsNullOrWhiteSpace(value) ? "#FF000000" : value.Trim()); }
        private bool skipOnClick = true;
        public bool SkipOnClick { get => skipOnClick; set => SetValue(ref skipOnClick,value); }
        // Framing and playback reuse the background media options (Media.Fit is the Size option: fit, fill/crop, stretch
        // or original size). Transitions default to showing the whole image without cropping or distortion, muted, no loop.
        private BackgroundAppearanceSettings media;
        public BackgroundAppearanceSettings Media { get => media ?? (media = DefaultMedia()); set => SetValue(ref media,value ?? DefaultMedia()); }

        public static BackgroundAppearanceSettings DefaultMedia() => new BackgroundAppearanceSettings
            { Fit = BackgroundFit.Fit, LoopMedia = false, PlayMedia = true, PauseMediaWhenInactive = false, VideoMuted = true, VideoVolume = .6 };

        [JsonIgnore] public bool UsesFolder => Source == IntroMediaSource.RandomFromFolder;

        // The file to play now, or null when nothing usable exists (the transition then runs without media).
        public string ResolveMedia(Random random = null) => StartupIntroSettings.ResolveMedia(Source,MediaPath,FolderPath,random);

        public void ResetOptions()
        {
            Placement = TransitionMediaPlacement.RevealedByEffect; Length = TransitionMediaLength.UntilMediaEnds;
            FixedMs = 1200; MaximumMs = 4000; FadeMs = 250; BackgroundColor = "#FF000000"; SkipOnClick = true; Media = DefaultMedia();
            AfterEnd = MediaEndBehavior.HoldLastFrame; AfterEndRepeatMs = 1000;
        }
    }
}
