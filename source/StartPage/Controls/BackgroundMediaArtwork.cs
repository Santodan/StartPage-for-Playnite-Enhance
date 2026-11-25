using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media.Imaging;
using LandingPage.Settings;
using LandingPage.Utils;
namespace LandingPage.Controls
{
    public sealed class BackgroundMediaArtwork : BackgroundArtwork
    {
        public static readonly DependencyProperty MediaUriProperty = DependencyProperty.Register(nameof(MediaUri),typeof(Uri),typeof(BackgroundMediaArtwork),new PropertyMetadata(null,(s,e) => { var view = (BackgroundMediaArtwork)s; if(view.IsLoaded) view.Reload(); }));
        public Uri MediaUri { get => (Uri)GetValue(MediaUriProperty); set => SetValue(MediaUriProperty,value); }
        public static readonly DependencyProperty PlaybackVisibleProperty = DependencyProperty.Register(nameof(PlaybackVisible),typeof(bool),typeof(BackgroundMediaArtwork),new PropertyMetadata(true,(s,e) => ((BackgroundMediaArtwork)s).VisibilityChanged()));
        public bool PlaybackVisible { get => (bool)GetValue(PlaybackVisibleProperty); set => SetValue(PlaybackVisibleProperty,value); }
        public static readonly DependencyProperty MediaErrorProperty = DependencyProperty.Register(nameof(MediaError),typeof(string),typeof(BackgroundMediaArtwork),new PropertyMetadata(null));
        public string MediaError { get => (string)GetValue(MediaErrorProperty); private set => SetValue(MediaErrorProperty,value); }
        // What non-looping media does after its end (intros and transitions): stop on the last frame (default, also for
        // backgrounds and the image widget), or keep repeating its first/last EndRepeatMs. MediaFinished is still raised
        // once, at the first natural end, so "until the media ends" lengths are unchanged.
        public static readonly DependencyProperty EndBehaviorProperty = DependencyProperty.Register(nameof(EndBehavior),typeof(MediaEndBehavior),typeof(BackgroundMediaArtwork),
            new PropertyMetadata(MediaEndBehavior.HoldLastFrame,(s,e) => ((BackgroundMediaArtwork)s).endBehavior = (int)(MediaEndBehavior)e.NewValue));
        public MediaEndBehavior EndBehavior { get => (MediaEndBehavior)GetValue(EndBehaviorProperty); set => SetValue(EndBehaviorProperty,value); }
        public static readonly DependencyProperty EndRepeatMsProperty = DependencyProperty.Register(nameof(EndRepeatMs),typeof(int),typeof(BackgroundMediaArtwork),
            new PropertyMetadata(1000,(s,e) => ((BackgroundMediaArtwork)s).endRepeatMs = Math.Max(1,(int)e.NewValue)));
        public int EndRepeatMs { get => (int)GetValue(EndRepeatMsProperty); set => SetValue(EndRepeatMsProperty,value); }
        private volatile int endBehavior, endRepeatMs = 1000;
        private bool finishedRaised;
        private System.Windows.Threading.DispatcherTimer segmentTimer;
        private TimeSpan segmentStart, segmentEnd;
        // True once the media is repeating its first/last part after its end (tests read it).
        public bool IsRepeatingPart { get; private set; }
        // Current GIF playback position in ms (written by the playback loop; diagnostics and tests).
        public double GifPositionMs { get; private set; }
        // How many times the repeated part has played to its end since the clip ended (previews wait for a few).
        private int repeatCount;
        public int RepeatCount => Volatile.Read(ref repeatCount);
        // Raised once when non-looping media reaches its end, or when playback fails (MediaError is then set).
        public event EventHandler MediaFinished;
        private void RaiseFinished() => MediaFinished?.Invoke(this,EventArgs.Empty);
        private CancellationTokenSource cancellation;
        private readonly SemaphoreSlim resume = new SemaphoreSlim(0,1);
        private MediaPlayer player;
        private WriteableBitmap gifFrame;
        private Size natural;
        private Window window;
        private int generation;
        private int decodeLimit = -1;
        private bool loading,ended;
        private volatile bool playing,loop = true;
        private int frameRate = 24;
        private double speed = 1;
        private Task gifTask;
        public BackgroundMediaArtwork()
        {
            Loaded += (s,e) => {
                window = Window.GetWindow(this);
                if(window != null) { window.Activated += WindowChanged; window.Deactivated += WindowChanged; window.StateChanged += WindowChanged; }
                Reload();
            };
            Unloaded += (s,e) => {
                if(window != null) { window.Activated -= WindowChanged; window.Deactivated -= WindowChanged; window.StateChanged -= WindowChanged; window = null; }
                CloseMedia(); gifFrame = null;
            };
            IsVisibleChanged += (s,e) => VisibilityChanged();
        }
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if(e.Property == OpacityProperty && IsLoaded) VisibilityChanged();
        }
        private void VisibilityChanged()
        {
            if(!IsLoaded) return;
            if(!IsVisible || !PlaybackVisible || Opacity<=0) CloseMedia();
            else if(!loading && player == null && cancellation == null && !ended && MediaError == null) Reload();
            else UpdatePlayback();
        }
        private void WindowChanged(object sender,EventArgs e) => UpdatePlayback();
        protected override void AppearanceUpdated(string property)
        {
            if(!IsLoaded) return;
            if(property == "RestartMedia" || property == nameof(BackgroundAppearanceSettings.GifMaxDimension) || (property == null && BackgroundMediaFiles.IsGif(MediaUri) && decodeLimit>=0 && Appearance?.GifMaxDimension != decodeLimit)) Reload();
            else UpdatePlayback();
        }
        private bool WantsPlayback()
        {
            var options = Appearance;
            return IsLoaded && IsVisible && PlaybackVisible && Opacity>0 && options?.ImageOpacity>0 && options.PlayMedia && !ended &&
                (window == null || window.WindowState != WindowState.Minimized) && (!options.PauseMediaWhenInactive || window == null || window.IsActive);
        }
        private void UpdatePlayback()
        {
            if(!BackgroundMediaFiles.IsAnimated(MediaUri)) return;
            var options = Appearance ?? new BackgroundAppearanceSettings();
            loop = options.LoopMedia; Volatile.Write(ref frameRate,options.GifFrameRate); Volatile.Write(ref speed,options.PlaybackSpeed);
            var next = WantsPlayback(); var changed = playing != next; playing = next;
            if(next && resume.CurrentCount == 0) { try { resume.Release(); } catch(SemaphoreFullException) { } }
            if(player != null) { player.IsMuted = options.VideoMuted; player.Volume = options.VideoVolume; player.SpeedRatio = options.PlaybackSpeed; if(next) player.Play(); else player.Pause(); }
            if(changed) LandingPageExtension.logger?.Debug("Background media "+(next ? "playing" : "paused")+".");
        }
        private void CloseMedia()
        {
            generation++; playing = false; loading = false;
            segmentTimer?.Stop(); finishedRaised = false; IsRepeatingPart = false; Volatile.Write(ref repeatCount,0);
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            if(player != null) { player.MediaOpened -= VideoOpened; player.MediaEnded -= VideoEnded; player.MediaFailed -= VideoFailed; player.Close(); player = null; }
        }
        private void Reload()
        {
            CloseMedia(); gifFrame = null; natural = new Size(); ended = false; MediaError = null; InvalidateVisual();
            var uri = MediaUri; if(!IsLoaded || !IsVisible || !PlaybackVisible || Opacity<=0 || !BackgroundMediaFiles.IsAnimated(uri)) return;
            cancellation = new CancellationTokenSource(); var token = cancellation.Token; var version = generation;
            if(BackgroundMediaFiles.IsGif(uri)) { loading = true; gifTask = LoadGif(uri,version,token); }
            else {
                player = new MediaPlayer(); player.MediaOpened += VideoOpened; player.MediaEnded += VideoEnded; player.MediaFailed += VideoFailed;
                try { player.IsMuted = true; player.Open(uri); UpdatePlayback(); }
                catch(Exception error) { Fail(error,"The video could not be opened. Check the file and installed Windows video codecs."); }
            }
        }
        private async Task LoadGif(Uri uri,int version,CancellationToken token)
        {
            BackgroundGifDecoder decoder = null;
            try {
                var watch = Stopwatch.StartNew(); var limit = Appearance?.GifMaxDimension ?? 1920; decodeLimit = limit;
                decoder = await BackgroundGifDecoder.OpenAsync(uri,limit,token);
                if(!IsLoaded || token.IsCancellationRequested || version != generation) return;
                loading = false; natural = new Size(decoder.NaturalWidth,decoder.NaturalHeight);
                gifFrame = new WriteableBitmap(decoder.Width,decoder.Height,96,96,PixelFormats.Pbgra32,null);
                gifFrame.WritePixels(new Int32Rect(0,0,decoder.Width,decoder.Height),decoder.Pixels,decoder.Width*4,0);
                LandingPageExtension.logger?.Debug("GIF background ready: "+decoder.Width+"x"+decoder.Height+"; frames="+decoder.FrameCount+"; pixelBufferBytes="+decoder.Pixels.Length+"; loadMs="+watch.ElapsedMilliseconds+".");
                InvalidateVisual(); UpdatePlayback();
                var loaded = decoder;
                await Task.Run(async () => {
                    var clock = Stopwatch.StartNew(); var last = clock.Elapsed.TotalMilliseconds; double position = 0;
                    var loggedAt = 0.0; var rendered = 0; double decodeTotal = 0,decodeMax = 0;
                    var repeating = false; double partStart = 0,partEnd = 0;
                    while(!token.IsCancellationRequested && loaded.FrameCount>1) {
                        if(!playing) { await resume.WaitAsync(token).ConfigureAwait(false); last = clock.Elapsed.TotalMilliseconds; continue; }
                        var now = clock.Elapsed.TotalMilliseconds; position += (now-last)*Volatile.Read(ref speed); last = now;
                        if(repeating && position>=partEnd) { position = partStart + (position-partEnd)%Math.Max(1,partEnd-partStart); Interlocked.Increment(ref repeatCount); }
                        var finished = !loop && !repeating && position>=loaded.Duration;
                        var behavior = endBehavior;
                        if(finished && behavior != (int)MediaEndBehavior.HoldLastFrame) {
                            // Keep playing the first or last part instead of stopping on the last frame.
                            var part = Math.Min(loaded.Duration,Math.Max(1,endRepeatMs));
                            partStart = behavior == (int)MediaEndBehavior.RepeatStart ? 0 : loaded.Duration-part; partEnd = partStart+part;
                            position = partStart + (position-loaded.Duration)%part; repeating = true; finished = false;
                            await Dispatcher.InvokeAsync(() => { if(version == generation) { IsRepeatingPart = true; if(!finishedRaised) { finishedRaised = true; RaiseFinished(); } } });
                        }
                        GifPositionMs = position;
                        var decodeStart = Stopwatch.GetTimestamp();
                        if(loaded.Render(loaded.FrameAt(position,loop))) {
                            var decodeMs = (Stopwatch.GetTimestamp()-decodeStart)*1000.0/Stopwatch.Frequency;
                            rendered++; decodeTotal += decodeMs; decodeMax = Math.Max(decodeMax,decodeMs);
                            await Dispatcher.InvokeAsync(() => {
                                if(token.IsCancellationRequested || version != generation || gifFrame == null || !playing) return;
                                gifFrame.WritePixels(new Int32Rect(0,0,loaded.Width,loaded.Height),loaded.Pixels,loaded.Width*4,0); InvalidateVisual();
                            });
                        }
                        if(clock.Elapsed.TotalMilliseconds-loggedAt>=5000 && rendered>0) {
                            LandingPageExtension.logger?.Debug("GIF playback: frames="+rendered+"; avgDecodeMs="+(decodeTotal/rendered).ToString("F2")+"; maxDecodeMs="+decodeMax.ToString("F2")+"; fpsLimit="+Volatile.Read(ref frameRate)+"; resolution="+loaded.Width+"x"+loaded.Height+".");
                            loggedAt = clock.Elapsed.TotalMilliseconds; rendered = 0; decodeTotal = decodeMax = 0;
                        }
                        if(finished) { await Dispatcher.InvokeAsync(() => { if(version == generation) { ended = true; UpdatePlayback(); RaiseFinished(); } }); break; }
                        await Task.Delay(Math.Max(16,1000/Volatile.Read(ref frameRate)),token).ConfigureAwait(false);
                    }
                },token);
            }
            catch(OperationCanceledException) { }
            catch(Exception error) { if(!token.IsCancellationRequested && version == generation) Fail(error,"The GIF could not be played. Try a smaller GIF or a video background."); }
            finally { decoder?.Dispose(); if(version == generation) loading = false; }
        }
        private void VideoOpened(object sender,EventArgs e)
        {
            if(sender != player) return;
            if(!player.HasVideo || player.NaturalVideoWidth<=0) { Fail(new InvalidOperationException("No video track"),"This file has no supported video track."); return; }
            natural = new Size(player.NaturalVideoWidth,player.NaturalVideoHeight); InvalidateVisual(); UpdatePlayback();
            LandingPageExtension.logger?.Debug("Video background ready: "+natural+".");
        }
        private void VideoEnded(object sender,EventArgs e)
        {
            if(sender != player) return;
            if(Appearance?.LoopMedia == true) { player.Position = TimeSpan.Zero; UpdatePlayback(); }
            else if(endBehavior != (int)MediaEndBehavior.HoldLastFrame && player.NaturalDuration.HasTimeSpan && player.NaturalDuration.TimeSpan > TimeSpan.Zero) {
                // Keep playing the first or last part: jump to its start; the first part also needs a stop point.
                if(IsRepeatingPart) Interlocked.Increment(ref repeatCount); // the last part reached the end again
                var duration = player.NaturalDuration.TimeSpan;
                var part = TimeSpan.FromMilliseconds(Math.Min(duration.TotalMilliseconds,Math.Max(1,endRepeatMs)));
                segmentStart = endBehavior == (int)MediaEndBehavior.RepeatStart ? TimeSpan.Zero : duration-part; segmentEnd = segmentStart+part;
                player.Position = segmentStart; IsRepeatingPart = true; UpdatePlayback();
                if(endBehavior == (int)MediaEndBehavior.RepeatStart && segmentEnd < duration) {
                    if(segmentTimer == null) {
                        segmentTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render,Dispatcher) { Interval = TimeSpan.FromMilliseconds(15) };
                        segmentTimer.Tick += (s,a) => { if(player != null && IsRepeatingPart && player.Position >= segmentEnd) { player.Position = segmentStart; Interlocked.Increment(ref repeatCount); } };
                    }
                    segmentTimer.Start();
                }
                if(!finishedRaised) { finishedRaised = true; RaiseFinished(); }
            }
            else { ended = true; UpdatePlayback(); RaiseFinished(); }
        }
        private void VideoFailed(object sender,ExceptionEventArgs e) { if(sender == player) Fail(e.ErrorException,"The video could not be played. Check the file and installed Windows video codecs."); }
        private void Fail(Exception error,string message) { CloseMedia(); MediaError = message; LandingPageExtension.logger?.Warn(error,"Background media playback failed."); InvalidateVisual(); RaiseFinished(); }
        protected override void OnRender(DrawingContext context)
        {
            if(!BackgroundMediaFiles.IsAnimated(MediaUri)) { base.OnRender(context); return; }
            var options = Appearance ?? new BackgroundAppearanceSettings();
            if(natural.Width>0 && natural.Height>0 && options.ImageOpacity>0 && (gifFrame != null || player != null)) {
                var dpi = VisualTreeHelper.GetDpi(this); var size = options.Fit == BackgroundFit.OriginalSize ? new Size(natural.Width/dpi.DpiScaleX,natural.Height/dpi.DpiScaleY) : natural;
                var bounds = ImageBounds(RenderSize,size,options); if(bounds.IsEmpty) return;
                context.PushOpacity(options.ImageOpacity);
                context.PushTransform(new ScaleTransform(options.FlipHorizontal ? -1 : 1,options.FlipVertical ? -1 : 1,bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2));
                if(gifFrame != null) context.DrawImage(gifFrame,bounds); else context.DrawVideo(player,bounds);
                context.Pop(); context.Pop();
            }
            if(!string.IsNullOrEmpty(MediaError) && RenderSize.Width>40 && RenderSize.Height>40) {
                var text = new FormattedText(MediaError,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = Math.Max(20,RenderSize.Width-32),MaxTextHeight = 60 };
                context.DrawText(text,new Point(16,Math.Max(0,RenderSize.Height-76)));
            }
        }
    }
    public sealed class BackgroundPlaybackVisibility : IMultiValueConverter
    {
        public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture) => values.Length>1 && values[0] is bool visible && visible && values[1] is double opacity && opacity>0;
        public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture) => throw new NotSupportedException();
    }

}
