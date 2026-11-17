using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LandingPage.Settings;
using LandingPage.Views;

namespace LandingPage.Controls
{
    // Draw bridges in dashboard coordinates, beyond individual panel clipping, without taking input or layout space.
    public sealed class DividerConnectionLayer : FrameworkElement
    {
        private sealed class Line
        {
            public DividerWidgetView View;
            public Point Start,End;
            public DividerWidgetSettings Settings;
            public Brush Brush;
            public double Thickness,Opacity,PatternSize,Scale;
        }
        private sealed class Bridge { public Line Line,Target; public Point Start,End; public bool Parallel; }
        private sealed class Outline { public Point Start,End; public PathGeometry Geometry; }
        private readonly Dictionary<Line,Outline> outlines = new Dictionary<Line,Outline>();
        private readonly HashSet<DividerWidgetView> widgets = new HashSet<DividerWidgetView>();
        private List<Bridge> bridges = new List<Bridge>();
        private List<Line> connectedLines = new List<Line>();
        private string signature;
        private bool queued;
        public DividerConnectionLayer()
        {
            IsHitTestVisible = false; Focusable = false; ClipToBounds = true;
            Loaded += (s,e) => { LayoutUpdated += LayoutChanged; Queue(); };
            Unloaded += (s,e) => { LayoutUpdated -= LayoutChanged; bridges.Clear(); outlines.Clear(); connectedLines.Clear(); foreach(var view in widgets) view.Drawing.RenderExternally = false; signature = null; InvalidateVisual(); };
        }
        internal event EventHandler ConnectionsChanged;
        internal DividerWidgetSettings AppearanceTarget(DividerWidgetSettings source,int endpoint)
        {
            if(IsLoaded) Refresh();
            return bridges.Where(bridge => ReferenceEquals(bridge.Line.Settings,source) &&
                (endpoint == 0 || ((bridge.Start-bridge.Line.Start).Length<.001 ? endpoint == 1 : endpoint == 2)))
                .OrderBy(bridge => (bridge.End-bridge.Start).Length).Select(bridge => bridge.Target.Settings).FirstOrDefault();
        }
        internal void Register(DividerWidgetView view) { if(widgets.Add(view)) view.GeometryChanged += Changed; Queue(); }
        internal void Unregister(DividerWidgetView view) { view.Drawing.RenderExternally = false; if(widgets.Remove(view)) view.GeometryChanged -= Changed; Queue(); }
        private void Changed(object sender,EventArgs e) => Queue();
        private void LayoutChanged(object sender,EventArgs e)
        {
            if(widgets.Any(view => view.Settings?.ConnectEnds == true)) Refresh();
        }
        private void Queue()
        {
            if(queued) return; queued = true;
            Dispatcher.BeginInvoke(new Action(() => { queued = false; if(IsLoaded) Refresh(); }),DispatcherPriority.Render);
        }
        private void Refresh()
        {
            var lines = new List<Line>(); var key = new StringBuilder();
            foreach(var view in widgets) {
                var options = view.Settings;
                if(!view.IsLoaded || !view.IsVisible || options == null || options.Mode == DividerMode.Spacer || options.LineOpacity <= 0) continue;
                var drawing = view.Drawing; var bounds = DividerDrawing.LineBounds(drawing.RenderSize,options); if(bounds.IsEmpty) continue;
                var horizontal = options.Orientation == Orientation.Horizontal;
                try {
                    var start = drawing.TranslatePoint(horizontal ? new Point(bounds.Left,bounds.Top+bounds.Height/2) : new Point(bounds.Left+bounds.Width/2,bounds.Top),this);
                    var end = drawing.TranslatePoint(horizontal ? new Point(bounds.Right,bounds.Top+bounds.Height/2) : new Point(bounds.Left+bounds.Width/2,bounds.Bottom),this);
                    var brush = options.CustomColor ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(options.Color)) : drawing.Stroke ?? SystemColors.ControlTextBrush;
                    var localThickness = Math.Min(options.LineThickness,horizontal ? drawing.RenderSize.Height : drawing.RenderSize.Width);
                    var perpendicular = drawing.TranslatePoint(horizontal ? new Point(bounds.Left,bounds.Top+bounds.Height/2+localThickness) : new Point(bounds.Left+bounds.Width/2+localThickness,bounds.Top),this);
                    var opacity = options.LineOpacity; var common = VisualTreeHelper.GetParent(this);
                    for(DependencyObject ancestor = drawing; ancestor != null && ancestor != common; ancestor = VisualTreeHelper.GetParent(ancestor))
                        if(ancestor is UIElement element) opacity *= element.Opacity;
                    var thickness = (perpendicular-start).Length;
                    lines.Add(new Line { View = view,Start = start,End = end,Settings = options,Brush = brush,Thickness = thickness,Opacity = opacity,PatternSize = DividerRenderer.PatternSize(horizontal ? bounds.Height : bounds.Width,localThickness,options)*thickness/localThickness,Scale = (end-start).Length/(horizontal ? bounds.Width : bounds.Height) });
                    key.Append(start.ToString(CultureInfo.InvariantCulture)).Append('|').Append(end.ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(options.ConnectEnds).Append(options.ConnectStart).Append(options.ConnectEnd).Append(options.ConnectionRange).Append('|')
                        .Append(thickness).Append(opacity).Append(options.LineStyle).Append(options.Decoration).Append(options.PatternSize).Append(options.PatternSpacing).Append(options.FillShapes).Append(options.RoundCaps).Append(options.DashLength).Append(options.DashGap).Append(brush.ToString()).Append(';');
                } catch(InvalidOperationException) { }
            }
            var next = key.ToString(); if(next == signature) return; signature = next;
            bridges = new List<Bridge>(); outlines.Clear();
            foreach(var line in lines.Where(item => item.Settings.ConnectEnds)) {
                if(line.Settings.ConnectStart) Connect(line,true,lines);
                if(line.Settings.ConnectEnd) Connect(line,false,lines);
            }
            // A decorative target does not necessarily pass through its nominal axis.
            // Resolve junctions against the same widened geometry that WPF paints.
            for(var pass=0;pass<8;pass++) {
                var changed = false;
                foreach(var bridge in bridges.Where(item => !item.Parallel).ToList()) {
                    var hit = SurfaceIntersection(bridge.Line,bridge.Start,bridge.Target,(bridge.Start-bridge.Line.Start).Length<.001);
                    if(!hit.HasValue) { bridges.Remove(bridge); changed = true; continue; }
                    if((hit.Value-bridge.End).Length>.01) { bridge.End = hit.Value; changed = true; }
                }
                if(!changed) break;
            }
            connectedLines = bridges.Select(bridge => bridge.Line).Distinct().ToList();
            foreach(var view in widgets) view.Drawing.RenderExternally = connectedLines.Any(line => line.View == view);
            InvalidateVisual();
            ConnectionsChanged?.Invoke(this,EventArgs.Empty);
        }
        private void Connect(Line source,bool start,List<Line> lines)
        {
            var horizontal = source.Settings.Orientation == Orientation.Horizontal;
            var origin = start ? source.Start : source.End; var sign = start ? -1 : 1;
            var nearest = source.Settings.ConnectionRange+0.001; Point? destination = null; Line selected = null; var selectedParallel = false;
            foreach(var target in lines) {
                if(target.View == source.View) continue;
                var parallel = horizontal == (target.Settings.Orientation == Orientation.Horizontal);
                Point point;
                if(parallel) {
                    if(Math.Abs(horizontal ? origin.Y-target.Start.Y : origin.X-target.Start.X) > .5) continue;
                    var first = horizontal ? target.Start.X : target.Start.Y;
                    var last = horizontal ? target.End.X : target.End.Y;
                    var coordinate = horizontal ? origin.X : origin.Y;
                    if(coordinate >= first && coordinate <= last) continue;
                    point = coordinate < first ? target.Start : target.End;
                } else {
                    var cross = horizontal ? origin.Y : origin.X;
                    var first = horizontal ? target.Start.Y : target.Start.X; var last = horizontal ? target.End.Y : target.End.X;
                    if(cross < first-target.Thickness/2 || cross > last+target.Thickness/2) continue;
                    point = horizontal ? new Point(target.Start.X,origin.Y) : new Point(origin.X,target.Start.Y);
                }
                var distance = sign*(horizontal ? point.X-origin.X : point.Y-origin.Y);
                if(distance > .001 && distance < nearest) { nearest = distance; destination = point; selected = target; selectedParallel = parallel; }
            }
            if(!destination.HasValue) return;
            var end = destination.Value;
            if(bridges.Any(bridge => ((bridge.Start-origin).Length<.5 && (bridge.End-end).Length<.5) || ((bridge.Start-end).Length<.5 && (bridge.End-origin).Length<.5))) return;
            bridges.Add(new Bridge { Line = source,Target = selected,Parallel = selectedParallel,Start = origin,End = end });
        }
        private bool Joined(Line line,bool start) => bridges.Any(bridge => bridge.Line == line && ((bridge.Start-line.Start).Length<.001) == start);
        private void Span(Line line,out Point start,out Point end)
        {
            start = line.Start; end = line.End;
            foreach(var bridge in bridges.Where(item => item.Line == line)) {
                if((bridge.Start-line.Start).Length<.001) start = bridge.End; else end = bridge.End;
            }
        }
        private PathGeometry Surface(Line line)
        {
            Span(line,out var start,out var end);
            if(outlines.TryGetValue(line,out var cached) && cached.Start == start && cached.End == end) return cached.Geometry;
            var drawing = new DrawingGroup();
            using(var context = drawing.Open()) DividerRenderer.Draw(context,start,end,Brushes.White,line.Thickness,line.PatternSize,line.Scale,line.Settings,Joined(line,true),Joined(line,false));
            var surface = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach(var child in drawing.Children.OfType<GeometryDrawing>()) {
                if(child.Brush != null) surface.Children.Add(child.Geometry);
                if(child.Pen != null) surface.Children.Add(child.Geometry.GetWidenedPathGeometry(child.Pen,.025,ToleranceType.Absolute));
            }
            var flattened = surface.GetFlattenedPathGeometry(.025,ToleranceType.Absolute); flattened.Freeze();
            outlines[line] = new Outline { Start = start,End = end,Geometry = flattened }; return flattened;
        }
        private Point? SurfaceIntersection(Line source,Point origin,Line target,bool start)
        {
            var horizontal = source.Settings.Orientation == Orientation.Horizontal; var direction = start ? -1 : 1;
            var nearest = source.Settings.ConnectionRange+.001; Point? result = null;
            Action<Point,Point> segment = (a,b) => {
                var denominator = horizontal ? b.Y-a.Y : b.X-a.X;
                if(Math.Abs(denominator)<.000001) return;
                var ratio = (horizontal ? origin.Y-a.Y : origin.X-a.X)/denominator;
                if(ratio < -.000001 || ratio > 1.000001) return;
                var point = a+(b-a)*Math.Max(0,Math.Min(1,ratio));
                // Keep the source axis exact rather than rotating the whole divider.
                if(horizontal) point.Y = origin.Y; else point.X = origin.X;
                var distance = direction*(horizontal ? point.X-origin.X : point.Y-origin.Y);
                if(distance > .001 && distance < nearest) { result = point; nearest = distance; }
            };
            foreach(var figure in Surface(target).Figures) {
                var previous = figure.StartPoint;
                foreach(var part in figure.Segments) {
                    if(part is PolyLineSegment poly) foreach(var point in poly.Points) { segment(previous,point); previous = point; }
                    else if(part is LineSegment line) { segment(previous,line.Point); previous = line.Point; }
                }
                if(figure.IsClosed) segment(previous,figure.StartPoint);
            }
            return result;
        }
        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            foreach(var line in connectedLines) {
                var options = line.Settings;
                Span(line,out var start,out var end);
                context.PushOpacity(line.Opacity); DividerRenderer.Draw(context,start,end,line.Brush,line.Thickness,line.PatternSize,line.Scale,options,Joined(line,true),Joined(line,false)); context.Pop();
            }
        }
    }
}
