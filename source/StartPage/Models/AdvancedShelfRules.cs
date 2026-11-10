using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.Serialization;
using Playnite.SDK.Models;

namespace LandingPage.Models
{
    public enum ShelfMatchMode { All, Any }
    public enum ShelfFilterAction { Include, Exclude }

    public partial class ShelveProperties : IDataErrorInfo
    {
        // WPF displays invalid pattern errors without discarding the user's input.
        [Newtonsoft.Json.JsonIgnore]
        public string Error => null;
        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(IncludedNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedNamePatterns);
                    case nameof(ExcludedNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedNamePatterns);
                    case nameof(IncludedInstallPathPatterns): return ShelfFilterRules.ValidatePatterns(IncludedInstallPathPatterns);
                    case nameof(ExcludedInstallPathPatterns): return ShelfFilterRules.ValidatePatterns(ExcludedInstallPathPatterns);
                    case nameof(IncludedSortingNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedSortingNamePatterns);
                    case nameof(ExcludedSortingNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedSortingNamePatterns);
                    case nameof(IncludedLaunchPathPatterns): return ShelfFilterRules.ValidatePatterns(IncludedLaunchPathPatterns);
                    case nameof(ExcludedLaunchPathPatterns): return ShelfFilterRules.ValidatePatterns(ExcludedLaunchPathPatterns);
                    case nameof(IncludedLaunchArgumentsPatterns): return ShelfFilterRules.ValidatePatterns(IncludedLaunchArgumentsPatterns);
                    case nameof(ExcludedLaunchArgumentsPatterns): return ShelfFilterRules.ValidatePatterns(ExcludedLaunchArgumentsPatterns);
                    case nameof(IncludedWorkingDirectoryPatterns): return ShelfFilterRules.ValidatePatterns(IncludedWorkingDirectoryPatterns);
                    case nameof(ExcludedWorkingDirectoryPatterns): return ShelfFilterRules.ValidatePatterns(ExcludedWorkingDirectoryPatterns);
                    case nameof(IncludedActionNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedActionNamePatterns);
                    case nameof(ExcludedActionNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedActionNamePatterns);
                    case nameof(IncludedRomPathPatterns): return ShelfFilterRules.ValidatePatterns(IncludedRomPathPatterns);
                    case nameof(ExcludedRomPathPatterns): return ShelfFilterRules.ValidatePatterns(ExcludedRomPathPatterns);
                    case nameof(IncludedSourceNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedSourceNamePatterns);
                    case nameof(ExcludedSourceNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedSourceNamePatterns);
                    case nameof(IncludedPlatformNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedPlatformNamePatterns);
                    case nameof(ExcludedPlatformNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedPlatformNamePatterns);
                    case nameof(IncludedTagNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedTagNamePatterns);
                    case nameof(ExcludedTagNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedTagNamePatterns);
                    case nameof(IncludedCategoryNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedCategoryNamePatterns);
                    case nameof(ExcludedCategoryNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedCategoryNamePatterns);
                    case nameof(IncludedGenreNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedGenreNamePatterns);
                    case nameof(ExcludedGenreNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedGenreNamePatterns);
                    case nameof(IncludedFeatureNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedFeatureNamePatterns);
                    case nameof(ExcludedFeatureNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedFeatureNamePatterns);
                    case nameof(IncludedDeveloperNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedDeveloperNamePatterns);
                    case nameof(ExcludedDeveloperNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedDeveloperNamePatterns);
                    case nameof(IncludedPublisherNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedPublisherNamePatterns);
                    case nameof(ExcludedPublisherNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedPublisherNamePatterns);
                    case nameof(IncludedSeriesNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedSeriesNamePatterns);
                    case nameof(ExcludedSeriesNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedSeriesNamePatterns);
                    case nameof(IncludedCompletionNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedCompletionNamePatterns);
                    case nameof(ExcludedCompletionNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedCompletionNamePatterns);
                    case nameof(IncludedRegionNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedRegionNamePatterns);
                    case nameof(ExcludedRegionNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedRegionNamePatterns);
                    case nameof(IncludedAgeRatingNamePatterns): return ShelfFilterRules.ValidatePatterns(IncludedAgeRatingNamePatterns);
                    case nameof(ExcludedAgeRatingNamePatterns): return ShelfFilterRules.ValidatePatterns(ExcludedAgeRatingNamePatterns);
                    case nameof(IncludedVersionPatterns): return ShelfFilterRules.ValidatePatterns(IncludedVersionPatterns);
                    case nameof(ExcludedVersionPatterns): return ShelfFilterRules.ValidatePatterns(ExcludedVersionPatterns);
                    default: return null;
                }
            }
        }

        private string includedNamePatterns = string.Empty;
        public string IncludedNamePatterns { get => includedNamePatterns; set => SetValue(ref includedNamePatterns, value ?? string.Empty); }
        private string excludedNamePatterns = string.Empty;
        public string ExcludedNamePatterns { get => excludedNamePatterns; set => SetValue(ref excludedNamePatterns, value ?? string.Empty); }
        private string includedInstallPathPatterns = string.Empty;
        public string IncludedInstallPathPatterns { get => includedInstallPathPatterns; set => SetValue(ref includedInstallPathPatterns, value ?? string.Empty); }
        private string excludedInstallPathPatterns = string.Empty;
        public string ExcludedInstallPathPatterns { get => excludedInstallPathPatterns; set => SetValue(ref excludedInstallPathPatterns, value ?? string.Empty); }
        private string includedSortingNamePatterns = string.Empty;
        public string IncludedSortingNamePatterns { get => includedSortingNamePatterns; set => SetValue(ref includedSortingNamePatterns, value ?? string.Empty); }
        private string excludedSortingNamePatterns = string.Empty;
        public string ExcludedSortingNamePatterns { get => excludedSortingNamePatterns; set => SetValue(ref excludedSortingNamePatterns, value ?? string.Empty); }
        private string includedLaunchPathPatterns = string.Empty;
        public string IncludedLaunchPathPatterns { get => includedLaunchPathPatterns; set => SetValue(ref includedLaunchPathPatterns, value ?? string.Empty); }
        private string excludedLaunchPathPatterns = string.Empty;
        public string ExcludedLaunchPathPatterns { get => excludedLaunchPathPatterns; set => SetValue(ref excludedLaunchPathPatterns, value ?? string.Empty); }
        private string includedLaunchArgumentsPatterns = string.Empty;
        public string IncludedLaunchArgumentsPatterns { get => includedLaunchArgumentsPatterns; set => SetValue(ref includedLaunchArgumentsPatterns, value ?? string.Empty); }
        private string excludedLaunchArgumentsPatterns = string.Empty;
        public string ExcludedLaunchArgumentsPatterns { get => excludedLaunchArgumentsPatterns; set => SetValue(ref excludedLaunchArgumentsPatterns, value ?? string.Empty); }
        private string includedWorkingDirectoryPatterns = string.Empty;
        public string IncludedWorkingDirectoryPatterns { get => includedWorkingDirectoryPatterns; set => SetValue(ref includedWorkingDirectoryPatterns, value ?? string.Empty); }
        private string excludedWorkingDirectoryPatterns = string.Empty;
        public string ExcludedWorkingDirectoryPatterns { get => excludedWorkingDirectoryPatterns; set => SetValue(ref excludedWorkingDirectoryPatterns, value ?? string.Empty); }
        private string includedActionNamePatterns = string.Empty;
        public string IncludedActionNamePatterns { get => includedActionNamePatterns; set => SetValue(ref includedActionNamePatterns, value ?? string.Empty); }
        private string excludedActionNamePatterns = string.Empty;
        public string ExcludedActionNamePatterns { get => excludedActionNamePatterns; set => SetValue(ref excludedActionNamePatterns, value ?? string.Empty); }
        private string includedRomPathPatterns = string.Empty;
        public string IncludedRomPathPatterns { get => includedRomPathPatterns; set => SetValue(ref includedRomPathPatterns, value ?? string.Empty); }
        private string excludedRomPathPatterns = string.Empty;
        public string ExcludedRomPathPatterns { get => excludedRomPathPatterns; set => SetValue(ref excludedRomPathPatterns, value ?? string.Empty); }
        private string includedSourceNamePatterns = string.Empty;
        public string IncludedSourceNamePatterns { get => includedSourceNamePatterns; set => SetValue(ref includedSourceNamePatterns, value ?? string.Empty); }
        private string excludedSourceNamePatterns = string.Empty;
        public string ExcludedSourceNamePatterns { get => excludedSourceNamePatterns; set => SetValue(ref excludedSourceNamePatterns, value ?? string.Empty); }
        private string includedPlatformNamePatterns = string.Empty;
        public string IncludedPlatformNamePatterns { get => includedPlatformNamePatterns; set => SetValue(ref includedPlatformNamePatterns, value ?? string.Empty); }
        private string excludedPlatformNamePatterns = string.Empty;
        public string ExcludedPlatformNamePatterns { get => excludedPlatformNamePatterns; set => SetValue(ref excludedPlatformNamePatterns, value ?? string.Empty); }
        private string includedTagNamePatterns = string.Empty;
        public string IncludedTagNamePatterns { get => includedTagNamePatterns; set => SetValue(ref includedTagNamePatterns, value ?? string.Empty); }
        private string excludedTagNamePatterns = string.Empty;
        public string ExcludedTagNamePatterns { get => excludedTagNamePatterns; set => SetValue(ref excludedTagNamePatterns, value ?? string.Empty); }
        private string includedCategoryNamePatterns = string.Empty;
        public string IncludedCategoryNamePatterns { get => includedCategoryNamePatterns; set => SetValue(ref includedCategoryNamePatterns, value ?? string.Empty); }
        private string excludedCategoryNamePatterns = string.Empty;
        public string ExcludedCategoryNamePatterns { get => excludedCategoryNamePatterns; set => SetValue(ref excludedCategoryNamePatterns, value ?? string.Empty); }
        private string includedGenreNamePatterns = string.Empty;
        public string IncludedGenreNamePatterns { get => includedGenreNamePatterns; set => SetValue(ref includedGenreNamePatterns, value ?? string.Empty); }
        private string excludedGenreNamePatterns = string.Empty;
        public string ExcludedGenreNamePatterns { get => excludedGenreNamePatterns; set => SetValue(ref excludedGenreNamePatterns, value ?? string.Empty); }
        private string includedFeatureNamePatterns = string.Empty;
        public string IncludedFeatureNamePatterns { get => includedFeatureNamePatterns; set => SetValue(ref includedFeatureNamePatterns, value ?? string.Empty); }
        private string excludedFeatureNamePatterns = string.Empty;
        public string ExcludedFeatureNamePatterns { get => excludedFeatureNamePatterns; set => SetValue(ref excludedFeatureNamePatterns, value ?? string.Empty); }
        private string includedDeveloperNamePatterns = string.Empty;
        public string IncludedDeveloperNamePatterns { get => includedDeveloperNamePatterns; set => SetValue(ref includedDeveloperNamePatterns, value ?? string.Empty); }
        private string excludedDeveloperNamePatterns = string.Empty;
        public string ExcludedDeveloperNamePatterns { get => excludedDeveloperNamePatterns; set => SetValue(ref excludedDeveloperNamePatterns, value ?? string.Empty); }
        private string includedPublisherNamePatterns = string.Empty;
        public string IncludedPublisherNamePatterns { get => includedPublisherNamePatterns; set => SetValue(ref includedPublisherNamePatterns, value ?? string.Empty); }
        private string excludedPublisherNamePatterns = string.Empty;
        public string ExcludedPublisherNamePatterns { get => excludedPublisherNamePatterns; set => SetValue(ref excludedPublisherNamePatterns, value ?? string.Empty); }
        private string includedSeriesNamePatterns = string.Empty;
        public string IncludedSeriesNamePatterns { get => includedSeriesNamePatterns; set => SetValue(ref includedSeriesNamePatterns, value ?? string.Empty); }
        private string excludedSeriesNamePatterns = string.Empty;
        public string ExcludedSeriesNamePatterns { get => excludedSeriesNamePatterns; set => SetValue(ref excludedSeriesNamePatterns, value ?? string.Empty); }
        private string includedCompletionNamePatterns = string.Empty;
        public string IncludedCompletionNamePatterns { get => includedCompletionNamePatterns; set => SetValue(ref includedCompletionNamePatterns, value ?? string.Empty); }
        private string excludedCompletionNamePatterns = string.Empty;
        public string ExcludedCompletionNamePatterns { get => excludedCompletionNamePatterns; set => SetValue(ref excludedCompletionNamePatterns, value ?? string.Empty); }
        private string includedRegionNamePatterns = string.Empty;
        public string IncludedRegionNamePatterns { get => includedRegionNamePatterns; set => SetValue(ref includedRegionNamePatterns, value ?? string.Empty); }
        private string excludedRegionNamePatterns = string.Empty;
        public string ExcludedRegionNamePatterns { get => excludedRegionNamePatterns; set => SetValue(ref excludedRegionNamePatterns, value ?? string.Empty); }
        private string includedAgeRatingNamePatterns = string.Empty;
        public string IncludedAgeRatingNamePatterns { get => includedAgeRatingNamePatterns; set => SetValue(ref includedAgeRatingNamePatterns, value ?? string.Empty); }
        private string excludedAgeRatingNamePatterns = string.Empty;
        public string ExcludedAgeRatingNamePatterns { get => excludedAgeRatingNamePatterns; set => SetValue(ref excludedAgeRatingNamePatterns, value ?? string.Empty); }
        private string includedVersionPatterns = string.Empty;
        public string IncludedVersionPatterns { get => includedVersionPatterns; set => SetValue(ref includedVersionPatterns, value ?? string.Empty); }
        private string excludedVersionPatterns = string.Empty;
        public string ExcludedVersionPatterns { get => excludedVersionPatterns; set => SetValue(ref excludedVersionPatterns, value ?? string.Empty); }
        private bool neverPlayedOnly = false;
        public bool NeverPlayedOnly { get => neverPlayedOnly; set => SetValue(ref neverPlayedOnly, value); }
        private bool useInactivityRule = false;
        public bool UseInactivityRule { get => useInactivityRule; set => SetValue(ref useInactivityRule, value); }
        private int inactiveDays = 30;
        public int InactiveDays { get => inactiveDays; set => SetValue(ref inactiveDays, Math.Max(0, Math.Min(36500, value))); }
        private bool includeNeverPlayedInInactivity = false;
        public bool IncludeNeverPlayedInInactivity { get => includeNeverPlayedInInactivity; set => SetValue(ref includeNeverPlayedInInactivity, value); }
        private bool useMinimumAddedAge = false;
        public bool UseMinimumAddedAge { get => useMinimumAddedAge; set => SetValue(ref useMinimumAddedAge, value); }
        private int minimumAddedDays = 0;
        public int MinimumAddedDays { get => minimumAddedDays; set => SetValue(ref minimumAddedDays, Math.Max(0, Math.Min(36500, value))); }
        private bool useMaximumAddedAge = false;
        public bool UseMaximumAddedAge { get => useMaximumAddedAge; set => SetValue(ref useMaximumAddedAge, value); }
        private int maximumAddedDays = 30;
        public int MaximumAddedDays { get => maximumAddedDays; set => SetValue(ref maximumAddedDays, Math.Max(0, Math.Min(36500, value))); }
        private ShelfMatchMode filterMatchMode = ShelfMatchMode.All;
        public ShelfMatchMode FilterMatchMode { get => filterMatchMode; set => SetValue(ref filterMatchMode, value); }
        private bool matchAllSelectedValues = false;
        public bool MatchAllSelectedValues { get => matchAllSelectedValues; set => SetValue(ref matchAllSelectedValues, value); }
        private ShelfFilterAction categoryFilterAction = ShelfFilterAction.Include;
        public ShelfFilterAction CategoryFilterAction { get => categoryFilterAction; set => SetValue(ref categoryFilterAction, value); }
        private ShelfFilterAction tagFilterAction = ShelfFilterAction.Include;
        public ShelfFilterAction TagFilterAction { get => tagFilterAction; set => SetValue(ref tagFilterAction, value); }
        private ShelfFilterAction genreFilterAction = ShelfFilterAction.Include;
        public ShelfFilterAction GenreFilterAction { get => genreFilterAction; set => SetValue(ref genreFilterAction, value); }
        private ShelfFilterAction platformFilterAction = ShelfFilterAction.Include;
        public ShelfFilterAction PlatformFilterAction { get => platformFilterAction; set => SetValue(ref platformFilterAction, value); }
        private ShelfFilterAction sourceFilterAction = ShelfFilterAction.Include;
        public ShelfFilterAction SourceFilterAction { get => sourceFilterAction; set => SetValue(ref sourceFilterAction, value); }
        private ShelfFilterAction featureFilterAction = ShelfFilterAction.Include;
        public ShelfFilterAction FeatureFilterAction { get => featureFilterAction; set => SetValue(ref featureFilterAction, value); }
        private ShelfFilterAction completionFilterAction = ShelfFilterAction.Include;
        public ShelfFilterAction CompletionFilterAction { get => completionFilterAction; set => SetValue(ref completionFilterAction, value); }

        private HashSet<Guid> excludedCategories = new HashSet<Guid>();
        public HashSet<Guid> ExcludedCategories { get => excludedCategories; set => SetValue(ref excludedCategories, value ?? new HashSet<Guid>()); }
        private HashSet<Guid> excludedTags = new HashSet<Guid>();
        public HashSet<Guid> ExcludedTags { get => excludedTags; set => SetValue(ref excludedTags, value ?? new HashSet<Guid>()); }
        private HashSet<Guid> excludedGenres = new HashSet<Guid>();
        public HashSet<Guid> ExcludedGenres { get => excludedGenres; set => SetValue(ref excludedGenres, value ?? new HashSet<Guid>()); }
        private HashSet<Guid> excludedPlatforms = new HashSet<Guid>();
        public HashSet<Guid> ExcludedPlatforms { get => excludedPlatforms; set => SetValue(ref excludedPlatforms, value ?? new HashSet<Guid>()); }
        private HashSet<Guid> excludedSources = new HashSet<Guid>();
        public HashSet<Guid> ExcludedSources { get => excludedSources; set => SetValue(ref excludedSources, value ?? new HashSet<Guid>()); }
        private HashSet<Guid> excludedFeatures = new HashSet<Guid>();
        public HashSet<Guid> ExcludedFeatures { get => excludedFeatures; set => SetValue(ref excludedFeatures, value ?? new HashSet<Guid>()); }
        private HashSet<Guid> excludedCompletionStatus = new HashSet<Guid>();
        public HashSet<Guid> ExcludedCompletionStatus { get => excludedCompletionStatus; set => SetValue(ref excludedCompletionStatus, value ?? new HashSet<Guid>()); }

        [OnDeserialized]
        private void RestoreIndependentSelections(StreamingContext context)
        {
            if (CategoryFilterAction == ShelfFilterAction.Exclude)
            {
                if (Categories != null) ExcludedCategories.UnionWith(Categories);
                Categories?.Clear();
                CategoryFilterAction = ShelfFilterAction.Include;
            }
            if (TagFilterAction == ShelfFilterAction.Exclude)
            {
                if (Tags != null) ExcludedTags.UnionWith(Tags);
                Tags?.Clear();
                TagFilterAction = ShelfFilterAction.Include;
            }
            if (GenreFilterAction == ShelfFilterAction.Exclude)
            {
                if (Genres != null) ExcludedGenres.UnionWith(Genres);
                Genres?.Clear();
                GenreFilterAction = ShelfFilterAction.Include;
            }
            if (PlatformFilterAction == ShelfFilterAction.Exclude)
            {
                if (Platforms != null) ExcludedPlatforms.UnionWith(Platforms);
                Platforms?.Clear();
                PlatformFilterAction = ShelfFilterAction.Include;
            }
            if (SourceFilterAction == ShelfFilterAction.Exclude)
            {
                if (Sources != null) ExcludedSources.UnionWith(Sources);
                Sources?.Clear();
                SourceFilterAction = ShelfFilterAction.Include;
            }
            if (FeatureFilterAction == ShelfFilterAction.Exclude)
            {
                if (Features != null) ExcludedFeatures.UnionWith(Features);
                Features?.Clear();
                FeatureFilterAction = ShelfFilterAction.Include;
            }
            if (CompletionFilterAction == ShelfFilterAction.Exclude)
            {
                if (CompletionStatus != null) ExcludedCompletionStatus.UnionWith(CompletionStatus);
                CompletionStatus?.Clear();
                CompletionFilterAction = ShelfFilterAction.Include;
            }
        }

        public void ResetAdvancedFilters()
        {
            IncludedNamePatterns = ExcludedNamePatterns = IncludedInstallPathPatterns = ExcludedInstallPathPatterns = string.Empty;
            IncludedSortingNamePatterns = ExcludedSortingNamePatterns = string.Empty;
            IncludedLaunchPathPatterns = ExcludedLaunchPathPatterns = string.Empty;
            IncludedLaunchArgumentsPatterns = ExcludedLaunchArgumentsPatterns = string.Empty;
            IncludedWorkingDirectoryPatterns = ExcludedWorkingDirectoryPatterns = string.Empty;
            IncludedActionNamePatterns = ExcludedActionNamePatterns = string.Empty;
            IncludedRomPathPatterns = ExcludedRomPathPatterns = string.Empty;
            IncludedSourceNamePatterns = ExcludedSourceNamePatterns = string.Empty;
            IncludedPlatformNamePatterns = ExcludedPlatformNamePatterns = string.Empty;
            IncludedTagNamePatterns = ExcludedTagNamePatterns = string.Empty;
            IncludedCategoryNamePatterns = ExcludedCategoryNamePatterns = string.Empty;
            IncludedGenreNamePatterns = ExcludedGenreNamePatterns = string.Empty;
            IncludedFeatureNamePatterns = ExcludedFeatureNamePatterns = string.Empty;
            IncludedDeveloperNamePatterns = ExcludedDeveloperNamePatterns = string.Empty;
            IncludedPublisherNamePatterns = ExcludedPublisherNamePatterns = string.Empty;
            IncludedSeriesNamePatterns = ExcludedSeriesNamePatterns = string.Empty;
            IncludedCompletionNamePatterns = ExcludedCompletionNamePatterns = string.Empty;
            IncludedRegionNamePatterns = ExcludedRegionNamePatterns = string.Empty;
            IncludedAgeRatingNamePatterns = ExcludedAgeRatingNamePatterns = string.Empty;
            IncludedVersionPatterns = ExcludedVersionPatterns = string.Empty;
            ExcludedCategories.Clear();
            ExcludedTags.Clear();
            ExcludedGenres.Clear();
            ExcludedPlatforms.Clear();
            ExcludedSources.Clear();
            ExcludedFeatures.Clear();
            ExcludedCompletionStatus.Clear();
            NeverPlayedOnly = false;
            UseInactivityRule = false;
            InactiveDays = 30;
            IncludeNeverPlayedInInactivity = false;
            UseMinimumAddedAge = false;
            MinimumAddedDays = 0;
            UseMaximumAddedAge = false;
            MaximumAddedDays = 30;
            FilterMatchMode = ShelfMatchMode.All;
            MatchAllSelectedValues = false;
            CategoryFilterAction = ShelfFilterAction.Include;
            TagFilterAction = ShelfFilterAction.Include;
            GenreFilterAction = ShelfFilterAction.Include;
            PlatformFilterAction = ShelfFilterAction.Include;
            SourceFilterAction = ShelfFilterAction.Include;
            FeatureFilterAction = ShelfFilterAction.Include;
            CompletionFilterAction = ShelfFilterAction.Include;
        }
    }

    public static class ShelfFilterRules
    {
        public static IEnumerable<Game> SelectWindow(IEnumerable<Game> games, ShelveProperties settings, int skipped)
        {
            var selected = games.Skip(Math.Max(0, skipped));
            return settings.UnlimitedGames ? selected : selected.Take(settings.NumberOfGames);
        }

        public static bool Matches(Game game, ShelveProperties settings, DateTime now)
        {
            if (settings.FavoritesOnly && !game.Favorite || settings.IgnoreHidden && game.Hidden
                || settings.InstalledOnly && !game.IsInstalled) return false;
            // Wildcard restrictions are mandatory, independently of ID-group All/Any mode.
            if ((HasPatterns(settings.IncludedNamePatterns, settings.ExcludedNamePatterns) && !MatchesPatterns(game.Name, settings.IncludedNamePatterns, settings.ExcludedNamePatterns, false))
                || (HasPatterns(settings.IncludedInstallPathPatterns, settings.ExcludedInstallPathPatterns) && !MatchesPatterns(game.InstallDirectory, settings.IncludedInstallPathPatterns, settings.ExcludedInstallPathPatterns, true))
                || (HasPatterns(settings.IncludedSortingNamePatterns, settings.ExcludedSortingNamePatterns) && !MatchesPatterns(game.SortingName, settings.IncludedSortingNamePatterns, settings.ExcludedSortingNamePatterns, false))
                || (HasPatterns(settings.IncludedLaunchPathPatterns, settings.ExcludedLaunchPathPatterns) && !MatchesPatterns(game.GameActions?.Select(action => action.Path), settings.IncludedLaunchPathPatterns, settings.ExcludedLaunchPathPatterns, true))
                || (HasPatterns(settings.IncludedLaunchArgumentsPatterns, settings.ExcludedLaunchArgumentsPatterns) && !MatchesPatterns(game.GameActions?.SelectMany(action => new[] { action.Arguments, action.AdditionalArguments }), settings.IncludedLaunchArgumentsPatterns, settings.ExcludedLaunchArgumentsPatterns, false))
                || (HasPatterns(settings.IncludedWorkingDirectoryPatterns, settings.ExcludedWorkingDirectoryPatterns) && !MatchesPatterns(game.GameActions?.Select(action => action.WorkingDir), settings.IncludedWorkingDirectoryPatterns, settings.ExcludedWorkingDirectoryPatterns, true))
                || (HasPatterns(settings.IncludedActionNamePatterns, settings.ExcludedActionNamePatterns) && !MatchesPatterns(game.GameActions?.Select(action => action.Name), settings.IncludedActionNamePatterns, settings.ExcludedActionNamePatterns, false))
                || (HasPatterns(settings.IncludedRomPathPatterns, settings.ExcludedRomPathPatterns) && !MatchesPatterns(game.Roms?.Select(rom => rom.Path), settings.IncludedRomPathPatterns, settings.ExcludedRomPathPatterns, true))
                || (HasPatterns(settings.IncludedSourceNamePatterns, settings.ExcludedSourceNamePatterns) && !MatchesPatterns(game.Source?.Name, settings.IncludedSourceNamePatterns, settings.ExcludedSourceNamePatterns, false))
                || (HasPatterns(settings.IncludedPlatformNamePatterns, settings.ExcludedPlatformNamePatterns) && !MatchesPatterns(game.Platforms?.Select(item => item.Name), settings.IncludedPlatformNamePatterns, settings.ExcludedPlatformNamePatterns, false))
                || (HasPatterns(settings.IncludedTagNamePatterns, settings.ExcludedTagNamePatterns) && !MatchesPatterns(game.Tags?.Select(item => item.Name), settings.IncludedTagNamePatterns, settings.ExcludedTagNamePatterns, false))
                || (HasPatterns(settings.IncludedCategoryNamePatterns, settings.ExcludedCategoryNamePatterns) && !MatchesPatterns(game.Categories?.Select(item => item.Name), settings.IncludedCategoryNamePatterns, settings.ExcludedCategoryNamePatterns, false))
                || (HasPatterns(settings.IncludedGenreNamePatterns, settings.ExcludedGenreNamePatterns) && !MatchesPatterns(game.Genres?.Select(item => item.Name), settings.IncludedGenreNamePatterns, settings.ExcludedGenreNamePatterns, false))
                || (HasPatterns(settings.IncludedFeatureNamePatterns, settings.ExcludedFeatureNamePatterns) && !MatchesPatterns(game.Features?.Select(item => item.Name), settings.IncludedFeatureNamePatterns, settings.ExcludedFeatureNamePatterns, false))
                || (HasPatterns(settings.IncludedDeveloperNamePatterns, settings.ExcludedDeveloperNamePatterns) && !MatchesPatterns(game.Developers?.Select(item => item.Name), settings.IncludedDeveloperNamePatterns, settings.ExcludedDeveloperNamePatterns, false))
                || (HasPatterns(settings.IncludedPublisherNamePatterns, settings.ExcludedPublisherNamePatterns) && !MatchesPatterns(game.Publishers?.Select(item => item.Name), settings.IncludedPublisherNamePatterns, settings.ExcludedPublisherNamePatterns, false))
                || (HasPatterns(settings.IncludedSeriesNamePatterns, settings.ExcludedSeriesNamePatterns) && !MatchesPatterns(game.Series?.Select(item => item.Name), settings.IncludedSeriesNamePatterns, settings.ExcludedSeriesNamePatterns, false))
                || (HasPatterns(settings.IncludedCompletionNamePatterns, settings.ExcludedCompletionNamePatterns) && !MatchesPatterns(game.CompletionStatus?.Name, settings.IncludedCompletionNamePatterns, settings.ExcludedCompletionNamePatterns, false))
                || (HasPatterns(settings.IncludedRegionNamePatterns, settings.ExcludedRegionNamePatterns) && !MatchesPatterns(game.Regions?.Select(item => item.Name), settings.IncludedRegionNamePatterns, settings.ExcludedRegionNamePatterns, false))
                || (HasPatterns(settings.IncludedAgeRatingNamePatterns, settings.ExcludedAgeRatingNamePatterns) && !MatchesPatterns(game.AgeRatings?.Select(item => item.Name), settings.IncludedAgeRatingNamePatterns, settings.ExcludedAgeRatingNamePatterns, false))
                || (HasPatterns(settings.IncludedVersionPatterns, settings.ExcludedVersionPatterns) && !MatchesPatterns(game.Version, settings.IncludedVersionPatterns, settings.ExcludedVersionPatterns, false))
                ) return false;
            var neverPlayed = game.Playtime == 0 && !game.LastActivity.HasValue;
            if (settings.NeverPlayedOnly && !neverPlayed) return false;
            var today = now.Date;
            if (settings.UseInactivityRule)
            {
                if (!game.LastActivity.HasValue)
                {
                    if (!(settings.IncludeNeverPlayedInInactivity && neverPlayed)) return false;
                }
                else if (LocalDate(game.LastActivity.Value) > today.AddDays(-settings.InactiveDays)) return false;
            }
            if (settings.UseMinimumAddedAge || settings.UseMaximumAddedAge)
            {
                if (!game.Added.HasValue) return false;
                var added = LocalDate(game.Added.Value);
                if (added > today) return false;
                if (settings.UseMinimumAddedAge && added > today.AddDays(-settings.MinimumAddedDays)) return false;
                if (settings.UseMaximumAddedAge && added < today.AddDays(-settings.MaximumAddedDays)) return false;
            }
            // Independent exclusions are always applied, even when an include group matches.
            var ignored = new List<bool>();
            if (!Group(game.CategoryIds, settings.ExcludedCategories, ShelfFilterAction.Exclude, false, ignored)) return false;
            if (!Group(game.TagIds, settings.ExcludedTags, ShelfFilterAction.Exclude, false, ignored)) return false;
            if (!Group(game.GenreIds, settings.ExcludedGenres, ShelfFilterAction.Exclude, false, ignored)) return false;
            if (!Group(game.PlatformIds, settings.ExcludedPlatforms, ShelfFilterAction.Exclude, false, ignored)) return false;
            if (!Group(new[] { game.SourceId }, settings.ExcludedSources, ShelfFilterAction.Exclude, false, ignored)) return false;
            if (!Group(game.FeatureIds, settings.ExcludedFeatures, ShelfFilterAction.Exclude, false, ignored)) return false;
            if (!Group(new[] { game.CompletionStatusId }, settings.ExcludedCompletionStatus, ShelfFilterAction.Exclude, false, ignored)) return false;
            var included = new List<bool>();
            if (!Group(game.CategoryIds, settings.Categories, settings.CategoryFilterAction, settings.MatchAllSelectedValues, included)
                || !Group(game.TagIds, settings.Tags, settings.TagFilterAction, settings.MatchAllSelectedValues, included)
                || !Group(game.GenreIds, settings.Genres, settings.GenreFilterAction, settings.MatchAllSelectedValues, included)
                || !Group(game.PlatformIds, settings.Platforms, settings.PlatformFilterAction, settings.MatchAllSelectedValues, included)
                || !Group(game.FeatureIds, settings.Features, settings.FeatureFilterAction, settings.MatchAllSelectedValues, included)
                || !Group(new[] { game.SourceId }, settings.Sources, settings.SourceFilterAction, false, included)
                || !Group(new[] { game.CompletionStatusId }, settings.CompletionStatus, settings.CompletionFilterAction, false, included)) return false;
            return included.Count == 0 || (settings.FilterMatchMode == ShelfMatchMode.All ? included.All(x => x) : included.Any(x => x));
        }

        private static bool HasPatterns(string includes, string excludes)
            => !string.IsNullOrWhiteSpace(includes) || !string.IsNullOrWhiteSpace(excludes);

        private static bool MatchesPatterns(string value, string includes, string excludes, bool path)
            => MatchesPatterns(new[] { value }, includes, excludes, path);

        private static bool MatchesPatterns(IEnumerable<string> values, string includes, string excludes, bool path)
        {
            if (string.IsNullOrWhiteSpace(includes) && string.IsNullOrWhiteSpace(excludes)) return true;
            var includePatterns = SplitPatterns(includes);
            var excludePatterns = SplitPatterns(excludes);
            // Invalid rules fail closed for both includes and excludes.
            if (includePatterns.Concat(excludePatterns).Any(pattern => PatternError(pattern) != null)) return false;
            var entries = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? new string[0];
            Func<string, bool> matches = pattern => pattern.Equals("missing:", StringComparison.OrdinalIgnoreCase)
                ? entries.Length == 0 : entries.Any(value => MatchPattern(value, pattern, path));
            try
            {
                return !excludePatterns.Any(matches) && (includePatterns.Length == 0 || includePatterns.Any(matches));
            }
            catch (RegexMatchTimeoutException) { return false; }
            catch (ArgumentException) { return false; }
        }

        private static string[] SplitPatterns(string patterns) => (patterns ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(pattern => pattern.Trim()).Where(pattern => pattern.Length > 0).ToArray();

        private static readonly ConcurrentDictionary<string, Regex> patternCache = new ConcurrentDictionary<string, Regex>();

        public static string ValidatePatterns(string patterns)
        {
            var lines = SplitPatterns(patterns);
            for (var i = 0; i < lines.Length; ++i)
            {
                var error = PatternError(lines[i]);
                if (error != null) return "Pattern " + (i + 1) + ": " + error;
            }
            return null;
        }

        private static string PatternError(string pattern)
        {
            if (pattern.Length > 2048) return "Patterns must contain at most 2048 characters.";
            try { GetPatternRegex(pattern); return null; }
            catch (ArgumentException ex) { return ex.Message; }
        }

        private static Regex GetPatternRegex(string pattern)
        {
            if (patternCache.TryGetValue(pattern, out var cached)) return cached;
            string expression;
            if (pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase)) expression = pattern.Substring(6);
            else if (pattern.StartsWith("glob:", StringComparison.OrdinalIgnoreCase)) expression = GlobExpression(pattern.Substring(5));
            else if (pattern.StartsWith("literal:", StringComparison.OrdinalIgnoreCase)) expression = "\\A" + Regex.Escape(pattern.Substring(8)) + "\\z";
            else if (pattern.StartsWith("contains:", StringComparison.OrdinalIgnoreCase)) expression = Regex.Escape(pattern.Substring(9));
            else if (pattern.StartsWith("starts:", StringComparison.OrdinalIgnoreCase)) expression = "\\A" + Regex.Escape(pattern.Substring(7));
            else if (pattern.StartsWith("ends:", StringComparison.OrdinalIgnoreCase)) expression = Regex.Escape(pattern.Substring(5)) + "\\z";
            else expression = "\\A" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "\\z";
            var result = new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(25));
            if (patternCache.Count > 1024) patternCache.Clear();
            return patternCache.GetOrAdd(pattern, result);
        }

        private static string GlobExpression(string pattern)
        {
            var expression = new StringBuilder("\\A");
            for (var i = 0; i < pattern.Length; ++i)
            {
                var ch = pattern[i];
                if (ch == '*') expression.Append(".*");
                else if (ch == '?') expression.Append('.');
                else if (ch == '[')
                {
                    var end = pattern.IndexOf(']', i + 1);
                    if (end < 0 || end == i + 1) throw new ArgumentException("Unclosed or empty character set. Use literal: to match brackets literally.");
                    var content = pattern.Substring(i + 1, end - i - 1);
                    expression.Append('[');
                    var start = content[0] == '!' ? 1 : 0;
                    if (start == 1) expression.Append('^');
                    if (start == content.Length) throw new ArgumentException("Empty negated character set.");
                    for (var j = start; j < content.Length; ++j)
                    {
                        var item = content[j];
                        if (item == '\\' || item == '[' || item == '^') expression.Append('\\');
                        expression.Append(item);
                    }
                    expression.Append(']');
                    i = end;
                }
                else expression.Append(Regex.Escape(ch.ToString()));
            }
            return expression.Append("\\z").ToString();
        }

        private static bool MatchPattern(string value, string pattern, bool path)
        {
            // Regex and literal helpers use their exact input. Only glob/wildcard paths
            // normalize separators; backslashes in regex retain their normal meaning.
            var hasPrefix = new[] { "regex:", "literal:", "contains:", "starts:", "ends:" }
                .Any(prefix => pattern.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (path && !hasPrefix) { value = value.Replace('/', '\\'); pattern = pattern.Replace('/', '\\'); }
            return GetPatternRegex(pattern).IsMatch(value);
        }

        private static DateTime LocalDate(DateTime value) => (value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value).Date;

        private static bool Group(IEnumerable<Guid> ids, HashSet<Guid> selected, ShelfFilterAction action, bool allValues, List<bool> included)
        {
            if (selected == null || selected.Count == 0) return true;
            var values = ids?.Where(id => id != Guid.Empty).ToList() ?? new List<Guid>();
            Func<Guid, bool> matches = id => id == Guid.Empty ? values.Count == 0 : values.Contains(id);
            // Exclusions always veto any selected value, regardless of the include matching mode.
            if (action == ShelfFilterAction.Exclude) return !selected.Any(matches);
            included.Add(allValues ? selected.All(matches) : selected.Any(matches));
            return true;
        }
    }
}
