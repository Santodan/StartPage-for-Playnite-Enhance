using System;
using System.Windows;
using System.Collections.ObjectModel;
using System.Linq;
namespace LandingPage.Settings
{
    public enum StatisticsTextSize { Custom, ThemeSmall, ThemeNormal, ThemeLarge, ThemeLarger, ThemeLargest }
    public enum StatisticsValueColor { Theme, MatchLabels, Custom }
    public enum StatisticsChartStyle { Bars, Doughnut, Pie, VerticalBars }
    public enum StatisticsBreakdown { Installation, Played, Completed, Sources, Platforms, Genres, CompletionStatuses, Games, PlaytimeRanges }
    public enum StatisticsMetric { GameCount, Playtime }
    public enum StatisticsLabelPosition { Legend, BesideSegments, AboveBars }
    public enum StatisticsLegendPosition { Bottom, Top, Left, Right }
    public enum StatisticsCounter { Total, Installed, Unplayed, Completed, TotalPlaytime, AveragePlaytime, PlayedGames, Today, Week, Month, Year, NotInstalled, Hidden, Favorites, SourceCount, PlatformCount, GenreCount, CategoryCount, TagCount, FeatureCount, DeveloperCount, PublisherCount, StatusCount, PlayedIncludingDates, NeverLaunched, TotalLaunches, InstalledPercent, PlayedPercent, CompletedPercent, FavoritesPercent, MedianPlaytime, LongestPlaytime, AverageAllPlaytime, InstalledPlaytime, CompletedPlaytime, UninstalledPlaytime, MostPlayedGame, LargestSource, NoSource, NoPlatform, NoGenre, NoCategory, NoTags, NoFeatures, NoCompletionStatus, MissingCover, MissingBackground, MissingIcon, MissingDescription, NoReleaseDate, SourcesBreakdown, PlatformsBreakdown, GenresBreakdown, CategoriesBreakdown, TagsBreakdown, FeaturesBreakdown, DevelopersBreakdown, PublishersBreakdown, CompletionStatusesBreakdown }
    public sealed class StatisticColors : ObservableObjectExt
    {
        private bool useGlobalValueColor = true,useGlobalLabelColor = true;
        private string valueColor = "#FFFFFF",labelColor = "#FFFFFF";
        public bool UseGlobalValueColor { get => useGlobalValueColor; set => SetValue(ref useGlobalValueColor,value); }
        public bool UseGlobalLabelColor { get => useGlobalLabelColor; set => SetValue(ref useGlobalLabelColor,value); }
        public string ValueColor { get => valueColor; set => SetValue(ref valueColor,Normalize(value)); }
        public string LabelColor { get => labelColor; set => SetValue(ref labelColor,Normalize(value)); }
        private static string Normalize(string value) { try { return ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value)).ToString(); } catch { return "#FFFFFF"; } }
    }
    public sealed class CounterPlacement : ObservableObjectExt
    {
        public StatisticsCounter Counter { get; set; }
        private StatisticColors colors = new StatisticColors();
        public StatisticColors Colors { get => colors; set => SetValue(ref colors,value ?? new StatisticColors()); }
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore]
        public string Label => GetLabel(Counter);
        public static string GetLabel(StatisticsCounter counter) { var labels = new[] { "Total games","Installed","Unplayed","Completed","Total playtime","Average playtime","Games with playtime","Today","This week","This month","This year","Not installed","Hidden games","Favorite games","Sources represented","Platforms represented","Genres represented","Categories represented","Tags represented","Features represented","Developers represented","Publishers represented","Completion statuses represented","Played games (time or last played)","Games with no recorded launches","Recorded game launches","Installed percentage","Played percentage","Completed percentage","Favorites percentage","Median playtime (played games)","Longest recorded playtime","Average playtime (all games)","Installed games playtime","Completed games playtime","Uninstalled games playtime","Most-played game","Largest source by game count","Games without a source","Games without a platform","Games without a genre","Games without a category","Games without tags","Games without features","Games without a completion status","Games missing cover artwork","Games missing background artwork","Games missing an icon","Games missing a description","Games without a release date","By source","By platform","By genre","By category","By tag","By feature","By developer","By publisher","By completion status" }; return (int)counter >= 0 && (int)counter < labels.Length ? labels[(int)counter] : "Unknown counter"; }
        private int row = 1, column = 1, rowSpan = 1, columnSpan = 1;
        public int Row { get => row; set => SetValue(ref row,Math.Max(1,Math.Min(100,value))); }
        public int Column { get => column; set => SetValue(ref column,Math.Max(1,Math.Min(100,value))); }
        public int RowSpan { get => rowSpan; set => SetValue(ref rowSpan,Math.Max(1,Math.Min(100,value))); }
        public int ColumnSpan { get => columnSpan; set => SetValue(ref columnSpan,Math.Max(1,Math.Min(100,value))); }
    }
    public sealed class StatisticCounterOption : ObservableObjectExt
    {
        public StatisticsCounter Counter { get; set; }
        private bool enabled;
        public bool Enabled { get => enabled; set => SetValue(ref enabled,value); }
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore] public string Label => CounterPlacement.GetLabel(Counter);
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore] public string Section => (int)Counter >= (int)StatisticsCounter.NoSource ? "Metadata coverage" : (int)Counter >= (int)StatisticsCounter.PlayedIncludingDates && Counter != StatisticsCounter.LargestSource ? "Playtime and progress" : "Library";
    }
    public enum StatisticGroup { Sources,Platforms,Genres,Categories,Tags,Features,Developers,Publishers,CompletionStatuses }
    public sealed class StatisticSelectionChoice { public Guid Id { get; set; } public string Name { get; set; } }
    public sealed class StatisticGroupOption : ObservableObjectExt
    {
        public StatisticGroup Group { get; set; }
        private bool enabled,showPercentages,includeUnassigned = true;
        private int limit;
        private bool selectedOnly,showName = true,showIcon,showHeading = true;
        private Guid selectedId;
        private double iconSize = 24;
        public bool SelectedOnly { get => selectedOnly; set => SetValue(ref selectedOnly,value); }
        public Guid SelectedId { get => selectedId; set => SetValue(ref selectedId,value); }
        public bool ShowName { get => showName; set => SetValue(ref showName,value); }
        public bool ShowIcon { get => showIcon; set => SetValue(ref showIcon,value); }
        public bool ShowHeading { get => showHeading; set => SetValue(ref showHeading,value); }
        public double IconSize { get => iconSize; set => SetValue(ref iconSize,double.IsNaN(value) || double.IsInfinity(value) ? 24 : Math.Max(8,Math.Min(128,value))); }
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore] public bool SupportsSelection => Group == StatisticGroup.Sources || Group == StatisticGroup.Platforms;
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore] public ObservableCollection<StatisticSelectionChoice> Choices { get; } = new ObservableCollection<StatisticSelectionChoice>();
        private StatisticsMetric metric;
        public bool Enabled { get => enabled; set => SetValue(ref enabled,value); }
        public bool ShowPercentages { get => showPercentages; set => SetValue(ref showPercentages,value); }
        public bool IncludeUnassigned { get => includeUnassigned; set => SetValue(ref includeUnassigned,value); }
        public int Limit { get => limit; set => SetValue(ref limit,Math.Max(0,value)); }
        public StatisticsMetric Metric { get => metric; set => SetValue(ref metric,value); }
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore] public StatisticsCounter Counter => (StatisticsCounter)((int)StatisticsCounter.SourcesBreakdown+(int)Group);
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore] public string Label => CounterPlacement.GetLabel(Counter);
    }
    public enum ChartConnectorStyle { Elbow, Straight }
    public sealed class ChartLabelOffset : ObservableObjectExt
    {
        public string Key { get; set; }
        public string Name { get; set; }
        private double x,y;
        public double X { get => x; set => SetValue(ref x,double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(-2000,Math.Min(2000,value))); }
        public double Y { get => y; set => SetValue(ref y,double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(-2000,Math.Min(2000,value))); }
    }
    public class LibraryStatisticsSettings : ObservableObjectExt
    {
        private bool customSegmentBorderColor;
        private string segmentBorderColor = "#808080";
        public bool CustomSegmentBorderColor { get => customSegmentBorderColor; set => SetValue(ref customSegmentBorderColor,value); }
        public string SegmentBorderColor { get => segmentBorderColor; set => SetValue(ref segmentBorderColor,ValidColor(value)); }
        private bool glossySeries = true, showChartAxes = true, showChartGrid = true, chartAnimations;
        private double sliceSeparatorWidth = 1;
        public bool GlossySeries { get => glossySeries; set => SetValue(ref glossySeries,value); }
        public bool ShowChartAxes { get => showChartAxes; set => SetValue(ref showChartAxes,value); }
        public bool ShowChartGrid { get => showChartGrid; set => SetValue(ref showChartGrid,value); }
        public bool ChartAnimations { get => chartAnimations; set => SetValue(ref chartAnimations,value); }
        public double SliceSeparatorWidth { get => sliceSeparatorWidth; set => SetValue(ref sliceSeparatorWidth,Clamp(value,0,10)); }
        private ObservableCollection<StatisticCounterOption> additionalCounters = new ObservableCollection<StatisticCounterOption>(Enum.GetValues(typeof(StatisticsCounter)).Cast<StatisticsCounter>().Where(counter => counter >= StatisticsCounter.NotInstalled && counter < StatisticsCounter.SourcesBreakdown).Select(counter => new StatisticCounterOption { Counter = counter }));
        private ObservableCollection<StatisticGroupOption> statisticGroups = new ObservableCollection<StatisticGroupOption>(Enum.GetValues(typeof(StatisticGroup)).Cast<StatisticGroup>().Select(group => new StatisticGroupOption { Group = group }));
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public ObservableCollection<StatisticCounterOption> AdditionalCounters { get => additionalCounters; set => SetValue(ref additionalCounters,value ?? new ObservableCollection<StatisticCounterOption>()); }
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public ObservableCollection<StatisticGroupOption> StatisticGroups { get => statisticGroups; set => SetValue(ref statisticGroups,value ?? new ObservableCollection<StatisticGroupOption>()); }
        public void EnsureStatisticOptions()
        {
            foreach(var counter in Enum.GetValues(typeof(StatisticsCounter)).Cast<StatisticsCounter>())
            {
                if(counter >= StatisticsCounter.NotInstalled && counter < StatisticsCounter.SourcesBreakdown && !AdditionalCounters.Any(item => item != null && item.Counter == counter)) AdditionalCounters.Add(new StatisticCounterOption { Counter = counter });
                if(!CounterPlacements.Any(item => item != null && item.Counter == counter)) CounterPlacements.Add(new CounterPlacement { Counter = counter,Row = CounterPlacements.Count == 0 ? 1 : CounterPlacements.Where(item => item != null).Select(item => item.Row+item.RowSpan-1).DefaultIfEmpty(0).Max()+1 });
            }
            foreach(var group in Enum.GetValues(typeof(StatisticGroup)).Cast<StatisticGroup>()) if(!StatisticGroups.Any(item => item != null && item.Group == group)) StatisticGroups.Add(new StatisticGroupOption { Group = group });
        }
        private bool editCharts;
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore]
        public bool EditCharts { get => editCharts; set => SetValue(ref editCharts,value); }
        private double chartWidth, chartScale = 1, chartRotation, outsideLabelDistance = 20, labelOffsetX, labelOffsetY, connectorThickness = 1;
        public double ChartWidth { get => chartWidth; set => SetValue(ref chartWidth,Clamp(value,0,4000)); }
        public double ChartScale { get => chartScale; set => SetValue(ref chartScale,Clamp(value,0.1,4)); }
        public double ChartRotation { get => chartRotation; set => SetValue(ref chartRotation,Clamp(value,-360,360)); }
        public double OutsideLabelDistance { get => outsideLabelDistance; set => SetValue(ref outsideLabelDistance,Clamp(value,0,500)); }
        public double LabelOffsetX { get => labelOffsetX; set => SetValue(ref labelOffsetX,Clamp(value,-2000,2000)); }
        public double LabelOffsetY { get => labelOffsetY; set => SetValue(ref labelOffsetY,Clamp(value,-2000,2000)); }
        public double ConnectorThickness { get => connectorThickness; set => SetValue(ref connectorThickness,Clamp(value,0.1,20)); }
        private bool showConnectorLines = true, customConnectorColor;
        private string connectorColor = "#FFFFFF";
        public bool ShowConnectorLines { get => showConnectorLines; set => SetValue(ref showConnectorLines,value); }
        public bool CustomConnectorColor { get => customConnectorColor; set => SetValue(ref customConnectorColor,value); }
        public string ConnectorColor { get => connectorColor; set => SetValue(ref connectorColor,ValidColor(value)); }
        private ChartConnectorStyle connectorStyle;
        public ChartConnectorStyle ConnectorStyle { get => connectorStyle; set => SetValue(ref connectorStyle,value); }
        private ObservableCollection<ChartLabelOffset> labelOffsets = new ObservableCollection<ChartLabelOffset>();
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public ObservableCollection<ChartLabelOffset> LabelOffsets { get => labelOffsets; set => SetValue(ref labelOffsets,value ?? new ObservableCollection<ChartLabelOffset>()); }
        public bool ShouldSerializeLabelOffsets() => LabelOffsets.Any(offset => offset.X != 0 || offset.Y != 0);
        private string chartName = "Chart";
        public string ChartName { get => chartName; set => SetValue(ref chartName,value ?? string.Empty); }
        private ObservableCollection<LibraryStatisticsSettings> extraCharts = new ObservableCollection<LibraryStatisticsSettings>();
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public ObservableCollection<LibraryStatisticsSettings> ExtraCharts { get => extraCharts; set => SetValue(ref extraCharts,value ?? new ObservableCollection<LibraryStatisticsSettings>()); }
        private int chartColumns = 1;
        public int ChartColumns { get => chartColumns; set => SetValue(ref chartColumns,Math.Max(1,value)); }
        private bool useCounterGrid;
        public bool UseCounterGrid { get => useCounterGrid; set => SetValue(ref useCounterGrid,value); }
        private int counterRows = 1, counterColumns = 3;
        public int CounterRows { get => counterRows; set => SetValue(ref counterRows,Math.Max(1,Math.Min(100,value))); }
        public int CounterColumns { get => counterColumns; set => SetValue(ref counterColumns,Math.Max(1,Math.Min(100,value))); }
        private ObservableCollection<CounterPlacement> counterPlacements = DefaultCounterPlacements();
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public ObservableCollection<CounterPlacement> CounterPlacements { get => counterPlacements; set => SetValue(ref counterPlacements,value ?? DefaultCounterPlacements()); }
        private static ObservableCollection<CounterPlacement> DefaultCounterPlacements() => new ObservableCollection<CounterPlacement>(Enum.GetValues(typeof(StatisticsCounter)).Cast<StatisticsCounter>().Select((counter,index) => new CounterPlacement { Counter = counter,Row = index/3+1,Column = index%3+1 }));
        public bool IsCounterEnabled(StatisticsCounter counter)
        {
            switch(counter)
            {
                case StatisticsCounter.Total: return ShowTotal;
                case StatisticsCounter.Installed: return ShowInstalled;
                case StatisticsCounter.Unplayed: return ShowUnplayed;
                case StatisticsCounter.Completed: return ShowCompleted;
                case StatisticsCounter.TotalPlaytime: return ShowPlaytime;
                case StatisticsCounter.AveragePlaytime: return ShowAveragePlaytime;
                case StatisticsCounter.PlayedGames: return ShowPlayedGames;
                case StatisticsCounter.Today: return ShowTodayPlaytime;
                case StatisticsCounter.Week: return ShowWeekPlaytime;
                case StatisticsCounter.Month: return ShowMonthPlaytime;
                case StatisticsCounter.Year: return ShowYearPlaytime;
                default: return AdditionalCounters.Any(item => item != null && item.Counter == counter && item.Enabled) || StatisticGroups.Any(item => item != null && item.Counter == counter && item.Enabled);
            }
        }
        public void ResetCounterPositions()
        {
            var colors = CounterPlacements.Where(item => item != null).GroupBy(item => item.Counter).ToDictionary(group => group.Key,group => group.First().Colors);
            var positions = DefaultCounterPlacements(); foreach(var item in positions) if(colors.TryGetValue(item.Counter,out var saved)) item.Colors = saved;
            CounterPlacements = positions;
        }
        private string valueFontFamily = string.Empty, valueColor = "#FFFFFF";
        private bool valueBold,valueItalic,valueUnderline,valueStrikeThrough,labelStrikeThrough;
        private bool inheritTextRendering = true, wrapCounterText = true;
        private double valueOpacity = 1,labelOpacity = 1,labelLineHeight;
        private StatisticsTextSize valueSizePreset,labelSizePreset;
        private StatisticsValueColor valueColorMode;
        private TextTrimming counterTextTrimming;
        private System.Windows.Media.TextFormattingMode textFormatting;
        private System.Windows.Media.TextRenderingMode textRendering;
        public string ValueFontFamily { get => valueFontFamily; set => SetValue(ref valueFontFamily,value ?? string.Empty); }
        public bool ValueBold { get => valueBold; set => SetValue(ref valueBold,value); }
        public bool ValueItalic { get => valueItalic; set => SetValue(ref valueItalic,value); }
        public bool ValueUnderline { get => valueUnderline; set => SetValue(ref valueUnderline,value); }
        public bool ValueStrikeThrough { get => valueStrikeThrough; set => SetValue(ref valueStrikeThrough,value); }
        public bool LabelStrikeThrough { get => labelStrikeThrough; set => SetValue(ref labelStrikeThrough,value); }
        public string ValueColor { get => valueColor; set => SetValue(ref valueColor,ValidColor(value)); }
        public StatisticsValueColor ValueColorMode { get => valueColorMode; set => SetValue(ref valueColorMode,value); }
        public StatisticsTextSize ValueSizePreset { get => valueSizePreset; set => SetValue(ref valueSizePreset,value); }
        public StatisticsTextSize LabelSizePreset { get => labelSizePreset; set => SetValue(ref labelSizePreset,value); }
        public double ValueOpacity { get => valueOpacity; set => SetValue(ref valueOpacity,Clamp(value,0,1)); }
        public double LabelOpacity { get => labelOpacity; set => SetValue(ref labelOpacity,Clamp(value,0,1)); }
        public double LabelLineHeight { get => labelLineHeight; set => SetValue(ref labelLineHeight,Clamp(value,0,300)); }
        public bool WrapCounterText { get => wrapCounterText; set => SetValue(ref wrapCounterText,value); }
        public TextTrimming CounterTextTrimming { get => counterTextTrimming; set => SetValue(ref counterTextTrimming,value); }
        public bool InheritTextRendering { get => inheritTextRendering; set => SetValue(ref inheritTextRendering,value); }
        public System.Windows.Media.TextFormattingMode TextFormatting { get => textFormatting; set => SetValue(ref textFormatting,value); }
        public System.Windows.Media.TextRenderingMode TextRendering { get => textRendering; set => SetValue(ref textRendering,value); }
        private string labelFontFamily = string.Empty, labelColor = "#FFFFFF";
        private bool labelBold, labelItalic, labelUnderline, customLabelColor;
        public string LabelFontFamily { get => labelFontFamily; set => SetValue(ref labelFontFamily,value ?? string.Empty); }
        public bool LabelBold { get => labelBold; set => SetValue(ref labelBold,value); }
        public bool LabelItalic { get => labelItalic; set => SetValue(ref labelItalic,value); }
        public bool LabelUnderline { get => labelUnderline; set => SetValue(ref labelUnderline,value); }
        public bool CustomLabelColor { get => customLabelColor; set => SetValue(ref customLabelColor,value); }
        public string LabelColor { get => labelColor; set => SetValue(ref labelColor,ValidColor(value)); }
        private bool showTotal = true, showInstalled = true, showUnplayed = true, showCompleted = true, showPlaytime = true, includeHidden, installedOnly, rows;
        public bool ShowTotal { get => showTotal; set => SetValue(ref showTotal,value); }
        public bool ShowInstalled { get => showInstalled; set => SetValue(ref showInstalled,value); }
        public bool ShowUnplayed { get => showUnplayed; set => SetValue(ref showUnplayed,value); }
        public bool ShowCompleted { get => showCompleted; set => SetValue(ref showCompleted,value); }
        public bool ShowPlaytime { get => showPlaytime; set => SetValue(ref showPlaytime,value); }
        public bool IncludeHidden { get => includeHidden; set => SetValue(ref includeHidden,value); }
        public bool InstalledOnly { get => installedOnly; set => SetValue(ref installedOnly,value); }
        public bool Rows { get => rows; set => SetValue(ref rows,value); }
        private bool showChart, showLegend = true, showPercentages = true, customChartColors;
        public bool ShowChart { get => showChart; set => SetValue(ref showChart,value); }
        public bool ShowLegend { get => showLegend; set => SetValue(ref showLegend,value); }
        public bool ShowPercentages { get => showPercentages; set => SetValue(ref showPercentages,value); }
        public bool CustomChartColors { get => customChartColors; set => SetValue(ref customChartColors,value); }
        private StatisticsChartStyle chartStyle;
        public StatisticsChartStyle ChartStyle { get => chartStyle; set => SetValue(ref chartStyle,value); }
        private StatisticsBreakdown breakdown;
        public StatisticsBreakdown Breakdown { get => breakdown; set => SetValue(ref breakdown,value); }
        private string chartTitle = string.Empty;
        public string ChartTitle { get => chartTitle; set => SetValue(ref chartTitle,value ?? string.Empty); }
        private double chartHeight = 220, holeSize = 0.6;
        public double ChartHeight { get => chartHeight; set => SetValue(ref chartHeight,Clamp(value,60,1000)); }
        public double HoleSize { get => holeSize; set => SetValue(ref holeSize,Clamp(value,0.1,0.9)); }
        private int groupLimit;
        public int GroupLimit { get => groupLimit; set => SetValue(ref groupLimit,Math.Max(0,value)); }
        private string chartColor1 = "#42A5F5", chartColor2 = "#66BB6A", chartColor3 = "#FFA726", chartColor4 = "#AB47BC", chartColor5 = "#EF5350", chartColor6 = "#26C6DA";
        private static string ValidColor(string value) { try { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); return value; } catch { throw new ArgumentException("Enter a valid color."); } }
        public string ChartColor1 { get => chartColor1; set => SetValue(ref chartColor1,ValidColor(value)); }
        public string ChartColor2 { get => chartColor2; set => SetValue(ref chartColor2,ValidColor(value)); }
        public string ChartColor3 { get => chartColor3; set => SetValue(ref chartColor3,ValidColor(value)); }
        public string ChartColor4 { get => chartColor4; set => SetValue(ref chartColor4,ValidColor(value)); }
        public string ChartColor5 { get => chartColor5; set => SetValue(ref chartColor5,ValidColor(value)); }
        public string ChartColor6 { get => chartColor6; set => SetValue(ref chartColor6,ValidColor(value)); }
        private StatisticsMetric metric;
        public StatisticsMetric Metric { get => metric; set => SetValue(ref metric,value); }
        private StatisticsLabelPosition labelPosition;
        public StatisticsLabelPosition LabelPosition { get => labelPosition; set => SetValue(ref labelPosition,value); }
        private StatisticsLegendPosition legendPosition;
        public StatisticsLegendPosition LegendPosition { get => legendPosition; set => SetValue(ref legendPosition,value); }
        private HorizontalAlignment legendAlignment = HorizontalAlignment.Center;
        public HorizontalAlignment LegendAlignment { get => legendAlignment; set => SetValue(ref legendAlignment,value); }
        private VerticalAlignment legendVerticalAlignment = VerticalAlignment.Center;
        public VerticalAlignment LegendVerticalAlignment { get => legendVerticalAlignment; set => SetValue(ref legendVerticalAlignment,value); }
        private bool legendHorizontal = true, showAveragePlaytime, showPlayedGames;
        public bool LegendHorizontal { get => legendHorizontal; set => SetValue(ref legendHorizontal,value); }
        public bool ShowAveragePlaytime { get => showAveragePlaytime; set => SetValue(ref showAveragePlaytime,value); }
        public bool ShowPlayedGames { get => showPlayedGames; set => SetValue(ref showPlayedGames,value); }
        private double labelWidth = 150;
        public double LabelWidth { get => labelWidth; set => SetValue(ref labelWidth,Clamp(value,50,600)); }
        public static LibraryStatisticsSettings CreateChartSettings() => new LibraryStatisticsSettings {
            ShowTotal = false, ShowInstalled = false, ShowUnplayed = false, ShowCompleted = false, ShowPlaytime = false,
            ShowChart = true, Breakdown = StatisticsBreakdown.Sources, ChartStyle = StatisticsChartStyle.Doughnut, GroupLimit = 10, LegendPosition = StatisticsLegendPosition.Right, LegendHorizontal = false
        };
        private bool showTodayPlaytime, showWeekPlaytime, showMonthPlaytime, showYearPlaytime;
        public bool ShowTodayPlaytime { get => showTodayPlaytime; set => SetValue(ref showTodayPlaytime,value); }
        public bool ShowWeekPlaytime { get => showWeekPlaytime; set => SetValue(ref showWeekPlaytime,value); }
        public bool ShowMonthPlaytime { get => showMonthPlaytime; set => SetValue(ref showMonthPlaytime,value); }
        public bool ShowYearPlaytime { get => showYearPlaytime; set => SetValue(ref showYearPlaytime,value); }
        private DayOfWeek weekStartsOn = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        public DayOfWeek WeekStartsOn { get => weekStartsOn; set => SetValue(ref weekStartsOn,(int)value < 0 || (int)value > 6 ? DayOfWeek.Monday : value); }
        private bool useCustomCompletedStatuses;
        public bool UseCustomCompletedStatuses { get => useCustomCompletedStatuses; set => SetValue(ref useCustomCompletedStatuses,value); }
        private Guid[] completedStatusIds = new Guid[0];
        public Guid[] CompletedStatusIds { get => completedStatusIds; set => SetValue(ref completedStatusIds,value ?? new Guid[0]); }
        private double valueSize = 32, labelSize = 14, tileWidth = 150, spacing = 12;
        private static double Clamp(double value,double min,double max) => double.IsNaN(value) || double.IsInfinity(value) ? min : Math.Max(min,Math.Min(max,value));
        public double ValueSize { get => valueSize; set => SetValue(ref valueSize,Clamp(value,8,200)); }
        public double LabelSize { get => labelSize; set => SetValue(ref labelSize,Clamp(value,8,100)); }
        public double TileWidth { get => tileWidth; set => SetValue(ref tileWidth,Clamp(value,60,1000)); }
        public double Spacing { get => spacing; set => SetValue(ref spacing,Clamp(value,0,100)); }
        private TextAlignment alignment = TextAlignment.Center;
        public TextAlignment Alignment { get => alignment; set => SetValue(ref alignment,value); }
    }
}
