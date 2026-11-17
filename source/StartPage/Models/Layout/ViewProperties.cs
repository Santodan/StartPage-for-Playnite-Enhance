using Playnite.SDK.Data;
using StartPage.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandingPage.Models.Layout
{
    public class ViewProperties : ObservableObject
    {
        private Settings.AchievementWidgetSettings achievementSettings;
        public Settings.AchievementWidgetSettings AchievementSettings {
            get => achievementSettings ?? (achievementSettings = Settings.AchievementWidgetSettings.Create(ViewId));
            set => SetValue(ref achievementSettings,value ?? Settings.AchievementWidgetSettings.Create(ViewId));
        }
        internal void MigrateRecentAchievementSettings(int total,int perGame)
        {
            if (ViewId != "RecentAchivements" || achievementSettings != null) return;
            achievementSettings = Settings.AchievementWidgetSettings.Create(ViewId);
            achievementSettings.Limit = Math.Max(0,total); achievementSettings.PerGameLimit = Math.Max(0,perGame);
        }
        public bool ShouldSerializeAchievementSettings() => Settings.AchievementWidgetSettings.IsWidget(ViewId) && (ViewId != "RecentAchivements" || achievementSettings != null);
        [DontSerialize, Newtonsoft.Json.JsonIgnore]
        public bool IsAchievementWidget => Settings.AchievementWidgetSettings.IsWidget(ViewId);
        private PanelAppearance appearance = new PanelAppearance();
        public PanelAppearance Appearance { get => appearance; set => SetValue(ref appearance, value ?? new PanelAppearance()); }
        private Settings.LibraryStatisticsSettings libraryStatisticsSettings;
        public Settings.LibraryStatisticsSettings LibraryStatisticsSettings { get => libraryStatisticsSettings ?? (libraryStatisticsSettings = ViewId == "LibraryChart" ? Settings.LibraryStatisticsSettings.CreateChartSettings() : new Settings.LibraryStatisticsSettings()); set => SetValue(ref libraryStatisticsSettings,value ?? (ViewId == "LibraryChart" ? Settings.LibraryStatisticsSettings.CreateChartSettings() : new Settings.LibraryStatisticsSettings())); }
        public bool ShouldSerializeLibraryStatisticsSettings() => ViewId == "LibraryStatistics" || ViewId == "LibraryChart";
        private Settings.ClockSettings clockSettings = new Settings.ClockSettings();
        public Settings.ClockSettings ClockSettings { get => clockSettings; set => SetValue(ref clockSettings, value ?? new Settings.ClockSettings()); }
        private Settings.TextWidgetSettings textSettings = new Settings.TextWidgetSettings();
        public Settings.TextWidgetSettings TextSettings { get => textSettings; set => SetValue(ref textSettings, value ?? new Settings.TextWidgetSettings()); }
        private Settings.ShortcutWidgetSettings shortcutSettings;
        public Settings.ShortcutWidgetSettings ShortcutSettings { get => shortcutSettings ?? (shortcutSettings = new Settings.ShortcutWidgetSettings()); set => SetValue(ref shortcutSettings,value ?? new Settings.ShortcutWidgetSettings()); }
        public bool ShouldSerializeShortcutSettings() => ViewId == "ShortcutWidget";
        private Settings.ImageWidgetSettings imageSettings = new Settings.ImageWidgetSettings();
        public Settings.ImageWidgetSettings ImageSettings { get => imageSettings; set => SetValue(ref imageSettings, value ?? new Settings.ImageWidgetSettings()); }
        private Settings.FeaturedGameSettings gameDetailSettings;
        public Settings.FeaturedGameSettings GameDetailSettings { get => gameDetailSettings ?? (gameDetailSettings = GameDetailWidgets.CreateSettings(ViewId)); set => SetValue(ref gameDetailSettings,value ?? GameDetailWidgets.CreateSettings(ViewId)); }
        [DontSerialize, Newtonsoft.Json.JsonIgnore]
        public bool IsGameDetailWidget => GameDetailWidgets.Find(ViewId) != null;
        public bool ShouldSerializeGameDetailSettings() => ViewId?.StartsWith("GameDetail",StringComparison.Ordinal) == true;
        private Settings.FeaturedGameSettings featuredGameSettings = new Settings.FeaturedGameSettings();
        public Settings.FeaturedGameSettings FeaturedGameSettings { get => featuredGameSettings; set => SetValue(ref featuredGameSettings, value ?? new Settings.FeaturedGameSettings()); }
        private Settings.ToolbarWidgetSettings toolbarSettings = new Settings.ToolbarWidgetSettings();
        public Settings.ToolbarWidgetSettings ToolbarSettings { get => toolbarSettings; set => SetValue(ref toolbarSettings, value ?? new Settings.ToolbarWidgetSettings()); }
        private string viewId;
        public string ViewId { get => viewId; set { SetValue(ref viewId,value); OnPropertyChanged(nameof(IsGameDetailWidget)); } }
        private Guid instanceId = Guid.NewGuid();
        public Guid InstanceId { get => instanceId; set => SetValue(ref instanceId, value); }
        private Guid pluginId; 
        public Guid PluginId { get => pluginId ; set => SetValue(ref pluginId, value); }
        private bool showLabel = false;
        public bool ShowLabel { get => showLabel; set=> SetValue(ref showLabel, value); }

        [DontSerialize]
        internal object view = null;

        [DontSerialize]
        private StartPageViewArgsBase startPageViewArgs = null;
        [DontSerialize]
        public StartPageViewArgsBase StartPageViewArgs { get => startPageViewArgs; set => SetValue(ref startPageViewArgs, value); }
    }

    public class PanelAppearance : ObservableObjectExt
    {
        private bool enabled;
        public bool Enabled { get => enabled; set => SetValue(ref enabled, value); }
        private string title = string.Empty;
        public string Title { get => title; set => SetValue(ref title, value ?? string.Empty); }
        private string background = "#202020";
        public string Background { get => background; set { ValidateColor(value); SetValue(ref background, value); } }
        private string borderColor = "#808080";
        public string BorderColor { get => borderColor; set { ValidateColor(value); SetValue(ref borderColor, value); } }
        private string titleColor = "#FFFFFF";
        public string TitleColor { get => titleColor; set { ValidateColor(value); SetValue(ref titleColor, value); } }
        private bool customizeText;
        public bool CustomizeText { get => customizeText; set => SetValue(ref customizeText, value); }
        private bool customizeIcons;
        public bool CustomizeIcons { get => customizeIcons; set => SetValue(ref customizeIcons, value); }
        private string iconColor = "#FFFFFF";
        public string IconColor { get => iconColor; set { ValidateColor(value); SetValue(ref iconColor, value); } }
        private bool customizeCharts;
        public bool CustomizeCharts { get => customizeCharts; set => SetValue(ref customizeCharts, value); }
        private bool customizeHighlights;
        public bool CustomizeHighlights { get => customizeHighlights; set => SetValue(ref customizeHighlights, value); }
        private string textColor = "#FFFFFF";
        public string TextColor { get => textColor; set { ValidateColor(value); SetValue(ref textColor, value); } }
        private string chartColor = "#2196F3";
        public string ChartColor { get => chartColor; set { ValidateColor(value); SetValue(ref chartColor, value); } }
        private string highlightColor = "#607D8B";
        public string HighlightColor { get => highlightColor; set { ValidateColor(value); SetValue(ref highlightColor, value); } }
        private double backgroundOpacity = 0.6;
        public double BackgroundOpacity { get => backgroundOpacity; set => SetValue(ref backgroundOpacity, Clamp(value, 0, 1)); }
        private double borderWidth;
        public double BorderWidth { get => borderWidth; set => SetValue(ref borderWidth, Clamp(value, 0, 10)); }
        private double cornerRadius = 8;
        public double CornerRadius { get => cornerRadius; set => SetValue(ref cornerRadius, Clamp(value, 0, 100)); }
        private double padding = 8;
        public double Padding { get => padding; set => SetValue(ref padding, Clamp(value, 0, 100)); }
        private double margin;
        public double Margin { get => margin; set => SetValue(ref margin, Clamp(value, 0, 100)); }
        private double titleSize = 16;
        public double TitleSize { get => titleSize; set => SetValue(ref titleSize, Clamp(value, 8, 72)); }
        private System.Windows.TextAlignment titleAlignment = System.Windows.TextAlignment.Left;
        public System.Windows.TextAlignment TitleAlignment { get => titleAlignment; set => SetValue(ref titleAlignment, value); }
        private bool shadow;
        public bool Shadow { get => shadow; set => SetValue(ref shadow, value); }

        private static double Clamp(double value, double min, double max)
            => double.IsNaN(value) || double.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));

        private static void ValidateColor(string value)
        {
            try { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); }
            catch (Exception) { throw new ArgumentException("Enter a color name or #RRGGBB / #AARRGGBB."); }
        }
    }

    public class StartPageViewArgs : StartPageViewArgsBase
    {
        public StartPageViewArgs() : base() {}
        public StartPageViewArgs(Guid pluginId)
        {
            PluginId = pluginId;
        }
        public StartPageViewArgs(StartPageViewArgsBase baseArgs, Guid pluginId) : this(pluginId)
        {
            var props = GetType().GetProperties();
            foreach(var baseProp in typeof(StartPageViewArgsBase).GetProperties())
            {
                var targetProp = props.FirstOrDefault(prop => prop.Name == baseProp.Name && prop.PropertyType == baseProp.PropertyType);
                if(targetProp != null)
                {
                    targetProp.SetValue(this, baseProp.GetValue(baseArgs), null);
                }
            }
            
        }
        public Guid PluginId { get; set; }
    }
}
