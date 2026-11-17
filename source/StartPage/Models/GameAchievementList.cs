using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LandingPage.Settings;
using Newtonsoft.Json.Linq;
using SqlNado;

namespace LandingPage.Models
{
    internal sealed class GameAchievementEntry
    {
        public string GameId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string UnlockedIconPath { get; set; }
        public string LockedIconPath { get; set; }
        public int Unlocked { get; set; }
        public DateTime? UnlockedOn { get; set; }
    }
    internal static class GameAchievementList
    {
        private sealed class Cached { public DateTime Expires; public Task<List<GameAchievementEntry>> Data; }
        private static readonly Dictionary<string,Cached> cache = new Dictionary<string,Cached>();
        internal static Task<List<GameAchievementEntry>> GetAsync(string root,AchievementsProvider provider,Guid gameId)
        {
            var key = root + "|" + provider + "|" + gameId;
            lock (cache) {
                if (cache.TryGetValue(key,out var item) && DateTime.UtcNow < item.Expires) return item.Data;
                if (cache.Count >= 64) cache.Clear();
                var task = Task.Run(() => Read(root,provider,gameId));
                cache[key] = new Cached { Expires = DateTime.UtcNow.AddSeconds(10), Data = task }; return task;
            }
        }
        internal static Task<List<GameAchievementEntry>> GetRecentAsync(string root,AchievementsProvider provider,
            string[] eligibleGames,int limit,int perGame,int days)
        {
            var key = root + "|recent|" + provider + "|" + limit + "|" + perGame + "|" + days + "|" + string.Join(",",eligibleGames.OrderBy(id => id));
            lock (cache) {
                if (cache.TryGetValue(key,out var cached) && DateTime.UtcNow < cached.Expires && !cached.Data.IsFaulted) return cached.Data;
                if (cache.Count >= 64) cache.Clear();
                var task = Task.Run(() => ReadRecent(root,provider,eligibleGames,limit,perGame,days));
                cache[key] = new Cached { Expires = DateTime.UtcNow.AddSeconds(10), Data = task }; return task;
            }
        }
        internal static List<GameAchievementEntry> ReadRecent(string root,AchievementsProvider provider,string[] eligibleGames,int limit,int perGame,int days)
        {
            var eligible = new HashSet<string>(eligibleGames,StringComparer.OrdinalIgnoreCase);
            IEnumerable<GameAchievementEntry> entries;
            if (provider == AchievementsProvider.SuccessStory) {
                var all = new List<GameAchievementEntry>();
                foreach (var id in eligibleGames) if (Guid.TryParse(id,out var game)) {
                    foreach (var entry in Read(root,provider,game).Where(item => item.Unlocked != 0)) { entry.GameId = id; all.Add(entry); }
                }
                entries = all.OrderByDescending(entry => entry.UnlockedOn);
                return SelectRecent(entries,eligible,limit,perGame,days);
            }
            var database = FindDatabase(root);
            if (string.IsNullOrEmpty(database) || !File.Exists(database)) return new List<GameAchievementEntry>();
            using (var db = new SQLiteDatabase(database,SQLiteOpenOptions.SQLITE_OPEN_READONLY)) {
                entries = db.Load<GameAchievementEntry>(@"SELECT COALESCE(g.PlayniteGameId,ugp.CacheKey) AS GameId,
ad.DisplayName AS Name, ad.Description, ad.UnlockedIconPath, ad.LockedIconPath,
1 AS Unlocked, MAX(ua.UnlockTimeUtc) AS UnlockedOn
FROM UserAchievements ua JOIN UserGameProgress ugp ON ugp.Id = ua.UserGameProgressId
JOIN AchievementDefinitions ad ON ad.Id = ua.AchievementDefinitionId AND ad.GameId = ugp.GameId
JOIN Games g ON g.Id = ad.GameId WHERE ua.Unlocked = 1
GROUP BY COALESCE(g.PlayniteGameId,ugp.CacheKey), ad.Id ORDER BY UnlockedOn DESC");
                var selected = SelectRecent(entries,eligible,limit,perGame,days);
                foreach (var entry in selected) entry.UnlockedIconPath = ResolveIcon(entry.UnlockedIconPath,Path.GetDirectoryName(database));
                return selected;
            }
        }
        private static List<GameAchievementEntry> SelectRecent(IEnumerable<GameAchievementEntry> entries,HashSet<string> eligible,int limit,int perGame,int days)
        {
            var result = new List<GameAchievementEntry>(); var counts = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            var cutoff = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;
            foreach (var entry in entries) {
                if (!eligible.Contains(entry.GameId) || !entry.UnlockedOn.HasValue || entry.UnlockedOn.Value.ToUniversalTime() < cutoff) continue;
                counts.TryGetValue(entry.GameId,out var count);
                if (perGame > 0 && count >= perGame) continue;
                counts[entry.GameId] = count + 1; result.Add(entry);
                if (limit > 0 && result.Count >= limit) break;
            }
            return result;
        }
        private static readonly object pathGate = new object();
        private static string cachedRoot, cachedDatabase;
        private static DateTime pathExpires;
        private static string FindDatabase(string root)
        {
            lock (pathGate) {
                if (cachedRoot == root && DateTime.UtcNow < pathExpires && File.Exists(cachedDatabase)) return cachedDatabase;
                cachedRoot = root; cachedDatabase = PlayniteAchievementsProvider.FindAchievementCachePath(root);
                pathExpires = DateTime.UtcNow.AddMinutes(1); return cachedDatabase;
            }
        }
        internal static List<GameAchievementEntry> Read(string root,AchievementsProvider provider,Guid gameId)
        {
            if (string.IsNullOrEmpty(root)) return new List<GameAchievementEntry>();
            if (provider == AchievementsProvider.SuccessStory) {
                var path = Path.Combine(root,"cebe6d32-8c46-4459-b993-5a5189d60788","SuccessStory",gameId + ".json");
                if (!File.Exists(path)) return new List<GameAchievementEntry>();
                var data = JObject.Parse(File.ReadAllText(path));
                return (data["Items"] as JArray ?? new JArray()).OfType<JObject>().Select(item => {
                    var unlocked = item["DateUnlocked"]?.Type == JTokenType.Null ? null : item["DateUnlocked"]?.Value<DateTime?>();
                    if (unlocked == default(DateTime)) unlocked = null;
                    return new GameAchievementEntry { Name = (string)item["Name"], Description = (string)item["Description"],
                        Unlocked = unlocked.HasValue ? 1 : 0, UnlockedOn = unlocked,
                        UnlockedIconPath = (string)(item["UrlUnlocked"] ?? item["UriUnlocked"]), LockedIconPath = (string)(item["UrlLocked"] ?? item["UriLocked"]) };
                }).ToList();
            }
            var database = FindDatabase(root);
            if (string.IsNullOrEmpty(database) || !File.Exists(database)) return new List<GameAchievementEntry>();
            using (var db = new SQLiteDatabase(database,SQLiteOpenOptions.SQLITE_OPEN_READONLY)) {
                var entries = db.Load<GameAchievementEntry>(@"SELECT ad.DisplayName AS Name, ad.Description, ad.UnlockedIconPath, ad.LockedIconPath,
COALESCE(MAX(ua.Unlocked),0) AS Unlocked, MAX(CASE WHEN ua.Unlocked = 1 THEN ua.UnlockTimeUtc END) AS UnlockedOn
FROM AchievementDefinitions ad
LEFT JOIN UserGameProgress ugp ON ugp.GameId = ad.GameId
LEFT JOIN UserAchievements ua ON ua.AchievementDefinitionId = ad.Id AND ua.UserGameProgressId = ugp.Id
WHERE ad.GameId IN (SELECT Id FROM Games WHERE PlayniteGameId = ?
                       UNION SELECT GameId FROM UserGameProgress WHERE CacheKey = ?)
GROUP BY ad.Id",gameId.ToString(),gameId.ToString()).ToList();
                foreach (var entry in entries) {
                    entry.UnlockedIconPath = ResolveIcon(entry.UnlockedIconPath,Path.GetDirectoryName(database));
                    entry.LockedIconPath = ResolveIcon(entry.LockedIconPath,Path.GetDirectoryName(database));
                }
                return entries;
            }
        }
        private static string ResolveIcon(string path,string folder) => string.IsNullOrEmpty(path) || Uri.TryCreate(path,UriKind.Absolute,out _) ? path : Path.Combine(folder,path);
    }
}
