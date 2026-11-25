using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LandingPage.Models;
using LandingPage.Settings;

namespace LandingPage.Controls
{
    // Plays one page transition between two elements that cover the same area: `outgoing` (removed; the dashboard uses
    // a snapshot of the old page) and `incoming` (revealed; the new page), plus the optional GIF/video in `media`.
    // Used by the dashboard (StartPageView) and by the preview box in the transition editor, so both play the same thing.
    // The media layer is loaded paused by Configure and played in one of three ways around the effect:
    //   Instead:          outgoing -> fade into media -> media plays -> fade into the incoming page (the effect is not used).
    //   RevealedByEffect: the effect removes `outgoing` and brings in the media, the media plays, then the same effect
    //                     removes the media and brings in `incoming` (e.g. the media arrives along the wipe line).
    //   OverEffect:       the media fades in on top while the effect runs underneath, then fades out.
    public sealed class TransitionPlayer
    {
        public const int OutgoingZ = 900, MediaBelowOutgoingZ = 899, MediaAboveOutgoingZ = 905;
        private readonly FrameworkElement outgoing, incoming;
        private readonly TransitionMediaLayer media;
        private DashboardTransitionPreset activePreset;
        private DashboardSlideDirection activeDirection;
        private int milliseconds;
        private bool forward;
        private TransitionMediaSettings activeMedia;

        public TransitionPlayer(FrameworkElement outgoing, FrameworkElement incoming, TransitionMediaLayer media)
        {
            this.outgoing = outgoing ?? throw new ArgumentNullException(nameof(outgoing));
            this.incoming = incoming ?? throw new ArgumentNullException(nameof(incoming));
            this.media = media ?? throw new ArgumentNullException(nameof(media));
        }

        public DashboardTransition ActiveTransition { get; private set; }
        public TransitionMediaPlacement ActiveMediaPlacement { get; private set; }
        public bool HasMedia => activeMedia != null;
        public TransitionMediaLayer MediaLayer => media;

        // Takes a copy of the transition options and, when `mediaFile` is given, loads the media paused.
        public void Configure(PageTransitionSettings transition, bool forward, string mediaFile)
        {
            ActiveTransition = transition.Transition;
            activePreset = ActiveTransition == DashboardTransition.Custom ? TransitionPresets.Clone(transition.Custom) : null;
            activeDirection = transition.Direction;
            milliseconds = transition.DurationMs;
            this.forward = forward;
            PrepareMedia(transition.Media, mediaFile);
        }

        private void PrepareMedia(TransitionMediaSettings options, string file)
        {
            activeMedia = null;
            if (options == null || string.IsNullOrEmpty(file)) return;
            var copy = DashboardProfiles.Clone(options);
            // Without an effect there is nothing to reveal the media with or to play over: play it directly.
            var placement = ActiveTransition == DashboardTransition.None ? TransitionMediaPlacement.Instead : copy.Placement;
            if (!media.Prepare(copy, file, placement != TransitionMediaPlacement.OverEffect)) return;
            Panel.SetZIndex(media, placement == TransitionMediaPlacement.RevealedByEffect ? MediaBelowOutgoingZ : MediaAboveOutgoingZ);
            if (placement != TransitionMediaPlacement.RevealedByEffect) media.Opacity = 0;
            activeMedia = copy; ActiveMediaPlacement = placement;
        }

