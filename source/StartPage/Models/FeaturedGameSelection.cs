using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using LandingPage.Settings;
using Playnite.SDK.Models;

namespace LandingPage.Models
{
    public static class FeaturedGameSelection
    {
        private static readonly Random random = new Random();
        public static Game Select(IEnumerable<Game> library, FeaturedGameSettings settings, Guid? currentId = null, bool shuffle = false, Guid? selectedId = null)
        {
            if (settings.Source == FeaturedGameSource.Pinned) return library.FirstOrDefault(game => game.Id == settings.PinnedGameId);
            var candidates = library.Where(game => (settings.IncludeHidden || !game.Hidden) && (!settings.InstalledOnly || game.IsInstalled));
            if (settings.Source == FeaturedGameSource.SelectedGame) return candidates.FirstOrDefault(game => game.Id == selectedId);
            if (settings.Source == FeaturedGameSource.LastPlayed) return candidates.Where(game => game.LastActivity.HasValue).OrderByDescending(game => game.LastActivity).ThenBy(game => game.Name).FirstOrDefault();
            if (settings.Source == FeaturedGameSource.LastAdded) return candidates.OrderByDescending(game => game.Added).ThenBy(game => game.Name).FirstOrDefault();
            if (settings.Source == FeaturedGameSource.RandomFavorite) candidates = candidates.Where(game => game.Favorite);
            var games = candidates.ToList();
            if (!shuffle && currentId.HasValue && games.FirstOrDefault(game => game.Id == currentId) is Game existing) return existing;
            if (shuffle && games.Count > 1) games.RemoveAll(game => game.Id == currentId);
            lock (random) return games.Count == 0 ? null : games[random.Next(games.Count)];
        }
        public static string Description(string html, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(html) || maxLength <= 0) return string.Empty;
            html = html.Substring(0, Math.Min(html.Length, 100000));
            var text = Regex.Replace(html, "<[^>]*>", " ", RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
            text = Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
            return text.Length <= maxLength ? text : text.Substring(0, maxLength).TrimEnd() + "\u2026";
        }
    }
}
