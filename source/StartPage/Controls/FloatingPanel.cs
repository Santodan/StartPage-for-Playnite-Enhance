using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LandingPage.Controls
{
    // Shared chrome for in-app editors. Geometry lasts for this editor instance;
    // it is never written into dashboard/profile settings.
    public static class FloatingPanel
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(FloatingPanel), new PropertyMetadata(false, Changed));
        public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
        public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "State", typeof(State), typeof(FloatingPanel));
        private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (!(target is Border border)) return;
            if ((bool)args.NewValue)
            {
                var state = new State(border); border.SetValue(StateProperty,state); state.Attach();
            }
            else { (border.GetValue(StateProperty) as State)?.Detach(); border.ClearValue(StateProperty); }
        }
        [Flags] private enum Edges { Move = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }
        private sealed class State
        {
            private readonly Border panel;
            private FrameworkElement host;
            private Popup popup;
            private Point startPointer;
            private Rect startBounds;
            private Edges edges;
            private bool dragging, positioned;
            private Grid wrapper;
            private TextBlock grip;
            public State(Border panel) { this.panel = panel; }
            public void Attach()
            {
                panel.Loaded += Loaded; panel.Unloaded += Unloaded;
                panel.PreviewMouseLeftButtonDown += Down;
                panel.PreviewMouseMove += Move;
                panel.PreviewMouseLeftButtonUp += Up;
                panel.LostMouseCapture += LostCapture;
                if (panel.IsLoaded) Loaded(panel,new RoutedEventArgs());
            }
            public void Detach()
            {
                Unloaded(panel,new RoutedEventArgs());
                panel.Loaded -= Loaded; panel.Unloaded -= Unloaded;
                panel.PreviewMouseLeftButtonDown -= Down; panel.PreviewMouseMove -= Move;
                panel.PreviewMouseLeftButtonUp -= Up; panel.LostMouseCapture -= LostCapture;
                if (wrapper != null)
                {
                    var child = wrapper.Children[0]; wrapper.Children.Clear(); panel.Child = null;
                    panel.Child = child; wrapper = null;
                }
            }
            private void Loaded(object sender, RoutedEventArgs args)
            {
                if (host != null) host.SizeChanged -= HostResized;
                popup = null;
                DependencyObject parent = panel;
                while (parent != null)
                {
                    if (parent is Popup found) { popup = found; break; }
                    parent = LogicalTreeHelper.GetParent(parent) ?? (parent is Visual ? VisualTreeHelper.GetParent(parent) : null);
                }
                host = popup != null ? Window.GetWindow(popup.PlacementTarget) : VisualTreeHelper.GetParent(panel) as FrameworkElement;
                if (host != null) host.SizeChanged += HostResized;
                if (wrapper == null && panel.Child != null)
                {
                    var child = panel.Child; panel.Child = null;
                    wrapper = new Grid(); wrapper.Children.Add(child);
                    grip = new TextBlock { Text = "\u25e2", FontSize = 14, Opacity = 0.7,
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                        Cursor = Cursors.SizeNWSE, ToolTip = "Drag to resize" };
                    grip.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
                    wrapper.Children.Add(grip); panel.Child = wrapper;
                }
                // Popups have no parent layout constraint: keep long editors within Playnite.
                if (popup != null && host != null)
                {
                    if (double.IsNaN(panel.Height))
                    {
                        // Loaded can precede the first arranged size (and popup bindings).
                        // Never freeze an auto-sized editor at ActualHeight == 0.
                        var initialHeight = panel.ActualHeight > 140 ? panel.ActualHeight : 560;
                        panel.Height = Math.Min(initialHeight,Math.Max(140,host.ActualHeight-32));
                    }
                    panel.MaxHeight = Math.Max(120,host.ActualHeight-16);
                    panel.MaxWidth = Math.Max(160,host.ActualWidth-16);
                }
            }
            private void Unloaded(object sender, RoutedEventArgs args)
            {
                Stop(); if (host != null) host.SizeChanged -= HostResized; host = null;
            }
            private void HostResized(object sender, SizeChangedEventArgs args)
            {
                if (popup != null)
                {
                    panel.MaxHeight = Math.Max(120,host.ActualHeight-16);
                    panel.MaxWidth = Math.Max(160,host.ActualWidth-16);
                }
                if (positioned && panel.IsVisible) ApplyBounds(CurrentBounds());
            }
            private Rect CurrentBounds()
            {
                var point = popup != null ? host.PointFromScreen(panel.PointToScreen(new Point())) : panel.TranslatePoint(new Point(),host);
                return new Rect(point,new Size(panel.ActualWidth,panel.ActualHeight));
            }
            private static bool Interactive(DependencyObject source, Border border)
            {
                while (source != null && source != border)
                {
                    if (source is ButtonBase || source is TextBoxBase || source is ComboBox || source is Slider || source is PasswordBox) return true;
                    source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
                }
                return false;
            }
            private Edges Hit(Point point)
            {
                if (point.X >= panel.ActualWidth-22 && point.Y >= panel.ActualHeight-22) return Edges.Right|Edges.Bottom;
                var hit = Edges.Move;
                if (point.X <= 5) hit |= Edges.Left;
                if (point.X >= panel.ActualWidth-5) hit |= Edges.Right;
                if (point.Y <= 5) hit |= Edges.Top;
                if (point.Y >= panel.ActualHeight-5) hit |= Edges.Bottom;
                return hit;
            }
            private void Down(object sender, MouseButtonEventArgs args)
            {
                if (host == null || Interactive(args.OriginalSource as DependencyObject,panel)) return;
                var point = args.GetPosition(panel); var hit = Hit(point);
                if (hit == Edges.Move && point.Y > 48) return;
                Begin(args.GetPosition(host),hit);
                if (panel.CaptureMouse()) args.Handled = true; else Stop();
            }
            private void Begin(Point pointer, Edges hit)
            {
                startPointer = pointer; startBounds = CurrentBounds(); edges = hit;
                dragging = true;
            }
            private void Move(object sender, MouseEventArgs args)
            {
                if (dragging && host != null) { Update(args.GetPosition(host)); args.Handled = true; return; }
                var point = args.GetPosition(panel); var hit = Hit(point);
                panel.Cursor = hit == (Edges.Right|Edges.Bottom) || hit == (Edges.Left|Edges.Top) ? Cursors.SizeNWSE
                    : hit == (Edges.Left|Edges.Bottom) || hit == (Edges.Right|Edges.Top) ? Cursors.SizeNESW
                    : (hit & (Edges.Left|Edges.Right)) != 0 ? Cursors.SizeWE
                    : (hit & (Edges.Top|Edges.Bottom)) != 0 ? Cursors.SizeNS
                    : point.Y <= 48 && !Interactive(args.OriginalSource as DependencyObject,panel) ? Cursors.SizeAll : null;
            }
            private void Update(Point pointer)
            {
                var dx = pointer.X-startPointer.X; var dy = pointer.Y-startPointer.Y;
                var rect = startBounds;
                if (edges == Edges.Move) { rect.X += dx; rect.Y += dy; }
                else
                {
                    if ((edges & Edges.Left) != 0) { rect.X += dx; rect.Width = Math.Max(1,startBounds.Width-dx); }
                    if ((edges & Edges.Right) != 0) rect.Width = Math.Max(1,startBounds.Width+dx);
                    if ((edges & Edges.Top) != 0) { rect.Y += dy; rect.Height = Math.Max(1,startBounds.Height-dy); }
                    if ((edges & Edges.Bottom) != 0) rect.Height = Math.Max(1,startBounds.Height+dy);
                }
                ApplyBounds(rect);
            }
            private void ApplyBounds(Rect rect)
            {
                var maxWidth = Math.Max(1,host.ActualWidth-16); var maxHeight = Math.Max(1,host.ActualHeight-16);
                rect.Width = Math.Max(Math.Min(Math.Max(240,panel.MinWidth),maxWidth),Math.Min(rect.Width,maxWidth));
                rect.Height = Math.Max(Math.Min(Math.Max(140,panel.MinHeight),maxHeight),Math.Min(rect.Height,maxHeight));
                rect.X = Math.Max(8,Math.Min(rect.X,host.ActualWidth-rect.Width-8));
                rect.Y = Math.Max(8,Math.Min(rect.Y,host.ActualHeight-rect.Height-8));
                panel.SetCurrentValue(FrameworkElement.WidthProperty,rect.Width);
                panel.SetCurrentValue(FrameworkElement.HeightProperty,rect.Height);
                if (popup != null)
                {
                    var current = CurrentBounds();
                    popup.SetCurrentValue(Popup.HorizontalOffsetProperty,popup.HorizontalOffset+rect.X-current.X);
                    popup.SetCurrentValue(Popup.VerticalOffsetProperty,popup.VerticalOffset+rect.Y-current.Y);
                }
                else
                {
                    panel.HorizontalAlignment = HorizontalAlignment.Left; panel.VerticalAlignment = VerticalAlignment.Top;
                    panel.Margin = new Thickness(rect.X,rect.Y,0,0);
                }
                positioned = true;
            }
            private void Up(object sender, MouseButtonEventArgs args) { if (dragging) { Stop(); args.Handled = true; } }
            private void LostCapture(object sender, MouseEventArgs args) { dragging = false; }
            private void Stop() { dragging = false; if (panel.IsMouseCaptured) panel.ReleaseMouseCapture(); }
        }
    }
}
