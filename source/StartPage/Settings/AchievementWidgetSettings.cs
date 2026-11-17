using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace LandingPage.Settings
{
    public enum RecentAchievementGroupOrder { RecentUnlock, Name }
    public enum RecentAchievementGrouping { Game, UnlockDay, Source, Platform, None }
    public enum AchievementListFilter { All, Locked, Unlocked }
    public enum AchievementListOrder { ProviderOrder, Name, RecentUnlock, LockedFirst, UnlockedFirst }
    public enum AchievementGoalSource { PinnedGames, SelectedGame, HoveredGame, HoveredOrSelectedGame }
    public enum AchievementCardLayout { Rows, Cards }
    public enum AchievementProgressStyle { Bar, Ring, Both, None }
    public enum AchievementGameOrder { ProgressDescending, RemainingAscending, Name, LastPlayed }
    public enum AchievementTextRole { Description, GameName, Counts, Percentage, Remaining, UnlockState, UnlockDate, Goal, GroupTitle }
    public sealed class AchievementTextStyle : ObservableObjectExt
    {
        private bool useDefaults = true, bold, italic, useCustomColor;
        private double size = 13;
        private string color = "#FFFFFF";
        public bool UseDefaults { get => useDefaults; set => SetValue(ref useDefaults,value); }
        public bool Bold { get => bold; set { SetValue(ref bold,value,nameof(Bold),nameof(FontWeight)); } }
        public bool Italic { get => italic; set { SetValue(ref italic,value,nameof(Italic),nameof(FontStyle)); } }
        public bool UseCustomColor { get => useCustomColor; set => SetValue(ref useCustomColor,value); }
        public double Size { get => size; set => SetValue(ref size,double.IsNaN(value) || double.IsInfinity(value) ? 13 : Math.Max(8,Math.Min(100,value))); }
        public string Color { get => color; set { try { SetValue(ref color,((System.Windows.Media.Color)ColorConverter.ConvertFromString(value)).ToString()); } catch { } } }
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore] public FontWeight FontWeight => Bold ? FontWeights.Bold : FontWeights.Normal;
        [Playnite.SDK.Data.DontSerialize,Newtonsoft.Json.JsonIgnore] public FontStyle FontStyle => Italic ? FontStyles.Italic : FontStyles.Normal;
    }
    public sealed class AchievementGoal : ObservableObjectExt
    {
        public Guid GameId { get; set; }
        public string GameName { get; set; }
        private double targetPercent = 100;
        public double TargetPercent { get => targetPercent; set => SetValue(ref targetPercent, double.IsNaN(value) || double.IsInfinity(value) ? 100 : Math.Max(1, Math.Min(100, value))); }
    }
    public sealed class AchievementWidgetSettings : ObservableObjectExt
    {
        private bool customizeRecentIconSize, customizeRecentTypography, customizeRecentProgress, showRecentExtraDetails, showRecentGameName;
        public bool CustomizeRecentIconSize { get => customizeRecentIconSize; set => SetValue(ref customizeRecentIconSize,value); }
        public bool CustomizeRecentTypography { get => customizeRecentTypography; set => SetValue(ref customizeRecentTypography,value); }
        public bool CustomizeRecentProgress { get => customizeRecentProgress; set => SetValue(ref customizeRecentProgress,value); }
        public bool ShowRecentExtraDetails { get => showRecentExtraDetails; set => SetValue(ref showRecentExtraDetails,value); }
        public bool ShowRecentGameName { get => showRecentGameName; set => SetValue(ref showRecentGameName,value); }
        private RecentAchievementGroupOrder recentGroupOrder;
        public RecentAchievementGroupOrder RecentGroupOrder { get => recentGroupOrder; set => SetValue(ref recentGroupOrder,value); }
        private RecentAchievementGrouping recentGrouping;
        public RecentAchievementGrouping RecentGrouping { get => groupByGame ? recentGrouping : RecentAchievementGrouping.None; set { SetValue(ref recentGrouping,value); GroupByGame = value != RecentAchievementGrouping.None; } }
        private bool showGroupProgress = true;
        public bool ShowGroupProgress { get => showGroupProgress; set => SetValue(ref showGroupProgress,value); }
        private string gameNameFilter = "", sourceNameFilter = "", platformNameFilter = "";
        public string GameNameFilter { get => gameNameFilter; set => SetValue(ref gameNameFilter,value ?? ""); }
        public string SourceNameFilter { get => sourceNameFilter; set => SetValue(ref sourceNameFilter,value ?? ""); }
        public string PlatformNameFilter { get => platformNameFilter; set => SetValue(ref platformNameFilter,value ?? ""); }
        private bool detailBold;
        private bool? detailItalic;
        public bool DetailBold { get => detailBold; set => SetValue(ref detailBold,value); }
        public bool DetailItalic { get => detailItalic ?? Italic; set => SetValue(ref detailItalic,(bool?)value); }
        private Dictionary<string,AchievementTextStyle> textStyles = Enum.GetValues(typeof(AchievementTextRole)).Cast<AchievementTextRole>().ToDictionary(role => role.ToString(),role => new AchievementTextStyle());
        public Dictionary<string,AchievementTextStyle> TextStyles { get => textStyles; set {
            var styles = value ?? new Dictionary<string,AchievementTextStyle>();
            foreach (AchievementTextRole role in Enum.GetValues(typeof(AchievementTextRole))) if (!styles.TryGetValue(role.ToString(),out var style) || style == null) styles[role.ToString()] = new AchievementTextStyle();
            SetValue(ref textStyles,styles);
        } }
        private int perGameLimit = 5, recentDays;
        public int PerGameLimit { get => perGameLimit; set => SetValue(ref perGameLimit,Math.Max(0,value)); }
        public int RecentDays { get => recentDays; set => SetValue(ref recentDays,Math.Max(0,value)); }
        private bool groupByGame = true;
        public bool GroupByGame { get => groupByGame; set => SetValue(ref groupByGame,value); }
        private AchievementListFilter listFilter;
        public AchievementListFilter ListFilter { get => listFilter; set => SetValue(ref listFilter,value); }
        private AchievementListOrder listOrder;
        public AchievementListOrder ListOrder { get => listOrder; set => SetValue(ref listOrder,value); }
        private bool showDescription = true, showUnlockDate = true, showUnlockState = true;
        public bool ShowDescription { get => showDescription; set => SetValue(ref showDescription,value); }
        public bool ShowUnlockDate { get => showUnlockDate; set => SetValue(ref showUnlockDate,value); }
        public bool ShowUnlockState { get => showUnlockState; set => SetValue(ref showUnlockState,value); }
        private bool keepLastHoveredGame = true;
        public bool KeepLastHoveredGame { get => keepLastHoveredGame; set => SetValue(ref keepLastHoveredGame,value); }
        private AchievementGoalSource gameSource;
        public AchievementGoalSource GameSource { get => gameSource; set => SetValue(ref gameSource,value); }
        private double targetPercent = 100;
        public double TargetPercent { get => targetPercent; set => SetValue(ref targetPercent,Clamp(value,1,100)); }
        public Guid? ResolveGoalGame(Guid? selected, Guid? hovered) => GameSource == AchievementGoalSource.HoveredOrSelectedGame ? hovered ?? selected
            : GameSource == AchievementGoalSource.HoveredGame ? hovered : GameSource == AchievementGoalSource.SelectedGame ? selected : null;
        private AchievementCardLayout layout;
        public AchievementCardLayout Layout { get => layout; set => SetValue(ref layout,value); }
        private AchievementProgressStyle progressStyle;
        public AchievementProgressStyle ProgressStyle { get => progressStyle; set => SetValue(ref progressStyle,value); }
        private AchievementGameOrder order;
        public AchievementGameOrder Order { get => order; set => SetValue(ref order,value); }
        private double minimumPercent;
        public double MinimumPercent { get => minimumPercent; set => SetValue(ref minimumPercent,Clamp(value,0,100)); }
        private int maximumRemaining;
        public int MaximumRemaining { get => maximumRemaining; set => SetValue(ref maximumRemaining,Math.Max(0,value)); }
        private int limit = 12;
        public int Limit { get => limit; set => SetValue(ref limit,Math.Max(0,value)); }
        private bool includeHidden, installedOnly, favoritesOnly, includeCompleted = true, showArtwork = true,
            showTitle = true, showCounts = true, showPercent = true, showRemaining = true, bold = true, italic;
        public bool IncludeHidden { get => includeHidden; set => SetValue(ref includeHidden,value); }
        public bool InstalledOnly { get => installedOnly; set => SetValue(ref installedOnly,value); }
        public bool FavoritesOnly { get => favoritesOnly; set => SetValue(ref favoritesOnly,value); }
        public bool IncludeCompleted { get => includeCompleted; set => SetValue(ref includeCompleted,value); }
        public bool ShowArtwork { get => showArtwork; set => SetValue(ref showArtwork,value); }
        public bool ShowTitle { get => showTitle; set => SetValue(ref showTitle,value); }
        public bool ShowCounts { get => showCounts; set => SetValue(ref showCounts,value); }
        public bool ShowPercent { get => showPercent; set => SetValue(ref showPercent,value); }
        public bool ShowRemaining { get => showRemaining; set => SetValue(ref showRemaining,value); }
        public bool Bold { get => bold; set => SetValue(ref bold,value); }
        public bool Italic { get => italic; set => SetValue(ref italic,value); }
        private double titleSize = 18, detailSize = 13, cardWidth = 260, artworkSize = 54, spacing = 8,
            barHeight = 8, ringSize = 64, borderWidth = 1, cornerRadius = 8, padding = 12, backgroundOpacity = .2;
        public double TitleSize { get => titleSize; set => SetValue(ref titleSize,Clamp(value,8,100)); }
        public double DetailSize { get => detailSize; set => SetValue(ref detailSize,Clamp(value,8,100)); }
        public double CardWidth { get => cardWidth; set => SetValue(ref cardWidth,Clamp(value,140,1000)); }
        public double ArtworkSize { get => artworkSize; set => SetValue(ref artworkSize,Clamp(value,20,200)); }
        public double Spacing { get => spacing; set => SetValue(ref spacing,Clamp(value,0,100)); }
        public double BarHeight { get => barHeight; set => SetValue(ref barHeight,Clamp(value,2,50)); }
        public double RingSize { get => ringSize; set => SetValue(ref ringSize,Clamp(value,30,200)); }
        public double BorderWidth { get => borderWidth; set => SetValue(ref borderWidth,Clamp(value,0,20)); }
        public double CornerRadius { get => cornerRadius; set => SetValue(ref cornerRadius,Clamp(value,0,100)); }
        public double Padding { get => padding; set => SetValue(ref padding,Clamp(value,0,100)); }
        public double BackgroundOpacity { get => backgroundOpacity; set => SetValue(ref backgroundOpacity,Clamp(value,0,1)); }
        private TextAlignment alignment;
        public TextAlignment Alignment { get => alignment; set => SetValue(ref alignment,value); }
        private bool customTextColors;
        public bool CustomTextColors { get => customTextColors; set => SetValue(ref customTextColors,value); }
        private string titleColor = "#FFFFFF", detailColor = "#FFFFFF", progressColor = "#42B5F5", completeColor = "#66BB6A",
            trackColor = "#404040", cardColor = "#808080", borderColor = "#606060";
        public string TitleColor { get => titleColor; set => SetValue(ref titleColor,Color(value)); }
        public string DetailColor { get => detailColor; set => SetValue(ref detailColor,Color(value)); }
        public string ProgressColor { get => progressColor; set => SetValue(ref progressColor,Color(value)); }
        public string CompleteColor { get => completeColor; set => SetValue(ref completeColor,Color(value)); }
        public string TrackColor { get => trackColor; set => SetValue(ref trackColor,Color(value)); }
        public string CardColor { get => cardColor; set => SetValue(ref cardColor,Color(value)); }
        public string BorderColor { get => borderColor; set => SetValue(ref borderColor,Color(value)); }
        private ObservableCollection<AchievementGoal> goals = new ObservableCollection<AchievementGoal>();
        public ObservableCollection<AchievementGoal> Goals { get => goals; set => SetValue(ref goals,value ?? new ObservableCollection<AchievementGoal>()); }
        private static double Clamp(double n,double min,double max) => double.IsNaN(n) || double.IsInfinity(n) ? min : Math.Max(min,Math.Min(max,n));
        private static string Color(string value) { try { return ((System.Windows.Media.Color)ColorConverter.ConvertFromString(value)).ToString(); } catch { return "#FFFFFF"; } }
        private static void MigrateLayout(Models.Layout.GridNode layout,int total,int perGame)
        {
            if (layout == null) return;
            foreach (var node in layout.Enumerate()) node.ViewProperties?.MigrateRecentAchievementSettings(total,perGame);
        }
        public static void MigrateLegacy(LandingPageSettings settings)
        {
            if (settings == null) return;
            MigrateLayout(settings.GridLayout,settings.MaxNumberRecentAchievements,settings.MaxNumberRecentAchievementsPerGame);
            if (settings.DashboardPages != null) foreach (var page in settings.DashboardPages)
                MigrateLayout(page?.Layout,settings.MaxNumberRecentAchievements,settings.MaxNumberRecentAchievementsPerGame);
            if (settings.GridNodePresets != null) foreach (var preset in settings.GridNodePresets)
                MigrateLayout(preset?.Node,settings.MaxNumberRecentAchievements,settings.MaxNumberRecentAchievementsPerGame);
            if (settings.DashboardProfiles != null) foreach (var profile in settings.DashboardProfiles)
                MigrateLegacySnapshot(profile?.Snapshot,settings.MaxNumberRecentAchievements,settings.MaxNumberRecentAchievementsPerGame);
        }
        public static void MigrateLegacySnapshot(Models.DashboardSnapshot snapshot,int total = 20,int perGame = 5)
        {
            if (snapshot?.Pages == null) return;
            if (snapshot.SharedSettings != null) {
                if (snapshot.SharedSettings.TryGetValue("MaxNumberRecentAchievements",out var count) && count.Type == Newtonsoft.Json.Linq.JTokenType.Integer) total = count.ToObject<int>();
                if (snapshot.SharedSettings.TryGetValue("MaxNumberRecentAchievementsPerGame",out count) && count.Type == Newtonsoft.Json.Linq.JTokenType.Integer) perGame = count.ToObject<int>();
            }
            foreach (var page in snapshot.Pages) MigrateLayout(page?.Layout,total,perGame);
        }
        public static bool IsWidget(string id) => id == "AchievementProgress" || id == "AchievementFinishNext" || id == "AchievementGoals" || id == "GameAchievementList" || id == "RecentAchivements";
        public static AchievementWidgetSettings Create(string id) => new AchievementWidgetSettings {
            ArtworkSize = id == "RecentAchivements" ? 35 : 54,
            GameSource = id == "GameAchievementList" ? AchievementGoalSource.HoveredOrSelectedGame : AchievementGoalSource.PinnedGames,
            Limit = id == "GameAchievementList" ? 0 : id == "RecentAchivements" ? 20 : 12,
            ListOrder = id == "RecentAchivements" ? AchievementListOrder.RecentUnlock : AchievementListOrder.ProviderOrder,
            Layout = id == "AchievementProgress" ? AchievementCardLayout.Cards : AchievementCardLayout.Rows,
            ProgressStyle = id == "AchievementProgress" ? AchievementProgressStyle.Ring : AchievementProgressStyle.Bar,
            IncludeCompleted = id != "AchievementFinishNext", Order = id == "AchievementFinishNext" ? AchievementGameOrder.RemainingAscending : AchievementGameOrder.ProgressDescending };
    }
}
