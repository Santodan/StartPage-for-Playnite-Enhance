using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace LandingPage.Controls
{
    public partial class ColorPicker : UserControl
    {
        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
            nameof(SelectedColor), typeof(string), typeof(ColorPicker),
            new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (sender, args) => ((ColorPicker)sender).RefreshColor()), IsValidColor);

        public string SelectedColor
        {
            get => (string)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private bool updating;

        public ColorPicker()
        {
            InitializeComponent();
            HexInput.SetBinding(TextBox.TextProperty, new Binding(nameof(SelectedColor))
            {
                Source = this, Mode = BindingMode.TwoWay, ValidatesOnExceptions = true
            });
            foreach (var slider in new[] { Red, Green, Blue, Alpha }) slider.ValueChanged += ChannelChanged;
            foreach (var hex in new[] { "#FFFFFF", "#C0C0C0", "#808080", "#202020", "#000000", "#E53935",
                "#FB8C00", "#FDD835", "#43A047", "#00ACC1", "#1E88E5", "#3949AB", "#8E24AA", "#D81B60", "#795548", "#00000000" })
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var button = new Button
                {
                    Content = new Border { Background = new SolidColorBrush(color), Width = 24, Height = 24 },
                    Padding = new Thickness(2), Margin = new Thickness(2), ToolTip = hex
                };
                button.Click += (sender, args) => SetCurrentValue(SelectedColorProperty, hex);
                Palette.Children.Add(button);
            }
            RefreshColor();
        }

        private static bool IsValidColor(object value)
        {
            try { return ColorConverter.ConvertFromString(value as string ?? string.Empty) is Color; }
            catch (Exception) { return false; }
        }

        private void RefreshColor()
        {
            if (Preview == null) return;
            var color = (Color)ColorConverter.ConvertFromString(SelectedColor);
            updating = true;
            try
            {
                Preview.Background = new SolidColorBrush(color);
                ColorLabel.Text = SelectedColor;
                Red.Value = color.R;
                Green.Value = color.G;
                Blue.Value = color.B;
                Alpha.Value = color.A;
            }
            finally { updating = false; }
        }

        private void ChannelChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (updating) return;
            var color = Color.FromArgb((byte)Alpha.Value, (byte)Red.Value, (byte)Green.Value, (byte)Blue.Value);
            SetCurrentValue(SelectedColorProperty, color.ToString());
        }
    }
}
