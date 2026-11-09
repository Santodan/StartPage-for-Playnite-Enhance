using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using LandingPage.Settings;

namespace LandingPage.Models
{
    public class Clock : INotifyPropertyChanged
    {
        internal DispatcherTimer dispatcherTimer;
        internal DispatcherTimer leetTimer;
        internal double elapsedSeconds;
        private readonly Random random = new Random();
        private ClockSettings settings = new ClockSettings();
        private bool subscribed;
        public ClockSettings Settings
        {
            get => settings;
            set
            {
                if (subscribed) settings.PropertyChanged -= SettingsChanged;
                settings = value ?? new ClockSettings();
                if (subscribed) settings.PropertyChanged += SettingsChanged;
                SettingsChanged(this, null);
            }
        }

        public Clock() : this(true) { }
        public Clock(bool start)
        {
            dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher);
            dispatcherTimer.Tick += (sender, args) =>
            {
                UpdateTime(DateTime.Now, true);
                ScheduleTick();
            };
            TimeString = GetTimeString();
            if (start) Start();
        }

        public void Start()
        {
            if (!subscribed) { settings.PropertyChanged += SettingsChanged; subscribed = true; }
            UpdateTime(DateTime.Now, false);
            ScheduleTick();
            dispatcherTimer.Start();
        }

        public void Stop()
        {
            dispatcherTimer.Stop();
            StopAnimation();
            if (subscribed) settings.PropertyChanged -= SettingsChanged;
            subscribed = false;
        }

