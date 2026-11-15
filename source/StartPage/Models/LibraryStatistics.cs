using System;
using System.Collections.Generic;
using System.Linq;
using LandingPage.Settings;
using Playnite.SDK;
using Playnite.SDK.Models;
namespace LandingPage.Models
{
    public sealed class LibraryStatisticsTotals
    {
        public int Total { get; internal set; }
        public int Installed { get; internal set; }
        public int Unplayed { get; internal set; }
        public int Completed { get; internal set; }
        public decimal PlaytimeSeconds { get; internal set; }
        public Dictionary<Guid,Guid> SourcePluginIds { get; } = new Dictionary<Guid,Guid>();
        public Dictionary<Guid,int> Sources { get; } = new Dictionary<Guid,int>();
        public Dictionary<Guid,int> Platforms { get; } = new Dictionary<Guid,int>();
        public Dictionary<Guid,int> Genres { get; } = new Dictionary<Guid,int>();
        public Dictionary<Guid,int> CompletionStatuses { get; } = new Dictionary<Guid,int>();
        public int Hidden { get; internal set; }
        public int Favorites { get; internal set; }
        public int NeverLaunched { get; internal set; }
        public decimal TotalLaunches { get; internal set; }
        public decimal MedianPlaytimeSeconds { get; internal set; }
        public ulong LongestPlaytimeSeconds { get; internal set; }
        public Guid MostPlayedGameId { get; internal set; }
        public string MostPlayedGameName { get; internal set; }
        public int MissingCover { get; internal set; }
        public int MissingBackground { get; internal set; }
        public int MissingIcon { get; internal set; }
        public int MissingDescription { get; internal set; }
        public int NoReleaseDate { get; internal set; }
        public Dictionary<Guid,int> Categories { get; } = new Dictionary<Guid,int>();
        public Dictionary<Guid,int> Tags { get; } = new Dictionary<Guid,int>();
        public Dictionary<Guid,int> Features { get; } = new Dictionary<Guid,int>();
        public Dictionary<Guid,int> Developers { get; } = new Dictionary<Guid,int>();
        public Dictionary<Guid,int> Publishers { get; } = new Dictionary<Guid,int>();
        public Dictionary<Guid,decimal> CategoryPlaytime { get; } = new Dictionary<Guid,decimal>();
        public Dictionary<Guid,decimal> TagPlaytime { get; } = new Dictionary<Guid,decimal>();
        public Dictionary<Guid,decimal> FeaturePlaytime { get; } = new Dictionary<Guid,decimal>();
        public Dictionary<Guid,decimal> DeveloperPlaytime { get; } = new Dictionary<Guid,decimal>();
        public Dictionary<Guid,decimal> PublisherPlaytime { get; } = new Dictionary<Guid,decimal>();
        public int GamesWithPlaytime { get; internal set; }
        public decimal InstalledPlaytime { get; internal set; }
        public decimal CompletedPlaytime { get; internal set; }
        public string AveragePlaytime => (GamesWithPlaytime == 0 ? 0 : PlaytimeSeconds / GamesWithPlaytime / 3600m).ToString("N1") + " h";
        public Dictionary<Guid,decimal> SourcePlaytime { get; } = new Dictionary<Guid,decimal>();
        public Dictionary<Guid,decimal> PlatformPlaytime { get; } = new Dictionary<Guid,decimal>();
        public Dictionary<Guid,decimal> GenrePlaytime { get; } = new Dictionary<Guid,decimal>();
        public Dictionary<Guid,decimal> CompletionPlaytime { get; } = new Dictionary<Guid,decimal>();
        public Dictionary<Guid,decimal> GamePlaytime { get; } = new Dictionary<Guid,decimal>();
        public Dictionary<Guid,string> GameNames { get; } = new Dictionary<Guid,string>();
        public int[] PlaytimeRangeCounts { get; } = new int[6];
        public decimal[] PlaytimeRangeSeconds { get; } = new decimal[6];
        internal static void AddTime(Dictionary<Guid,decimal> group,Guid id,decimal seconds) { group.TryGetValue(id,out var time); group[id] = time + seconds; }
        internal static void Increment(Dictionary<Guid,int> group,Guid id) { group.TryGetValue(id,out var count); group[id] = count + 1; }
        public string Playtime => (PlaytimeSeconds / 3600m).ToString("N1") + " h";
    }
    // Store only fields used by counters; covers, descriptions and Game objects are not retained.
    public sealed class LibraryStatisticsIndex
    {
        private sealed class Entry { public bool Hidden, Installed, Unplayed, Favorite, MissingCover, MissingBackground, MissingIcon, MissingDescription, NoReleaseDate; public Guid Status, Source, Plugin; public string Name; public Guid[] Platforms, Genres, Categories, Tags, Features, Developers, Publishers; public ulong Playtime, PlayCount; }
        private readonly Dictionary<Guid,Entry> games = new Dictionary<Guid,Entry>();
        private readonly Dictionary<string,LibraryStatisticsTotals> totals = new Dictionary<string,LibraryStatisticsTotals>();
        public void Update(Game game) { if (game != null) { totals.Clear(); games[game.Id] = new Entry { Name = game.Name, Plugin = game.PluginId, Favorite = game.Favorite, PlayCount = game.PlayCount,
                MissingCover = string.IsNullOrWhiteSpace(game.CoverImage), MissingBackground = string.IsNullOrWhiteSpace(game.BackgroundImage), MissingIcon = string.IsNullOrWhiteSpace(game.Icon), MissingDescription = string.IsNullOrWhiteSpace(game.Description), NoReleaseDate = !game.ReleaseDate.HasValue,
                Categories = Ids(game.CategoryIds), Tags = Ids(game.TagIds), Features = Ids(game.FeatureIds), Developers = Ids(game.DeveloperIds), Publishers = Ids(game.PublisherIds), Hidden = game.Hidden, Installed = game.IsInstalled, Unplayed = game.Playtime == 0 && !game.LastActivity.HasValue, Status = game.CompletionStatusId, Source = game.SourceId, Platforms = game.PlatformIds?.Distinct().ToArray() ?? new Guid[0], Genres = game.GenreIds?.Distinct().ToArray() ?? new Guid[0], Playtime = game.Playtime }; } }
        private static Guid[] Ids(IEnumerable<Guid> ids) => ids?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? new Guid[0];
        private static void Aggregate(Guid[] ids,Dictionary<Guid,int> counts,Dictionary<Guid,decimal> times,ulong seconds)
        {
            foreach(var id in ids.Length == 0 ? new[] { Guid.Empty } : ids) { LibraryStatisticsTotals.Increment(counts,id); LibraryStatisticsTotals.AddTime(times,id,seconds); }
        }
        public void Remove(Guid id) { if(games.Remove(id)) totals.Clear(); }
        public HashSet<Guid> IncludedGameIds(LibraryStatisticsSettings settings) => new HashSet<Guid>(games.Where(pair => (settings.IncludeHidden || !pair.Value.Hidden) && (!settings.InstalledOnly || pair.Value.Installed)).Select(pair => pair.Key));
        public static Guid[] ResolveCompletedStatuses(LibraryStatisticsSettings settings,IEnumerable<CompletionStatus> statuses,bool automaticOnly = false)
            => !automaticOnly && settings.UseCustomCompletedStatuses ? settings.CompletedStatusIds.Where(id => id != Guid.Empty).Distinct().ToArray()
                : (statuses ?? Enumerable.Empty<CompletionStatus>()).Where(status => string.Equals(status.Name?.Trim(),"Completed",StringComparison.OrdinalIgnoreCase)).Select(status => status.Id).ToArray();
        public LibraryStatisticsTotals Calculate(LibraryStatisticsSettings settings,IEnumerable<Guid> completedStatuses = null)
        {
            var statuses = new HashSet<Guid>((completedStatuses ?? (settings.UseCustomCompletedStatuses ? settings.CompletedStatusIds : new Guid[0])).Where(id => id != Guid.Empty));
            var includeGames = settings.Breakdown == StatisticsBreakdown.Games;
            var key = settings.IncludeHidden + ":" + settings.InstalledOnly + ":" + includeGames + ":" + string.Join(",",statuses.OrderBy(id => id));
            if(totals.TryGetValue(key,out var cached)) return cached;
            var result = new LibraryStatisticsTotals(); var playedTimes = new List<ulong>();
            foreach (var pair in games)
            {
                var game = pair.Value;
                if ((!settings.IncludeHidden && game.Hidden) || (settings.InstalledOnly && !game.Installed)) continue;
                if(game.Hidden) result.Hidden++; if(game.Favorite) result.Favorites++;
                if(game.PlayCount == 0) result.NeverLaunched++; result.TotalLaunches += game.PlayCount;
                if(game.MissingCover) result.MissingCover++; if(game.MissingBackground) result.MissingBackground++; if(game.MissingIcon) result.MissingIcon++; if(game.MissingDescription) result.MissingDescription++; if(game.NoReleaseDate) result.NoReleaseDate++;
                if(game.Playtime > 0)
                {
                    playedTimes.Add(game.Playtime);
                    if(game.Playtime > result.LongestPlaytimeSeconds || (game.Playtime == result.LongestPlaytimeSeconds && string.Compare(game.Name,result.MostPlayedGameName,StringComparison.CurrentCultureIgnoreCase) < 0)) { result.LongestPlaytimeSeconds = game.Playtime; result.MostPlayedGameId = pair.Key; result.MostPlayedGameName = game.Name; }
                }
                Aggregate(game.Categories,result.Categories,result.CategoryPlaytime,game.Playtime);
                Aggregate(game.Tags,result.Tags,result.TagPlaytime,game.Playtime);
                Aggregate(game.Features,result.Features,result.FeaturePlaytime,game.Playtime);
                Aggregate(game.Developers,result.Developers,result.DeveloperPlaytime,game.Playtime);
                Aggregate(game.Publishers,result.Publishers,result.PublisherPlaytime,game.Playtime);
                result.Total++; if (game.Installed) result.Installed++; if (game.Unplayed) result.Unplayed++;
                if (statuses.Contains(game.Status)) result.Completed++;
                result.PlaytimeSeconds += game.Playtime;
                if(game.Playtime > 0) result.GamesWithPlaytime++;
                if(game.Installed) result.InstalledPlaytime += game.Playtime;
                if(statuses.Contains(game.Status)) result.CompletedPlaytime += game.Playtime;
                if(includeGames) { result.GamePlaytime[pair.Key] = game.Playtime; result.GameNames[pair.Key] = game.Name; }
                LibraryStatisticsTotals.AddTime(result.SourcePlaytime,game.Source,game.Playtime);
                LibraryStatisticsTotals.AddTime(result.CompletionPlaytime,game.Status,game.Playtime);
                var range = game.Playtime == 0 ? 0 : game.Playtime < 3600 ? 1 : game.Playtime < 36000 ? 2 : game.Playtime < 180000 ? 3 : game.Playtime < 360000 ? 4 : 5;
                result.PlaytimeRangeCounts[range]++; result.PlaytimeRangeSeconds[range] += game.Playtime;
                LibraryStatisticsTotals.Increment(result.Sources,game.Source);
                if(game.Plugin != Guid.Empty && !result.SourcePluginIds.ContainsKey(game.Source)) result.SourcePluginIds[game.Source] = game.Plugin;
                LibraryStatisticsTotals.Increment(result.CompletionStatuses,game.Status);
                foreach(var id in game.Platforms.Length == 0 ? new[] { Guid.Empty } : game.Platforms) { LibraryStatisticsTotals.Increment(result.Platforms,id); LibraryStatisticsTotals.AddTime(result.PlatformPlaytime,id,game.Playtime); }
                foreach(var id in game.Genres.Length == 0 ? new[] { Guid.Empty } : game.Genres) { LibraryStatisticsTotals.Increment(result.Genres,id); LibraryStatisticsTotals.AddTime(result.GenrePlaytime,id,game.Playtime); }
            }
            if(playedTimes.Count > 0) { playedTimes.Sort(); var middle = playedTimes.Count/2; result.MedianPlaytimeSeconds = playedTimes.Count%2 == 1 ? playedTimes[middle] : ((decimal)playedTimes[middle-1]+playedTimes[middle])/2; }
            if(totals.Count >= 32) totals.Clear();
            totals[key] = result;
            return result;
        }
    }
    internal sealed class LibraryStatisticsSource : IDisposable
    {
        private readonly IPlayniteAPI api;
        private readonly object gate = new object();
        private readonly LibraryStatisticsIndex index = new LibraryStatisticsIndex();
        public event EventHandler Changed;
        internal LibraryStatisticsSource(IPlayniteAPI api)
        {
            this.api = api;
            lock (gate)
            {
                api.Database.Games.ItemUpdated += Updated;
                api.Database.Games.ItemCollectionChanged += CollectionChanged;
                if(api.Database.Sources != null) { api.Database.Sources.ItemUpdated += NamesChanged; api.Database.Sources.ItemCollectionChanged += NamesChanged; }
                if(api.Database.Platforms != null) { api.Database.Platforms.ItemUpdated += NamesChanged; api.Database.Platforms.ItemCollectionChanged += NamesChanged; }
                if(api.Database.Genres != null) { api.Database.Genres.ItemUpdated += NamesChanged; api.Database.Genres.ItemCollectionChanged += NamesChanged; }
                if(api.Database.CompletionStatuses != null) { api.Database.CompletionStatuses.ItemUpdated += NamesChanged; api.Database.CompletionStatuses.ItemCollectionChanged += NamesChanged; }
                if(api.Database.Categories != null) { api.Database.Categories.ItemUpdated += NamesChanged; api.Database.Categories.ItemCollectionChanged += NamesChanged; }
                if(api.Database.Tags != null) { api.Database.Tags.ItemUpdated += NamesChanged; api.Database.Tags.ItemCollectionChanged += NamesChanged; }
                if(api.Database.Features != null) { api.Database.Features.ItemUpdated += NamesChanged; api.Database.Features.ItemCollectionChanged += NamesChanged; }
                if(api.Database.Companies != null) { api.Database.Companies.ItemUpdated += NamesChanged; api.Database.Companies.ItemCollectionChanged += NamesChanged; }
                foreach (var game in api.Database.Games) index.Update(game);
            }
        }
        internal LibraryStatisticsTotals Calculate(LibraryStatisticsSettings settings,bool automaticCompletion = false) { lock(gate) return index.Calculate(settings,LibraryStatisticsIndex.ResolveCompletedStatuses(settings,api.Database.CompletionStatuses,automaticCompletion)); }
        internal HashSet<Guid> IncludedGameIds(LibraryStatisticsSettings settings) { lock(gate) return index.IncludedGameIds(settings); }
        private void Updated(object sender, ItemUpdatedEventArgs<Game> args)
        { lock(gate) foreach(var item in args.UpdatedItems) index.Update(item.NewData); Changed?.Invoke(this,EventArgs.Empty); }
        private void CollectionChanged(object sender, ItemCollectionChangedEventArgs<Game> args)
        { lock(gate) { foreach(var game in args.RemovedItems) index.Remove(game.Id); foreach(var game in args.AddedItems) index.Update(game); } Changed?.Invoke(this,EventArgs.Empty); }
        private void NamesChanged(object sender,EventArgs args) => Changed?.Invoke(this,EventArgs.Empty);
        public void Dispose()
        {
            if(api.Database.Categories != null) { api.Database.Categories.ItemUpdated -= NamesChanged; api.Database.Categories.ItemCollectionChanged -= NamesChanged; }
            if(api.Database.Tags != null) { api.Database.Tags.ItemUpdated -= NamesChanged; api.Database.Tags.ItemCollectionChanged -= NamesChanged; }
            if(api.Database.Features != null) { api.Database.Features.ItemUpdated -= NamesChanged; api.Database.Features.ItemCollectionChanged -= NamesChanged; }
            if(api.Database.Companies != null) { api.Database.Companies.ItemUpdated -= NamesChanged; api.Database.Companies.ItemCollectionChanged -= NamesChanged; }
            api.Database.Games.ItemUpdated -= Updated; api.Database.Games.ItemCollectionChanged -= CollectionChanged;
            if(api.Database.Sources != null) { api.Database.Sources.ItemUpdated -= NamesChanged; api.Database.Sources.ItemCollectionChanged -= NamesChanged; }
            if(api.Database.Platforms != null) { api.Database.Platforms.ItemUpdated -= NamesChanged; api.Database.Platforms.ItemCollectionChanged -= NamesChanged; }
            if(api.Database.Genres != null) { api.Database.Genres.ItemUpdated -= NamesChanged; api.Database.Genres.ItemCollectionChanged -= NamesChanged; }
            if(api.Database.CompletionStatuses != null) { api.Database.CompletionStatuses.ItemUpdated -= NamesChanged; api.Database.CompletionStatuses.ItemCollectionChanged -= NamesChanged; }
            Changed = null;
        }
    }
}
