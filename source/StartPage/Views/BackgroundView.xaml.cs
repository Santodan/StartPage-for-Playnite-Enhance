using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.ComponentModel;
using LandingPage.Settings;

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
        public static readonly DependencyProperty LineAppearanceProperty = DependencyProperty.Register("LineAppearance",typeof(BackgroundAppearanceSettings),typeof(BackgroundView),new PropertyMetadata(null,(s,e) => { var view = (BackgroundView)s; if(view.IsLoaded) view.ObserveLines(); }));
        private BackgroundAppearanceSettings lineSettings;
        private FrameworkElement lineToolbar;
        private bool lineToolbarLookedUp,lineSidebarLookedUp;
        private FrameworkElement[] captionButtons;
        private Border nativeSidebarBorder;
        private object savedBorderBrush;
        private BindingBase savedBorderBinding;
        private void RestoreSidebarBorder()
        {
            if(nativeSidebarBorder == null) return;
            if(savedBorderBinding != null) BindingOperations.SetBinding(nativeSidebarBorder,Border.BorderBrushProperty,savedBorderBinding);
            else if(savedBorderBrush == DependencyProperty.UnsetValue) nativeSidebarBorder.ClearValue(Border.BorderBrushProperty);
            else nativeSidebarBorder.SetCurrentValue(Border.BorderBrushProperty,savedBorderBrush);
            nativeSidebarBorder = null; savedBorderBinding = null; savedBorderBrush = null;
        }
        private void MountSeparatorOverlay()
        {
            if(!IsGlobalBackground || !(Parent is Grid host)) return;
            if(SeparatorCanvas.Parent is Panel old && old != host) old.Children.Remove(SeparatorCanvas);
            if(SeparatorCanvas.Parent == null) {
                Panel.SetZIndex(SeparatorCanvas,int.MaxValue);
                SeparatorCanvas.SetBinding(VisibilityProperty,new Binding("Visibility") { Source = this });
                SeparatorCanvas.SetBinding(OpacityProperty,new Binding("Opacity") { Source = this });
                host.Children.Add(SeparatorCanvas);
            }
        }
        private readonly System.Windows.Shapes.Rectangle sidebarLine = new System.Windows.Shapes.Rectangle();
        private readonly System.Windows.Shapes.Rectangle topLine = new System.Windows.Shapes.Rectangle();
        private void ObserveLines()
        {
            if(lineSettings != null) PropertyChangedEventManager.RemoveHandler(lineSettings,LineSettingsChanged,string.Empty);
            lineSettings = GetValue(LineAppearanceProperty) as BackgroundAppearanceSettings;
            if(lineSettings != null) PropertyChangedEventManager.AddHandler(lineSettings,LineSettingsChanged,string.Empty);
            UpdateBackgroundBounds();
        }
        private void LineSettingsChanged(object sender,PropertyChangedEventArgs e)
        {
            UpdateBackgroundBounds();
            if(IsGlobalBackground) LandingPageExtension.Instance?.UpdateGlobalBackgroundVisibility();
        }
        public BackgroundView()
        {
            InitializeComponent();
            SeparatorCanvas.Children.Add(sidebarLine); SeparatorCanvas.Children.Add(topLine);
            DataContextChanged += (s,e) => { if(IsLoaded) ObserveLines(); };
            SetBinding(LineAppearanceProperty,new Binding("Settings.Settings.BackgroundAppearance"));
            SetBinding(ArtworkEnabledProperty,new Binding("Settings.Settings.EnableGlobalBackground"));
            SetBinding(ExcludeSidebarProperty,new Binding("Settings.Settings.BackgroundAppearance.ExcludeSidebar"));
            IsVisibleChanged += (s,e) => { if(!IsVisible) RestoreSidebarBorder(); else { lineSidebarLookedUp = false; UpdateBackgroundBounds(); } };
            Loaded += (s,e) => { MountSeparatorOverlay(); lookedUp = false; lineToolbarLookedUp = lineSidebarLookedUp = false; captionButtons = null; LayoutUpdated += LayoutChanged; ObserveLines(); };
            Unloaded += (s,e) => { RestoreSidebarBorder(); if(IsGlobalBackground && SeparatorCanvas.Parent is Panel parent) parent.Children.Remove(SeparatorCanvas); LayoutUpdated -= LayoutChanged; if(lineSettings != null) PropertyChangedEventManager.RemoveHandler(lineSettings,LineSettingsChanged,string.Empty); lineSettings = null; captionButtons = null; hostWindow = null; hostContent = hostSidebar = lineToolbar = null; lookedUp = lineSidebarLookedUp = false; };
        }
        public static readonly DependencyProperty ArtworkEnabledProperty = DependencyProperty.Register("ArtworkEnabled",typeof(bool),typeof(BackgroundView),new PropertyMetadata(true,BoundsChanged));
        private static void BoundsChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args) => ((BackgroundView)sender).UpdateBackgroundBounds();
        private void LayoutChanged(object sender,EventArgs e) { if(IsGlobalBackground) UpdateBackgroundBounds(); }
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
            BackgroundViewport.Visibility = !IsGlobalBackground || (bool)GetValue(ArtworkEnabledProperty) ? Visibility.Visible : Visibility.Collapsed;
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
            UpdateLines();
        }
        private void UpdateLines()
        {
            if(SeparatorCanvas == null) return;
            if(!IsLoaded || !IsVisible || !IsGlobalBackground || lineSettings == null || (!lineSettings.ShowSidebarLine && !lineSettings.ShowTopBarLine)) { RestoreSidebarBorder(); sidebarLine.Visibility = topLine.Visibility = Visibility.Collapsed; return; }
            var window = Window.GetWindow(this); if(window == null) return;
            // LayoutUpdated fires constantly; walk the window tree only once (or after the sidebar unloads),
            // otherwise themes without PART_Sidebar pay a full visual-tree search on every layout pass.
            if(hostSidebar != null && !hostSidebar.IsLoaded) { hostSidebar = null; lineSidebarLookedUp = false; }
            if(hostSidebar == null && !lineSidebarLookedUp) { hostSidebar = Part(window,"PART_Sidebar"); lineSidebarLookedUp = true; }
            var sidebar = hostSidebar;
            var border = sidebar == null ? null : Part(sidebar,"BorderContentHolder") as Border;
            if(!lineSettings.ShowSidebarLine || sidebar?.IsVisible != true || border != nativeSidebarBorder) RestoreSidebarBorder();
            if(lineSettings.ShowSidebarLine && sidebar?.IsVisible == true && border != null && nativeSidebarBorder == null) {
                nativeSidebarBorder = border;
                savedBorderBrush = border.ReadLocalValue(Border.BorderBrushProperty) == DependencyProperty.UnsetValue ? DependencyProperty.UnsetValue : border.GetValue(Border.BorderBrushProperty);
                savedBorderBinding = BindingOperations.GetBindingBase(border,Border.BorderBrushProperty);
                border.SetCurrentValue(Border.BorderBrushProperty,Brushes.Transparent);
            }
            sidebarLine.Visibility = lineSettings.ShowSidebarLine && sidebar?.IsVisible == true && sidebar.ActualWidth>0 && sidebar.ActualHeight>0 ? Visibility.Visible : Visibility.Collapsed;
            if(!lineSettings.ShowTopBarLine) topLine.Visibility = Visibility.Collapsed;
            var left = 0d; var right = ActualWidth;
            try {
                if(sidebar?.IsVisible == true && sidebar.ActualWidth>0 && sidebar.ActualHeight>0) {
                    var rect = sidebar.TransformToVisual(this).TransformBounds(new Rect(sidebar.RenderSize));
                    var thickness = lineSettings.SidebarLineThickness;
                    var dock = DockPanel.GetDock(sidebar);
                    if(dock == Dock.Left) { left = rect.Right + (lineSettings.ShowSidebarLine ? thickness/2 : 0); rect = new Rect(rect.Right-thickness/2,rect.Top,thickness,rect.Height); }
                    else if(dock == Dock.Right) { right = rect.Left - (lineSettings.ShowSidebarLine ? thickness/2 : 0); rect = new Rect(rect.Left-thickness/2,rect.Top,thickness,rect.Height); }
                    else rect = new Rect(rect.Left,(dock == Dock.Top ? rect.Bottom : rect.Top)-thickness/2,rect.Width,thickness);
                    if(lineSettings.ShowSidebarLine) DrawLine(sidebarLine,rect,lineSettings.SidebarLineColor);
                }
                if(lineSettings.ShowTopBarLine) {
                    if(!lineToolbarLookedUp || (lineToolbar != null && !lineToolbar.IsLoaded)) { lineToolbar = Controls.NativeToolbarCatalog.FindNativeTopPanel(window); lineToolbarLookedUp = true; }
                    var toolbar = lineToolbar;
                    var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
                    var y = chrome == null ? 0 : Math.Max(32,chrome.CaptionHeight);
                    if(captionButtons == null) captionButtons = new[] { Part(window,"PART_ButtonClose"),Part(window,"PART_ButtonMaximize"),Part(window,"PART_ButtonMinimize") };
                    // CaptionHeight describes the drag area, not the painted theme buttons.
                    // Use the visible buttons' actual bounds so the separator cannot cross them.
                    foreach(var button in captionButtons) {
                        if(button?.IsVisible == true && button.ActualWidth>0 && button.ActualHeight>0)
                            y = Math.Max(y,button.TransformToVisual(this).TransformBounds(new Rect(button.RenderSize)).Bottom);
                    }
                    if(toolbar?.IsVisible == true && Window.GetWindow(toolbar) == window && toolbar.ActualHeight>0 && toolbar.ActualHeight<=160)
                        y = Math.Max(y,toolbar.TransformToVisual(this).TransformBounds(new Rect(toolbar.RenderSize)).Bottom);
                    if(y<=0 || y>=ActualHeight || right<=left) topLine.Visibility = Visibility.Collapsed;
                    else DrawLine(topLine,new Rect(left,y,right-left,lineSettings.TopBarLineThickness),lineSettings.TopBarLineColor);
                }
            } catch(InvalidOperationException) { }
        }
        private void DrawLine(System.Windows.Shapes.Rectangle line,Rect bounds,string color)
        {
            line.Visibility = Visibility.Visible;
            if(line.Width != bounds.Width) line.Width = bounds.Width;
            if(line.Height != bounds.Height) line.Height = bounds.Height;
            if(Canvas.GetLeft(line) != bounds.Left) Canvas.SetLeft(line,bounds.Left);
            if(Canvas.GetTop(line) != bounds.Top) Canvas.SetTop(line,bounds.Top);
            var value = (Color)ColorConverter.ConvertFromString(color);
            if(!(line.Fill is SolidColorBrush brush) || brush.Color != value) { var fill = new SolidColorBrush(value); fill.Freeze(); line.Fill = fill; }
            line.Opacity = lineSettings.LineOpacity;
        }
    }
}
