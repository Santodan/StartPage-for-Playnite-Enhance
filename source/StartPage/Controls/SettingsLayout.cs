using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Gu.Wpf.NumericInput;

namespace LandingPage.Controls
{
    // Shared building blocks for widget settings editors, so every editor uses the same labels, spacing,
    // numeric boxes, sliders, formatting buttons and responsive two-column layout.
    internal static class SettingsLayout
    {
        internal const double NumberWidth = 64;
        internal static readonly Thickness LabelMargin = new Thickness(0,6,0,2);
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject,System.Collections.Generic.Dictionary<DependencyProperty,ValueProxy>> proxies =
            new System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject,System.Collections.Generic.Dictionary<DependencyProperty,ValueProxy>>();
        // Two-way link from an inner control to one of the owner's properties. Inner controls must not bind
        // straight to the owner: their write-back would be a SetValue that replaces a binding applied by a
        // DataTemplate (template values rank below local values), silently disconnecting the setting.
        // The proxy writes with SetCurrentValue, which keeps any binding and still updates its source.
        internal static Binding Bind(DependencyObject owner,DependencyProperty property)
        {
            var map = proxies.GetOrCreateValue(owner);
            if (!map.TryGetValue(property,out var proxy)) map[property] = proxy = new ValueProxy(owner,property);
            return new Binding(nameof(ValueProxy.Value)) { Source = proxy, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
        }
        // Owners call this from OnPropertyChanged so inner controls follow changes from the bound setting.
        internal static void Notify(DependencyObject owner,DependencyProperty property) { if (proxies.TryGetValue(owner,out var map) && map.TryGetValue(property,out var proxy)) proxy.Changed(); }
        internal sealed class ValueProxy : System.ComponentModel.INotifyPropertyChanged
        {
            private readonly DependencyObject owner; private readonly DependencyProperty property;
            internal ValueProxy(DependencyObject owner,DependencyProperty property) { this.owner = owner; this.property = property; }
            public object Value
            {
                get => owner.GetValue(property);
                set
                {
                    if (value == null && property.PropertyType.IsValueType) return; // Empty numeric box: keep the current value.
                    if (value != null && !property.PropertyType.IsInstanceOfType(value)) value = Convert.ChangeType(value,property.PropertyType,System.Globalization.CultureInfo.InvariantCulture);
                    if (!Equals(owner.GetValue(property),value)) owner.SetCurrentValue(property,value);
                }
            }
            internal void Changed() => PropertyChanged?.Invoke(this,new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        }
        internal static DoubleBox Number(DependencyObject owner,DependencyProperty value,DependencyProperty min,DependencyProperty max,string name)
        {
            var box = new DoubleBox { Width = NumberWidth, VerticalAlignment = VerticalAlignment.Center, ToolTip = name };
            box.SetResourceReference(FrameworkElement.StyleProperty,typeof(TextBox)); AutomationProperties.SetName(box,name);
            box.SetBinding(DoubleBox.ValueProperty,Bind(owner,value));
            box.SetBinding(DoubleBox.MinValueProperty,new Binding(min.Name) { Source = owner }); box.SetBinding(DoubleBox.MaxValueProperty,new Binding(max.Name) { Source = owner });
            return box;
        }
        internal static ToggleButton FormatToggle(string symbol,string name)
        {
            var text = new TextBlock { Text = symbol, FontFamily = new FontFamily("Segoe UI"), FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                FontWeight = symbol == "B" ? FontWeights.Bold : FontWeights.Normal, FontStyle = symbol == "I" ? FontStyles.Italic : FontStyles.Normal };
            text.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
            var button = new ToggleButton { Content = text, Width = 32, Height = 28, Margin = new Thickness(4,0,0,0), VerticalAlignment = VerticalAlignment.Center, ToolTip = name };
            AutomationProperties.SetName(button,name); return button;
        }
    }

    // A label above one control: <controls:SettingsField Label="Font size"><ComboBox/></controls:SettingsField>
    public class SettingsField : StackPanel
    {
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label),typeof(string),typeof(SettingsField),new PropertyMetadata(null,(d,e) => ((SettingsField)d).UpdateLabel()));
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty,value); }
        public TextBlock LabelBlock { get; } = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = SettingsLayout.LabelMargin, Visibility = Visibility.Collapsed };
        public SettingsField() { LabelBlock.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); Children.Add(LabelBlock); }
        private void UpdateLabel() { LabelBlock.Text = Label; LabelBlock.Visibility = string.IsNullOrEmpty(Label) ? Visibility.Collapsed : Visibility.Visible; }
    }

    // Short help text below a field, smaller and dimmer than labels.
    public class SettingsHint : TextBlock
    {
        public SettingsHint() { TextWrapping = TextWrapping.Wrap; Margin = new Thickness(0,3,0,0); Opacity = .75; SetResourceReference(ForegroundProperty,"TextBrush"); SetResourceReference(FontSizeProperty,"FontSizeSmall"); }
    }

    // Full-width labeled slider with a numeric box for exact values.
    public class SettingsSlider : Grid
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value),typeof(double),typeof(SettingsSlider),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum),typeof(double),typeof(SettingsSlider),new PropertyMetadata(0d));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum),typeof(double),typeof(SettingsSlider),new PropertyMetadata(1d));
        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step),typeof(double),typeof(SettingsSlider),new PropertyMetadata(.01));
        public static readonly DependencyProperty PrecisionProperty = DependencyProperty.Register(nameof(Precision),typeof(int),typeof(SettingsSlider),new PropertyMetadata(2));
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label),typeof(string),typeof(SettingsSlider),new PropertyMetadata(null,(d,e) => ((SettingsSlider)d).UpdateLabel()));
        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty,value); }
        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty,value); }
        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty,value); }
        public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty,value); }
        public int Precision { get => (int)GetValue(PrecisionProperty); set => SetValue(PrecisionProperty,value); }
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty,value); }
        public TextBlock LabelBlock { get; } = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = SettingsLayout.LabelMargin, Visibility = Visibility.Collapsed };
        public Slider Slider { get; } = new Slider { IsSnapToTickEnabled = true, AutoToolTipPlacement = AutoToolTipPlacement.TopLeft, VerticalAlignment = VerticalAlignment.Center };
        public DoubleBox NumberBox { get; }
        public SettingsSlider()
        {
            ColumnDefinitions.Add(new ColumnDefinition()); ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            LabelBlock.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush"); SetColumnSpan(LabelBlock,2); Children.Add(LabelBlock);
            Slider.SetBinding(RangeBase.ValueProperty,SettingsLayout.Bind(this,ValueProperty));
            Slider.SetBinding(RangeBase.MinimumProperty,new Binding(nameof(Minimum)) { Source = this }); Slider.SetBinding(RangeBase.MaximumProperty,new Binding(nameof(Maximum)) { Source = this });
            Slider.SetBinding(Slider.TickFrequencyProperty,new Binding(nameof(Step)) { Source = this }); Slider.SetBinding(RangeBase.SmallChangeProperty,new Binding(nameof(Step)) { Source = this });
            Slider.SetBinding(RangeBase.LargeChangeProperty,new Binding(nameof(Step)) { Source = this, Converter = TimesTen.Instance });
            Slider.SetBinding(Slider.AutoToolTipPrecisionProperty,new Binding(nameof(Precision)) { Source = this });
            SetRow(Slider,1); Children.Add(Slider);
            NumberBox = SettingsLayout.Number(this,ValueProperty,MinimumProperty,MaximumProperty,"Value"); NumberBox.Margin = new Thickness(8,0,0,0);
            SetRow(NumberBox,1); SetColumn(NumberBox,1); Children.Add(NumberBox);
        }
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) { base.OnPropertyChanged(e); SettingsLayout.Notify(this,e.Property); }
        private void UpdateLabel() { LabelBlock.Text = Label; LabelBlock.Visibility = string.IsNullOrEmpty(Label) ? Visibility.Collapsed : Visibility.Visible; AutomationProperties.SetName(Slider,Label ?? ""); AutomationProperties.SetName(NumberBox,Label ?? "Value"); NumberBox.ToolTip = Label; }
        private sealed class TimesTen : IValueConverter
        {
            internal static readonly TimesTen Instance = new TimesTen();
            public object Convert(object value,Type type,object parameter,System.Globalization.CultureInfo culture) => value is double step ? step*10 : 1d;
            public object ConvertBack(object value,Type type,object parameter,System.Globalization.CultureInfo culture) => Binding.DoNothing;
        }
    }

    // Font size, optional bold/italic toggles and a labeled color swatch on one wrapping row.
    public class TextFormatBar : WrapPanel
    {
        private static FrameworkPropertyMetadata TwoWay(object value) => new FrameworkPropertyMetadata(value,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        public static readonly DependencyProperty TextSizeProperty = DependencyProperty.Register(nameof(TextSize),typeof(double),typeof(TextFormatBar),TwoWay(14d));
        public static readonly DependencyProperty MinSizeProperty = DependencyProperty.Register(nameof(MinSize),typeof(double),typeof(TextFormatBar),new PropertyMetadata(1d));
        public static readonly DependencyProperty MaxSizeProperty = DependencyProperty.Register(nameof(MaxSize),typeof(double),typeof(TextFormatBar),new PropertyMetadata(400d));
        public static readonly DependencyProperty BoldProperty = DependencyProperty.Register(nameof(Bold),typeof(bool),typeof(TextFormatBar),TwoWay(false));
        public static readonly DependencyProperty ItalicProperty = DependencyProperty.Register(nameof(Italic),typeof(bool),typeof(TextFormatBar),TwoWay(false));
        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color),typeof(string),typeof(TextFormatBar),TwoWay(null));
        public static readonly DependencyProperty UseCustomColorProperty = DependencyProperty.Register(nameof(UseCustomColor),typeof(bool),typeof(TextFormatBar),TwoWay(false));
        public static readonly DependencyProperty ShowStyleProperty = DependencyProperty.Register(nameof(ShowStyle),typeof(bool),typeof(TextFormatBar),new PropertyMetadata(true,(d,e) => ((TextFormatBar)d).UpdateParts()));
        public static readonly DependencyProperty ShowColorProperty = DependencyProperty.Register(nameof(ShowColor),typeof(bool),typeof(TextFormatBar),new PropertyMetadata(true,(d,e) => ((TextFormatBar)d).UpdateParts()));
        public static readonly DependencyProperty ColorLabelProperty = DependencyProperty.Register(nameof(ColorLabel),typeof(string),typeof(TextFormatBar),new PropertyMetadata("Color"));
        public double TextSize { get => (double)GetValue(TextSizeProperty); set => SetValue(TextSizeProperty,value); }
        public double MinSize { get => (double)GetValue(MinSizeProperty); set => SetValue(MinSizeProperty,value); }
        public double MaxSize { get => (double)GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty,value); }
        public bool Bold { get => (bool)GetValue(BoldProperty); set => SetValue(BoldProperty,value); }
        public bool Italic { get => (bool)GetValue(ItalicProperty); set => SetValue(ItalicProperty,value); }
        public string Color { get => (string)GetValue(ColorProperty); set => SetValue(ColorProperty,value); }
        public bool UseCustomColor { get => (bool)GetValue(UseCustomColorProperty); set => SetValue(UseCustomColorProperty,value); }
        public bool ShowStyle { get => (bool)GetValue(ShowStyleProperty); set => SetValue(ShowStyleProperty,value); }
        public bool ShowColor { get => (bool)GetValue(ShowColorProperty); set => SetValue(ShowColorProperty,value); }
        public string ColorLabel { get => (string)GetValue(ColorLabelProperty); set => SetValue(ColorLabelProperty,value); }
        public DoubleBox SizeBox { get; }
        public ToggleButton BoldButton { get; } = SettingsLayout.FormatToggle("B","Bold");
        public ToggleButton ItalicButton { get; } = SettingsLayout.FormatToggle("I","Italic");
        public PopupColorPicker ColorPicker { get; } = new PopupColorPicker { Margin = new Thickness(8,0,0,0), VerticalAlignment = VerticalAlignment.Center };
        public TextFormatBar()
        {
            SizeBox = SettingsLayout.Number(this,TextSizeProperty,MinSizeProperty,MaxSizeProperty,"Size");
            BoldButton.SetBinding(ToggleButton.IsCheckedProperty,SettingsLayout.Bind(this,BoldProperty)); ItalicButton.SetBinding(ToggleButton.IsCheckedProperty,SettingsLayout.Bind(this,ItalicProperty));
            ColorPicker.SetBinding(PopupColorPicker.SelectedColorProperty,SettingsLayout.Bind(this,ColorProperty)); ColorPicker.SetBinding(PopupColorPicker.UseCustomColorProperty,SettingsLayout.Bind(this,UseCustomColorProperty));
            ColorPicker.SetBinding(PopupColorPicker.LabelProperty,new Binding(nameof(ColorLabel)) { Source = this });
            foreach (var part in new FrameworkElement[] { SizeBox,BoldButton,ItalicButton,ColorPicker }) Children.Add(part);
        }
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) { base.OnPropertyChanged(e); SettingsLayout.Notify(this,e.Property); }
        private void UpdateParts()
        {
            BoldButton.Visibility = ItalicButton.Visibility = ShowStyle ? Visibility.Visible : Visibility.Collapsed;
            ColorPicker.Visibility = ShowColor ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // Responsive grid for related fields: two (or Columns) equal columns, falling back to fewer columns when
    // each would be narrower than MinColumnWidth. Children with SettingsColumns.Span="True" take a full row.
    public class SettingsColumns : Panel
    {
        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns),typeof(int),typeof(SettingsColumns),new FrameworkPropertyMetadata(2,FrameworkPropertyMetadataOptions.AffectsMeasure),value => (int)value >= 1);
        public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(nameof(ColumnSpacing),typeof(double),typeof(SettingsColumns),new FrameworkPropertyMetadata(12d,FrameworkPropertyMetadataOptions.AffectsMeasure));
        public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(nameof(MinColumnWidth),typeof(double),typeof(SettingsColumns),new FrameworkPropertyMetadata(150d,FrameworkPropertyMetadataOptions.AffectsMeasure));
        public static readonly DependencyProperty SpanProperty = DependencyProperty.RegisterAttached("Span",typeof(bool),typeof(SettingsColumns),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsParentMeasure));
        public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty,value); }
        public double ColumnSpacing { get => (double)GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty,value); }
        public double MinColumnWidth { get => (double)GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty,value); }
        public static bool GetSpan(DependencyObject element) => (bool)element.GetValue(SpanProperty);
        public static void SetSpan(DependencyObject element,bool value) => element.SetValue(SpanProperty,value);
        // Number of columns actually used by the last layout pass (1 when the editor is narrow).
        public int ActiveColumns { get; private set; } = 2;

        private int ColumnsFor(double width) => double.IsInfinity(width) ? Columns : Math.Max(1,Math.Min(Columns,(int)Math.Floor((width+ColumnSpacing)/(MinColumnWidth+ColumnSpacing))));
        private double ColumnWidth(double width,int columns) => Math.Max(0,(width-ColumnSpacing*(columns-1))/columns);

        protected override Size MeasureOverride(Size available)
        {
            var columns = ActiveColumns = ColumnsFor(available.Width); var infinite = double.IsInfinity(available.Width);
            var cell = infinite ? double.PositiveInfinity : ColumnWidth(available.Width,columns); var full = available.Width;
            foreach (UIElement child in InternalChildren) child.Measure(new Size(GetSpan(child) || columns == 1 ? full : cell,double.PositiveInfinity));
            if (infinite) cell = InternalChildren.Cast<UIElement>().Where(child => !GetSpan(child)).Select(child => child.DesiredSize.Width).DefaultIfEmpty(0).Max();
            var width = infinite ? Math.Max(cell*columns+ColumnSpacing*(columns-1),InternalChildren.Cast<UIElement>().Where(GetSpan).Select(child => child.DesiredSize.Width).DefaultIfEmpty(0).Max()) : available.Width;
            return new Size(width,Layout(columns,cell,width,null));
        }
        protected override Size ArrangeOverride(Size final)
        {
            var columns = ActiveColumns = ColumnsFor(final.Width); Layout(columns,ColumnWidth(final.Width,columns),final.Width,true); return final;
        }
        // Rows hold up to `columns` cells; a spanning or single-column child occupies its own row.
        private double Layout(int columns,double cell,double width,bool? arrange)
        {
            double top = 0, rowHeight = 0; var column = 0; var row = new System.Collections.Generic.List<UIElement>();
            void Flush() { if (arrange == true) for (var i = 0; i < row.Count; i++) row[i].Arrange(new Rect(i*(cell+ColumnSpacing),top,cell,rowHeight)); top += rowHeight; rowHeight = 0; column = 0; row.Clear(); }
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed) { if (arrange == true) child.Arrange(new Rect(0,top,0,0)); continue; }
                if (GetSpan(child) || columns == 1) { if (row.Count > 0) Flush(); if (arrange == true) child.Arrange(new Rect(0,top,width,child.DesiredSize.Height)); top += child.DesiredSize.Height; continue; }
                row.Add(child); rowHeight = Math.Max(rowHeight,child.DesiredSize.Height); if (++column == columns) Flush();
            }
            if (row.Count > 0) Flush();
            return top;
        }
    }
}
