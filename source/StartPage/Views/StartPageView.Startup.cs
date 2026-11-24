using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LandingPage.Settings;
using LandingPage.ViewModels.Layout;

namespace LandingPage.Views
{
    // Optional startup reveal (roadmap step 22). The dashboard content stays hidden until the current page's
    // widgets finished loading, then StartupAnimator plays the chosen effect and restores everything it touched.
    public partial class StartPageView
    {
        public Controls.StartupAnimator StartupAnimator { get; } = new Controls.StartupAnimator();
        // "First open" means once per Playnite session; the view itself is created once and cached.
        private static bool startupAnimationPlayed;
        private int startupGeneration;
        internal static void ResetStartupSession() => startupAnimationPlayed = false;
        internal static int StartupLoadTimeoutMs = 6000;

        private void HookStartupAnimation()
        {
            Loaded += (sender,args) => BeginStartupAnimation();
            Unloaded += (sender,args) => CancelStartupAnimation();
            PreviewMouseDown += (sender,args) => SkipStartupOnInput();
            PreviewKeyDown += (sender,args) => SkipStartupOnInput();
        }

        private void SkipStartupOnInput()
        {
            if (StartupAnimator.IsRunning && observedPageModel?.Settings.Settings.StartupAnimation.SkipOnInput != false) CancelStartupAnimation();
        }

        // Finish immediately: page switches, Edit Mode, unload or user input.
        internal void CancelStartupAnimation() { ++startupGeneration; StartupAnimator.Finish(); }

        private async void BeginStartupAnimation()
        {
            try { await PlayStartupAnimationAsync(false); }
            catch (Exception ex) { CancelStartupAnimation(); LandingPageExtension.logger?.Debug(ex,"Startup animation failed; showing the dashboard without it."); }
        }

        private UIElement StartupBackground(StartupAnimationSettings options)
        {
            if (!options.AnimateBackground) return null;
            if (observedPageModel?.Settings.Settings.EnableGlobalBackground == true)
            {
                var global = LandingPageExtension.Instance?.GlobalBackgroundView;
                return global != null && global.IsVisible ? global : null;
            }
            return SceneBackground;
        }

        // Returns true when an animation was started. Preview ignores the "first open only" rule.
        internal async Task<bool> PlayStartupAnimationAsync(bool preview)
        {
            var settings = observedPageModel?.Settings.Settings; var options = settings?.StartupAnimation;
            if (options == null || !options.IsEnabled || !IsLoaded) return false;
            if (!preview && options.Trigger == StartupAnimationTrigger.FirstOpen && startupAnimationPlayed) return false;
            startupAnimationPlayed = true;
            if (settings.DashboardRespectWindowsAnimations && !DashboardAnimationsEnabled()) return false;
            if (DashboardTransitionImage.Source != null) return false; // A page transition owns the scene.
            var generation = ++startupGeneration;
            StartupAnimator.Finish();
            var background = StartupBackground(options);
            StartupAnimator.Hide(StartPageScrollViewer,background);
            // Wait (bounded) until the page's widgets initialized and opened, then for a layout pass.
            var watch = Stopwatch.StartNew();
            // A startup intro covers the dashboard first; the reveal starts as the intro begins its exit.
            while (((observedPageModel == null || observedPageModel.IsLoading || !observedPageModel.IsSelectedPageInitialized) && watch.ElapsedMilliseconds < StartupLoadTimeoutMs)
                || (Utils.IntroHost.IsPlaying && watch.ElapsedMilliseconds < 65000))
            {
                await Task.Delay(40);
                if (generation != startupGeneration) return false;
            }
            await Dispatcher.Yield(DispatcherPriority.Loaded);
            if (generation != startupGeneration) return false;
            if (!IsLoaded) { StartupAnimator.Finish(); return false; }
            StartPageScrollViewer.UpdateLayout();
            var panels = StartupPanels();
            StartupAnimator.Play(StartPageScrollViewer,background,panels,options,null,Environment.TickCount);
            ExtensionDiagnostics.Record(LandingPageExtension.Instance,
                $"Startup animation: effect={options.Effect}; panels={panels.Count}; waited={watch.ElapsedMilliseconds} ms; length={StartupAnimator.Length.TotalMilliseconds:0} ms; preview={preview}.");
            return true;
        }

        // Visible leaf panels inside the dashboard viewport, with their bounds relative to the content.
        internal List<KeyValuePair<FrameworkElement,Rect>> StartupPanels()
        {
            var result = new List<KeyValuePair<FrameworkElement,Rect>>();
            var viewport = new Rect(0,0,StartPageScrollViewer.ActualWidth,StartPageScrollViewer.ActualHeight);
            var pending = new Stack<DependencyObject>(); pending.Push(StartPageScrollViewer);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node is GridNodeView view && view.DataContext is GridNodeViewModel model && model.IsLeaf)
                {
                    if (view.IsVisible && view.ActualWidth > 1 && view.ActualHeight > 1)
                    {
                        var bounds = view.TransformToAncestor(StartPageScrollViewer).TransformBounds(new Rect(view.RenderSize));
                        if (bounds.IntersectsWith(viewport)) result.Add(new KeyValuePair<FrameworkElement,Rect>(view,bounds));
                    }
                    continue;
                }
                for (var index = VisualTreeHelper.GetChildrenCount(node) - 1; index >= 0; index--) pending.Push(VisualTreeHelper.GetChild(node,index));
            }
            return result;
        }

        private async void PreviewStartupAnimation_Click(object sender,RoutedEventArgs e)
        {
            try
            {
                var options = observedPageModel?.Settings.Settings.StartupAnimation;
                if (options == null || !options.IsEnabled) { StartupAnimationStatus.Text = "Choose an effect to preview."; return; }
                var started = await PlayStartupAnimationAsync(true);
                StartupAnimationStatus.Text = started ? "Playing the startup animation on the current page." : "Animations are off: enable Windows animations or uncheck Follow Windows animation preference.";
            }
            catch (Exception ex) { CancelStartupAnimation(); StartupAnimationStatus.Text = ex.Message; }
        }

        private void ResetStartupAnimation_Click(object sender,RoutedEventArgs e) => observedPageModel?.Settings.Settings.StartupAnimation.ResetOptions();
    }
}
