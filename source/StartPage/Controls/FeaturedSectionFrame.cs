using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using LandingPage.Settings;

namespace LandingPage.Controls
{
    internal sealed class FeaturedSectionCanvas : Canvas
    {
        protected override Size MeasureOverride(Size constraint)
        {
            var extent = new Size();
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
                extent.Width = Math.Max(extent.Width,GetLeft(child)+child.DesiredSize.Width+24);
                extent.Height = Math.Max(extent.Height,GetTop(child)+child.DesiredSize.Height+24);
            }
            return extent;
        }
    }

    internal sealed class FeaturedSectionFrame : SectionAppearanceBorder
    {
        internal readonly FeaturedSectionLayout Layout;
        internal readonly Thumb MoveHandle, ResizeHandle;
        internal readonly ScrollViewer Viewport;
        private bool subscribed;
        private readonly Border editOutline;
        public static readonly DependencyProperty EditingProperty = DependencyProperty.Register(nameof(Editing),typeof(bool),typeof(FeaturedSectionFrame),new PropertyMetadata(false,Changed));
        public bool Editing { get => (bool)GetValue(EditingProperty); set => SetValue(EditingProperty,value); }
        private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((FeaturedSectionFrame)sender).UpdateChrome();
        internal FeaturedSectionFrame(FeaturedSectionLayout layout, FeaturedGameSettings settings, FrameworkElement content) : base(layout,settings,content,true)
        {
            Layout = layout;
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(this,true);
            foreach (var pair in new[] { Tuple.Create(WidthProperty,"Width"),Tuple.Create(HeightProperty,"Height"),Tuple.Create(Canvas.LeftProperty,"X"),Tuple.Create(Canvas.TopProperty,"Y") })
                SetBinding(pair.Item1,new Binding(pair.Item2) { Source = layout });
            var grid = new Grid();
            MoveHandle = Handle(layout.Name,"Drag anywhere to move this section",true);
            Panel.SetZIndex(MoveHandle,10); MoveHandle.Cursor = System.Windows.Input.Cursors.SizeAll;
            MoveHandle.Focusable = true;
            MoveHandle.DragStarted += (sender,args) => MoveHandle.Focus();
            MoveHandle.DragDelta += (sender,args) => MoveBy(args.HorizontalChange,args.VerticalChange);
            grid.Children.Add(MoveHandle);
            var viewport = Viewport = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            grid.Children.Add(viewport);
            editOutline = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false };
            editOutline.SetResourceReference(BorderBrushProperty,"NormalBrush");
            Panel.SetZIndex(editOutline,9); grid.Children.Add(editOutline);
            ResizeHandle = Handle("\u25e2","Drag to resize this section"); ResizeHandle.Width = 18; ResizeHandle.Height = 18;
            ResizeHandle.Cursor = System.Windows.Input.Cursors.SizeNWSE;
            ResizeHandle.HorizontalAlignment = HorizontalAlignment.Right; ResizeHandle.VerticalAlignment = VerticalAlignment.Bottom;
            ResizeHandle.Focusable = true;
            ResizeHandle.DragStarted += (sender,args) => ResizeHandle.Focus();
            ResizeHandle.DragDelta += (sender,args) => ResizeBy(args.HorizontalChange,args.VerticalChange);
            Panel.SetZIndex(ResizeHandle,20); grid.Children.Add(ResizeHandle); Child = grid;
            Loaded += (sender,args) => { if (!subscribed) { layout.PropertyChanged += LayoutChanged; subscribed = true; } };
            Unloaded += (sender,args) => { if (subscribed) { layout.PropertyChanged -= LayoutChanged; subscribed = false; } };
            UpdateChrome();
        }
        private static Thumb Handle(string title, string hint, bool move = false)
        {
            var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetValue(TextBlock.TextProperty,title);
            text.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            text.SetValue(TextBlock.VerticalAlignmentProperty,move ? VerticalAlignment.Top : VerticalAlignment.Center);
            if (move)
            {
                text.SetValue(TextBlock.HorizontalAlignmentProperty,HorizontalAlignment.Right);
                text.SetValue(TextBlock.FontSizeProperty,11d);
                text.SetValue(TextBlock.PaddingProperty,new Thickness(3,0,3,0));
                text.SetResourceReference(TextBlock.BackgroundProperty,"PopupBackgroundBrush");
            }
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.BackgroundProperty,Brushes.Transparent); border.AppendChild(text);
            return new Thumb { ToolTip = hint, Template = new ControlTemplate(typeof(Thumb)) { VisualTree = border } };
        }
        private void UpdateChrome()
        {
            if (MoveHandle == null) return;
            if (!Editing) { MoveHandle.CancelDrag(); ResizeHandle.CancelDrag(); }
            MoveHandle.Visibility = ResizeHandle.Visibility = Editing ? Visibility.Visible : Visibility.Collapsed;
            editOutline.Visibility = Editing ? Visibility.Visible : Visibility.Collapsed;
        }
        private void LayoutChanged(object sender, PropertyChangedEventArgs args) => (Parent as Canvas)?.InvalidateMeasure();
        internal void MoveBy(double x, double y) { if (Editing) { Layout.X += x; Layout.Y += y; } }
        internal void ResizeBy(double width, double height) { if (Editing) { Layout.Width += width; Layout.Height += height; } }
    }
}
