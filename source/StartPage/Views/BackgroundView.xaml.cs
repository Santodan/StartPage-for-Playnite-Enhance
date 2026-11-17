using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace LandingPage.Views
{
    public partial class BackgroundView : UserControl
    {
        public static readonly DependencyProperty IsGlobalBackgroundProperty = DependencyProperty.Register(nameof(IsGlobalBackground),typeof(bool),typeof(BackgroundView),new PropertyMetadata(false,BoundsChanged));
        public bool IsGlobalBackground { get => (bool)GetValue(IsGlobalBackgroundProperty); set => SetValue(IsGlobalBackgroundProperty,value); }
        public static readonly DependencyProperty ExcludeSidebarProperty = DependencyProperty.Register(nameof(ExcludeSidebar),typeof(bool),typeof(BackgroundView),new PropertyMetadata(false,BoundsChanged));
        public bool ExcludeSidebar { get => (bool)GetValue(ExcludeSidebarProperty); set => SetValue(ExcludeSidebarProperty,value); }
        private Window hostWindow;
        private FrameworkElement hostContent,hostSidebar;
        private bool lookedUp;
        public BackgroundView()
        {
            InitializeComponent();
            SetBinding(ExcludeSidebarProperty,new Binding("Settings.Settings.BackgroundAppearance.ExcludeSidebar"));
            Loaded += (s,e) => { lookedUp = false; LayoutUpdated += LayoutChanged; UpdateBackgroundBounds(); };
            Unloaded += (s,e) => { LayoutUpdated -= LayoutChanged; hostWindow = null; hostContent = hostSidebar = null; lookedUp = false; };
        }
        private static void BoundsChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args) => ((BackgroundView)sender).UpdateBackgroundBounds();
        private void LayoutChanged(object sender,EventArgs e) { if(IsGlobalBackground && ExcludeSidebar) UpdateBackgroundBounds(); }
        private static FrameworkElement Part(DependencyObject parent,string name)
        {
            for(var index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++) {
                var child = VisualTreeHelper.GetChild(parent,index);
                if(child is FrameworkElement element && element.Name == name) return element;
                var found = Part(child,name); if(found != null) return found;
            }
            return null;
        }
        private void UpdateBackgroundBounds()
        {
            if(BackgroundViewport == null) return;
            var margin = new Thickness();
            if(IsLoaded && IsGlobalBackground && ExcludeSidebar && ActualWidth>0 && ActualHeight>0) {
                var window = Window.GetWindow(this);
                if(window != hostWindow || (hostContent != null && !hostContent.IsLoaded) || (hostSidebar != null && !hostSidebar.IsLoaded)) { hostWindow = window; lookedUp = false; hostContent = hostSidebar = null; }
                if(!lookedUp && window != null) { hostContent = Part(window,"PART_ContentView"); hostSidebar = Part(window,"PART_Sidebar"); lookedUp = true; }
                try {
                    var area = new Rect(0,0,ActualWidth,ActualHeight);
                    if(hostContent != null && hostContent.ActualWidth>0 && hostContent.ActualHeight>0) {
                        var bounds = hostContent.TransformToVisual(this).TransformBounds(new Rect(hostContent.RenderSize)); bounds.Intersect(area);
                        if(!bounds.IsEmpty) margin = new Thickness(bounds.Left,bounds.Top,Math.Max(0,ActualWidth-bounds.Right),Math.Max(0,ActualHeight-bounds.Bottom));
                    } else if(hostSidebar?.IsVisible == true && hostSidebar.ActualWidth>0 && hostSidebar.ActualHeight>0) {
                        var bounds = hostSidebar.TransformToVisual(this).TransformBounds(new Rect(hostSidebar.RenderSize)); bounds.Intersect(area);
                        if(!bounds.IsEmpty) {
                            switch(DockPanel.GetDock(hostSidebar)) {
                                case Dock.Left: margin.Left = bounds.Right; break;
                                case Dock.Right: margin.Right = ActualWidth-bounds.Left; break;
                                case Dock.Top: margin.Top = bounds.Bottom; break;
                                case Dock.Bottom: margin.Bottom = ActualHeight-bounds.Top; break;
                            }
                        }
                    }
                } catch(InvalidOperationException) { }
            }
            var old = BackgroundViewport.Margin;
            if(Math.Abs(old.Left-margin.Left)>.1 || Math.Abs(old.Top-margin.Top)>.1 || Math.Abs(old.Right-margin.Right)>.1 || Math.Abs(old.Bottom-margin.Bottom)>.1) BackgroundViewport.Margin = margin;
        }
    }
}
