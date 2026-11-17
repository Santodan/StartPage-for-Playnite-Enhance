using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace LandingPage.Settings
{
    public enum DividerMode { Divider, Spacer }
    public enum DividerLineStyle { Solid, Dashed, Dotted, DashDot, DashDotDot, Custom }
    public enum DividerDecoration { Line, DoubleLine, TripleLine, Wave, DoubleWave, Zigzag, Chevrons, Circles, Squares, Diamonds }
    public enum DividerPlacement { Start, Center, End }
    public sealed class DividerWidgetSettings : ObservableObjectExt
    {
        private DividerMode mode;
        public DividerMode Mode { get => mode; set => SetValue(ref mode,value); }
        private Orientation orientation = Orientation.Horizontal;
        public Orientation Orientation { get => orientation; set => SetValue(ref orientation,value); }
        private DividerDecoration decoration;
        public DividerDecoration Decoration { get => decoration; set => SetValue(ref decoration,value); }
        private double patternSize = 8, patternSpacing = 24, dashLength = 6, dashGap = 4;
        public double PatternSize { get => patternSize; set => SetValue(ref patternSize,Limit(value,8,1,100)); }
        public double PatternSpacing { get => patternSpacing; set => SetValue(ref patternSpacing,Limit(value,24,4,200)); }
        public double DashLength { get => dashLength; set => SetValue(ref dashLength,Limit(value,6,.5,100)); }
        public double DashGap { get => dashGap; set => SetValue(ref dashGap,Limit(value,4,.5,100)); }
        private bool fillShapes, roundCaps;
        public bool FillShapes { get => fillShapes; set => SetValue(ref fillShapes,value); }
        public bool RoundCaps { get => roundCaps; set => SetValue(ref roundCaps,value); }
        private DividerLineStyle lineStyle;
        public DividerLineStyle LineStyle { get => lineStyle; set => SetValue(ref lineStyle,value); }
        private DividerPlacement position = DividerPlacement.Center, lengthAlignment = DividerPlacement.Center;
        public DividerPlacement Position { get => position; set => SetValue(ref position,value); }
        public DividerPlacement LengthAlignment { get => lengthAlignment; set => SetValue(ref lengthAlignment,value); }
        private static double Limit(double value,double fallback,double min,double max) => double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(min,Math.Min(max,value));
        private double spaceSize = 24, lineThickness = 1, length = 100, inset = 12, lineOpacity = .6;
        public double SpaceSize { get => spaceSize; set => SetValue(ref spaceSize,Limit(value,24,0,1000)); }
        public double LineThickness { get => lineThickness; set => SetValue(ref lineThickness,Limit(value,1,.5,30)); }
        public double Length { get => length; set => SetValue(ref length,Limit(value,100,1,100)); }
        public double Inset { get => inset; set => SetValue(ref inset,Limit(value,12,0,1000)); }
        public double LineOpacity { get => lineOpacity; set => SetValue(ref lineOpacity,Limit(value,.6,0,1)); }
        private bool connectEnds, connectStart = true, connectEnd = true;
        public bool ConnectEnds { get => connectEnds; set => SetValue(ref connectEnds,value); }
        public bool ConnectStart { get => connectStart; set => SetValue(ref connectStart,value); }
        public bool ConnectEnd { get => connectEnd; set => SetValue(ref connectEnd,value); }
        private double connectionRange = 100;
        public double ConnectionRange { get => connectionRange; set => SetValue(ref connectionRange,Limit(value,100,0,1000)); }
        public void CopyAppearanceFrom(DividerWidgetSettings target)
        {
            if(target == null || ReferenceEquals(this,target)) return;
            Decoration = target.Decoration; LineStyle = target.LineStyle;
            LineThickness = target.LineThickness; LineOpacity = target.LineOpacity;
            PatternSize = target.PatternSize; PatternSpacing = target.PatternSpacing;
            DashLength = target.DashLength; DashGap = target.DashGap;
            FillShapes = target.FillShapes; RoundCaps = target.RoundCaps;
            CustomColor = target.CustomColor; Color = target.Color;
        }
        private bool customColor;
        public bool CustomColor { get => customColor; set => SetValue(ref customColor,value); }
        private string color = "#FFFFFF";
        public string Color {
            get => color;
            set { if(!(ColorConverter.ConvertFromString(value ?? "") is System.Windows.Media.Color)) throw new ArgumentException("Enter a valid color."); SetValue(ref color,value); }
        }
    }
}
