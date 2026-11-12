using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Windows.Shapes;
using LandingPage.Models;

namespace LandingPage.Controls
{
    public struct ShelfHoverState
    {
        public double Scale, Lift, Angle, Opacity;
    }
    public static class ShelfHoverMath
    {
        public static ShelfHoverState Evaluate(ShelveProperties settings, bool active, bool focused, double distanceInCards, double pointerX)
        {
            var state = new ShelfHoverState { Scale = 1, Opacity = 1 };
            if (!active) return state;
            var influence = focused ? 1d : 0;
            if (settings.HoverEffect == ShelfHoverEffect.Dock)
            {
                var proximity = Math.Max(0, 1 - Math.Abs(distanceInCards) / settings.HoverRadius);
                influence = proximity * proximity;
            }
            var amount = settings.HoverEffect == ShelfHoverEffect.Classic ? 0.05 : settings.HoverScale - 1;
            if (settings.HoverEffect != ShelfHoverEffect.None) state.Scale += amount * influence;
            if (settings.HoverEffect == ShelfHoverEffect.Dock || settings.HoverEffect == ShelfHoverEffect.Lift || settings.HoverEffect == ShelfHoverEffect.Bounce)
                state.Lift = (settings.HoverDirection == ShelfHoverDirection.Down ? 1 : -1) * settings.HoverLift * influence;
            if (settings.HoverEffect == ShelfHoverEffect.Tilt && focused) state.Angle = Math.Max(-1, Math.Min(1, pointerX)) * settings.HoverTilt;
            if (settings.HoverDimOthers) state.Opacity = settings.HoverOtherOpacity + (1 - settings.HoverOtherOpacity) * influence;
            return state;
        }
    }

