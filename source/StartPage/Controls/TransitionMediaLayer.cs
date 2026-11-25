using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LandingPage.Settings;
using LandingPage.Utils;

namespace LandingPage.Controls
{
    // The image/GIF/video shown during a dashboard page transition: an optional backdrop plus the media.
    // Prepare() loads it paused while the next page builds, Start() begins playback and the length clock,
    // WaitAsync() returns when the media ended, its time is up or it was skipped. Clear() unloads it.
    public sealed class TransitionMediaLayer : Grid
    {
        public Rectangle Backdrop { get; } = new Rectangle();
        public BackgroundMediaArtwork Artwork { get; } = new BackgroundMediaArtwork();
        public string PreparedMedia { get; private set; }
        public bool IsAnimated { get; private set; }
        public bool WasSkipped { get; private set; }
        public TimeSpan Elapsed => watch.Elapsed;
        private readonly Stopwatch watch = new Stopwatch();
        private TransitionMediaSettings options;
        private TaskCompletionSource<bool> finished, skipped;

        public TransitionMediaLayer()
        {
            ClipToBounds = true; Visibility = Visibility.Collapsed; Background = Brushes.Transparent;
            Children.Add(Backdrop); Children.Add(Artwork);
            Artwork.MediaFinished += (sender,args) => finished?.TrySetResult(true);
            MouseDown += (sender,args) => { if (options?.SkipOnClick == true && PreparedMedia != null) { Skip(); args.Handled = true; } };
        }

        // Loads the media without playing it. Returns false when there is no usable file.
        // `withBackdrop` fills the area behind letterboxed media with the chosen color (not used over the effect).
        public bool Prepare(TransitionMediaSettings settings,string media,bool withBackdrop)
        {
            Clear();
            if (settings == null || string.IsNullOrEmpty(media)) return false;
            options = settings; PreparedMedia = media; WasSkipped = false;
            finished = new TaskCompletionSource<bool>(); skipped = new TaskCompletionSource<bool>();
            Backdrop.Fill = withBackdrop ? IntroPresenter.ParseBrush(settings.BackgroundColor) : Brushes.Transparent;
            var appearance = settings.Media.Copy();
            appearance.PlayMedia = false; appearance.PauseMediaWhenInactive = false;
            Artwork.Appearance = appearance;
            // While the transition still shows the media after the clip ended (e.g. a longer fixed time): hold or repeat a part.
            Artwork.EndBehavior = settings.AfterEnd; Artwork.EndRepeatMs = settings.AfterEndRepeatMs;
            var uri = new Uri(media,UriKind.Absolute);
            IsAnimated = BackgroundMediaFiles.IsAnimated(uri);
            if (IsAnimated) Artwork.MediaUri = uri;
            else Artwork.Source = LoadImage(uri);
            IsHitTestVisible = settings.SkipOnClick;
            Opacity = 1; RenderTransform = Transform.Identity; Clip = null;
            Visibility = Visibility.Visible;
            return true;
        }

        public void Start()
        {
            if (PreparedMedia == null) return;
            watch.Restart();
            if (Artwork.Appearance != null) Artwork.Appearance.PlayMedia = true;
        }

        public void Skip() { WasSkipped = true; skipped?.TrySetResult(true); }

        // Until the media ends (bounded by the maximum) or for the fixed time; still images and looping media use the fixed time.
        // Previews set this (3): a clip that repeats a part after its end then plays to its end and repeats that part this
        // many times, whatever the length setting, so the whole clip and the repeat can be seen. 0 = normal playback.
        public int PreviewRepeats { get; set; }

        public async Task WaitAsync(Task abort)
        {
            if (options == null || PreparedMedia == null) return;
            if (PreviewRepeats > 0 && IsAnimated && !options.Media.LoopMedia && options.RepeatsAfterEnd)
            {
                // Bounded so a clip that never reports its end cannot hold the preview.
                while (!skipped.Task.IsCompleted && !(abort?.IsCompleted ?? false) && watch.ElapsedMilliseconds < 60000
                    && !(finished.Task.IsCompleted && Artwork.RepeatCount >= PreviewRepeats))
                    await (abort == null ? Task.WhenAny(Task.Delay(30),skipped.Task) : Task.WhenAny(Task.Delay(30),skipped.Task,abort));
                return;
            }
            var untilEnd = options.Length == TransitionMediaLength.UntilMediaEnds && IsAnimated && !options.Media.LoopMedia;
            var limit = untilEnd ? options.MaximumMs : options.FixedMs;
            while (!skipped.Task.IsCompleted && !(abort?.IsCompleted ?? false) && watch.ElapsedMilliseconds < limit)
            {
                if (untilEnd && finished.Task.IsCompleted) return;
                var wait = Task.Delay((int)Math.Max(1,Math.Min(50,limit - watch.ElapsedMilliseconds)));
                // A finished clip with a fixed time must not be awaited again: it would return at once and spin the UI thread.
                var ended = finished.Task.IsCompleted ? wait : finished.Task;
                await (abort == null ? Task.WhenAny(wait,skipped.Task,ended) : Task.WhenAny(wait,skipped.Task,ended,abort));
            }
        }

        public void Clear()
        {
            Visibility = Visibility.Collapsed;
            BeginAnimation(OpacityProperty,null); Opacity = 1;
            RenderTransform = Transform.Identity; Clip = null;
            Artwork.MediaUri = null; Artwork.Source = null; Artwork.Appearance = null; Artwork.EndBehavior = MediaEndBehavior.HoldLastFrame;
            PreparedMedia = null; IsAnimated = false; options = null; watch.Reset();
            skipped?.TrySetResult(true); finished = null; skipped = null;
        }

        private static ImageSource LoadImage(Uri uri)
        {
            try
            {
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = uri; image.EndInit(); image.Freeze();
                return image;
            }
            catch (Exception ex) { LandingPageExtension.logger?.Warn(ex,"Transition image could not be loaded."); return null; }
        }
    }
}
