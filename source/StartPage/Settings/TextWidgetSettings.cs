using System;
using System.Windows;
using System.Windows.Media;

namespace LandingPage.Settings
{
    public class TextWidgetSettings : ObservableObjectExt
    {
        private string text = "Your text";
        public string Text { get => text; set => SetValue(ref text, value ?? string.Empty); }
        private double fontSize = 24;
        public double FontSize { get => fontSize; set => SetValue(ref fontSize,
            double.IsNaN(value) || double.IsInfinity(value) ? 24 : Math.Max(8, Math.Min(200, value))); }
        private bool bold;
        public bool Bold { get => bold; set => SetValue(ref bold, value); }
        private bool italic;
        public bool Italic { get => italic; set => SetValue(ref italic, value); }
        private bool wrap = true;
        public bool Wrap { get => wrap; set => SetValue(ref wrap, value); }
        private TextAlignment alignment = TextAlignment.Left;
        public TextAlignment Alignment { get => alignment; set => SetValue(ref alignment, value); }
        private VerticalAlignment verticalAlignment = VerticalAlignment.Top;
        public VerticalAlignment VerticalAlignment { get => verticalAlignment; set => SetValue(ref verticalAlignment, value); }
        private bool useCustomColor;
        public bool UseCustomColor { get => useCustomColor; set => SetValue(ref useCustomColor, value); }
        private string color = "#FFFFFF";
        public string Color
        {
            get => color;
            set
            {
                try { ColorConverter.ConvertFromString(value ?? string.Empty); }
                catch (Exception) { throw new ArgumentException("Enter a valid color."); }
                SetValue(ref color, value);
            }
        }
    }
}
