using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LandingPage.Settings;
using LandingPage.Utils;

namespace LandingPage.Controls
{
    // Full-cover intro layer: background color, the chosen image/GIF/video and an optional skip hint.
    // PlayAsync fades in, waits for the configured length (media end, fixed time or dashboard loaded,
    // bounded by minimum/maximum), then reveals what is underneath with the exit effect.
    public sealed class IntroPresenter : Grid
    {
        public Rectangle Backdrop { get; } = new Rectangle();
        public BackgroundMediaArtwork Artwork { get; } = new BackgroundMediaArtwork();
        public TextBlock Hint { get; } = new TextBlock { Text = "Click or press any key to skip", Foreground = Brushes.White, FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0,0,24,20), Opacity = 0 };
        public bool IsPlaying { get; private set; }
        public bool IsExiting { get; private set; }
        public bool WasSkipped { get; private set; }
        public string PlayedMedia { get; private set; }
        // Polled every 50 ms in "until loaded"; also how many ms have passed (tests read it).
        public TimeSpan Elapsed => watch.Elapsed;
        private readonly Stopwatch watch = new Stopwatch();
        private TaskCompletionSource<bool> skip;
        private StartupIntroSettings options;

        public IntroPresenter()
        {
            ClipToBounds = true; Focusable = true; Background = Brushes.Transparent;
            Hint.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = .9 };
            Children.Add(Backdrop); Children.Add(Artwork); Children.Add(Hint);
            MouseDown += (sender,args) => { if (options?.SkipOnInput != false) { Skip(); args.Handled = true; } };
        }

        public void Skip() { if (IsPlaying && !IsExiting) { WasSkipped = true; skip?.TrySetResult(true); } }

        public static Brush ParseBrush(string color)
        {
            try { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
            catch (Exception) { return Brushes.Black; }
        }

        // Returns false when there is nothing to show (no usable media and the length depends on the media).
        // `holdUntil` (optional) keeps the intro up after its length until it returns true, bounded by the maximum:
        // the launch intro uses it so Playnite's window is ready before it is revealed.
        public async Task<bool> PlayAsync(StartupIntroSettings settings,Func<bool> dashboardReady,Func<bool> holdUntil = null)
        {
            options = settings ?? throw new ArgumentNullException(nameof(settings));
            var media = settings.ResolveMedia();
            if (media == null && settings.Length == IntroLength.UntilMediaEnds) return false;
            IsPlaying = true; IsExiting = false; WasSkipped = false; PlayedMedia = media;
            skip = new TaskCompletionSource<bool>();
            var finished = new TaskCompletionSource<bool>();
            EventHandler onFinished = (sender,args) => finished.TrySetResult(true);
            try
            {
                Backdrop.Fill = ParseBrush(settings.BackgroundColor);
                Artwork.Appearance = settings.Media;
                Artwork.MediaFinished += onFinished;
                var animated = false;
                if (media != null)
                {
                    var uri = new Uri(media,UriKind.Absolute);
                    animated = BackgroundMediaFiles.IsAnimated(uri);
                    if (animated) Artwork.MediaUri = uri;
                    else Artwork.Source = LoadImage(uri);
                }
                Hint.Visibility = settings.SkipOnInput && settings.ShowSkipHint ? Visibility.Visible : Visibility.Collapsed;
                watch.Restart();
                Fade(this,0,1,settings.FadeInMs);
                if (Hint.Visibility == Visibility.Visible) Fade(Hint,0,.75,400,1000);
                Focus();
                await WaitForLength(settings,animated && !settings.Media.LoopMedia,finished.Task,dashboardReady);
                var holdLimit = Math.Max(settings.MaximumMs,settings.Length == IntroLength.FixedTime ? settings.FixedMs : 0);
                while (holdUntil != null && !skip.Task.IsCompleted && !SafeCall(holdUntil) && watch.ElapsedMilliseconds < holdLimit) await Task.WhenAny(skip.Task,Task.Delay(50));
                IsExiting = true;
                await ExitAsync(settings);
                return true;
            }
            finally
            {
                Artwork.MediaFinished -= onFinished;
                Artwork.MediaUri = null; Artwork.Source = null;
                IsPlaying = false; IsExiting = false; watch.Stop();
            }
        }

        private async Task WaitForLength(StartupIntroSettings settings,bool mediaEnds,Task finished,Func<bool> dashboardReady)
        {
            var minimum = settings.MinimumMs; var maximum = Math.Max(settings.MaximumMs,minimum);
            if (settings.Length == IntroLength.FixedTime) { minimum = maximum = settings.FixedMs; }
            // Media without a natural end (still images, looping media) falls back to the fixed time.
            var untilMedia = settings.Length == IntroLength.UntilMediaEnds;
            if (untilMedia && !mediaEnds) { minimum = maximum = Math.Min(maximum,settings.FixedMs); untilMedia = false; }
            while (true)
            {
                if (skip.Task.IsCompleted) return;
                var elapsed = watch.ElapsedMilliseconds;
                if (elapsed >= maximum) return;
                if (elapsed >= minimum)
                {
                    if (untilMedia && finished.IsCompleted) return;
                    if (settings.Length == IntroLength.UntilDashboardLoaded && (dashboardReady == null || dashboardReady())) return;
                    if (settings.Length == IntroLength.FixedTime) return;
                }
                await Task.WhenAny(skip.Task,finished,Task.Delay(50));
            }
        }

        private async Task ExitAsync(StartupIntroSettings settings)
        {
            var duration = settings.Exit == IntroExit.Cut ? 0 : settings.ExitMs;
            if (duration <= 0) return;
            var size = new Size(Math.Max(1,ActualWidth),Math.Max(1,ActualHeight));
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut }; easing.Freeze();
            var span = TimeSpan.FromMilliseconds(duration);
            switch (settings.Exit)
            {
                case IntroExit.ZoomIn:
                case IntroExit.ZoomOut:
                    var scale = new ScaleTransform(1,1); RenderTransformOrigin = new Point(.5,.5); RenderTransform = scale;
                    var to = settings.Exit == IntroExit.ZoomIn ? 1.15 : .85;
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(1,to,span) { EasingFunction = easing });
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(1,to,span) { EasingFunction = easing });
                    Fade(this,1,0,duration);
                    break;
                case IntroExit.Spotlight:
                    // A growing hole reveals Playnite/StartPage from the center outward.
                    var center = new Point(size.Width/2,size.Height/2); var radius = Math.Sqrt(center.X*center.X + center.Y*center.Y) + 2;
                    var hole = new EllipseGeometry(center,0,0);
                    Clip = new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(size)),hole);
                    hole.BeginAnimation(EllipseGeometry.RadiusXProperty,new DoubleAnimation(0,radius,span) { EasingFunction = easing });
                    hole.BeginAnimation(EllipseGeometry.RadiusYProperty,new DoubleAnimation(0,radius,span) { EasingFunction = easing });
                    break;
                case IntroExit.Curtain:
                    // The intro splits from the middle and slides apart.
                    var gap = new RectangleGeometry(new Rect(size.Width/2,0,0,size.Height));
                    Clip = new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(size)),gap);
                    gap.BeginAnimation(RectangleGeometry.RectProperty,new RectAnimation(gap.Rect,new Rect(0,0,size.Width,size.Height),span) { EasingFunction = easing });
                    break;
                default:
                    Fade(this,1,0,duration);
                    break;
            }
            await Task.Delay(duration + 20);
        }

        private static bool SafeCall(Func<bool> check) { try { return check(); } catch (Exception) { return true; } }

        private static void Fade(UIElement element,double from,double to,int milliseconds,int delay = 0)
        {
            var fade = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if (delay > 0) fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(to,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + Math.Max(1,milliseconds))),new SineEase { EasingMode = EasingMode.EaseOut }));
            element.BeginAnimation(OpacityProperty,fade);
        }

        private static ImageSource LoadImage(Uri uri)
        {
            try
            {
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = uri; image.EndInit(); image.Freeze();
                return image;
            }
            catch (Exception ex) { LandingPageExtension.logger?.Warn(ex,"Startup intro image could not be loaded."); return null; }
        }
    }
}
