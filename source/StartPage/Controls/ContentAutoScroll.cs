using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LandingPage.Controls
{
    public static class ContentAutoScroll
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(ContentAutoScroll),new PropertyMetadata(false,Changed));
        public static readonly DependencyProperty SpeedProperty = DependencyProperty.RegisterAttached("Speed",typeof(double),typeof(ContentAutoScroll),new PropertyMetadata(20d));
        public static readonly DependencyProperty PauseOnHoverProperty = DependencyProperty.RegisterAttached("PauseOnHover",typeof(bool),typeof(ContentAutoScroll),new PropertyMetadata(true));
        public static readonly DependencyProperty PausedProperty = DependencyProperty.RegisterAttached("Paused",typeof(bool),typeof(ContentAutoScroll),new PropertyMetadata(false,Changed));
        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State",typeof(State),typeof(ContentAutoScroll));
        public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty,value);
        public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
        public static void SetSpeed(DependencyObject target, double value) => target.SetValue(SpeedProperty,value);
        public static double GetSpeed(DependencyObject target) => (double)target.GetValue(SpeedProperty);
        public static void SetPauseOnHover(DependencyObject target, bool value) => target.SetValue(PauseOnHoverProperty,value);
        public static bool GetPauseOnHover(DependencyObject target) => (bool)target.GetValue(PauseOnHoverProperty);
        public static void SetPaused(DependencyObject target, bool value) => target.SetValue(PausedProperty,value);
        public static bool GetPaused(DependencyObject target) => (bool)target.GetValue(PausedProperty);
        private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (!(target is ScrollViewer view)) return;
            var state = view.GetValue(StateProperty) as State;
            if (state == null) { state = new State(view); view.SetValue(StateProperty,state); }
            state.Update();
        }
        private sealed class State
        {
            private readonly ScrollViewer view;
            private readonly DispatcherTimer timer;
            private readonly Stopwatch clock = new Stopwatch();
            private Window owner;
            private Application application;
            private bool applicationActive = true;
            private double wait = 2, position;
            private string lastStatus;
            public State(ScrollViewer view)
            {
                this.view = view;
                timer = new DispatcherTimer(DispatcherPriority.Background,view.Dispatcher) { Interval = TimeSpan.FromMilliseconds(50) };
                timer.Tick += (sender,args) => { var seconds = clock.Elapsed.TotalSeconds; clock.Restart(); Advance(Math.Min(0.2,seconds)); };
                view.Loaded += (sender,args) => { AttachOwner(); Update(); };
                view.Unloaded += (sender,args) => { timer.Stop(); clock.Stop(); if (application != null) { application.Activated -= Activated; application.Deactivated -= Deactivated; application = null; } owner = null; };
                view.IsVisibleChanged += (sender,args) => Update();
                view.PreviewMouseWheel += (sender,args) => wait = 2;
                view.ScrollChanged += (sender,args) => Update();
                if (view.IsLoaded) AttachOwner();
            }
            private void AttachOwner()
            {
                if (application != null) { application.Activated -= Activated; application.Deactivated -= Deactivated; }
                owner = Window.GetWindow(view);
                application = Application.Current;
                applicationActive = application == null || application.Windows.Cast<Window>().Any(window => window.IsActive);
                if (application != null) { application.Activated += Activated; application.Deactivated += Deactivated; }
            }
            private void Activated(object sender, EventArgs args) { applicationActive = true; Update(); }
            private void Deactivated(object sender, EventArgs args) { applicationActive = false; Update(); }
            private void Report(string status)
            {
                if (lastStatus == status) return; lastStatus = status;
                ExtensionDiagnostics.RecordAsync(LandingPageExtension.Instance,$"Content auto-scroll: {status}, viewport={view.ViewportHeight:F1}, scrollable={view.ScrollableHeight:F1}, offset={view.VerticalOffset:F1}, speed={GetSpeed(view):F1}, pauseOnHover={GetPauseOnHover(view)}.");
            }
            public void Update()
            {
                var status = !GetEnabled(view) ? "disabled" : GetPaused(view) ? "paused for section editing"
                    : !view.IsLoaded || !view.IsVisible ? "hidden" : view.ScrollableHeight <= 0 ? "no overflow"
                    : !applicationActive ? "inactive application" : "running";
                if (status == "running")
                {
                    if (!timer.IsEnabled) { wait = 2; position = view.VerticalOffset; clock.Restart(); timer.Start(); }
                }
                else { timer.Stop(); clock.Stop(); }
                Report(status);
            }
            private void Advance(double seconds)
            {
                if (!GetEnabled(view) || GetPaused(view) || !view.IsVisible || view.ScrollableHeight <= 0) { wait = 2; position = view.VerticalOffset; return; }
                if ((GetPauseOnHover(view) && view.IsMouseOver) || view.IsMouseCaptureWithin) { wait = 2; position = view.VerticalOffset; Report("paused for pointer interaction"); return; }
                Report("running");
                if (wait > 0) { wait -= seconds; position = view.VerticalOffset; return; }
                if (view.VerticalOffset >= view.ScrollableHeight-0.5) { view.ScrollToTop(); position = 0; wait = 2; return; }
                var speed = GetSpeed(view); if (double.IsNaN(speed) || double.IsInfinity(speed)) speed = 20;
                // Accumulate sub-pixel progress instead of losing it to layout rounding.
                position = Math.Min(view.ScrollableHeight,Math.Max(position,view.VerticalOffset)+Math.Max(1,Math.Min(120,speed))*seconds);
                var target = position;
                view.ScrollToVerticalOffset(target);
                if (target >= view.ScrollableHeight-0.5) wait = 2;
            }
        }
    }
}
