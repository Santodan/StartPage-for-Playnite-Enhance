using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LiveCharts.Wpf.Points;
using LandingPage.Settings;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Wpf.Charts.Base;

namespace LandingPage.Controls
{
    // LiveCharts is also used by PlayniteAchievements. Keep the library-specific
    // data and settings here so dashboard views do not depend on its view models.
    public sealed class LibraryChartPlot : Grid
    {
        public Chart Chart { get; }
        private readonly Canvas connectors = new Canvas { IsHitTestVisible = false };
        private readonly List<Action> labelUpdates = new List<Action>();
        private bool updating, updatePending;
        private int labelRevision;
        private void RequestLabelUpdate()
        {
            if(updatePending || labelUpdates.Count == 0) return;
            updatePending = true;
            Dispatcher.BeginInvoke(new Action(() => { updatePending = false; UpdateLabels(); }),DispatcherPriority.Loaded);
        }
        public void UpdateRotation(double degrees)
        {
            if(!(Chart is PieChart pie)) return;
            ++labelRevision; pie.StartingRotationAngle = (degrees%360+360)%360;
            pie.Update(true,true); RequestLabelUpdate();
        }
        private void UpdateLabels()
        {
            if(updating || !IsLoaded) return;
            updating = true; try { foreach(var update in labelUpdates.ToArray()) update(); } finally { updating = false; }
        }
        public LibraryChartPlot(LibraryStatisticsSettings settings, IList<string> names,
            IList<double> values, Func<int,Brush> color, Func<int,string> label,
            Func<int,TextBlock> labelStyle, Action<TextBlock,int> prepareLabel)
        {
            var circular = settings.ChartStyle == StatisticsChartStyle.Pie || settings.ChartStyle == StatisticsChartStyle.Doughnut;
            var horizontal = settings.ChartStyle == StatisticsChartStyle.Bars;
            var series = new SeriesCollection();
            var foreground = settings.CustomLabelColor ? (Brush)new BrushConverter().ConvertFromString(settings.LabelColor) : TryFindResource("TextBrush") as Brush ?? Brushes.White;
            Brush separator = settings.CustomSegmentBorderColor ? (Brush)new BrushConverter().ConvertFromString(settings.SegmentBorderColor) : new SolidColorBrush(Color.FromArgb(45,128,128,128)); separator.Freeze();
            if(circular)
            {
                Chart = new PieChart { StartingRotationAngle = (settings.ChartRotation%360+360)%360,
                    InnerRadius = settings.ChartStyle == StatisticsChartStyle.Doughnut ? settings.ChartHeight*settings.ChartScale*settings.HoleSize/2 : 0,
                    HoverPushOut = 0 };
            }
            else
            {
                var cartesian = new CartesianChart(); Chart = cartesian;
                var categories = Axis(settings,foreground,separator); categories.Labels = horizontal ? names.Reverse().ToList() : names.ToList(); categories.Separator.Step = 1; categories.Separator.IsEnabled = false;
                var numbers = Axis(settings,foreground,separator); numbers.MinValue = 0;
                numbers.LabelFormatter = value => value.ToString(settings.Metric == StatisticsMetric.Playtime ? "N1" : "N0");
                if(horizontal) { cartesian.AxisX.Add(numbers); cartesian.AxisY.Add(categories); }
                else { cartesian.AxisX.Add(categories); cartesian.AxisY.Add(numbers); }
            }
            for(var i = 0; i < (circular ? values.Count : 1); i++)
            {
                var index = i;
                Series item;
                if(circular) item = new PieSeries { Values = new ChartValues<double> { values[i] },
                    LabelPosition = settings.LabelPosition == StatisticsLabelPosition.AboveBars ? PieLabelPosition.InsideSlice : PieLabelPosition.OutsideSlice,
                    PushOut = 0 };
                else
                {
                    // One point per category avoids a quadratic matrix of mostly-zero
                    // stacked series when users choose all games/groups.
                    var mapping = LiveCharts.Configurations.Mappers.Xy<double>();
                    if(horizontal) mapping.X(value => value).Y((value,category) => values.Count-1-category);
                    else mapping.X((value,category) => category).Y(value => value);
                    mapping.Fill((value,category) => SeriesFill(color(category),settings.GlossySeries)).Stroke(value => separator);
                    if(horizontal) item = new RowSeries { Configuration = mapping,Values = new ChartValues<double>(values), RowPadding = 6,
                        LabelsPosition = settings.LabelPosition == StatisticsLabelPosition.AboveBars ? BarLabelPosition.Parallel : BarLabelPosition.Top };
                    else item = new ColumnSeries { Configuration = mapping,Values = new ChartValues<double>(values), ColumnPadding = 6,
                        LabelsPosition = settings.LabelPosition == StatisticsLabelPosition.AboveBars ? BarLabelPosition.Parallel : BarLabelPosition.Top };
                }
                item.Title = names[i]; item.Fill = SeriesFill(color(i),settings.GlossySeries);
                item.Stroke = separator;
                item.StrokeThickness = settings.SliceSeparatorWidth;
                item.Foreground = foreground; item.FontSize = settings.LabelSize;
                item.DataLabels = settings.ShowLegend && settings.LabelPosition != StatisticsLabelPosition.Legend;
                item.LabelPoint = point => (horizontal ? point.X : point.Y) <= 0 ? string.Empty : label(circular ? index : point.Key);
                var factory = new FrameworkElementFactory(typeof(TextBlock));
                factory.SetBinding(TextBlock.TextProperty,new Binding("FormattedText"));
                factory.AddHandler(FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,args) => {
                    var text = (TextBlock)sender; var labelIndex = circular ? index : ((DataLabelViewModel)text.DataContext).Point.Key; var style = labelStyle(labelIndex);
                    text.SetResourceReference(StyleProperty,"BaseTextBlockStyle");
                    text.FontSize = style.FontSize; if(!string.IsNullOrWhiteSpace(settings.LabelFontFamily)) text.FontFamily = new FontFamily(settings.LabelFontFamily); text.FontWeight = style.FontWeight;
                    text.FontStyle = style.FontStyle; text.TextDecorations = style.TextDecorations;
                    if(settings.CustomLabelColor) text.Foreground = style.Foreground; else text.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); WidgetPalette.SetPreserveTextColor(text,settings.CustomLabelColor); text.TextWrapping = TextWrapping.Wrap; text.MaxWidth = settings.LabelWidth;
                    text.Name = circular ? (settings.LabelPosition == StatisticsLabelPosition.AboveBars ? "ChartSliceLabel" : "ChartOutsideLabel") : "ChartBarLabel";
                    prepareLabel(text,labelIndex);
                    if(circular) ConfigureCircularLabel(text,(PieSeries)item,settings,color(index),() => prepareLabel(text,labelIndex));
                    else ConfigureBarLabel(text,item,settings,horizontal,labelIndex);
                    text.SizeChanged += (s,e) => RequestLabelUpdate();
                    // Dragging assigns a new transform; observe it without scanning on
                    // every LayoutUpdated event from the rest of the application.
                    var descriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(RenderTransformProperty,typeof(TextBlock));
                    EventHandler changed = (s,e) => RequestLabelUpdate();
                    descriptor.AddValueChanged(text,changed);
                    text.Unloaded += (s,e) => descriptor.RemoveValueChanged(text,changed);
                    RequestLabelUpdate();
                }));
                item.DataLabelsTemplate = new DataTemplate { VisualTree = factory };
                series.Add(item);
            }
            if(settings.ChartStyle == StatisticsChartStyle.Doughnut && Chart is PieChart doughnut)
                doughnut.SizeChanged += (sender,args) => doughnut.InnerRadius = Math.Max(0,Math.Min(doughnut.ActualWidth,doughnut.ActualHeight)-10)*settings.HoleSize/2;
            Chart.Series = series; Chart.LegendLocation = LegendLocation.None; Chart.DisableAnimations = !settings.ChartAnimations;
            Chart.AnimationsSpeed = TimeSpan.FromMilliseconds(250); Chart.Hoverable = true;
            WidgetPalette.SetPreserveTextColor(Chart,settings.CustomLabelColor);
            Chart.DataTooltip = new LibraryTooltip(settings,names);
            Children.Add(Chart); Children.Add(connectors);
            Chart.UpdaterTick += sender => {
                if(settings.ChartStyle == StatisticsChartStyle.Doughnut && Chart is PieChart ring)
                {
                    var view = (Chart.Series.FirstOrDefault() as Series)?.ChartPoints.FirstOrDefault()?.View;
                    var slice = view?.GetType().GetProperty("Slice")?.GetValue(view) as PieSlice;
                    if(slice != null && slice.Radius > 0)
                    {
                        var radius = slice.Radius*settings.HoleSize;
                        if(Math.Abs(ring.InnerRadius-radius) > 0.25) ring.InnerRadius = radius;
                    }
                }
                RequestLabelUpdate();
            };
            SizeChanged += (sender,args) => RequestLabelUpdate();
        }
        private void ConfigureBarLabel(TextBlock text,Series series,LibraryStatisticsSettings settings,bool horizontal,int index)
        {
            if(settings.LabelPosition != StatisticsLabelPosition.AboveBars) return;
            bool placed = false;
            Action update = () => {
                if(placed || string.IsNullOrEmpty(text.Text) || text.ActualWidth <= 0) return;
                var point = series.ChartPoints.FirstOrDefault(item => item.Key == index);
                var view = point?.View;
                var rectangle = view?.GetType().GetProperty("Rectangle")?.GetValue(view) as FrameworkElement;
                if(rectangle == null || !rectangle.IsLoaded || rectangle.ActualHeight <= 0) return;
                placed = true;
                var bar = rectangle.TranslatePoint(new Point(),this); var current = text.TranslatePoint(new Point(),this);
                var old = text.RenderTransform;
                var target = new Point(horizontal ? bar.X : bar.X+(rectangle.ActualWidth-text.ActualWidth)/2,bar.Y-text.ActualHeight-4);
                var transform = new TransformGroup(); if(old != null) transform.Children.Add(old);
                transform.Children.Add(new TranslateTransform(target.X-current.X+(old?.Value.OffsetX ?? 0),target.Y-current.Y+(old?.Value.OffsetY ?? 0))); text.RenderTransform = transform;
            };
            labelUpdates.Add(update); text.Unloaded += (sender,args) => labelUpdates.Remove(update);
        }
        private void ConfigureCircularLabel(TextBlock text,PieSeries series,LibraryStatisticsSettings settings,Brush color,Action resetPlacement)
        {
            // Native labels supply the measured position. Test against the real LiveCharts
            // slice, rather than a second approximation of the pie/ring geometry.
            Polyline line = null; Rect previous = Rect.Empty; bool initial = true;
            int revision = labelRevision;
            Action update = () => {
                var point = series.ChartPoints.FirstOrDefault();
                var view = point?.View;
                var slice = view?.GetType().GetProperty("Slice")?.GetValue(view) as PieSlice;
                if(slice == null || text.ActualWidth <= 0 || !slice.IsLoaded) return;
                if(revision != labelRevision) { revision = labelRevision; initial = true; previous = Rect.Empty; resetPlacement(); }
                if(initial && settings.LabelPosition == StatisticsLabelPosition.BesideSegments)
                {
                    initial = false;
                    var outsideAngle = (slice.RotationAngle+slice.WedgeAngle/2)*Math.PI/180;
                    var transforms = new TransformGroup(); if(text.RenderTransform != null) transforms.Children.Add(text.RenderTransform);
                    transforms.Children.Add(new TranslateTransform(Math.Sign(Math.Sin(outsideAngle))*settings.OutsideLabelDistance,0)); text.RenderTransform = transforms;
                }
                if(initial && settings.LabelPosition == StatisticsLabelPosition.AboveBars)
                {
                    initial = false;
                    var origin = text.TranslatePoint(new Point(),slice);
                    var fit = slice.RenderedGeometry.FillContainsWithDetail(new RectangleGeometry(new Rect(origin,new Size(text.ActualWidth,text.ActualHeight)))) == IntersectionDetail.FullyContains;
                    if(!fit)
                    {
                        var radians = (slice.RotationAngle+slice.WedgeAngle/2)*Math.PI/180;
                        var anchor = slice.TranslatePoint(new Point(slice.CentreX+(slice.Radius+settings.OutsideLabelDistance)*Math.Sin(radians),slice.CentreY-(slice.Radius+settings.OutsideLabelDistance)*Math.Cos(radians)),this);
                        var current = text.TranslatePoint(new Point(),this);
                        var old = text.RenderTransform;
                        var transform = new TransformGroup(); if(old != null) transform.Children.Add(old);
                        transform.Children.Add(new TranslateTransform(anchor.X-current.X-(Math.Sin(radians)<0 ? text.ActualWidth : 0),anchor.Y-current.Y-text.ActualHeight/2));
                        text.RenderTransform = transform; text.Name = "ChartOutsideLabel";
                    }
                }
                var position = text.TranslatePoint(new Point(),this);
                var bounds = new Rect(position,new Size(text.ActualWidth,text.ActualHeight));
                if(bounds == previous) return; previous = bounds;
                var local = text.TranslatePoint(new Point(),slice);
                var inside = slice.RenderedGeometry.FillContainsWithDetail(new RectangleGeometry(new Rect(local,bounds.Size))) == IntersectionDetail.FullyContains;
                if(inside || !settings.ShowConnectorLines) { if(line != null) { connectors.Children.Remove(line); line = null; } }
                if(!settings.CustomLabelColor)
                {
                    if(inside && color is SolidColorBrush fill) text.Foreground = (.2126*fill.Color.R+.7152*fill.Color.G+.0722*fill.Color.B)>150 ? Brushes.Black : Brushes.White;
                    else text.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
                }
                if(inside || !settings.ShowConnectorLines) return;
                var angle = (slice.RotationAngle+slice.WedgeAngle/2)*Math.PI/180;
                var start = slice.TranslatePoint(new Point(slice.CentreX+slice.Radius*Math.Sin(angle),slice.CentreY-slice.Radius*Math.Cos(angle)),this);
                var end = new Point(Math.Max(bounds.Left,Math.Min(bounds.Right,start.X)),Math.Max(bounds.Top,Math.Min(bounds.Bottom,start.Y)));
                var points = new PointCollection { start };
                if(settings.ConnectorStyle == ChartConnectorStyle.Elbow) points.Add(new Point((start.X+end.X)/2,end.Y));
                points.Add(end);
                if(line == null) { line = new Polyline { Name = "ChartConnector",Stroke = settings.CustomConnectorColor ? (Brush)new BrushConverter().ConvertFromString(settings.ConnectorColor) : color,StrokeThickness = settings.ConnectorThickness }; connectors.Children.Add(line); }
                line.Points = points;
            };
            labelUpdates.Add(update);
            text.Unloaded += (sender,args) => { labelUpdates.Remove(update); if(line != null) connectors.Children.Remove(line); };
        }
        private sealed class LibraryTooltip : UserControl,IChartTooltip
        {
            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            private readonly LibraryStatisticsSettings settings;
            private readonly IList<string> names;
            public LibraryTooltip(LibraryStatisticsSettings settings,IList<string> names) { this.settings = settings; this.names = names; }
            public TooltipSelectionMode? SelectionMode { get; set; } = TooltipSelectionMode.OnlySender;
            public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data),typeof(TooltipData),typeof(LibraryTooltip),new PropertyMetadata(null,(sender,args) => ((LibraryTooltip)sender).Refresh()));
            public TooltipData Data { get => (TooltipData)GetValue(DataProperty); set => SetValue(DataProperty,value); }
            private void Refresh()
            {
                var rows = new StackPanel();
                if(Data?.Points != null) foreach(var item in Data.Points)
                {
                    var horizontal = settings.ChartStyle == StatisticsChartStyle.Bars;
                    var value = horizontal ? item.ChartPoint.X : item.ChartPoint.Y; if(value <= 0) continue;
                    var row = new StackPanel { Orientation = Orientation.Horizontal,Margin = new Thickness(0,2,0,2) };
                    row.Children.Add(new Rectangle { Width = 10,Height = 10,Fill = item.ChartPoint.Fill as Brush ?? item.Series.Fill,Margin = new Thickness(0,0,8,0),VerticalAlignment = VerticalAlignment.Center });
                    var text = new TextBlock { Text = ((settings.ChartStyle == StatisticsChartStyle.Bars || settings.ChartStyle == StatisticsChartStyle.VerticalBars) ? names[item.ChartPoint.Key] : item.Series.Title)+"  "+value.ToString(settings.Metric == StatisticsMetric.Playtime ? "N1" : "N0")+(settings.Metric == StatisticsMetric.Playtime ? " h" : ""),FontSize = settings.LabelSize };
                    text.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); row.Children.Add(text); rows.Children.Add(row);
                }
                var border = new Border { CornerRadius = new CornerRadius(4),Padding = new Thickness(10,6,10,6),BorderThickness = new Thickness(1),Child = rows };
                border.SetResourceReference(Border.BackgroundProperty,"PopupBackgroundBrush"); border.SetResourceReference(Border.BorderBrushProperty,"PopupBorderBrush"); Content = border;
                PropertyChanged?.Invoke(this,new System.ComponentModel.PropertyChangedEventArgs(nameof(Data)));
            }
        }
        private static Axis Axis(LibraryStatisticsSettings settings, Brush foreground, Brush grid)
        {
            var axis = new Axis { Foreground = foreground, FontSize = settings.LabelSize, FontWeight = settings.LabelBold ? FontWeights.Bold : FontWeights.Normal, FontStyle = settings.LabelItalic ? FontStyles.Italic : FontStyles.Normal, ShowLabels = settings.ShowChartAxes,
                Separator = new LiveCharts.Wpf.Separator { Stroke = grid, StrokeThickness = 1, IsEnabled = settings.ShowChartGrid } };
            if(!settings.CustomLabelColor) axis.SetResourceReference(LiveCharts.Wpf.Axis.ForegroundProperty,"TextBrush");
            if(!string.IsNullOrWhiteSpace(settings.LabelFontFamily)) axis.FontFamily = new FontFamily(settings.LabelFontFamily);
            return axis;
        }
        public void Highlight(int index,bool active)
        {
            if(Chart is PieChart)
            {
                if(index >= 0 && index < Chart.Series.Count && Chart.Series[index] is PieSeries pie) pie.PushOut = active ? 4 : 0;
            }
            else
            {
                var view = (Chart.Series.FirstOrDefault() as Series)?.ChartPoints.FirstOrDefault(point => point.Key == index)?.View;
                var rectangle = view?.GetType().GetProperty("Rectangle")?.GetValue(view) as UIElement;
                if(rectangle != null) rectangle.Opacity = active ? .7 : 1;
            }
        }
        private static Brush SeriesFill(Brush source,bool glossy)
        {
            if(!glossy || !(source is SolidColorBrush solid)) return source;
            // A diagonal highlight and shade, relative to each slice/bar's bounds.
            var c = solid.Color;
            Func<double,Color> tint = amount => Color.FromArgb(c.A,
                (byte)Math.Round(amount > 0 ? c.R+(255-c.R)*amount : c.R*(1+amount)),
                (byte)Math.Round(amount > 0 ? c.G+(255-c.G)*amount : c.G*(1+amount)),
                (byte)Math.Round(amount > 0 ? c.B+(255-c.B)*amount : c.B*(1+amount)));
            var result = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(1,1) };
            result.GradientStops.Add(new GradientStop(tint(.35),0)); result.GradientStops.Add(new GradientStop(c,.5));
            result.GradientStops.Add(new GradientStop(tint(-.3),1)); result.Freeze(); return result;
        }
    }
}
