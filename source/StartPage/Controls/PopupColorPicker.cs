using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace LandingPage.Controls
{
    public sealed class PopupColorPicker : UserControl
    {
        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(nameof(SelectedColor),typeof(string),typeof(PopupColorPicker),
            new FrameworkPropertyMetadata("#FFFFFF",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,Changed),ValidColor);
        public static readonly DependencyProperty UseCustomColorProperty = DependencyProperty.Register(nameof(UseCustomColor),typeof(bool),typeof(PopupColorPicker),
            new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,Changed));
        public static readonly DependencyProperty ShowCustomColorOptionProperty = DependencyProperty.Register(nameof(ShowCustomColorOption),typeof(bool),typeof(PopupColorPicker),new PropertyMetadata(true,Changed));
        public static readonly DependencyProperty CustomColorLabelProperty = DependencyProperty.Register(nameof(CustomColorLabel),typeof(string),typeof(PopupColorPicker),new PropertyMetadata("Use custom color"));
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label),typeof(string),typeof(PopupColorPicker),new PropertyMetadata("Color"));
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty,value); }
        public string SelectedColor { get => (string)GetValue(SelectedColorProperty); set => SetValue(SelectedColorProperty,value); }
        public bool UseCustomColor { get => (bool)GetValue(UseCustomColorProperty); set => SetValue(UseCustomColorProperty,value); }
        public bool ShowCustomColorOption { get => (bool)GetValue(ShowCustomColorOptionProperty); set => SetValue(ShowCustomColorOptionProperty,value); }
        public string CustomColorLabel { get => (string)GetValue(CustomColorLabelProperty); set => SetValue(CustomColorLabelProperty,value); }
        private readonly Border swatch = new Border { Width = 26, Height = 18, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray };
        private readonly Button button = new Button { Padding = new Thickness(4), MinWidth = 38, Height = 28 };
        private readonly Popup popup = new Popup { Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade };
        private CheckBox custom;
        private ScrollViewer editorViewport;
        private Window owner;
        public PopupColorPicker()
        {
            var root = new Grid();
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
            label.SetBinding(TextBlock.TextProperty,new Binding(nameof(Label)) { Source = this });
            label.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            row.Children.Add(label); button.Content = swatch; Grid.SetColumn(button,1); row.Children.Add(button);
            root.Children.Add(row); root.Children.Add(popup); Content = root;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            SetResourceReference(Control.ForegroundProperty,"TextBrush");
            System.Windows.Automation.AutomationProperties.SetName(button,"Choose text color");
            popup.PlacementTarget = button;
            button.Click += (s,e) => { if (popup.IsOpen) popup.IsOpen = false; else Open(); e.Handled = true; };
            popup.Closed += (s,e) => DetachOwner();
            Unloaded += (s,e) => { popup.IsOpen = false; DetachOwner(); };
            IsVisibleChanged += (s,e) => { if (!IsVisible) popup.IsOpen = false; };
            UpdatePreview();
        }
        private static bool ValidColor(object value) { try { return ColorConverter.ConvertFromString(value as string ?? "") is Color; } catch { return false; } }
        public event RoutedEventHandler CustomColorChanged;
        private static void Changed(DependencyObject sender,DependencyPropertyChangedEventArgs e)
        {
            var picker = (PopupColorPicker)sender;
            picker.UpdatePreview();

        }
        private void UpdatePreview()
        {
            if (swatch == null) return;
            swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedColor));
            swatch.Opacity = !ShowCustomColorOption || UseCustomColor ? 1 : .45;
            button.ToolTip = "Choose color: " + SelectedColor + (ShowCustomColorOption && !UseCustomColor ? " (theme/default in use)" : "");
            if (custom != null) custom.Visibility = ShowCustomColorOption ? Visibility.Visible : Visibility.Collapsed;
            if (editorViewport != null) editorViewport.IsEnabled = !ShowCustomColorOption || UseCustomColor;
        }
        private void Open()
        {
            if (popup.Child == null) BuildEditor();
            owner = Window.GetWindow(this);
            if (owner != null) { owner.Deactivated += OwnerDeactivated; editorViewport.MaxHeight = Math.Max(140,Math.Min(420,owner.ActualHeight-100)); }
            UpdatePreview(); popup.IsOpen = true;
        }
        private void BuildEditor()
        {
            var panel = new StackPanel();
            var title = new TextBlock { Text = "Choose color", FontSize = 16, Margin = new Thickness(0,0,0,8) }; title.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); panel.Children.Add(title);
            custom = new CheckBox { Margin = new Thickness(0,0,0,8) };
            custom.SetResourceReference(Control.ForegroundProperty,"TextBrush");
            custom.SetBinding(ContentControl.ContentProperty,new Binding(nameof(CustomColorLabel)) { Source = this });
            custom.SetBinding(ToggleButton.IsCheckedProperty,new Binding(nameof(UseCustomColor)) { Source = this, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            custom.Click += (s,e) => CustomColorChanged?.Invoke(this,new RoutedEventArgs());
            panel.Children.Add(custom);
            var picker = new ColorPicker();
            // Reuse the existing themed palette/RGBA/hex editor, without its outer expander.
            var expander = (Expander)picker.Content; var body = expander.Content; expander.Content = null; picker.Content = body;
            picker.SetBinding(ColorPicker.SelectedColorProperty,new Binding(nameof(SelectedColor)) { Source = this, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            editorViewport = new ScrollViewer { Content = picker, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 420 };
            panel.Children.Add(editorViewport);
            var done = new Button { Content = "Done", Margin = new Thickness(0,8,0,0) }; done.Click += (s,e) => { popup.IsOpen = false; button.Focus(); }; panel.Children.Add(done);
            var border = new Border { Child = panel, Width = 300, Padding = new Thickness(12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Background = new SolidColorBrush(Color.FromRgb(32,32,32)), BorderBrush = Brushes.Gray };
            if (TryFindResource("PopupBackgroundBrush") != null) border.SetResourceReference(Border.BackgroundProperty,"PopupBackgroundBrush");
            if (TryFindResource("PopupBorderBrush") != null) border.SetResourceReference(Border.BorderBrushProperty,"PopupBorderBrush");
            border.PreviewKeyDown += (s,e) => { if (e.Key == Key.Escape) { popup.IsOpen = false; button.Focus(); e.Handled = true; } };
            popup.Child = border;
        }
        private void OwnerDeactivated(object sender,EventArgs e) => popup.IsOpen = false;
        private void DetachOwner() { if (owner != null) owner.Deactivated -= OwnerDeactivated; owner = null; }
    }
}
