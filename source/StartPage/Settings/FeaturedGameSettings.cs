using System;
using System.Windows;

namespace LandingPage.Settings
{
    public enum FeaturedGameSource { Pinned, LastPlayed, LastAdded, Random, RandomFavorite, SelectedGame }
    public class FeaturedGameSettings : ObservableObjectExt
    {
        private FeaturedGameSource source = FeaturedGameSource.LastPlayed;
        public FeaturedGameSource Source { get => source; set => SetValue(ref source, value); }
        private Guid pinnedGameId = Guid.Empty;
        public Guid PinnedGameId { get => pinnedGameId; set => SetValue(ref pinnedGameId, value); }
        private string pinnedGameName = string.Empty;
        public string PinnedGameName { get => pinnedGameName; set => SetValue(ref pinnedGameName, value ?? string.Empty); }
        private bool installedOnly = false;
        public bool InstalledOnly { get => installedOnly; set => SetValue(ref installedOnly, value); }
        private bool includeHidden = false;
        public bool IncludeHidden { get => includeHidden; set => SetValue(ref includeHidden, value); }
        private bool showDescription = true;
        public bool ShowDescription { get => showDescription; set => SetValue(ref showDescription, value); }
        private int descriptionLength = 400;
        public int DescriptionLength { get => descriptionLength; set => SetValue(ref descriptionLength, Math.Max(0, Math.Min(2000, value))); }
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
