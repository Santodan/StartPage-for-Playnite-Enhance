using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LandingPage.Settings;

namespace LandingPage.Controls
{
    // Plays the optional StartPage startup reveal. Opacity is only ever animated (never set locally), so bound
    // opacities such as the global background keep their bindings and return to their own value. Render
    // transform, its origin and clip are saved as raw local values (including bindings) and put back exactly
    // when the animation finishes, is skipped or is cancelled.
    public sealed class StartupAnimator
    {
        private sealed class Saved
        {
            public UIElement Element; public object Transform; public object Origin; public object Clip;
        }
        private readonly List<Saved> saved = new List<Saved>();
        private readonly List<Action> stops = new List<Action>();
        private DispatcherTimer finish;
        private Action completed;
        public bool IsRunning { get; private set; }
        // Total planned length (delay + stagger + duration), useful for previews and tests.
        public TimeSpan Length { get; private set; }

        // Ranks for staggered panels: rank 0 starts first. Rects are panel bounds inside `area`.
        public static int[] Ranks(IList<Rect> rects,Size area,StartupAnimationOrder order,StartupAnimationOrigin origin,bool fromOrigin,int seed)
        {
            var count = rects?.Count ?? 0; var ranks = new int[count];
            if (count == 0) return ranks;
            var originPoint = OriginPoint(origin,area); var center = new Point(area.Width/2,area.Height/2);
            Func<Rect,Point> mid = rect => new Point(rect.X + rect.Width/2,rect.Y + rect.Height/2);
            Func<Point,Point,double> distance = (a,b) => Math.Sqrt((a.X-b.X)*(a.X-b.X) + (a.Y-b.Y)*(a.Y-b.Y));
            var random = new Random(seed);
            var keys = Enumerable.Range(0,count).Select(index =>
            {
                var rect = rects[index]; var point = mid(rect);
                // Reading order groups panels into rows whose tops are within a quarter of the smaller height.
                double primary, secondary = 0;
                if (fromOrigin) primary = distance(point,originPoint);
                else switch (order)
                {
                    case StartupAnimationOrder.TopToBottom: primary = rect.Top; secondary = rect.Left; break;
                    case StartupAnimationOrder.BottomToTop: primary = -rect.Bottom; secondary = rect.Left; break;
                    case StartupAnimationOrder.LeftToRight: primary = rect.Left; secondary = rect.Top; break;
                    case StartupAnimationOrder.RightToLeft: primary = -rect.Right; secondary = rect.Top; break;
                    case StartupAnimationOrder.CenterOut: primary = distance(point,center); break;
                    case StartupAnimationOrder.EdgesIn: primary = -distance(point,center); break;
                    case StartupAnimationOrder.Random: primary = random.NextDouble(); break;
                    default: primary = Math.Round(rect.Top / Math.Max(24,Math.Min(area.Height,rect.Height) / 4)); secondary = rect.Left; break;
                }
                return new { index, primary, secondary };
            }).OrderBy(key => key.primary).ThenBy(key => key.secondary).ThenBy(key => key.index).ToList();
            for (var rank = 0; rank < keys.Count; rank++) ranks[keys[rank].index] = rank;
            return ranks;
        }

        public static Point OriginPoint(StartupAnimationOrigin origin,Size area)
        {
            var x = origin == StartupAnimationOrigin.TopLeft || origin == StartupAnimationOrigin.Left || origin == StartupAnimationOrigin.BottomLeft ? 0
                : origin == StartupAnimationOrigin.TopRight || origin == StartupAnimationOrigin.Right || origin == StartupAnimationOrigin.BottomRight ? area.Width : area.Width/2;
            var y = origin == StartupAnimationOrigin.TopLeft || origin == StartupAnimationOrigin.Top || origin == StartupAnimationOrigin.TopRight ? 0
                : origin == StartupAnimationOrigin.BottomLeft || origin == StartupAnimationOrigin.Bottom || origin == StartupAnimationOrigin.BottomRight ? area.Height : area.Height/2;
            return new Point(x,y);
        }