        // Plays the configured transition. Returns when it has finished, or early once `aborted` completes;
        // `stale` stops it between phases (a newer transition took over).
        public async Task PlayAsync(Task aborted, Func<bool> stale)
        {
            if (activeMedia == null) { await Task.WhenAny(RunEffect(outgoing, incoming), aborted); return; }
            Func<bool> stop = () => aborted.IsCompleted || (stale?.Invoke() ?? false);
            var options = activeMedia; var watch = System.Diagnostics.Stopwatch.StartNew();
            media.Start();
            switch (ActiveMediaPlacement)
            {
                case TransitionMediaPlacement.Instead:
                    await Task.WhenAny(FadeTo(media, 0, 1, options.FadeMs), aborted); if (stop()) return;
                    outgoing.Visibility = Visibility.Collapsed;
                    await media.WaitAsync(aborted); if (stop()) return;
                    await Task.WhenAny(FadeTo(media, 1, 0, options.FadeMs), aborted);
                    break;
                case TransitionMediaPlacement.RevealedByEffect:
                    // The new page stays hidden behind the media until the second half.
                    incoming.Opacity = 0;
                    await Task.WhenAny(RunEffect(outgoing, media), aborted); if (stop()) return;
                    outgoing.Visibility = Visibility.Collapsed;
                    await media.WaitAsync(aborted); if (stop()) return;
                    media.BeginAnimation(UIElement.OpacityProperty, null); media.Opacity = 1; media.RenderTransform = Transform.Identity; media.Clip = null;
                    incoming.Opacity = 1;
                    await Task.WhenAny(RunEffect(media, incoming), aborted);
                    break;
                default:
                    FadeTo(media, 0, 1, options.FadeMs);
                    var length = media.WaitAsync(aborted);
                    await Task.WhenAny(Task.WhenAll(RunEffect(outgoing, incoming), length), aborted); if (stop()) return;
                    outgoing.Visibility = Visibility.Collapsed;
                    await Task.WhenAny(FadeTo(media, 1, 0, options.FadeMs), aborted);
                    break;
            }
            ExtensionDiagnostics.Record(LandingPageExtension.Instance,
                $"Transition media: placement={ActiveMediaPlacement}; effect={ActiveTransition}; media={System.IO.Path.GetFileName(media.PreparedMedia)}; skipped={media.WasSkipped}; {watch.ElapsedMilliseconds} ms.");
        }

        // Restores opacity, transforms and clips of both elements and unloads the media. Visibility is left to the owner.
        public void Reset()
        {
            incoming.BeginAnimation(UIElement.OpacityProperty, null); incoming.Opacity = 1; incoming.RenderTransform = Transform.Identity;
            outgoing.Clip = null; outgoing.BeginAnimation(UIElement.OpacityProperty, null); outgoing.RenderTransform = Transform.Identity; outgoing.Opacity = 1;
            media.Clear(); activeMedia = null;
        }

        private double Width => incoming.ActualWidth;
        private double Height => incoming.ActualHeight;