        private void ScheduleTick()
        {
            var period = settings.ShowSeconds ? 1 : 60;
            dispatcherTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.01, period - DateTime.Now.TimeOfDay.TotalSeconds % period));
        }

        private void StopAnimation()
        {
            if (leetTimer != null) { leetTimer.Stop(); leetTimer.Tick -= LeetTimer_Tick; leetTimer = null; }
            elapsedSeconds = 0;
            LeetTime = false;
        }

        private void SettingsChanged(object sender, PropertyChangedEventArgs args)
        {
            StopAnimation();
            UpdateTime(DateTime.Now, false);
            ScheduleTick();
        }

        internal void UpdateTime(DateTime now, bool animate)
        {
            var previous = Time;
            Time = now;
            if (previous.Date != now.Date) DayChanged?.Invoke(this, EventArgs.Empty);
            if (leetTimer != null && (previous.Hour != now.Hour || previous.Minute != now.Minute)) StopAnimation();
            if (leetTimer == null) TimeString = GetTimeString();
            if (animate && settings.EnableAnimation && now.Hour == 13 && now.Minute == 37
                && (previous.Hour != now.Hour || previous.Minute != now.Minute))
            {
                elapsedSeconds = 0;
                LeetTime = true;
                leetTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher.CurrentDispatcher);
                leetTimer.Interval = TimeSpan.FromMilliseconds(25);
                leetTimer.Tick += LeetTimer_Tick;
                leetTimer.Start();
            }
            OnPropertyChanged(nameof(Time));
            OnPropertyChanged(nameof(DateString));
            OnPropertyChanged(nameof(Separator));
            OnPropertyChanged(nameof(T));
        }

        private void LeetTimer_Tick(object sender, EventArgs e)
        {
            elapsedSeconds += leetTimer.Interval.TotalSeconds;
            if (elapsedSeconds < 1)
            {
                var display = TimeString;
                var r = random.Next(33, 127);
                var pos = random.Next(display.Length);
                var c = (char)r;
                var sb = new StringBuilder();
                for (int i = 0; i < display.Length; ++i)
                {
                    if (i == pos)
                    {
                        sb.Append(c);
                    } else
                    {
                        sb.Append(display[i]);
                    }
                }
                TimeString = sb.ToString();
            } else
            {
                var display = TimeString;
                var time = GetTimeString();
                var pos = random.Next(display.Length);
                var c = time[pos];
                var sb = new StringBuilder();
                for (int i = 0; i < display.Length; ++i)
                {
                    if (i == pos)
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append(display[i]);
                    }
                }
                TimeString = sb.ToString();
                if (TimeString == time)
                {
                    leetTimer.Stop();
                    leetTimer = null;
                    elapsedSeconds = 0;
                    TimeString = GetTimeString();
                    LeetTime = false;
                }
            }
        }

        public bool LeetTime { get; private set; }
        public event EventHandler DayChanged;
        public DateTime Time { get; set; } = DateTime.Now;
        private string timeString;
        public string TimeString { get => timeString; set { timeString = value; OnPropertyChanged(nameof(TimeString)); } }
        public string Separator => DateTimeFormatInfo.CurrentInfo.TimeSeparator;
        public string T
        {
            get
            {
                if (settings.TimeFormat == ClockTimeFormat.TwentyFourHour) return string.Empty;
                if (settings.TimeFormat == ClockTimeFormat.TwelveHour) return Time.ToString("tt");
                var match = Regex.Match(DateTimeFormatInfo.CurrentInfo.ShortTimePattern, "t+");
                return match.Success ? Time.ToString(match.Value.Length == 1 ? "%t" : match.Value) : string.Empty;
            }
        }
        private string GetTimeString()
        {
            string format;
            if (settings.TimeFormat == ClockTimeFormat.TwelveHour) format = settings.ShowSeconds ? "h:mm:ss" : "h:mm";
            else if (settings.TimeFormat == ClockTimeFormat.TwentyFourHour) format = settings.ShowSeconds ? "HH:mm:ss" : "HH:mm";
            else format = (settings.ShowSeconds ? DateTimeFormatInfo.CurrentInfo.LongTimePattern : DateTimeFormatInfo.CurrentInfo.ShortTimePattern).Replace("t", string.Empty).Trim();
            return Time.ToString(format);
        }
        public string DateString
        {
            get
            {
                var culture = CultureInfo.CurrentCulture;
                if (settings.DateCapitalization == ClockDateCapitalization.UppercaseMonth
                    || settings.DateCapitalization == ClockDateCapitalization.CapitalizeMonth)
                {
                    culture = (CultureInfo)culture.Clone();
                    var format = culture.DateTimeFormat;
                    format.MonthNames = FormatMonthNames(format.MonthNames, culture, settings.DateCapitalization);
                    format.AbbreviatedMonthNames = FormatMonthNames(format.AbbreviatedMonthNames, culture, settings.DateCapitalization);
                    format.MonthGenitiveNames = FormatMonthNames(format.MonthGenitiveNames, culture, settings.DateCapitalization);
                    format.AbbreviatedMonthGenitiveNames = FormatMonthNames(format.AbbreviatedMonthGenitiveNames, culture, settings.DateCapitalization);
                }
                string result;
                switch (settings.DateFormat)
                {
                    case ClockDateFormat.Short: result = Time.ToString("d", culture); break;
                    case ClockDateFormat.ISO: result = Time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); break;
                    case ClockDateFormat.Custom: result = Time.ToString(settings.CustomDateFormat, culture); break;
                    default: result = Time.ToString("D", culture); break;
                }
                return settings.DateCapitalization == ClockDateCapitalization.UppercaseDate
                    ? culture.TextInfo.ToUpper(result) : result;
            }
        }
        private static string[] FormatMonthNames(string[] months, CultureInfo culture, ClockDateCapitalization capitalization)
        {
            var result = new string[months.Length];
            for (var index = 0; index < months.Length; index++)
            {
                var month = months[index];
                result[index] = capitalization == ClockDateCapitalization.UppercaseMonth
                    ? culture.TextInfo.ToUpper(month)
                    : string.IsNullOrEmpty(month) ? month
                    : culture.TextInfo.ToUpper(StringInfo.GetNextTextElement(month))
                        + month.Substring(StringInfo.GetNextTextElement(month).Length);
            }
            return result;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
