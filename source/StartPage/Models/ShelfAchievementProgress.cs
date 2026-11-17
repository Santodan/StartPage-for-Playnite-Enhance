using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LandingPage.Settings;
using Newtonsoft.Json;
using SqlNado;

namespace LandingPage.Models
{
    internal class ShelfAchievementProgress
    {
        public string GameId { get; set; }
        public int Total { get; set; }
        public int Unlocked { get; set; }
    }

    internal static class ShelfAchievements
    {
        private static readonly object gate = new object();
        private static string cachedRoot;
        private static DateTime cacheExpires;
        private static Task<Dictionary<Guid, ShelfAchievementProgress>> snapshot;

        public static async Task<ShelfAchievementProgress> GetAsync(Guid gameId, AchievementsProvider provider, string root)
        {
            if (string.IsNullOrEmpty(root)) return null;
            if (provider == AchievementsProvider.SuccessStory)
                return await Task.Run(() => ReadSuccessStory(Path.Combine(root,
                    "cebe6d32-8c46-4459-b993-5a5189d60788", "SuccessStory", gameId + ".json")));

            Task<Dictionary<Guid, ShelfAchievementProgress>> pending;
            lock (gate)
            {
                if (snapshot == null || cachedRoot != root || DateTime.UtcNow >= cacheExpires)
                {
                    cachedRoot = root;
                    cacheExpires = DateTime.UtcNow.AddSeconds(10);
                    snapshot = Task.Run(() => ReadDatabase(PlayniteAchievementsProvider.FindAchievementCachePath(root)));
                }
                pending = snapshot;
            }
            var progress = await pending;
            return progress.TryGetValue(gameId, out var result) ? result : null;
        }

        private static string bulkKey;
        private static DateTime bulkExpires;
        private static Task<Dictionary<Guid, ShelfAchievementProgress>> bulkSnapshot;
        internal static Task<Dictionary<Guid, ShelfAchievementProgress>> GetSnapshotAsync(AchievementsProvider provider, string root)
        {
            if (string.IsNullOrEmpty(root)) return Task.FromResult(new Dictionary<Guid, ShelfAchievementProgress>());
            if (provider != AchievementsProvider.SuccessStory) return GetDatabaseSnapshot(root);
            lock (gate)
            {
                var key = root + "|SuccessStory";
                if (bulkSnapshot == null || bulkKey != key || DateTime.UtcNow >= bulkExpires)
                {
                    bulkKey = key; bulkExpires = DateTime.UtcNow.AddSeconds(10);
                    bulkSnapshot = Task.Run(() => {
                        var result = new Dictionary<Guid, ShelfAchievementProgress>();
                        var folder = Path.Combine(root,"cebe6d32-8c46-4459-b993-5a5189d60788","SuccessStory");
                        try {
                            if (Directory.Exists(folder)) foreach (var path in Directory.EnumerateFiles(folder,"*.json"))
                                if (Guid.TryParse(Path.GetFileNameWithoutExtension(path),out var id)) {
                                    var progress = ReadSuccessStory(path); if (progress != null) result[id] = progress;
                                }
                        } catch (Exception ex) { LandingPageExtension.logger?.Debug(ex,"Unable to read achievement snapshot."); }
                        return result;
                    });
                }
                return bulkSnapshot;
            }
        }
        private static Task<Dictionary<Guid, ShelfAchievementProgress>> GetDatabaseSnapshot(string root)
        {
            lock (gate)
            {
                if (snapshot == null || cachedRoot != root || DateTime.UtcNow >= cacheExpires) {
                    cachedRoot = root; cacheExpires = DateTime.UtcNow.AddSeconds(10);
                    snapshot = Task.Run(() => ReadDatabase(PlayniteAchievementsProvider.FindAchievementCachePath(root)));
                }
                return snapshot;
            }
        }

        internal static ShelfAchievementProgress ReadSuccessStory(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var data = JsonConvert.DeserializeObject<SuccessStory.Achievements>(File.ReadAllText(path));
                return data?.Total > 0 ? new ShelfAchievementProgress { Total = data.Total,
                    Unlocked = Math.Max(0, Math.Min(data.Total, data.Unlocked)) } : null;
            }
            catch (Exception ex)
            {
                LandingPageExtension.logger.Debug(ex, "Unable to read optional shelf achievement data.");
                return null;
            }
        }

        internal static Dictionary<Guid, ShelfAchievementProgress> ReadDatabase(string path)
        {
            var result = new Dictionary<Guid, ShelfAchievementProgress>();
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;
                using (var db = new SQLiteDatabase(path, SQLiteOpenOptions.SQLITE_OPEN_READONLY))
                {
                    foreach (var data in db.Load<ShelfAchievementProgress>(@"
SELECT COALESCE(g.PlayniteGameId, ugp.CacheKey) AS GameId,
       MAX(ugp.TotalAchievements) AS Total, MAX(ugp.AchievementsUnlocked) AS Unlocked
FROM UserGameProgress ugp JOIN Games g ON g.Id = ugp.GameId
GROUP BY COALESCE(g.PlayniteGameId, ugp.CacheKey)"))
                    {
                        if (data.Total <= 0 || !Guid.TryParse(data.GameId, out var id)) continue;
                        data.Unlocked = Math.Max(0, Math.Min(data.Total, data.Unlocked));
                        result[id] = data;
                    }
                }
            }
            catch (Exception ex)
            {
                LandingPageExtension.logger.Debug(ex, "Unable to read optional shelf achievement data.");
            }
            return result;
        }
    }
}