        // Runs the effect once from `from` (removed) to `to` (revealed). Completes when the effect ends;
        // final values are held until Reset or TransitionMediaLayer.Clear restores the elements.
        private Task RunEffect(FrameworkElement from, FrameworkElement to)
        {
            var done = new TaskCompletionSource<bool>();
            EventHandler finish = (sender, args) => done.TrySetResult(true);
            var duration = TimeSpan.FromMilliseconds(milliseconds);
            if (ActiveTransition == DashboardTransition.None) done.TrySetResult(true);
            else if (ActiveTransition == DashboardTransition.Wipe)
            {
                var direction = ResolveDirection();
                var end = direction == DashboardSlideDirection.Left ? new Rect(0,0,0,Height)
                    : direction == DashboardSlideDirection.Right ? new Rect(Width,0,0,Height)
                    : direction == DashboardSlideDirection.Up ? new Rect(0,0,Width,0) : new Rect(0,Height,Width,0);
                var clip = new RectangleGeometry(new Rect(0,0,Width,Height));
                from.Clip = clip;
                var wipe = new RectAnimation(clip.Rect, end, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
                wipe.Completed += finish;
                clip.BeginAnimation(RectangleGeometry.RectProperty, wipe);
            }
            else if (ActiveTransition == DashboardTransition.Custom || ActiveTransition == DashboardTransition.SlideFade
                || ActiveTransition == DashboardTransition.ZoomIn || ActiveTransition == DashboardTransition.ZoomOut)
            {
                AnimateMotionPreset(activePreset ?? BuiltInMotionPreset(), from, to, finish);
            }
            else if (ActiveTransition == DashboardTransition.Fade)
            {
                var fade = new DoubleAnimation(1, 0, duration) { FillBehavior = FillBehavior.HoldEnd };
                fade.Completed += finish;
                from.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            else
            {
                var direction = ResolveDirection();
                var vertical = direction == DashboardSlideDirection.Up || direction == DashboardSlideDirection.Down;
                var offset = (vertical ? Height : Width) * (direction == DashboardSlideDirection.Right || direction == DashboardSlideDirection.Down ? 1 : -1);
                var leaving = new TranslateTransform(); var entering = new TranslateTransform();
                from.RenderTransform = leaving; to.RenderTransform = entering;
                var property = vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty;
                var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
                var leave = new DoubleAnimation(0, offset, duration) { EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };
                var enter = new DoubleAnimation(-offset, 0, duration) { EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };
                enter.Completed += finish;
                leaving.BeginAnimation(property, leave); entering.BeginAnimation(property, enter);
            }
            return done.Task;
        }

        private DashboardSlideDirection ResolveDirection() => activeDirection == DashboardSlideDirection.Automatic
            ? (forward ? DashboardSlideDirection.Left : DashboardSlideDirection.Right) : activeDirection;

        private DashboardTransitionPreset BuiltInMotionPreset()
        {
            var preset = new DashboardTransitionPreset { DurationMs = milliseconds, ReverseOnPrevious = false,
                Incoming = new TransitionMotion(), Outgoing = new TransitionMotion { EndOpacity = 0 } };
            if (ActiveTransition == DashboardTransition.ZoomIn) preset.Incoming.StartScale = 0.90;
            else if (ActiveTransition == DashboardTransition.ZoomOut) preset.Outgoing.EndScale = 1.10;
            else
            {
                var direction = ResolveDirection();
                var offset = direction == DashboardSlideDirection.Left || direction == DashboardSlideDirection.Up ? -15 : 15;
                if (direction == DashboardSlideDirection.Up || direction == DashboardSlideDirection.Down)
                { preset.Outgoing.EndY = offset; preset.Incoming.StartY = -offset; }
                else { preset.Outgoing.EndX = offset; preset.Incoming.StartX = -offset; }
                preset.Incoming.StartOpacity = 0;
            }
            return preset;
        }

        private void AnimateMotionPreset(DashboardTransitionPreset preset, FrameworkElement from, FrameworkElement to, EventHandler finish)
        {
            TransitionPresets.Validate(preset);
            var sign = preset.ReverseOnPrevious && !forward ? -1 : 1;
            IEasingFunction easing = null;
            if (preset.Easing == TransitionEasing.Quadratic) easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
            if (preset.Easing == TransitionEasing.Cubic) easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
            if (preset.Easing == TransitionEasing.Sine) easing = new SineEase { EasingMode = EasingMode.EaseInOut };
            Action<FrameworkElement, TransitionMotion, bool> animate = (element, motion, completes) =>
            {
                var scale = new ScaleTransform(); var translation = new TranslateTransform();
                var transforms = new TransformGroup(); transforms.Children.Add(scale); transforms.Children.Add(translation);
                element.RenderTransformOrigin = new Point(0.5,0.5); element.RenderTransform = transforms;
                Func<double,double,DoubleAnimation> make = (start,end) => new DoubleAnimation(start,end,TimeSpan.FromMilliseconds(preset.DurationMs))
                    { BeginTime = TimeSpan.FromMilliseconds(motion.DelayMs), EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };
                var opacity = make(motion.StartOpacity,motion.EndOpacity);
                // Explicit start values also cover the delay before an animation starts.
                element.Opacity = motion.StartOpacity;
                scale.ScaleX = scale.ScaleY = motion.StartScale;
                translation.X = sign * motion.StartX * Width / 100;
                translation.Y = sign * motion.StartY * Height / 100;
                if (completes) opacity.Completed += finish;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,make(motion.StartScale,motion.EndScale));
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,make(motion.StartScale,motion.EndScale));
                translation.BeginAnimation(TranslateTransform.XProperty,make(translation.X,sign * motion.EndX * Width / 100));
                translation.BeginAnimation(TranslateTransform.YProperty,make(translation.Y,sign * motion.EndY * Height / 100));
                element.BeginAnimation(UIElement.OpacityProperty,opacity);
            };
            var outgoingLast = preset.Outgoing.DelayMs > preset.Incoming.DelayMs;
            animate(from,preset.Outgoing,outgoingLast);
            animate(to,preset.Incoming,!outgoingLast);
        }

        public static Task FadeTo(UIElement element, double from, double to, int milliseconds)
        {
            var done = new TaskCompletionSource<bool>();
            if (milliseconds <= 0) { element.BeginAnimation(UIElement.OpacityProperty, null); element.Opacity = to; done.TrySetResult(true); return done.Task; }
            var fade = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
                { FillBehavior = FillBehavior.HoldEnd, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            fade.Completed += (sender, args) => done.TrySetResult(true);
            element.BeginAnimation(UIElement.OpacityProperty, fade);
            return done.Task;
        }
    }
}
