using System;
using LandingPage.Models;

namespace LandingPage.Settings
{
    // Everything that describes a dashboard page transition: effect, built-in duration and direction, the custom preset
    // and the optional GIF/video. Used for the global transition (LandingPageSettings.GlobalTransition, stored in the
    // original Dashboard* settings so existing configs keep working) and for a page's own transition (DashboardPage.Transition).
    public sealed class PageTransitionSettings : ObservableObjectExt
    {
        private DashboardTransition transition = DashboardTransition.Fade;
        public DashboardTransition Transition { get => transition; set => SetValue(ref transition,value); }
        private int durationMs = 200;
        public int DurationMs { get => durationMs; set => SetValue(ref durationMs,Math.Max(50,Math.Min(1000,value))); }
        private DashboardSlideDirection direction = DashboardSlideDirection.Automatic;
        public DashboardSlideDirection Direction { get => direction; set => SetValue(ref direction,value); }
        private DashboardTransitionPreset custom;
        public DashboardTransitionPreset Custom { get => custom ?? (custom = new DashboardTransitionPreset()); set => SetValue(ref custom,value ?? new DashboardTransitionPreset()); }
        private TransitionMediaSettings media;
        public TransitionMediaSettings Media { get => media ?? (media = new TransitionMediaSettings()); set => SetValue(ref media,value ?? new TransitionMediaSettings()); }

        // Replaces every value with an independent copy of `source` (the custom preset and media are cloned, not shared).
        public void CopyFrom(PageTransitionSettings source)
        {
            if (source == null || ReferenceEquals(source,this)) return;
            Transition = source.Transition; DurationMs = source.DurationMs; Direction = source.Direction;
            Custom = DashboardProfiles.Clone(source.Custom);
            Media = DashboardProfiles.Clone(source.Media);
        }

        public PageTransitionSettings Copy() { var copy = new PageTransitionSettings(); copy.CopyFrom(this); return copy; }
    }
}
