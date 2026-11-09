using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LandingPage.Models.Layout;

namespace LandingPage.Controls
{
    // Applied only to the hosted widget, never the layout editor or other panels.
    public static class WidgetPalette
    {
        public static readonly DependencyProperty PreserveTextColorProperty = DependencyProperty.RegisterAttached(
            "PreserveTextColor", typeof(bool), typeof(WidgetPalette), new PropertyMetadata(false));
        public static bool GetPreserveTextColor(DependencyObject target) => (bool)target.GetValue(PreserveTextColorProperty);
        public static void SetPreserveTextColor(DependencyObject target, bool value) => target.SetValue(PreserveTextColorProperty, value);
        public static readonly DependencyProperty AppearanceProperty = DependencyProperty.RegisterAttached(
            "Appearance", typeof(PanelAppearance), typeof(WidgetPalette), new PropertyMetadata(null, Changed));
        private static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached(
            "Controller", typeof(Controller), typeof(WidgetPalette));
        public static PanelAppearance GetAppearance(DependencyObject target) => (PanelAppearance)target.GetValue(AppearanceProperty);
        public static void SetAppearance(DependencyObject target, PanelAppearance value) => target.SetValue(AppearanceProperty, value);

        private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (!(target is FrameworkElement host)) return;
            var controller = (Controller)host.GetValue(ControllerProperty);
            if (controller == null)
            {
                controller = new Controller(host);
                host.SetValue(ControllerProperty, controller);
            }
            controller.SetAppearance(args.NewValue as PanelAppearance);
        }

        private sealed class SavedValue
        {
            public object Value;
            public bool HadLocalValue;
        }

        private sealed class IconTint
        {
            public ImageSource Tinted;
        }

        private sealed class Controller
        {
            private readonly FrameworkElement host;
            private readonly DispatcherTimer timer;
            private PanelAppearance appearance;
            private bool subscribed;
            private Brush text, chart, highlight, icon;
            private readonly Dictionary<Image, IconTint> iconTints = new Dictionary<Image, IconTint>();
            private readonly Dictionary<Tuple<DependencyObject, DependencyProperty>, SavedValue> saved =
                new Dictionary<Tuple<DependencyObject, DependencyProperty>, SavedValue>();
            private readonly HashSet<Tuple<DependencyObject, DependencyProperty>> touched =
                new HashSet<Tuple<DependencyObject, DependencyProperty>>();

            public Controller(FrameworkElement host)
            {
                this.host = host;
                timer = new DispatcherTimer(DispatcherPriority.Background, host.Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += (sender, args) => Apply();
                host.Loaded += (sender, args) => Refresh();
                host.Unloaded += (sender, args) => { timer.Stop(); Unsubscribe(); RestoreAll(); };
            }

            public void SetAppearance(PanelAppearance value)
            {
                Unsubscribe();
                RestoreAll();
                appearance = value;
                Refresh();
            }

            private void Unsubscribe()
            {
                if (subscribed && appearance != null) appearance.PropertyChanged -= AppearanceChanged;
                subscribed = false;
            }

            private void AppearanceChanged(object sender, PropertyChangedEventArgs args) => Refresh();

            private void Refresh()
            {
                timer.Stop();
                RestoreAll();
                if (!host.IsLoaded || appearance == null) return;
                if (!subscribed) { appearance.PropertyChanged += AppearanceChanged; subscribed = true; }
                if (!appearance.Enabled || !(appearance.CustomizeText || appearance.CustomizeCharts || appearance.CustomizeHighlights || appearance.CustomizeIcons)) return;
                text = MakeBrush(appearance.TextColor);
                chart = MakeBrush(appearance.ChartColor);
                highlight = MakeBrush(appearance.HighlightColor);
                icon = MakeBrush(appearance.IconColor);
                Apply();
                // Refresh realized/virtualized items and hover/selection states without touching widget data.
                timer.Start();
            }

            private static Brush MakeBrush(string color)
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                brush.Freeze();
                return brush;
            }

            private void Paint(DependencyObject element, DependencyProperty property, object value)
            {
                var key = Tuple.Create(element, property);
                touched.Add(key);
                if (!saved.ContainsKey(key)) saved[key] = new SavedValue
                {
                    Value = element.GetValue(property),
                    HadLocalValue = element.ReadLocalValue(property) != DependencyProperty.UnsetValue
                };
                element.SetCurrentValue(property, value);
            }

            private void Restore(Tuple<DependencyObject, DependencyProperty> key)
            {
                var value = saved[key];
                if (value.HadLocalValue)
                {
                    key.Item1.SetCurrentValue(key.Item2, value.Value);
                    // Re-evaluate a live binding rather than restoring an outdated snapshot.
                    BindingOperations.GetBindingExpression(key.Item1, key.Item2)?.UpdateTarget();
                    BindingOperations.GetMultiBindingExpression(key.Item1, key.Item2)?.UpdateTarget();
                    BindingOperations.GetPriorityBindingExpression(key.Item1, key.Item2)?.UpdateTarget();
                }
                else key.Item1.ClearValue(key.Item2);
                saved.Remove(key);
                if (key.Item1 is Image image && key.Item2 == Image.SourceProperty) iconTints.Remove(image);
            }