        public static IEasingFunction Easing(StartupAnimationEasing easing)
        {
            switch (easing)
            {
                case StartupAnimationEasing.Gentle: return Freeze(new SineEase { EasingMode = EasingMode.EaseOut });
                case StartupAnimationEasing.Snappy: return Freeze(new QuinticEase { EasingMode = EasingMode.EaseOut });
                case StartupAnimationEasing.Overshoot: return Freeze(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .45 });
                case StartupAnimationEasing.Bounce: return Freeze(new BounceEase { EasingMode = EasingMode.EaseOut, Bounces = 2, Bounciness = 3 });
                case StartupAnimationEasing.Elastic: return Freeze(new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 6 });
                default: return Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });
            }
        }
        private static IEasingFunction Freeze(EasingFunctionBase easing) { easing.Freeze(); return easing; }

        // Offset that the content starts from, for an effect that moves toward its final place in `direction`.
        public static Vector StartOffset(StartupAnimationDirection direction,double distance)
        {
            switch (direction)
            {
                case StartupAnimationDirection.Down: return new Vector(0,-distance);
                case StartupAnimationDirection.Left: return new Vector(distance,0);
                case StartupAnimationDirection.Right: return new Vector(-distance,0);
                default: return new Vector(0,distance);
            }
        }

        // Hide content (and optionally the background) until the dashboard has loaded, without a visible flash.
        public void Hide(UIElement content,UIElement background)
        {
            foreach (var element in new[] { content,background })
            {
                if (element == null) continue;
                Remember(element);
                element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,0,TimeSpan.Zero) { FillBehavior = FillBehavior.HoldEnd });
            }
        }

        // `area` is the content size; `panels` are visible leaf panels with their bounds inside the content.
        public void Play(FrameworkElement content,UIElement background,IList<KeyValuePair<FrameworkElement,Rect>> panels,StartupAnimationSettings options,Action onCompleted = null,int seed = 0)
        {
            if (IsRunning) Restore();
            IsRunning = true; completed = onCompleted;
            var area = new Size(Math.Max(1,content.ActualWidth),Math.Max(1,content.ActualHeight));
            var easing = Easing(options.Easing);
            var delay = TimeSpan.FromMilliseconds(options.DelayMs);
            var duration = TimeSpan.FromMilliseconds(options.DurationMs);
            var length = delay + duration;
            Remember(content); content.BeginAnimation(UIElement.OpacityProperty,null);
            if (background != null)
            {
                Remember(background);
                // The background settles slightly faster than the content so widgets arrive on a finished backdrop.
                var backgroundDuration = TimeSpan.FromMilliseconds(Math.Max(250,options.DurationMs * .8));
                FadeIn(background,delay,backgroundDuration);
                if (delay + backgroundDuration > length) length = delay + backgroundDuration;
            }
            switch (options.Effect)
            {
                case StartupAnimationEffect.Cascade:
                case StartupAnimationEffect.Ripple:
                    var list = panels ?? new List<KeyValuePair<FrameworkElement,Rect>>();
                    if (list.Count == 0) goto default;
                    var ranks = Ranks(list.Select(pair => pair.Value).ToList(),area,options.Order,options.Origin,options.Effect == StartupAnimationEffect.Ripple,seed);
                    for (var index = 0; index < list.Count; index++)
                    {
                        var panel = list[index].Key;
                        var begin = delay + TimeSpan.FromMilliseconds(ranks[index] * options.StaggerMs);
                        Remember(panel);
                        if (options.Effect == StartupAnimationEffect.Cascade) Move(panel,StartOffset(options.Direction,options.Distance),begin,duration,easing);
                        else Scale(panel,new Point(.5,.5),options.StartScale,begin,duration,easing);
                        FadeIn(panel,begin,TimeSpan.FromMilliseconds(Math.Max(100,options.DurationMs * .7)));
                        if (begin + duration > length) length = begin + duration;
                    }
                    break;
                case StartupAnimationEffect.Rise:
                    Move(content,StartOffset(options.Direction,options.Distance),delay,duration,easing);
                    FadeIn(content,delay,duration);
                    break;
                case StartupAnimationEffect.Zoom:
                    var origin = OriginPoint(options.Origin,area);
                    Scale(content,new Point(origin.X / area.Width,origin.Y / area.Height),options.StartScale,delay,duration,easing);
                    FadeIn(content,delay,duration);
                    break;
                case StartupAnimationEffect.Spotlight:
                    var center = OriginPoint(options.Origin,area);
                    var radius = new[] { new Point(0,0),new Point(area.Width,0),new Point(0,area.Height),new Point(area.Width,area.Height) }
                        .Max(corner => (corner - center).Length) + 2;
                    var circle = new EllipseGeometry(center,0,0); content.Clip = circle;
                    Animate(circle,EllipseGeometry.RadiusXProperty,0,radius,delay,duration,easing);
                    Animate(circle,EllipseGeometry.RadiusYProperty,0,radius,delay,duration,easing);
                    break;
                case StartupAnimationEffect.Curtain:
                    var horizontal = options.Direction == StartupAnimationDirection.Left || options.Direction == StartupAnimationDirection.Right;
                    var start = horizontal ? new Rect(area.Width/2,0,0,area.Height) : new Rect(0,area.Height/2,area.Width,0);
                    var curtain = new RectangleGeometry(start); content.Clip = curtain;
                    var opening = new RectAnimation(start,new Rect(0,0,area.Width,area.Height),duration) { BeginTime = delay, EasingFunction = easing };
                    curtain.BeginAnimation(RectangleGeometry.RectProperty,opening);
                    stops.Add(() => curtain.BeginAnimation(RectangleGeometry.RectProperty,null));
                    break;
                default:
                    FadeIn(content,delay,duration);
                    break;
            }
            Length = length;
            // One timer finishes everything at the planned end, so restoration does not depend on Completed events.
            finish = new DispatcherTimer(DispatcherPriority.Normal,content.Dispatcher) { Interval = length + TimeSpan.FromMilliseconds(30) };
            finish.Tick += (sender,args) => Finish();
            finish.Start();
        }

        // Jump to the final state immediately (input, page switch, unload).
        public void Finish()
        {
            if (!IsRunning && saved.Count == 0) return;
            Restore();
            var callback = completed; completed = null; callback?.Invoke();
        }

        private void Restore()
        {
            if (finish != null) { finish.Stop(); finish = null; }
            foreach (var stop in stops) stop(); stops.Clear();
            foreach (var item in saved)
            {
                item.Element.BeginAnimation(UIElement.OpacityProperty,null);
                Put(item.Element,UIElement.RenderTransformProperty,item.Transform);
                Put(item.Element,UIElement.RenderTransformOriginProperty,item.Origin);
                Put(item.Element,UIElement.ClipProperty,item.Clip);
            }
            saved.Clear(); IsRunning = false;
        }

        private void Remember(UIElement element)
        {
            if (element == null || saved.Any(item => ReferenceEquals(item.Element,element))) return;
            saved.Add(new Saved { Element = element, Transform = element.ReadLocalValue(UIElement.RenderTransformProperty),
                Origin = element.ReadLocalValue(UIElement.RenderTransformOriginProperty), Clip = element.ReadLocalValue(UIElement.ClipProperty) });
        }

        // Restore a raw local value: unset, a binding, or a plain value.
        private static void Put(UIElement element,DependencyProperty property,object local)
        {
            if (local == DependencyProperty.UnsetValue) element.ClearValue(property);
            else if (local is System.Windows.Data.BindingExpressionBase binding) System.Windows.Data.BindingOperations.SetBinding(element,property,binding.ParentBindingBase);
            else element.SetValue(property,local);
        }

        // Hold at 0 until `begin`, then ease to the element's own (possibly bound) opacity.
        private void FadeIn(UIElement element,TimeSpan begin,TimeSpan duration)
        {
            element.BeginAnimation(UIElement.OpacityProperty,null);
            var target = (double)element.GetValue(UIElement.OpacityProperty);
            var fade = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if (begin > TimeSpan.Zero) fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(begin)));
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(target,KeyTime.FromTimeSpan(begin + duration),Easing(StartupAnimationEasing.Gentle)));
            element.BeginAnimation(UIElement.OpacityProperty,fade);
            stops.Add(() => element.BeginAnimation(UIElement.OpacityProperty,null));
        }

        private void Animate(IAnimatable target,DependencyProperty property,double from,double to,TimeSpan begin,TimeSpan duration,IEasingFunction easing)
        {
            // Explicit start values cover the delay before the animation begins.
            if (target is DependencyObject dependency) dependency.SetValue(property,from);
            target.BeginAnimation(property,new DoubleAnimation(from,to,duration) { BeginTime = begin, EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd });
            stops.Add(() => { target.BeginAnimation(property,null); if (target is DependencyObject element && !(target is UIElement)) element.SetValue(property,to); });
        }

        private void Move(UIElement element,Vector offset,TimeSpan begin,TimeSpan duration,IEasingFunction easing)
        {
            var translate = new TranslateTransform(offset.X,offset.Y);
            element.RenderTransform = Compose(element,translate);
            Animate(translate,TranslateTransform.XProperty,offset.X,0,begin,duration,easing);
            Animate(translate,TranslateTransform.YProperty,offset.Y,0,begin,duration,easing);
        }

        private void Scale(UIElement element,Point origin,double from,TimeSpan begin,TimeSpan duration,IEasingFunction easing)
        {
            var scale = new ScaleTransform(from,from);
            element.RenderTransformOrigin = origin;
            element.RenderTransform = Compose(element,scale);
            Animate(scale,ScaleTransform.ScaleXProperty,from,1,begin,duration,easing);
            Animate(scale,ScaleTransform.ScaleYProperty,from,1,begin,duration,easing);
        }

        // Keep any transform the element already had (it is restored afterwards anyway).
        private Transform Compose(UIElement element,Transform added)
        {
            var original = element.RenderTransform;
            if (original == null || original == Transform.Identity || (original is MatrixTransform matrix && matrix.Matrix.IsIdentity)) return added;
            var group = new TransformGroup(); group.Children.Add(original); group.Children.Add(added); return group;
        }
    }
}
