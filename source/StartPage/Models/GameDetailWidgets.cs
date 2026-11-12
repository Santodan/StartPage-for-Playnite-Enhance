using System.Collections.ObjectModel;
using System.Linq;
using LandingPage.Settings;

namespace LandingPage.Models
{
    public sealed class GameDetailWidgetDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public FeaturedGameSection? Section { get; }
        public bool IsArtwork => !Section.HasValue;
        internal GameDetailWidgetDefinition(string suffix, string name, FeaturedGameSection? section)
        { Id = "GameDetail"+suffix; Name = name; Section = section; }
    }
    public static class GameDetailWidgets
    {
        public static ReadOnlyCollection<GameDetailWidgetDefinition> Definitions { get; } = new ReadOnlyCollection<GameDetailWidgetDefinition>(new[] {
            new GameDetailWidgetDefinition("Artwork","Game: Artwork",null),
            new GameDetailWidgetDefinition("Title","Game: Title",FeaturedGameSection.Title),
            new GameDetailWidgetDefinition("Description","Game: Description",FeaturedGameSection.Description),
            new GameDetailWidgetDefinition("Activity","Game: Activity",FeaturedGameSection.Activity),
            new GameDetailWidgetDefinition("Details","Game: Details",FeaturedGameSection.GameDetails),
            new GameDetailWidgetDefinition("Library","Game: Library information",FeaturedGameSection.LibraryInformation),
            new GameDetailWidgetDefinition("Scores","Game: Scores",FeaturedGameSection.Scores),
            new GameDetailWidgetDefinition("Links","Game: Links",FeaturedGameSection.Links),
            new GameDetailWidgetDefinition("Actions","Game: Actions",FeaturedGameSection.Actions)
        });
        public static GameDetailWidgetDefinition Find(string id) => Definitions.FirstOrDefault(item => item.Id == id);
        public static FeaturedGameSettings CreateSettings(string id)
        {
            var artwork = Find(id)?.IsArtwork == true;
            return new FeaturedGameSettings {
                Source = FeaturedGameSource.HoveredOrSelectedGame, IncludeHidden = true,
                ShowArtwork = artwork, UseCoverArtwork = artwork, OverlayOpacity = 0,
                ArtworkSizing = FeaturedArtworkSizing.Fit,
                ShowGameDetails = true, ShowLibraryDetails = true, ShowScores = true, ShowLinks = true,
                ShowLastPlayed = true, ShowAchievements = true, ShowDescriptionImages = Find(id)?.Section == FeaturedGameSection.Description, DescriptionLength = 0, ContentWidth = 1400,
                Alignment = System.Windows.HorizontalAlignment.Stretch, VerticalAlignment = System.Windows.VerticalAlignment.Top
            };
        }
    }
}
