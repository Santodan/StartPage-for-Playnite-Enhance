using System;
using Newtonsoft.Json;

namespace LandingPage.Settings
{
    // Whole-dashboard effects (Fade, Rise, Zoom, Spotlight, Curtain) animate the dashboard content as one piece;
    // panel effects (Cascade, Ripple) reveal each visible widget panel in turn.
    public enum StartupAnimationEffect { None, Fade, Rise, Zoom, Cascade, Ripple, Spotlight, Curtain }
    public enum StartupAnimationTrigger { FirstOpen, EveryOpen }
    public enum StartupAnimationOrder { Reading, TopToBottom, BottomToTop, LeftToRight, RightToLeft, CenterOut, EdgesIn, Random }
    public enum StartupAnimationEasing { Smooth, Gentle, Snappy, Overshoot, Bounce, Elastic }
    public enum StartupAnimationOrigin { Center, TopLeft, Top, TopRight, Left, Right, BottomLeft, Bottom, BottomRight }
    public enum StartupAnimationDirection { Up, Down, Left, Right }

    public sealed class StartupAnimationSettings : ObservableObjectExt
    {
        private static double Limit(double value,double fallback,double min,double max) => double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(min,Math.Min(max,value));
        private static int Limit(int value,int min,int max) => Math.Max(min,Math.Min(max,value));

        private StartupAnimationEffect effect = StartupAnimationEffect.None;
        public StartupAnimationEffect Effect { get => effect; set => SetValue(ref effect,value,nameof(Effect),nameof(IsEnabled),nameof(IsPanelEffect),nameof(UsesDirection),nameof(UsesOrigin),nameof(UsesDistance),nameof(UsesScale),nameof(UsesOrder)); }
        private StartupAnimationTrigger trigger = StartupAnimationTrigger.FirstOpen;
        public StartupAnimationTrigger Trigger { get => trigger; set => SetValue(ref trigger,value); }
        private int durationMs = 650;
        public int DurationMs { get => durationMs; set => SetValue(ref durationMs,Limit(value,100,3000)); }
        private int delayMs = 0;
        public int DelayMs { get => delayMs; set => SetValue(ref delayMs,Limit(value,0,3000)); }
        private int staggerMs = 70;
        public int StaggerMs { get => staggerMs; set => SetValue(ref staggerMs,Limit(value,0,600)); }
        private StartupAnimationOrder order = StartupAnimationOrder.Reading;
        public StartupAnimationOrder Order { get => order; set => SetValue(ref order,value); }
        private StartupAnimationEasing easing = StartupAnimationEasing.Smooth;
        public StartupAnimationEasing Easing { get => easing; set => SetValue(ref easing,value); }
        private StartupAnimationDirection direction = StartupAnimationDirection.Up;
        public StartupAnimationDirection Direction { get => direction; set => SetValue(ref direction,value); }
        private StartupAnimationOrigin origin = StartupAnimationOrigin.Center;
        public StartupAnimationOrigin Origin { get => origin; set => SetValue(ref origin,value); }
        private double distance = 48;
        public double Distance { get => distance; set => SetValue(ref distance,Limit(value,48,0,400)); }
        private double startScale = 0.88;
        public double StartScale { get => startScale; set => SetValue(ref startScale,Limit(value,.88,.3,1.3)); }
        private bool animateBackground = true;
        public bool AnimateBackground { get => animateBackground; set => SetValue(ref animateBackground,value); }
        private bool skipOnInput = true;
        public bool SkipOnInput { get => skipOnInput; set => SetValue(ref skipOnInput,value); }

        // Editor helpers: which options apply to the selected effect. Runtime only, never saved.
        [JsonIgnore] public bool IsEnabled => Effect != StartupAnimationEffect.None;
        [JsonIgnore] public bool IsPanelEffect => Effect == StartupAnimationEffect.Cascade || Effect == StartupAnimationEffect.Ripple;
        [JsonIgnore] public bool UsesOrder => Effect == StartupAnimationEffect.Cascade;
        [JsonIgnore] public bool UsesDirection => Effect == StartupAnimationEffect.Rise || Effect == StartupAnimationEffect.Cascade || Effect == StartupAnimationEffect.Curtain;
        [JsonIgnore] public bool UsesOrigin => Effect == StartupAnimationEffect.Zoom || Effect == StartupAnimationEffect.Ripple || Effect == StartupAnimationEffect.Spotlight;
        [JsonIgnore] public bool UsesDistance => Effect == StartupAnimationEffect.Rise || Effect == StartupAnimationEffect.Cascade;
        [JsonIgnore] public bool UsesScale => Effect == StartupAnimationEffect.Zoom || Effect == StartupAnimationEffect.Ripple;

        // Restores every option except the chosen effect.
        public void ResetOptions()
        {
            Trigger = StartupAnimationTrigger.FirstOpen; DurationMs = 650; DelayMs = 0; StaggerMs = 70;
            Order = StartupAnimationOrder.Reading; Easing = StartupAnimationEasing.Smooth; Direction = StartupAnimationDirection.Up;
            Origin = StartupAnimationOrigin.Center; Distance = 48; StartScale = .88; AnimateBackground = true; SkipOnInput = true;
        }
    }
}