    public static class ShelfHover
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.RegisterAttached("Settings", typeof(ShelveProperties), typeof(ShelfHover), new PropertyMetadata(null, Changed));
        public static void SetSettings(DependencyObject target, ShelveProperties value) => target.SetValue(SettingsProperty,value);
        public static ShelveProperties GetSettings(DependencyObject target) => (ShelveProperties)target.GetValue(SettingsProperty);
        private static readonly ConditionalWeakTable<ListBox, Controller> controllers = new ConditionalWeakTable<ListBox, Controller>();
        private static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached("Controller",typeof(Controller),typeof(ShelfHover));
        private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is ListBoxItem item)) return;
            item.Loaded -= Loaded; item.Unloaded -= Unloaded;
            if (args.NewValue == null)
            { (item.GetValue(ControllerProperty) as Controller)?.Remove(item); item.ClearValue(ControllerProperty); return; }
            item.Loaded += Loaded; item.Unloaded += Unloaded;
            if (item.IsLoaded) Attach(item);
        }
        private static void Loaded(object sender, RoutedEventArgs args) => Attach((ListBoxItem)sender);
        private static void Unloaded(object sender, RoutedEventArgs args)
        {
            var item = (ListBoxItem)sender;
            (item.GetValue(ControllerProperty) as Controller)?.Remove(item); item.ClearValue(ControllerProperty);
        }
        private static void Attach(ListBoxItem item)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(item);
            while (parent != null && !(parent is ListBox)) parent = VisualTreeHelper.GetParent(parent);
            if (!(parent is ListBox list) || !(item.DataContext is GameModel model) || string.IsNullOrWhiteSpace(model.Game?.Name)) return;
            var controller = controllers.GetValue(list,key => new Controller(key));
            item.SetValue(ControllerProperty,controller); controller.Add(item,GetSettings(item));
        }
        private sealed class Saved
        {
            public DependencyProperty Property; public object Local, Value; public BindingBase Binding;
            public static Saved Capture(DependencyObject item, DependencyProperty property) => new Saved
            { Property = property, Local = item.ReadLocalValue(property), Value = item.GetValue(property), Binding = BindingOperations.GetBindingBase(item,property) };
            public void Restore(DependencyObject item)
            {
                if (Binding != null) BindingOperations.SetBinding(item,Property,Binding);
                else if (Local == DependencyProperty.UnsetValue) item.ClearValue(Property);
                else item.SetCurrentValue(Property,Value);
            }
        }
        private sealed class Item
        {
            public ListBoxItem Element;
            public ScaleTransform Scale = new ScaleTransform();
            public RotateTransform Rotate = new RotateTransform();
            public TranslateTransform Move = new TranslateTransform();
            public TransformGroup LocalTransform;
            public Rectangle Floating;
            public Canvas Layer;
            public FrameworkElement Source;
            public DispatcherTimer ReturnTimer;
            public DropShadowEffect Glow;
            public Saved[] Original;
            public ShelfHoverState Target = new ShelfHoverState { Scale = 1, Opacity = 1 };
            public bool Focused;
            public void RemoveFloating()
            {
                ReturnTimer?.Stop();
                if (Floating == null) return;
                Layer.Children.Remove(Floating); Floating.Fill = null; Floating = null; Source = null; Layer = null;
                Element.SetCurrentValue(UIElement.RenderTransformProperty,LocalTransform);
                Element.SetCurrentValue(UIElement.OpacityProperty,(double)Original[2].Value * Target.Opacity);
            }
            public void Restore()
            {
                RemoveFloating(); Element.BeginAnimation(UIElement.OpacityProperty,null);
                foreach (var saved in Original) saved.Restore(Element);
            }
        }
        private sealed class Controller
        {
            private readonly ListBox list;
            private readonly Dictionary<ListBoxItem, Item> items = new Dictionary<ListBoxItem, Item>();
            private ShelveProperties settings;
            private bool scheduled;
            private double? queuedAt;
            private TimeSpan lastAppliedRenderTime = TimeSpan.MinValue;
            private TimeSpan lastDiagnosticRenderTime = TimeSpan.MinValue;
            // Collect in memory during motion; write one summary after leaving.
            private System.Diagnostics.Stopwatch hoverWatch;
            private double lastFrame, maxFrameGap, maxApply, maxQueueDelay;
            private int frames, slowFrames, applyCount, focusChanges, overlaysCreated, overlaysRemoved;
            private ListBoxItem previousFocus;
            private System.Threading.Tasks.Task diagnosticWrite;
            private void BeginDiagnostics()
            {
                if (hoverWatch != null || LandingPageExtension.Instance?.SettingsViewModel?.Settings?.EnableDashboardDiagnostics != true) return;
                hoverWatch = System.Diagnostics.Stopwatch.StartNew();
                lastFrame = maxFrameGap = maxApply = maxQueueDelay = 0;
                lastDiagnosticRenderTime = TimeSpan.MinValue;
                frames = slowFrames = applyCount = focusChanges = overlaysCreated = overlaysRemoved = 0;
                previousFocus = null; CompositionTarget.Rendering += DiagnoseFrame;
            }
            private void DiagnoseFrame(object sender, EventArgs args)
            {
                var rendering = args as RenderingEventArgs;
                if (rendering != null && rendering.RenderingTime == lastDiagnosticRenderTime) return;
                if (rendering != null) lastDiagnosticRenderTime = rendering.RenderingTime;
                var now = hoverWatch.Elapsed.TotalMilliseconds;
                if (lastFrame > 0) { var gap = now-lastFrame; maxFrameGap = Math.Max(maxFrameGap,gap); if (gap > 34) ++slowFrames; }
                lastFrame = now; ++frames;
            }
            private void EndDiagnostics(string reason)
            {
                if (hoverWatch == null) return;
                CompositionTarget.Rendering -= DiagnoseFrame;
                var message = $"Shelf hover performance: reason={reason}, effect={settings?.HoverEffect}, direction={settings?.HoverDirection}, overflow={settings?.HoverReserveSpace}, details={settings?.HoverShowDetails}, glow={settings?.HoverGlow}, durationMs={hoverWatch.ElapsedMilliseconds}, frames={frames}, gapsOver34ms={slowFrames}, maxFrameGapMs={maxFrameGap:F1}, updates={applyCount}, maxUpdateMs={maxApply:F2}, maxInputQueueMs={maxQueueDelay:F1}, focusChanges={focusChanges}, overlaysCreated={overlaysCreated}, overlaysRemoved={overlaysRemoved}, realizedCards={items.Count}, renderTier={RenderCapability.Tier >> 16}.";
                hoverWatch = null; previousFocus = null;
                var plugin = LandingPageExtension.Instance;
                diagnosticWrite = ExtensionDiagnostics.RecordAsync(plugin,message);
            }

            internal Func<bool> AnimationsEnabled = () => SystemParameters.ClientAreaAnimation;
            public Controller(ListBox list)
            {
                this.list = list;
                list.PreviewMouseMove += (sender,args) => Schedule();
                list.MouseLeave += (sender,args) => { CancelScheduled(); Apply(false); EndDiagnostics("mouse leave"); };
                list.IsVisibleChanged += (sender,args) => { if (!list.IsVisible) { CancelScheduled(); ClearFloating(); EndDiagnostics("hidden"); } };
                list.SizeChanged += (sender,args) => PositionFloating();
                list.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler((sender,args) => { PositionFloating(); Schedule(); }));
            }
            public void Add(ListBoxItem element, ShelveProperties value)
            {
                if (value == null) return;
                if (!ReferenceEquals(settings,value))
                {
                    if (settings != null) settings.PropertyChanged -= SettingsChanged;
                    settings = value; settings.PropertyChanged += SettingsChanged;
                }
                if (items.ContainsKey(element)) return;
                var state = new Item { Element = element, Original = new[] {
                    Saved.Capture(element,UIElement.RenderTransformProperty), Saved.Capture(element,UIElement.RenderTransformOriginProperty),
                    Saved.Capture(element,UIElement.OpacityProperty), Saved.Capture(element,UIElement.EffectProperty), Saved.Capture(element,Panel.ZIndexProperty) } };
                state.LocalTransform = new TransformGroup();
                if (element.RenderTransform != null) state.LocalTransform.Children.Add(element.RenderTransform);
                state.LocalTransform.Children.Add(state.Scale); state.LocalTransform.Children.Add(state.Rotate); state.LocalTransform.Children.Add(state.Move);
                element.SetCurrentValue(UIElement.RenderTransformProperty,state.LocalTransform);
                element.SetCurrentValue(UIElement.RenderTransformOriginProperty,new Point(0.5,settings.HoverDirection == ShelfHoverDirection.Down ? 0 : 1));
                items[element] = state; element.SizeChanged += ItemSizeChanged;
            }
            public void Remove(ListBoxItem element)
            {
                if (items.TryGetValue(element,out var state)) { element.SizeChanged -= ItemSizeChanged; state.Restore(); items.Remove(element); }
                if (items.Count == 0) { CancelScheduled(); EndDiagnostics("unloaded"); if (settings != null) settings.PropertyChanged -= SettingsChanged; settings = null; }
            }
            private void SettingsChanged(object sender, PropertyChangedEventArgs args)
            {
                if (!args.PropertyName.StartsWith("Hover",StringComparison.Ordinal) && args.PropertyName != nameof(ShelveProperties.DisplayStyle)) return;
                if (args.PropertyName == nameof(ShelveProperties.HoverReserveSpace)) ClearFloating();
                Apply(list.IsMouseOver);
            }
            private void ItemSizeChanged(object sender, SizeChangedEventArgs args) => PositionFloating();
            private void Schedule()
            {
                if (scheduled || !list.IsLoaded || !list.IsVisible) return;
                scheduled = true; queuedAt = hoverWatch?.Elapsed.TotalMilliseconds;
                CompositionTarget.Rendering += ApplyScheduled;
            }
            private void CancelScheduled()
            {
                CompositionTarget.Rendering -= ApplyScheduled;
                scheduled = false; queuedAt = null;
            }
            private void ApplyScheduled(object sender, EventArgs args)
            {
                var rendering = args as RenderingEventArgs;
                if (rendering != null && rendering.RenderingTime == lastAppliedRenderTime) return;
                if (rendering != null) lastAppliedRenderTime = rendering.RenderingTime;
                if (queuedAt.HasValue && hoverWatch != null) maxQueueDelay = Math.Max(maxQueueDelay,hoverWatch.Elapsed.TotalMilliseconds-queuedAt.Value);
                CancelScheduled(); Apply(list.IsMouseOver);
            }
            private Point Center(ListBoxItem element)
            {
                var parent = VisualTreeHelper.GetParent(element) as Visual;
                var offset = VisualTreeHelper.GetOffset(element);
                return parent == null ? new Point() : parent.TransformToAncestor(list).Transform(new Point(offset.X + element.ActualWidth / 2,offset.Y + element.ActualHeight / 2));
            }
            private static ContentPresenter FindContent(DependencyObject root)
            {
                if (root is ContentPresenter presenter) return presenter;
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); ++index)
                { var found = FindContent(VisualTreeHelper.GetChild(root,index)); if (found != null) return found; }
                return null;
            }
            private bool Float(Item state)
            {
                if (state.Floating != null) return true;
                DependencyObject parent = list;
                while (parent != null && !(parent is Views.StartPageView)) parent = VisualTreeHelper.GetParent(parent);
                var layer = (parent as Views.StartPageView)?.FindName("ShelfHoverLayer") as Canvas;
                var presenter = FindContent(state.Element);
                // Use the data-template root, not the presenter: the presenter may
                // have a narrow layout clip even when the artwork has a fixed width.
                var source = presenter != null && VisualTreeHelper.GetChildrenCount(presenter) > 0
                    ? VisualTreeHelper.GetChild(presenter,0) as FrameworkElement : null;
                if (layer == null || source == null || source.ActualWidth <= 0 || source.ActualHeight <= 0) return false;
                var transforms = new TransformGroup(); transforms.Children.Add(state.Scale); transforms.Children.Add(state.Rotate); transforms.Children.Add(state.Move);
                state.Element.BeginAnimation(UIElement.OpacityProperty,null);
                state.Element.SetCurrentValue(UIElement.RenderTransformProperty,state.Original[0].Value);
                state.Layer = layer; state.Source = source;
                state.Floating = new Rectangle { Fill = new VisualBrush(source) { AutoLayoutContent = false, Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top, ViewboxUnits = BrushMappingMode.Absolute }, RenderTransform = transforms,
                    CacheMode = new BitmapCache(1.5), IsHitTestVisible = false, Opacity = (double)state.Original[2].Value * state.Target.Opacity };
                state.Element.SetCurrentValue(UIElement.OpacityProperty,0d);
                // Fix the viewport for this hover. Descendant drawing bounds change
                // when hover buttons/glows appear and must never move the pivot.
                var bounds = new Rect(source.RenderSize);
                state.Floating.Width = bounds.Width; state.Floating.Height = bounds.Height;
                ((VisualBrush)state.Floating.Fill).Viewbox = bounds;
                layer.Children.Add(state.Floating); Position(state);
                if (hoverWatch != null) ++overlaysCreated;
                return true;
            }
            private void Position(Item state)
            {
                if (state.Floating == null || !state.Source.IsLoaded) return;
                var point = state.Source.TranslatePoint(new Point(),state.Layer);
                Canvas.SetLeft(state.Floating,point.X); Canvas.SetTop(state.Floating,point.Y);
                state.Floating.RenderTransformOrigin = new Point(0.5,settings?.HoverDirection == ShelfHoverDirection.Down ? 0 : 1);
            }
            private void PositionFloating() { foreach (var state in items.Values) Position(state); }
            private void ClearFloating() { foreach (var state in items.Values) state.RemoveFloating(); }
            private void Apply(bool active)
            {
                if (settings == null || !list.IsLoaded) return;
                ApplyAt(Mouse.GetPosition(list),active ? items.Keys.FirstOrDefault(item => item.IsMouseOver) : null);
            }
            private void ApplyAt(Point pointer, ListBoxItem focused)
            {
                if (settings == null) return;
                var active = focused != null;
                if (active) BeginDiagnostics();
                var updateStart = hoverWatch?.Elapsed.TotalMilliseconds;
                if (hoverWatch != null) { ++applyCount; if (!ReferenceEquals(focused,previousFocus)) { ++focusChanges; previousFocus = focused; } }
                foreach (var pair in items.ToArray())
                {
                    var state = pair.Value; var element = pair.Key;
                    if (!element.IsVisible) continue;
                    var center = Center(element); var dx = pointer.X - center.X; var dy = pointer.Y - center.Y;
                    var row = settings.DisplayStyle == ShelfDisplayStyle.CoverRow || settings.DisplayStyle == ShelfDisplayStyle.WideCards;
                    var distance = row ? Math.Abs(dx) / Math.Max(1,element.ActualWidth)
                        : Math.Sqrt(Math.Pow(dx / Math.Max(1,element.ActualWidth),2) + Math.Pow(dy / Math.Max(1,element.ActualHeight),2));
                    var isFocused = ReferenceEquals(element,focused);
                    var target = ShelfHoverMath.Evaluate(settings,active,isFocused,distance,dx / Math.Max(1,element.ActualWidth / 2));
                    var raised = target.Scale > 1.001 || Math.Abs(target.Lift) > 0.001 || Math.Abs(target.Angle) > 0.001 || (settings.HoverGlow && isFocused);
                    if (settings.HoverReserveSpace && raised) Float(state);
                    var visual = (UIElement)state.Floating ?? element;
                    visual.SetCurrentValue(UIElement.RenderTransformOriginProperty,new Point(0.5,settings.HoverDirection == ShelfHoverDirection.Down ? 0 : 1));
                    var duration = settings.HoverRespectWindowsAnimations && !AnimationsEnabled() ? 0 : settings.HoverEffect == ShelfHoverEffect.Classic ? 100 : settings.HoverDuration;
                    var bounce = settings.HoverEffect == ShelfHoverEffect.Bounce && isFocused && !state.Focused && duration > 0;
                    Animate(state.Scale,ScaleTransform.ScaleXProperty,state.Target.Scale,target.Scale,duration,bounce);
                    Animate(state.Scale,ScaleTransform.ScaleYProperty,state.Target.Scale,target.Scale,duration,bounce);
                    Animate(state.Move,TranslateTransform.YProperty,state.Target.Lift,target.Lift,duration,bounce);
                    Animate(state.Rotate,RotateTransform.AngleProperty,state.Target.Angle,target.Angle,duration,false);
                    var opacity = (double)state.Original[2].Value;
                    Animate(visual,UIElement.OpacityProperty,state.Target.Opacity * opacity,target.Opacity * opacity,duration);
                    visual.SetCurrentValue(Panel.ZIndexProperty,raised ? (isFocused ? 100 : (int)(target.Scale * 10)) : state.Original[4].Value);
                    if (settings.HoverGlow && isFocused)
                    {
                        if (state.Glow == null) state.Glow = new DropShadowEffect { ShadowDepth = 0, Opacity = 0.8 };
                        state.Glow.Color = (Color)ColorConverter.ConvertFromString(settings.HoverGlowColor); state.Glow.BlurRadius = settings.HoverGlowRadius;
                        visual.SetCurrentValue(UIElement.EffectProperty,state.Glow);
                    }
                    else visual.SetCurrentValue(UIElement.EffectProperty,state.Floating != null ? null : state.Original[3].Value);
                    state.Target = target; state.Focused = isFocused;
                    if (state.Floating != null)
                    {
                        if (raised) state.ReturnTimer?.Stop();
                        else if (duration == 0) { state.RemoveFloating(); if (hoverWatch != null) ++overlaysRemoved; }
                        else if (state.ReturnTimer == null || !state.ReturnTimer.IsEnabled)
                        {
                            if (state.ReturnTimer == null)
                            {
                                state.ReturnTimer = new DispatcherTimer(DispatcherPriority.Render,list.Dispatcher);
                                state.ReturnTimer.Tick += (sender,args) => { state.ReturnTimer.Stop(); if (state.Target.Scale <= 1.001 && Math.Abs(state.Target.Lift) < 0.001 && Math.Abs(state.Target.Angle) < 0.001 && !(state.Focused && settings?.HoverGlow == true)) { state.RemoveFloating(); if (hoverWatch != null) ++overlaysRemoved; } };
                            }
                            state.ReturnTimer.Interval = TimeSpan.FromMilliseconds(duration * 1.6 + 20); state.ReturnTimer.Start();
                        }
                    }
                }
                if (updateStart.HasValue && hoverWatch != null) maxApply = Math.Max(maxApply,hoverWatch.Elapsed.TotalMilliseconds-updateStart.Value);
            }
            private void Animate(Animatable target, DependencyProperty property, double previous, double value, int milliseconds, bool bounce)
            {
                if (Math.Abs(previous-value) < 0.001) return;
                if (milliseconds == 0) { target.BeginAnimation(property,null); target.SetCurrentValue(property,value); return; }
                IEasingFunction easing = settings.HoverEasing == ShelfHoverEasing.Linear ? null : settings.HoverEasing == ShelfHoverEasing.Spring
                    ? (IEasingFunction)new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 } : new QuadraticEase { EasingMode = EasingMode.EaseOut };
                if (bounce)
                {
                    var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(milliseconds * 1.6) };
                    animation.KeyFrames.Add(new EasingDoubleKeyFrame(value + (value-previous)*0.20,KeyTime.FromPercent(0.55),new QuadraticEase { EasingMode = EasingMode.EaseOut }));
                    animation.KeyFrames.Add(new EasingDoubleKeyFrame(value,KeyTime.FromPercent(1),new QuadraticEase { EasingMode = EasingMode.EaseInOut })); target.BeginAnimation(property,animation);
                }
                else target.BeginAnimation(property,new DoubleAnimation(value,TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing });
            }
            private void Animate(UIElement target, DependencyProperty property, double previous, double value, int milliseconds)
            {
                if (Math.Abs(previous-value) < 0.001) return;
                if (milliseconds == 0) { target.BeginAnimation(property,null); target.SetCurrentValue(property,value); return; }
                target.BeginAnimation(property,new DoubleAnimation(value,TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            }
        }
    }
}
