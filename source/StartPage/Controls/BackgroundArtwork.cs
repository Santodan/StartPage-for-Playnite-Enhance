using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LandingPage.Settings;
namespace LandingPage.Controls
{
    public class BackgroundArtwork : FrameworkElement
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source),typeof(ImageSource),typeof(BackgroundArtwork),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
        public ImageSource Source { get => (ImageSource)GetValue(SourceProperty); set => SetValue(SourceProperty,value); }
        public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(nameof(Appearance),typeof(BackgroundAppearanceSettings),typeof(BackgroundArtwork),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender,(s,e) => ((BackgroundArtwork)s).Observe()));
        public BackgroundAppearanceSettings Appearance { get => (BackgroundAppearanceSettings)GetValue(AppearanceProperty); set => SetValue(AppearanceProperty,value); }
        private BackgroundAppearanceSettings observed;
        public BackgroundArtwork() { RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality); ClipToBounds = true; IsHitTestVisible = false; Loaded += (s,e) => Observe(); Unloaded += (s,e) => Detach(); }
        private void Detach() { if(observed != null) observed.PropertyChanged -= Changed; observed = null; }
        private void Observe() { Detach(); if(IsLoaded && Appearance != null) { observed = Appearance; observed.PropertyChanged += Changed; } InvalidateVisual(); AppearanceUpdated(null); }
        protected virtual void AppearanceUpdated(string property) { }
        private void Changed(object sender,PropertyChangedEventArgs e) { InvalidateVisual(); AppearanceUpdated(e.PropertyName); }
        public static Rect ImageBounds(Size viewport,Size image,BackgroundAppearanceSettings appearance)
        {
            if(viewport.Width<=0 || viewport.Height<=0 || image.Width<=0 || image.Height<=0 || double.IsInfinity(viewport.Width) || double.IsInfinity(viewport.Height)) return Rect.Empty;
            var options = appearance ?? new BackgroundAppearanceSettings();
            var width = image.Width; var height = image.Height;
            if(options.Fit == BackgroundFit.Stretch) { width = viewport.Width; height = viewport.Height; }
            else if(options.Fit != BackgroundFit.OriginalSize) {
                var scale = options.Fit == BackgroundFit.Fit ? Math.Min(viewport.Width/width,viewport.Height/height) : Math.Max(viewport.Width/width,viewport.Height/height);
                width *= scale; height *= scale;
            }
            width *= options.Zoom; height *= options.Zoom;
            return new Rect((viewport.Width-width)*options.PositionX,(viewport.Height-height)*options.PositionY,width,height);
        }
        public static Size NaturalImageSize(ImageSource source,BackgroundFit fit,double dpiX,double dpiY)
        {
            if(source == null) return new Size();
            if(fit == BackgroundFit.OriginalSize && source is BitmapSource bitmap)
                return new Size(bitmap.PixelWidth/Math.Max(.01,dpiX),bitmap.PixelHeight/Math.Max(.01,dpiY));
            return new Size(source.Width,source.Height);
        }
        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context); var source = Source; if(source == null) return;
            var options = Appearance ?? new BackgroundAppearanceSettings(); var dpi = VisualTreeHelper.GetDpi(this);
            var bounds = ImageBounds(RenderSize,NaturalImageSize(source,options.Fit,dpi.DpiScaleX,dpi.DpiScaleY),options);
            if(!bounds.IsEmpty && options.Fit == BackgroundFit.OriginalSize)
                bounds = new Rect(Math.Round(bounds.X*dpi.DpiScaleX)/dpi.DpiScaleX,Math.Round(bounds.Y*dpi.DpiScaleY)/dpi.DpiScaleY,bounds.Width,bounds.Height);
            if(bounds.IsEmpty || options.ImageOpacity <= 0) return;
            context.PushOpacity(options.ImageOpacity);
            context.PushTransform(new ScaleTransform(options.FlipHorizontal ? -1 : 1,options.FlipVertical ? -1 : 1,bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2));
            context.DrawImage(source,bounds); context.Pop(); context.Pop();
        }
    }
    public sealed class BackgroundEffectsLayer : BackgroundArtwork
    {
        public static readonly DependencyProperty BaseLayerProperty = DependencyProperty.Register(nameof(BaseLayer),typeof(bool),typeof(BackgroundEffectsLayer),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
        public bool BaseLayer { get => (bool)GetValue(BaseLayerProperty); set => SetValue(BaseLayerProperty,value); }
        private static Color Color(string value) => (Color)ColorConverter.ConvertFromString(value);
        private static Brush Brush(string value) => new SolidColorBrush(Color(value));
        protected override void OnRender(DrawingContext context)
        {
            var options = Appearance; if(options == null || RenderSize.Width<=0 || RenderSize.Height<=0) return;
            var bounds = new Rect(RenderSize);
            if(BaseLayer) { if(options.UseBaseColor) context.DrawRectangle(Brush(options.BaseColor),null,bounds); return; }
            if(options.TintEnabled && options.TintOpacity>0) { context.PushOpacity(options.TintOpacity); context.DrawRectangle(Brush(options.TintColor),null,bounds); context.Pop(); }
            if(options.Fade != BackgroundFade.None && options.FadeOpacity>0) {
                var color = Color(options.FadeColor); var clear = color; clear.A = 0;
                var start = options.Fade == BackgroundFade.Top ? new Point(.5,1) : options.Fade == BackgroundFade.Bottom ? new Point(.5,0) : options.Fade == BackgroundFade.Left ? new Point(1,.5) : new Point(0,.5);
                var end = new Point(1-start.X,1-start.Y);
                var gradient = new LinearGradientBrush { StartPoint = start,EndPoint = end };
                gradient.GradientStops.Add(new GradientStop(clear,0)); gradient.GradientStops.Add(new GradientStop(color,1));
                context.PushOpacity(options.FadeOpacity); context.DrawRectangle(gradient,null,bounds); context.Pop();
            }
            if(options.Vignette>0) {
                var color = Color(options.VignetteColor); var clear = color; clear.A = 0;
                var gradient = new RadialGradientBrush { Center = new Point(.5,.5),GradientOrigin = new Point(.5,.5),RadiusX = .7,RadiusY = .7 };
                gradient.GradientStops.Add(new GradientStop(clear,0)); gradient.GradientStops.Add(new GradientStop(clear,.35)); gradient.GradientStops.Add(new GradientStop(color,1));
                context.PushOpacity(options.Vignette); context.DrawRectangle(gradient,null,bounds); context.Pop();
            }
        }
    }

}
