using System;
using System.Windows;
using System.Collections.ObjectModel;
using System.Linq;
using LandingPage.Models.Layout;

namespace LandingPage.Settings
{
    public enum FeaturedGameSource { Pinned, LastPlayed, LastAdded, Random, RandomFavorite, SelectedGame, HoveredGame, HoveredOrSelectedGame }
    public enum FeaturedArtworkSizing { Fill, Fit, FitWithoutEnlarging, Stretch, OriginalSize }
    public enum FeaturedGameSection { Title, Description, Activity, GameDetails, LibraryInformation, Scores, Links, Actions }
    public class FeaturedSectionLayout : ObservableObjectExt
    {
        public FeaturedGameSection Section { get; set; }
        private bool overrideAppearance;
        public bool OverrideAppearance { get => overrideAppearance; set => SetValue(ref overrideAppearance,value); }
        private PanelAppearance appearance = new PanelAppearance();
        public PanelAppearance Appearance { get => appearance; set => SetValue(ref appearance,value ?? new PanelAppearance()); }
        [Newtonsoft.Json.JsonIgnore]
        public string Name => Section == FeaturedGameSection.GameDetails ? "Game details" : Section == FeaturedGameSection.LibraryInformation ? "Library information" : Section.ToString();
        private double x = 24, y = 24, width = 620, height = 100;
        private static double Bound(double value, double min, double max) => double.IsNaN(value) || double.IsInfinity(value) ? min : Math.Max(min,Math.Min(max,value));
        public double X { get => x; set => SetValue(ref x,Bound(value,0,5000)); }
        public double Y { get => y; set => SetValue(ref y,Bound(value,0,5000)); }
        public double Width { get => width; set => SetValue(ref width,Bound(value,120,1400)); }
        public double Height { get => height; set => SetValue(ref height,Bound(value,48,2000)); }
    }
    public class FeaturedGameSettings : ObservableObjectExt
    {
        private PanelAppearance sectionAppearance = new PanelAppearance();
        public PanelAppearance SectionAppearance { get => sectionAppearance; set => SetValue(ref sectionAppearance,value ?? new PanelAppearance()); }
        private bool editSectionLayout;
        [Newtonsoft.Json.JsonIgnore]
        public bool EditSectionLayout { get => editSectionLayout; set => SetValue(ref editSectionLayout,value); }
        private bool useCustomSectionLayout;
        public bool UseCustomSectionLayout { get => useCustomSectionLayout; set => SetValue(ref useCustomSectionLayout,value); }
        private bool hasCustomSectionPositions;
        public bool HasCustomSectionPositions { get => hasCustomSectionPositions; set => SetValue(ref hasCustomSectionPositions,value); }
        private ObservableCollection<FeaturedSectionLayout> sectionLayouts = DefaultSectionLayouts();
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public ObservableCollection<FeaturedSectionLayout> SectionLayouts { get => sectionLayouts; set => SetValue(ref sectionLayouts,value ?? DefaultSectionLayouts()); }
        private static ObservableCollection<FeaturedSectionLayout> DefaultSectionLayouts() => new ObservableCollection<FeaturedSectionLayout>(
            Enum.GetValues(typeof(FeaturedGameSection)).Cast<FeaturedGameSection>().Select((section,index) => new FeaturedSectionLayout { Section = section, Y = 24+index*150 }));
        public FeaturedSectionLayout GetSectionLayout(FeaturedGameSection section)
        {
            var layout = SectionLayouts.FirstOrDefault(item => item != null && item.Section == section);
            if (layout == null) { layout = new FeaturedSectionLayout { Section = section }; SectionLayouts.Add(layout); }
            return layout;
        }
        public void ResetSectionLayout()
        {
            EditSectionLayout = false; UseCustomSectionLayout = false; HasCustomSectionPositions = false;
            var defaults = DefaultSectionLayouts();
            foreach (var item in defaults)
            {
                var saved = SectionLayouts.FirstOrDefault(old => old != null && old.Section == item.Section);
                if (saved != null) { item.OverrideAppearance = saved.OverrideAppearance; item.Appearance = saved.Appearance; }
            }
            SectionLayouts = defaults;
        }
        private FeaturedGameSource source = FeaturedGameSource.LastPlayed;
        public FeaturedGameSource Source { get => source; set => SetValue(ref source, value); }
        private int hoverDelayMilliseconds = 150;
        public int HoverDelayMilliseconds { get => hoverDelayMilliseconds; set => SetValue(ref hoverDelayMilliseconds,Math.Max(0,Math.Min(1000,value))); }
        private bool keepLastHoveredGame = true;
        public bool KeepLastHoveredGame { get => keepLastHoveredGame; set => SetValue(ref keepLastHoveredGame,value); }
        private Guid pinnedGameId = Guid.Empty;
        public Guid PinnedGameId { get => pinnedGameId; set => SetValue(ref pinnedGameId, value); }
        private string pinnedGameName = string.Empty;
        public string PinnedGameName { get => pinnedGameName; set => SetValue(ref pinnedGameName, value ?? string.Empty); }
        private bool installedOnly = false;
        public bool InstalledOnly { get => installedOnly; set => SetValue(ref installedOnly, value); }
        private bool includeHidden = false;
        public bool IncludeHidden { get => includeHidden; set => SetValue(ref includeHidden, value); }
        private bool showTitle = true;
        public bool ShowTitle { get => showTitle; set => SetValue(ref showTitle, value); }
        private bool showSectionHeadings = true;
        public bool ShowSectionHeadings { get => showSectionHeadings; set => SetValue(ref showSectionHeadings, value); }
        private bool showGameDetails = false;
        public bool ShowGameDetails { get => showGameDetails; set => SetValue(ref showGameDetails, value); }
        private bool showPlatforms = true;
        public bool ShowPlatforms { get => showPlatforms; set => SetValue(ref showPlatforms, value); }
        private bool showGenres = true;
        public bool ShowGenres { get => showGenres; set => SetValue(ref showGenres, value); }
        private bool showReleaseDate = true;
        public bool ShowReleaseDate { get => showReleaseDate; set => SetValue(ref showReleaseDate, value); }
        private bool showDevelopers = true;
        public bool ShowDevelopers { get => showDevelopers; set => SetValue(ref showDevelopers, value); }
        private bool showPublishers = true;
        public bool ShowPublishers { get => showPublishers; set => SetValue(ref showPublishers, value); }
        private bool showLibraryDetails = false;
        public bool ShowLibraryDetails { get => showLibraryDetails; set => SetValue(ref showLibraryDetails, value); }
        private bool showCompletionStatus = true;
        public bool ShowCompletionStatus { get => showCompletionStatus; set => SetValue(ref showCompletionStatus, value); }
        private bool showCategories = true;
        public bool ShowCategories { get => showCategories; set => SetValue(ref showCategories, value); }
        private bool showTags = true;
        public bool ShowTags { get => showTags; set => SetValue(ref showTags, value); }
        private bool showFeatures = true;
        public bool ShowFeatures { get => showFeatures; set => SetValue(ref showFeatures, value); }
        private bool showDateAdded = true;
        public bool ShowDateAdded { get => showDateAdded; set => SetValue(ref showDateAdded, value); }
        private bool showInstallationStatus = true;
        public bool ShowInstallationStatus { get => showInstallationStatus; set => SetValue(ref showInstallationStatus, value); }
        private bool showVersion = true;
        public bool ShowVersion { get => showVersion; set => SetValue(ref showVersion, value); }
        private bool showScores = false;
        public bool ShowScores { get => showScores; set => SetValue(ref showScores, value); }
        private bool showUserScore = true;
        public bool ShowUserScore { get => showUserScore; set => SetValue(ref showUserScore, value); }
        private bool showCriticScore = true;
        public bool ShowCriticScore { get => showCriticScore; set => SetValue(ref showCriticScore, value); }
        private bool showCommunityScore = true;
        public bool ShowCommunityScore { get => showCommunityScore; set => SetValue(ref showCommunityScore, value); }
        private bool showLinks = false;
        public bool ShowLinks { get => showLinks; set => SetValue(ref showLinks, value); }
        private bool showDescription = true;
        public bool ShowDescription { get => showDescription; set => SetValue(ref showDescription, value); }
        private bool preserveDescriptionFormatting = true;
        public bool PreserveDescriptionFormatting { get => preserveDescriptionFormatting; set => SetValue(ref preserveDescriptionFormatting,value); }
        private bool showDescriptionImages;
        public bool ShowDescriptionImages { get => showDescriptionImages; set => SetValue(ref showDescriptionImages,value); }
        private bool autoScrollContent;
        public bool AutoScrollContent { get => autoScrollContent; set => SetValue(ref autoScrollContent,value); }
        private double autoScrollSpeed = 20;
        public double AutoScrollSpeed { get => autoScrollSpeed; set => SetValue(ref autoScrollSpeed,Clamp(value,1,120)); }
        private bool pauseAutoScrollOnHover = true;
        public bool PauseAutoScrollOnHover { get => pauseAutoScrollOnHover; set => SetValue(ref pauseAutoScrollOnHover,value); }
        private int descriptionLength = 400;
        public int DescriptionLength { get => descriptionLength; set => SetValue(ref descriptionLength, Math.Max(0,value)); }
        private bool showPlaytime = true;
        public bool ShowPlaytime { get => showPlaytime; set => SetValue(ref showPlaytime, value); }
        private bool showLastPlayed = false;
        public bool ShowLastPlayed { get => showLastPlayed; set => SetValue(ref showLastPlayed, value); }
        private bool showSource = true;
        public bool ShowSource { get => showSource; set => SetValue(ref showSource, value); }
        private bool showAchievements = false;
        public bool ShowAchievements { get => showAchievements; set => SetValue(ref showAchievements, value); }
        private bool showPlayButton = true;
        public bool ShowPlayButton { get => showPlayButton; set => SetValue(ref showPlayButton, value); }
        private bool showLibraryButton = true;
        public bool ShowLibraryButton { get => showLibraryButton; set => SetValue(ref showLibraryButton, value); }
        private bool showArtwork = true;
        public bool ShowArtwork { get => showArtwork; set => SetValue(ref showArtwork, value); }
        private System.Windows.Media.Stretch artworkStretch = System.Windows.Media.Stretch.UniformToFill;
        public System.Windows.Media.Stretch ArtworkStretch { get => artworkStretch; set => SetValue(ref artworkStretch,
            Enum.IsDefined(typeof(System.Windows.Media.Stretch),value) ? value : System.Windows.Media.Stretch.UniformToFill,
            nameof(ArtworkStretch),nameof(ArtworkSizing)); }
        private bool artworkShrinkOnly;
        public bool ArtworkShrinkOnly { get => artworkShrinkOnly; set => SetValue(ref artworkShrinkOnly,value,nameof(ArtworkShrinkOnly),nameof(ArtworkSizing)); }
        // Keep the persisted stretch value so existing Fit/Fill profiles retain their appearance.
        [Newtonsoft.Json.JsonIgnore]
        public FeaturedArtworkSizing ArtworkSizing
        {
            get => artworkStretch == System.Windows.Media.Stretch.None ? FeaturedArtworkSizing.OriginalSize
                : artworkStretch == System.Windows.Media.Stretch.Fill ? FeaturedArtworkSizing.Stretch
                : artworkStretch == System.Windows.Media.Stretch.Uniform ? (artworkShrinkOnly ? FeaturedArtworkSizing.FitWithoutEnlarging : FeaturedArtworkSizing.Fit)
                : FeaturedArtworkSizing.Fill;
            set
            {
                artworkShrinkOnly = value == FeaturedArtworkSizing.FitWithoutEnlarging;
                artworkStretch = value == FeaturedArtworkSizing.OriginalSize ? System.Windows.Media.Stretch.None
                    : value == FeaturedArtworkSizing.Stretch ? System.Windows.Media.Stretch.Fill
                    : value == FeaturedArtworkSizing.Fit || value == FeaturedArtworkSizing.FitWithoutEnlarging ? System.Windows.Media.Stretch.Uniform
                    : System.Windows.Media.Stretch.UniformToFill;
                NotifyPropertyChanged(nameof(ArtworkSizing),null,ArtworkSizing);
            }
        }
        private bool useCoverArtwork = false;
        public bool UseCoverArtwork { get => useCoverArtwork; set => SetValue(ref useCoverArtwork, value); }
        private double artworkOpacity = 1;
        public double ArtworkOpacity { get => artworkOpacity; set => SetValue(ref artworkOpacity, Clamp(value, 0, 1)); }
        private double overlayOpacity = 0.6;
        public double OverlayOpacity { get => overlayOpacity; set => SetValue(ref overlayOpacity, Clamp(value, 0, 1)); }
        private string overlayColor = "#101010";
        public string OverlayColor { get => overlayColor; set { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); SetValue(ref overlayColor, value); } }
        private bool useCustomTextColor = false;
        public bool UseCustomTextColor { get => useCustomTextColor; set => SetValue(ref useCustomTextColor, value); }
        private string titleColor = "#FFFFFF";
        public string TitleColor { get => titleColor; set { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); SetValue(ref titleColor, value); } }
        private string textColor = "#FFFFFF";
        public string TextColor { get => textColor; set { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); SetValue(ref textColor, value); } }
        private double titleSize = 32;
        public double TitleSize { get => titleSize; set => SetValue(ref titleSize, Clamp(value, 12, 96)); }
        private double textSize = 14;
        public double TextSize { get => textSize; set => SetValue(ref textSize, Clamp(value, 8, 48)); }
        private bool matchMetadataTextSize = true;
        public bool MatchMetadataTextSize { get => matchMetadataTextSize; set => SetValue(ref matchMetadataTextSize, value); }
        private double metadataTextSize = 14;
        public double MetadataTextSize { get => metadataTextSize; set => SetValue(ref metadataTextSize, Clamp(value, 8, 48)); }
        private bool matchMetadataTextColor = true;
        public bool MatchMetadataTextColor { get => matchMetadataTextColor; set => SetValue(ref matchMetadataTextColor, value); }
        private bool useCustomMetadataColor;
        public bool UseCustomMetadataColor { get => useCustomMetadataColor; set => SetValue(ref useCustomMetadataColor, value); }
        private string metadataTextColor = "#FFFFFF";
        public string MetadataTextColor { get => metadataTextColor; set { System.Windows.Media.ColorConverter.ConvertFromString(value ?? string.Empty); SetValue(ref metadataTextColor, value); } }
        private bool matchMetadataIconSize = true;
        public bool MatchMetadataIconSize { get => matchMetadataIconSize; set => SetValue(ref matchMetadataIconSize, value); }
        private double metadataIconSize = 16;
        public double MetadataIconSize { get => metadataIconSize; set => SetValue(ref metadataIconSize, Clamp(value, 8, 64)); }
        private double contentWidth = 620;
        public double ContentWidth { get => contentWidth; set => SetValue(ref contentWidth, Clamp(value, 160, 1400)); }
        private VerticalAlignment verticalAlignment = VerticalAlignment.Bottom;
        public VerticalAlignment VerticalAlignment { get => verticalAlignment; set => SetValue(ref verticalAlignment, value); }
        private HorizontalAlignment alignment = HorizontalAlignment.Left;
        public HorizontalAlignment Alignment { get => alignment; set => SetValue(ref alignment, value); }
        private static double Clamp(double value, double min, double max) => double.IsNaN(value) || double.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));
    }
}
