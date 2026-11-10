using LandingPage.Converters;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandingPage.Models
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum Order
    {
        [Description("LOCMenuSortAscending")]
        Ascending = 0,
        [Description("LOCMenuSortDescending")]
        Descending = 1
    }

    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum SortingField
    {
        [Description("LOCGameNameTitle")]
        Name,
        [Description("LOCGameSortingNameTitle")]
        SortingName,
        [Description("LOCGameLastActivityTitle")]
        LastActivity,
        [Description("LOCDateAddedLabel")]
        DateAdded,
        [Description("LOCTimePlayed")]
        Playtime,
        [Description("LOCGameReleaseDateTitle")]
        ReleaseDate,
        [Description("LOCCriticScore")]
        CriticScore,
        [Description("LOCUserScore")]
        UserScore,
        [Description("LOCCommunityScore")]
        CommunityScore,
        [Description("LOCCompletionStatus")]
        CompletionStatus,
        [Description("LOC_SPG_BackgroundImageSourceRandom")]
        Random,
        [Description("LOCCustomLabel")]
        Custom
    }

    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum GroupingField
    {
        [Description("LOCMenuGroupDont")]
        None,
        [Description("LOCGameLastActivityTitle")]
        LastActivity,
        [Description("LOCDateAddedLabel")]
        DateAdded,
        [Description("LOCCriticScore")]
        CriticScore,
        [Description("LOCUserScore")]
        UserScore,
        [Description("LOCCommunityScore")]
        CommunityScore,
        [Description("LOCLibrary")]
        Library,
        [Description("LOCCompletionStatus")]
        CompletionStatus
    }

    public enum ShelfMetadataPosition { BelowArtwork, OverArtwork }

    public enum ShelfTitleDisplay { Default, Show, Hide }

    public enum ShelfDisplayStyle { CoverRow, CoverGrid, CompactList, WideCards }

    public partial class ShelveProperties : ObservableObject
    {
        private ShelfDisplayStyle displayStyle;
        public ShelfDisplayStyle DisplayStyle { get => displayStyle; set => SetValue(ref displayStyle, value); }
        private double cardWidth = 160;
        public double CardWidth { get => cardWidth; set => SetValue(ref cardWidth,
            double.IsNaN(value) || double.IsInfinity(value) ? 160 : Math.Max(80, Math.Min(500, value))); }

        private ShelfMetadataPosition metadataPosition;
        public ShelfMetadataPosition MetadataPosition { get => metadataPosition; set => SetValue(ref metadataPosition, value); }
        private string metadataBackground = "#000000";
        public string MetadataBackground
        {
            get => metadataBackground;
            set
            {
                try { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); }
                catch (Exception) { throw new ArgumentException("Enter a valid color."); }
                SetValue(ref metadataBackground, value);
            }
        }
        private double metadataOpacity = 0.7;
        public double MetadataOpacity { get => metadataOpacity; set => SetValue(ref metadataOpacity,
            double.IsNaN(value) || double.IsInfinity(value) ? 0.7 : Math.Max(0, Math.Min(1, value))); }

        private bool customizeMetadataText = false;
        public bool CustomizeMetadataText { get => customizeMetadataText; set => SetValue(ref customizeMetadataText, value); }
        private string metadataTextColor = "#FFFFFF";
        public string MetadataTextColor { get => metadataTextColor; set
            {
                try { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); }
                catch (Exception) { throw new ArgumentException("Enter a valid color."); }
                SetValue(ref metadataTextColor, value);
            } }
        private string metadataTitleColor = "#FFFFFF";
        public string MetadataTitleColor { get => metadataTitleColor; set
            {
                try { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); }
                catch (Exception) { throw new ArgumentException("Enter a valid color."); }
                SetValue(ref metadataTitleColor, value);
            } }
        private double metadataTextSize = 12;
        public double MetadataTextSize { get => metadataTextSize; set => SetValue(ref metadataTextSize, double.IsNaN(value) || double.IsInfinity(value) ? 12 : Math.Max(8, Math.Min(72, value))); }
        private double metadataTitleSize = 14;
        public double MetadataTitleSize { get => metadataTitleSize; set => SetValue(ref metadataTitleSize, double.IsNaN(value) || double.IsInfinity(value) ? 14 : Math.Max(8, Math.Min(72, value))); }
        private bool metadataTitleBold = true;
        public bool MetadataTitleBold { get => metadataTitleBold; set => SetValue(ref metadataTitleBold, value); }
        private bool metadataTextBold = false;
        public bool MetadataTextBold { get => metadataTextBold; set => SetValue(ref metadataTextBold, value); }
        private bool metadataItalic = false;
        public bool MetadataItalic { get => metadataItalic; set => SetValue(ref metadataItalic, value); }
        private bool metadataWrap = false;
        public bool MetadataWrap { get => metadataWrap; set => SetValue(ref metadataWrap, value); }
        private System.Windows.TextAlignment metadataTextAlignment = System.Windows.TextAlignment.Left;
        public System.Windows.TextAlignment MetadataTextAlignment { get => metadataTextAlignment; set => SetValue(ref metadataTextAlignment, value); }
        private bool showMetadataBackground = true;
        public bool ShowMetadataBackground { get => showMetadataBackground; set => SetValue(ref showMetadataBackground, value); }
        private double metadataPadding = 6;
        public double MetadataPadding { get => metadataPadding; set => SetValue(ref metadataPadding, double.IsNaN(value) || double.IsInfinity(value) ? 6 : Math.Max(0, Math.Min(40, value))); }
        private double metadataCornerRadius = 0;
        public double MetadataCornerRadius { get => metadataCornerRadius; set => SetValue(ref metadataCornerRadius, double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(40, value))); }

        private ShelfTitleDisplay titleDisplay;
        public ShelfTitleDisplay TitleDisplay { get => titleDisplay; set => SetValue(ref titleDisplay, value); }
        private bool showPlaytime;
        public bool ShowPlaytime { get => showPlaytime; set => SetValue(ref showPlaytime, value); }
        private bool showLastPlayed;
        public bool ShowLastPlayed { get => showLastPlayed; set => SetValue(ref showLastPlayed, value); }
        private bool showPlatform;
        public bool ShowPlatform { get => showPlatform; set => SetValue(ref showPlatform, value); }
        private bool showSource;
        public bool ShowSource { get => showSource; set => SetValue(ref showSource, value); }
        private bool showSourceIcon;
        public bool ShowSourceIcon { get => showSourceIcon; set => SetValue(ref showSourceIcon, value); }
        private bool showPlatformIcons;
        public bool ShowPlatformIcons { get => showPlatformIcons; set => SetValue(ref showPlatformIcons, value); }
        private bool showAchievementTrophy;
        public bool ShowAchievementTrophy { get => showAchievementTrophy; set => SetValue(ref showAchievementTrophy, value); }
        private double metadataIconSize = 16;
        public double MetadataIconSize { get => metadataIconSize; set => SetValue(ref metadataIconSize,
            double.IsNaN(value) || double.IsInfinity(value) ? 16 : Math.Max(8, Math.Min(48, value))); }
        private bool showFavoriteBadge;
        public bool ShowFavoriteBadge { get => showFavoriteBadge; set => SetValue(ref showFavoriteBadge, value); }
        private bool showInstallationStatus;
        public bool ShowInstallationStatus { get => showInstallationStatus; set => SetValue(ref showInstallationStatus, value); }
        private bool showAchievementProgress;
        public bool ShowAchievementProgress { get => showAchievementProgress; set => SetValue(ref showAchievementProgress, value); }

        public static ShelveProperties RecentlyPlayed
            => new ShelveProperties { Order = Order.Descending, SortBy = SortingField.LastActivity, GroupBy = GroupingField.LastActivity, Name = ResourceProvider.GetString("LOCQuickFilterRecentlyPlayed") };

        public static ShelveProperties RecentlyAdded
            => new ShelveProperties { Order = Order.Descending, SortBy = SortingField.DateAdded, GroupBy = GroupingField.DateAdded, Name = ResourceProvider.GetString("LOC_SPG_RecentlyAddedGames") };

        private string name = string.Empty;
        public string Name { get => name; set => SetValue(ref name, value); }

        private bool unlimitedGames;
        public bool UnlimitedGames { get => unlimitedGames; set => SetValue(ref unlimitedGames, value); }

        private int numberOfGames = 11;
        public int NumberOfGames { get => numberOfGames; set => SetValue(ref numberOfGames, Math.Max(0, value)); }

        private int skippedGames = 0;
        public int SkippedGames { get => Math.Max(0, skippedGames); set => SetValue(ref skippedGames, Math.Max(0, value)); }

        private Order order = Order.Ascending;
        public Order Order { get => order; set => SetValue(ref order, value); }

        private SortingField sortBy = SortingField.Name;
        public SortingField SortBy { get => sortBy; set => SetValue(ref sortBy, value); }

        private GroupingField groupBy = GroupingField.LastActivity;
        public GroupingField GroupBy { get => groupBy; set => SetValue(ref groupBy, value); }

        private bool ignoreHidden = true;
        public bool IgnoreHidden { get => ignoreHidden; set => SetValue(ref ignoreHidden, value); }

        private bool installedOnly = false;
        public bool InstalledOnly { get => installedOnly; set => SetValue(ref installedOnly, value); }

        private bool favoritesOnly = false;
        public bool FavoritesOnly { get => favoritesOnly; set => SetValue(ref favoritesOnly, value); }

        private HashSet<Guid> categories = new HashSet<Guid>();
        public HashSet<Guid> Categories { get => categories; set => SetValue(ref categories, value); }

        private HashSet<Guid> tags = new HashSet<Guid>();
        public HashSet<Guid> Tags { get => tags; set => SetValue(ref tags, value); }

        private HashSet<Guid> completionStatus = new HashSet<Guid>();
        public HashSet<Guid> CompletionStatus { get => completionStatus; set => SetValue(ref completionStatus, value); }

        private HashSet<Guid> features = new HashSet<Guid>();
        public HashSet<Guid> Features { get => features; set => SetValue(ref features, value); }

        private HashSet<Guid> platforms = new HashSet<Guid>();
        public HashSet<Guid> Platforms { get => platforms; set => SetValue(ref platforms, value); }

        private HashSet<Guid> sources = new HashSet<Guid>();
        public HashSet<Guid> Sources { get => sources; set => SetValue(ref sources, value); }

        private HashSet<Guid> genres = new HashSet<Guid>();
        public HashSet<Guid> Genres { get => genres; set => SetValue(ref genres, value); }
    }
}
