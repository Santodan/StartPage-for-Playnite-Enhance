using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LandingPage.Controls;
using LandingPage.Settings;

namespace LandingPage.Utils
{
    // Shows an IntroPresenter as an adorner over a target element: the Playnite window root (whole window,
    // sidebar and top bar included) or the StartPage view (StartPage area only). One intro plays at a time.
    public static class IntroHost
    {
        public static IntroPresenter Current { get; private set; }
        // True while an intro covers the screen; false once it starts its exit so a dashboard reveal can run beneath.
        public static bool IsPlaying => (Current != null && Current.IsPlaying && !Current.IsExiting) || LaunchIntro.IsCovering;
        // Session bookkeeping for "once per session" intros.
        public static bool GlobalPlayed { get; set; }
        public static readonly HashSet<Guid> PagesPlayed = new HashSet<Guid>();
        public static void ResetSession() { GlobalPlayed = false; PagesPlayed.Clear(); }

        public static Window MainWindow()
        {
            var app = Application.Current; if (app == null) return null;
            foreach (Window window in app.Windows) if (window.Name == "WindowMain") return window;
            return app.MainWindow;
        }

        // The element an intro covers for the chosen coverage. StartPage area needs the StartPage view.
        public static FrameworkElement Target(IntroCoverage coverage,FrameworkElement startPage)
        {
            if (coverage == IntroCoverage.StartPageArea) return startPage != null && startPage.IsLoaded ? startPage : null;
            var window = startPage != null ? Window.GetWindow(startPage) ?? MainWindow() : MainWindow();
            return window?.Content as FrameworkElement;
        }

        public static async Task<bool> PlayAsync(FrameworkElement target,StartupIntroSettings options,Func<bool> dashboardReady)
        {
            if (Current != null || target == null || options == null || !options.Enabled) return false;
            var layer = AdornerLayer.GetAdornerLayer(target);
            if (layer == null) return false;
            var presenter = new IntroPresenter();
            var adorner = new IntroAdorner(target,presenter);
            var window = Window.GetWindow(target);
            KeyEventHandler keys = (sender,args) => { if (options.SkipOnInput && presenter.IsPlaying) { presenter.Skip(); args.Handled = true; } };
            Current = presenter;
            layer.Add(adorner);
            if (window != null) window.PreviewKeyDown += keys;
            try
            {
                var played = await presenter.PlayAsync(options,dashboardReady);
                ExtensionDiagnostics.Record(LandingPageExtension.Instance,
                    $"Startup intro: played={played}; media={presenter.PlayedMedia ?? "(none)"}; coverage={options.Coverage}; length={options.Length}; elapsed={presenter.Elapsed.TotalMilliseconds:0} ms; skipped={presenter.WasSkipped}.");
                return played;
            }
            catch (Exception ex) { LandingPageExtension.logger?.Warn(ex,"Startup intro failed; continuing without it."); return false; }
            finally
            {
                if (window != null) window.PreviewKeyDown -= keys;
                layer.Remove(adorner);
                if (ReferenceEquals(Current,presenter)) Current = null;
            }
        }

        public static void SkipCurrent() => Current?.Skip();

        private sealed class IntroAdorner : Adorner
        {
            private readonly VisualCollection children;
            private readonly IntroPresenter presenter;
            public IntroAdorner(UIElement adorned,IntroPresenter presenter) : base(adorned)
            {
                this.presenter = presenter; children = new VisualCollection(this) { presenter };
                IsHitTestVisible = true;
            }
            protected override int VisualChildrenCount => children.Count;
            protected override Visual GetVisualChild(int index) => children[index];
            protected override Size MeasureOverride(Size constraint) { presenter.Measure(AdornedElement.RenderSize); return AdornedElement.RenderSize; }
            protected override Size ArrangeOverride(Size finalSize) { presenter.Arrange(new Rect(finalSize)); return finalSize; }
        }
    }
}
