using System;
using System.Windows;
using System.Windows.Media;
using LandingPage.Settings;

namespace LandingPage.Controls
{
    internal static class DividerRenderer
    {
        public static double Extent(DividerWidgetSettings settings) => settings.Decoration == DividerDecoration.Line ? settings.LineThickness
            : settings.LineThickness+settings.PatternSize*(settings.Decoration == DividerDecoration.DoubleWave ? 2 : 1);
        public static double PatternSize(double envelope,double thickness,DividerWidgetSettings settings) => Math.Min(settings.PatternSize,Math.Max(0,envelope-thickness)/(settings.Decoration == DividerDecoration.DoubleWave ? 2 : 1));
        public static void Draw(DrawingContext context,Point start,Point end,Brush brush,double thickness,double size,double scale,DividerWidgetSettings options,bool joinStart = false,bool joinEnd = false)
        {
            var vector = end-start; var length = vector.Length; if(length <= .001 || thickness <= 0) return;
            vector.Normalize(); var normal = new Vector(-vector.Y,vector.X);
            Func<double,double,Point> point = (distance,offset) => start+vector*distance+normal*offset;
            var pen = new Pen(brush,thickness) { LineJoin = options.RoundCaps ? PenLineJoin.Round : PenLineJoin.Bevel };
            if(options.RoundCaps) { pen.StartLineCap = pen.EndLineCap = pen.DashCap = PenLineCap.Round; }
            switch(options.LineStyle) {
                case DividerLineStyle.Dashed: pen.DashStyle = DashStyles.Dash; break;
                case DividerLineStyle.Dotted: pen.DashStyle = new DashStyle(new double[] {0,2},0); pen.DashCap = PenLineCap.Round; break;
                case DividerLineStyle.DashDot: pen.DashStyle = DashStyles.DashDot; break;
                case DividerLineStyle.DashDotDot: pen.DashStyle = DashStyles.DashDotDot; break;
                case DividerLineStyle.Custom: pen.DashStyle = new DashStyle(new[] {options.DashLength*scale/thickness,options.DashGap*scale/thickness},0); break;
            }
            var taper = Math.Min(length/3,Math.Max(size*2,8*scale));
            Func<double,double> envelope = distance => {
                var value = Math.Min(joinStart ? distance/taper : 1,joinEnd ? (length-distance)/taper : 1);
                value = Math.Max(0,Math.Min(1,value)); return value*value*(3-2*value);
            };
            var decoration = size <= .001 ? DividerDecoration.Line : options.Decoration;
            if(decoration == DividerDecoration.Line) { context.DrawLine(pen,start,end); return; }
            if(decoration == DividerDecoration.DoubleLine || decoration == DividerDecoration.TripleLine) {
                Action<double> stripe = offset => {
                    var path = new StreamGeometry();
                    using(var drawing = path.Open()) {
                        drawing.BeginFigure(point(0,offset*envelope(0)),false,false);
                        if(joinStart) for(var step=1;step<=12;step++) { var x = taper*step/12; drawing.LineTo(point(x,offset*envelope(x)),true,false); }
                        drawing.LineTo(point(joinEnd ? length-taper : length,offset),true,false);
                        if(joinEnd) for(var step=1;step<=12;step++) { var x = length-taper+taper*step/12; drawing.LineTo(point(x,offset*envelope(x)),true,false); }
                    }
                    path.Freeze(); context.DrawGeometry(null,pen,path);
                };
                stripe(-size/2); stripe(size/2);
                if(decoration == DividerDecoration.TripleLine) context.DrawLine(pen,start,end); return;
            }
            var spacing = Math.Max(4,options.PatternSpacing*scale);
            if(decoration == DividerDecoration.Wave || decoration == DividerDecoration.DoubleWave || decoration == DividerDecoration.Zigzag) {
                var cycles = Math.Max(1,Math.Min(512,(int)Math.Round(length/spacing)));
                var samples = decoration == DividerDecoration.Zigzag ? cycles*4 : cycles*24;
                Action<double> wave = offset => {
                    var geometry = new StreamGeometry();
                    using(var drawing = geometry.Open()) {
                        drawing.BeginFigure(point(0,offset*envelope(0)),false,false);
                        for(var index=1;index<=samples;index++) {
                            var phase = (double)index/samples*cycles*2*Math.PI;
                            var amplitude = decoration == DividerDecoration.Zigzag ? (index%4 == 1 ? 1 : index%4 == 3 ? -1 : 0) : Math.Sin(phase);
                            var distance = length*index/samples;
                            drawing.LineTo(point(distance,(offset+amplitude*size/2)*envelope(distance)),true,false);
                        }
                    }
                    geometry.Freeze(); context.DrawGeometry(null,pen,geometry);
                };
                if(decoration == DividerDecoration.DoubleWave) { wave(-size/2); wave(size/2); } else wave(0);
                return;
            }
            size = Math.Min(size,Math.Max(0,length-thickness)); if(size <= .001) return;
            spacing = Math.Max(spacing,size+thickness);
            var count = Math.Max(1,Math.Min(1024,(int)Math.Floor((length-size-thickness)/spacing)+1));
            var first = (length-(count-1)*spacing)/2;
            if(joinStart) context.DrawLine(pen,start,point(first+(decoration == DividerDecoration.Chevrons ? size/2 : -size/2),0));
            if(joinEnd) context.DrawLine(pen,point(first+(count-1)*spacing+size/2,0),end);
            for(var index=0;index<count;index++) {
                var distance = first+index*spacing; var center = point(distance,0); var radius = size/2; var fill = options.FillShapes ? brush : null;
                if(decoration == DividerDecoration.Circles) { context.DrawEllipse(fill,pen,center,radius,radius); continue; }
                var geometry = new StreamGeometry();
                using(var drawing = geometry.Open()) {
                    if(decoration == DividerDecoration.Chevrons) {
                        drawing.BeginFigure(point(distance-radius,-radius),false,false); drawing.LineTo(point(distance+radius,0),true,false); drawing.LineTo(point(distance-radius,radius),true,false);
                    } else if(decoration == DividerDecoration.Diamonds) {
                        drawing.BeginFigure(point(distance-radius,0),options.FillShapes,true); drawing.LineTo(point(distance,-radius),true,false); drawing.LineTo(point(distance+radius,0),true,false); drawing.LineTo(point(distance,radius),true,false);
                    } else {
                        drawing.BeginFigure(point(distance-radius,-radius),options.FillShapes,true); drawing.LineTo(point(distance+radius,-radius),true,false); drawing.LineTo(point(distance+radius,radius),true,false); drawing.LineTo(point(distance-radius,radius),true,false);
                    }
                }
                geometry.Freeze(); context.DrawGeometry(fill,pen,geometry);
            }
        }
    }
}
