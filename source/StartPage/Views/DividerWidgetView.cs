using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LandingPage.Settings;
using LandingPage.Controls;

namespace LandingPage.Views
{
    public sealed class DividerWidgetView : UserControl
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings),typeof(DividerWidgetSettings),typeof(DividerWidgetView),new PropertyMetadata(null,(s,e) => ((DividerWidgetView)s).Attach()));
        public DividerWidgetSettings Settings { get => (DividerWidgetSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty,value); }
        private DividerWidgetSettings observed;
        private readonly DividerDrawing drawing = new DividerDrawing();
        internal DividerDrawing Drawing => drawing;
        internal event EventHandler GeometryChanged;
        private Controls.DividerConnectionLayer connectionLayer;
        public DividerWidgetView()
        {
            Content = drawing; Focusable = false; SnapsToDevicePixels = true; UseLayoutRounding = true;
            Settings = new DividerWidgetSettings();
            SetBinding(SettingsProperty,new Binding("DataContext.GridNode.ViewProperties.DividerSettings") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,typeof(GridNodeView),1),FallbackValue = Settings,TargetNullValue = Settings });
            Loaded += (s,e) => {
                Attach();
                DependencyObject parent = this;
                while(parent != null && !(parent is StartPageView)) parent = VisualTreeHelper.GetParent(parent);
                connectionLayer = (parent as StartPageView)?.FindName("DividerConnections") as Controls.DividerConnectionLayer;
                connectionLayer?.Register(this);
            };
            Unloaded += (s,e) => { connectionLayer?.Unregister(this); connectionLayer = null; Detach(); };
            SizeChanged += (s,e) => GeometryChanged?.Invoke(this,EventArgs.Empty);
        }
        private void Detach() { if(observed != null) observed.PropertyChanged -= Changed; observed = null; }
        private void Attach() { Detach(); drawing.Settings = Settings; if(IsLoaded && Settings != null) { observed = Settings; observed.PropertyChanged += Changed; } drawing.InvalidateMeasure(); drawing.InvalidateVisual(); GeometryChanged?.Invoke(this,EventArgs.Empty); }
        private void Changed(object sender,PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(DividerWidgetSettings.SpaceSize) || e.PropertyName == nameof(DividerWidgetSettings.Orientation) || e.PropertyName == nameof(DividerWidgetSettings.Decoration) || e.PropertyName == nameof(DividerWidgetSettings.PatternSize) || e.PropertyName == nameof(DividerWidgetSettings.LineThickness)) drawing.InvalidateMeasure();
            drawing.InvalidateVisual();
            GeometryChanged?.Invoke(this,EventArgs.Empty);
        }
    }
    public sealed class DividerDrawing : FrameworkElement
    {
        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke),typeof(Brush),typeof(DividerDrawing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
        public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty,value); }
        internal DividerWidgetSettings Settings { get; set; }
        private bool renderExternally;
        internal bool RenderExternally { get => renderExternally; set { if(renderExternally == value) return; renderExternally = value; InvalidateVisual(); } }
        public DividerDrawing() { ClipToBounds = true; IsHitTestVisible = false; SetResourceReference(StrokeProperty,"TextBrush"); }
        protected override Size MeasureOverride(Size available)
        {
            var options = Settings ?? new DividerWidgetSettings();
            var width = options.Orientation == Orientation.Vertical ? Math.Max(options.SpaceSize,DividerRenderer.Extent(options)) : 100;
            var height = options.Orientation == Orientation.Horizontal ? Math.Max(options.SpaceSize,DividerRenderer.Extent(options)) : 100;
            return new Size(Math.Min(width,available.Width),Math.Min(height,available.Height));
        }
        private static double Position(DividerPlacement placement) => placement == DividerPlacement.Start ? 0 : placement == DividerPlacement.End ? 1 : .5;
        public static Rect LineBounds(Size size,DividerWidgetSettings settings)
        {
            if(settings == null || settings.Mode == DividerMode.Spacer || size.Width <= 0 || size.Height <= 0 || double.IsInfinity(size.Width) || double.IsInfinity(size.Height)) return Rect.Empty;
            var horizontal = settings.Orientation == Orientation.Horizontal;
            var main = horizontal ? size.Width : size.Height; var cross = horizontal ? size.Height : size.Width;
            var inset = Math.Min(settings.Inset,main/2);
            var available = Math.Max(0,main-2*inset); var length = available*settings.Length/100;
            if(length <= 0) return Rect.Empty;
            var thickness = Math.Min(DividerRenderer.Extent(settings),cross);
            var offset = inset+(available-length)*Position(settings.LengthAlignment);
            var position = Math.Max(0,cross-thickness)*Position(settings.Position);
            return horizontal ? new Rect(offset,position,length,thickness) : new Rect(position,offset,thickness,length);
        }
        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context); if(RenderExternally) return; var options = Settings; var bounds = LineBounds(RenderSize,options);
            if(bounds.IsEmpty || options.LineOpacity <= 0) return;
            var brush = options.CustomColor ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(options.Color)) : Stroke ?? SystemColors.ControlTextBrush;
            var horizontal = options.Orientation == Orientation.Horizontal;
            var thickness = Math.Min(options.LineThickness,horizontal ? RenderSize.Height : RenderSize.Width);
            var size = DividerRenderer.PatternSize(horizontal ? bounds.Height : bounds.Width,thickness,options);
            context.PushOpacity(options.LineOpacity);
            DividerRenderer.Draw(context,horizontal ? new Point(bounds.Left,bounds.Top+bounds.Height/2) : new Point(bounds.Left+bounds.Width/2,bounds.Top),horizontal ? new Point(bounds.Right,bounds.Top+bounds.Height/2) : new Point(bounds.Left+bounds.Width/2,bounds.Bottom),brush,thickness,size,1,options);
            context.Pop();
        }
    }
}