            private void PaintIcon(Image image)
            {
                // Small, square-ish images are icons; leave covers, banners, and backgrounds alone.
                if (image.Source == null || image.ActualWidth <= 0 || image.ActualHeight <= 0
                    || image.ActualWidth > 64 || image.ActualHeight > 64
                    || image.ActualWidth / image.ActualHeight < 0.5 || image.ActualWidth / image.ActualHeight > 2) return;
                if (!iconTints.TryGetValue(image, out var tint) || image.Source != tint.Tinted)
                {
                    var source = image.Source;
                    var width = source.Width > 0 && !double.IsInfinity(source.Width) ? source.Width : image.ActualWidth;
                    var height = source.Height > 0 && !double.IsInfinity(source.Height) ? source.Height : image.ActualHeight;
                    var drawing = new DrawingGroup { OpacityMask = new ImageBrush(source) { Stretch = Stretch.Fill } };
                    drawing.Children.Add(new GeometryDrawing(icon, null, new RectangleGeometry(new Rect(0, 0, width, height))));
                    drawing.Freeze();
                    tint = new IconTint { Tinted = new DrawingImage(drawing) };
                    tint.Tinted.Freeze();
                    iconTints[image] = tint;
                    // A recycled image may have received a new source binding since the last scan.
                    if (saved.TryGetValue(Tuple.Create((DependencyObject)image, Image.SourceProperty), out var previous)) previous.Value = source;
                }
                Paint(image, Image.SourceProperty, tint.Tinted);
            }

            private void PaintThemeHighlight(FrameworkElement element)
            {
                // FusionZ paints these separately from ListViewItem.Background.
                // Keep their visibility triggers, so selection/hover behavior stays intact.
                var name = element.Name;
                if (element is Shape && (name == "HoverBg" || name == "SelectedBg"
                    || name == "SelectionBackground" || name == "MouseOverBackground"))
                    Paint(element, Shape.FillProperty, highlight);
                if (element is Border border && border.Style != null
                    && ReferenceEquals(border.Style, border.TryFindResource("HighlightBg")))
                    Paint(border, Border.BackgroundProperty, highlight);
            }

            private void RestoreAll()
            {
                foreach (var key in saved.Keys.ToArray()) Restore(key);
            }

            private void Apply()
            {
                if (!host.IsVisible) return;
                touched.Clear();
                Visit(host, false, false, false);
                foreach (var key in saved.Keys.Where(key => !touched.Contains(key)).ToArray()) Restore(key);
            }

            private void Visit(DependencyObject element, bool inChart, bool customContentColor, bool inImageWidget)
            {
                inImageWidget |= element is Views.ImageWidgetView;
                var type = element.GetType();
                inChart |= type.Name.IndexOf("Chart", StringComparison.OrdinalIgnoreCase) >= 0
                    || type.Name == "GameActivityView" || type.Namespace?.IndexOf("LiveCharts", StringComparison.OrdinalIgnoreCase) >= 0;
                if (element is Views.ClockView clockView) customContentColor = clockView.Settings?.UseCustomColors == true;
                if (element is Views.ShelfGameInfo info) customContentColor = info.Settings?.CustomizeMetadataText == true;
                if (GetPreserveTextColor(element)) customContentColor = true;
                if (element is Views.TextWidgetView textView) customContentColor = textView.Settings?.UseCustomColor == true;
                if (appearance.CustomizeText && !customContentColor)
                {
                    if (element is TextBlock) Paint(element, TextBlock.ForegroundProperty, text);
                    else if (element is Control) Paint(element, Control.ForegroundProperty, text);
                }
                if (element is FrameworkElement fe)
                {
                    if (appearance.CustomizeIcons && !inImageWidget && element is Image image) PaintIcon(image);
                    var bar = element is Border && fe.Name.IndexOf("Bar", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (appearance.CustomizeCharts && (inChart || bar))
                    {
                        var brush = appearance.CustomizeHighlights && fe.IsMouseOver ? highlight : chart;
                        if (bar) { Paint(element, Border.BackgroundProperty, brush); Paint(element, Border.BorderBrushProperty, brush); }
                        if (element is Shape shape)
                        {
                            if (shape.Fill != null && !IsTransparent(shape.Fill)) Paint(shape, Shape.FillProperty, brush);
                            if (shape.Stroke != null && !IsTransparent(shape.Stroke)) Paint(shape, Shape.StrokeProperty, brush);
                        }
                    }
                    if (appearance.CustomizeHighlights)
                    {
                        PaintThemeHighlight(fe);
                        var selected = element is ListBoxItem item && item.IsSelected
                            || element is DataGridRow row && row.IsSelected
                            || element is DataGridCell cell && cell.IsSelected
                            || element is TreeViewItem tree && tree.IsSelected;
                        if (element is Control && (selected || fe.IsMouseOver &&
                            (element is ListBoxItem || element is DataGridRow || element is DataGridCell || element is TreeViewItem || element is DataGridColumnHeader || element is GridViewColumnHeader)))
                            Paint(element, Control.BackgroundProperty, highlight);
                        if (element is ProgressBar) Paint(element, Control.ForegroundProperty, highlight);
                    }
                }
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                    Visit(VisualTreeHelper.GetChild(element, index), inChart, customContentColor, inImageWidget);
            }

            private static bool IsTransparent(Brush brush) => brush is SolidColorBrush solid && solid.Color.A == 0;
        }
    }
}
