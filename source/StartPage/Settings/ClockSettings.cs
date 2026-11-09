using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace LandingPage.Settings
{
    public enum ClockTimeFormat { System, TwelveHour, TwentyFourHour }
    public enum ClockDateFormat { Long, Short, ISO, Custom }
    public enum ClockDateCapitalization { Default, UppercaseMonth, UppercaseDate, CapitalizeMonth }

    public class ClockSettings : ObservableObjectExt
    {
        private double timeSize = 140;
        public double TimeSize { get => timeSize; set => SetValue(ref timeSize, Clamp(value, 20, 300), nameof(TimeSize), nameof(DesignatorSize)); }
        [Playnite.SDK.Data.DontSerialize]
        public double DesignatorSize => TimeSize * 24 / 140;
        private double dateSize = 16;
        public double DateSize { get => dateSize; set => SetValue(ref dateSize, Clamp(value, 8, 72)); }
        private HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center;
        public HorizontalAlignment HorizontalAlignment { get => horizontalAlignment; set => SetValue(ref horizontalAlignment, value); }
        private VerticalAlignment verticalAlignment = VerticalAlignment.Center;
        public VerticalAlignment VerticalAlignment { get => verticalAlignment; set => SetValue(ref verticalAlignment, value); }
        private bool showDate = true;
        public bool ShowDate { get => showDate; set => SetValue(ref showDate, value); }
        private bool showSeconds;
        public bool ShowSeconds { get => showSeconds; set => SetValue(ref showSeconds, value); }
        private bool enableAnimation = true;
        public bool EnableAnimation { get => enableAnimation; set => SetValue(ref enableAnimation, value); }
        private ClockTimeFormat timeFormat;
        public ClockTimeFormat TimeFormat { get => timeFormat; set => SetValue(ref timeFormat, value); }
        private ClockDateFormat dateFormat;
        public ClockDateFormat DateFormat { get => dateFormat; set => SetValue(ref dateFormat, value); }
        private ClockDateCapitalization dateCapitalization;
        public ClockDateCapitalization DateCapitalization { get => dateCapitalization; set => SetValue(ref dateCapitalization, value); }
        private string customDateFormat = "dddd, d MMMM yyyy";
        public string CustomDateFormat
        {
            get => customDateFormat;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Enter a date format, for example yyyy-MM-dd.");
                try { DateTime.Now.ToString(value, CultureInfo.CurrentCulture); }
                catch (FormatException) { throw new ArgumentException("Invalid date format. Try yyyy-MM-dd or dddd, d MMMM yyyy."); }
                SetValue(ref customDateFormat, value);
            }
        }
        private bool useCustomColors;
        public bool UseCustomColors { get => useCustomColors; set => SetValue(ref useCustomColors, value); }
        private string timeColor = "#FFFFFF";
        public string TimeColor { get => timeColor; set { ValidateColor(value); SetValue(ref timeColor, value); } }
        private string dateColor = "#FFFFFF";
        public string DateColor { get => dateColor; set { ValidateColor(value); SetValue(ref dateColor, value); } }

        private static double Clamp(double value, double min, double max)
            => double.IsNaN(value) || double.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));
        private static void ValidateColor(string value)
        {
            try { ColorConverter.ConvertFromString(value ?? string.Empty); }
            catch (Exception) { throw new ArgumentException("Enter a valid color."); }
        }
    }
}
