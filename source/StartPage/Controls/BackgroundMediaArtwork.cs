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
                    while(!token.IsCancellationRequested && loaded.FrameCount>1) {
                        if(!playing) { await resume.WaitAsync(token).ConfigureAwait(false); last = clock.Elapsed.TotalMilliseconds; continue; }
                        var now = clock.Elapsed.TotalMilliseconds; position += (now-last)*Volatile.Read(ref speed); last = now;
                        var finished = !loop && position>=loaded.Duration;
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
                        if(finished) { await Dispatcher.InvokeAsync(() => { if(version == generation) { ended = true; UpdatePlayback(); } }); break; }
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
            else { ended = true; UpdatePlayback(); }
        }
        private void VideoFailed(object sender,ExceptionEventArgs e) { if(sender == player) Fail(e.ErrorException,"The video could not be played. Check the file and installed Windows video codecs."); }
        private void Fail(Exception error,string message) { CloseMedia(); MediaError = message; LandingPageExtension.logger?.Warn(error,"Background media playback failed."); InvalidateVisual(); }
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
