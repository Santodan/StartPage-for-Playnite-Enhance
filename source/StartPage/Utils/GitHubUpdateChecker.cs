using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LandingPage.Utils
{
    public sealed class GitHubUpdateChecker : IDisposable
    {
        public const string Repository = "https://github.com/Santodan/StartPage-for-Playnite-Enhance";
        public const string ReleasesApi = "https://api.github.com/repos/Santodan/StartPage-for-Playnite-Enhance/releases";
        private readonly HttpClient client;
        public GitHubUpdateChecker(HttpMessageHandler handler = null)
        {
            client = handler == null ? new HttpClient() : new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("StartPage-Enhanced/" + typeof(GitHubUpdateChecker).Assembly.GetName().Version.ToString(3));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }
        public sealed class Release
        {
            public string Tag { get; internal set; }
            public Version Version { get; internal set; }
            public string PrereleaseSuffix { get; internal set; }
            public string Url { get; internal set; }
            public bool Prerelease { get; internal set; }
        }
        private static Version Normalize(Version version) => new Version(version.Major,version.Minor,Math.Max(0,version.Build),Math.Max(0,version.Revision));
        private static bool Parse(string tag,out Version version,out string suffix)
        {
            version = null; suffix = null;
            if (string.IsNullOrWhiteSpace(tag)) return false;
            var value = tag.Trim(); if (value.StartsWith("v",StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            value = value.Split('+')[0]; var split = value.IndexOf('-');
            if (split >= 0) { suffix = value.Substring(split+1); value = value.Substring(0,split); if (suffix.Length == 0) return false; }
            if (!Version.TryParse(value,out version)) return false;
            version = Normalize(version); return true;
        }
        private static int CompareSuffix(string left,string right)
        {
            if (left == right) return 0;
            if (left == null) return 1; if (right == null) return -1;
            var a = left.Split('.'); var b = right.Split('.');
            for (var i=0;i<Math.Min(a.Length,b.Length);i++) {
                var an = a[i].All(char.IsDigit); var bn = b[i].All(char.IsDigit);
                int result;
                if (an && bn) { var av = a[i].TrimStart('0'); var bv = b[i].TrimStart('0'); result = av.Length.CompareTo(bv.Length); if(result == 0) result = string.CompareOrdinal(av,bv); }
                else result = an != bn ? (an ? -1 : 1) : string.CompareOrdinal(a[i],b[i]);
                if (result != 0) return result;
            }
            return a.Length.CompareTo(b.Length);
        }
        public static Release SelectUpdate(string json,Version installed)
        {
            Release best = null;
            foreach (var item in JArray.Parse(json).OfType<JObject>()) {
                if ((bool?)item["draft"] == true || !Parse((string)item["tag_name"],out var version,out var suffix)) continue;
                var prerelease = (bool?)item["prerelease"] == true || suffix != null;
                if (installed.Major >= 1 && prerelease) continue;
                if (version.CompareTo(Normalize(installed)) <= 0) continue;
                // An incomplete release must not trigger an installation notification.
                var assets = item["assets"] as JArray;
                if (assets == null || !assets.OfType<JObject>().Any(asset =>
                    ((string)asset["name"] ?? "").StartsWith("santodan_StartPage_Enhanced_Plugin_",StringComparison.OrdinalIgnoreCase) &&
                    ((string)asset["name"] ?? "").EndsWith(".pext",StringComparison.OrdinalIgnoreCase) &&
                    (string)asset["state"] == "uploaded" && (long?)asset["size"] > 0)) continue;
                if (best != null && (version.CompareTo(best.Version) < 0 || (version == best.Version && CompareSuffix(suffix,best.PrereleaseSuffix) <= 0))) continue;
                var tag = (string)item["tag_name"];
                best = new Release { Tag = tag,Version = version,PrereleaseSuffix = suffix,Prerelease = prerelease,
                    Url = Repository + "/releases/tag/" + Uri.EscapeDataString(tag) };
            }
            return best;
        }
        public async Task<Release> CheckAsync(Version installed,CancellationToken cancellation)
        {
            Release best = null;
            // Follow full pages so release ordering does not determine version ordering.
            for (var page=1;page<=10;page++) {
                using (var response = await client.GetAsync(ReleasesApi+"?per_page=100&page="+page,cancellation).ConfigureAwait(false)) {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                    var candidate = SelectUpdate(json,installed);
                    if(candidate != null && (best == null || candidate.Version > best.Version ||
                        (candidate.Version == best.Version && CompareSuffix(candidate.PrereleaseSuffix,best.PrereleaseSuffix)>0))) best = candidate;
                    if(JArray.Parse(json).Count < 100) break;
                }
            }
            return best;
        }
        public void Dispose() => client.Dispose();
    }
}
