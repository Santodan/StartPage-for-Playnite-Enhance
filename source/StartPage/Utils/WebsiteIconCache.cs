using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LandingPage.Utils
{
    public sealed class WebsiteIconCache : IDisposable
    {
        private readonly HttpClient client;
        private readonly string cacheDirectory;
        private readonly SemaphoreSlim downloads = new SemaphoreSlim(4);
        private readonly ConcurrentDictionary<string,Lazy<Task<BitmapSource>>> requests = new ConcurrentDictionary<string,Lazy<Task<BitmapSource>>>();
        private static readonly Regex links = new Regex(@"<link\b[^>]*>",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
        private static readonly Regex attributes = new Regex(@"(?<name>[\w-]+)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
        public WebsiteIconCache(string directory,HttpMessageHandler handler = null)
        {
            cacheDirectory = directory;
            client = handler == null ? new HttpClient(new HttpClientHandler { UseCookies = false,MaxAutomaticRedirections = 5,AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) : new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(12);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("StartPage-Enhanced/"+typeof(WebsiteIconCache).Assembly.GetName().Version.ToString(3));
        }
        public static Uri PageUri(string target)
        {
            if(!Uri.TryCreate(target,UriKind.Absolute,out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return null;
            return new UriBuilder(uri) { UserName = "",Password = "",Query = "",Fragment = "" }.Uri;
        }
        public static IList<Uri> Discover(string html,Uri page)
        {
            var result = new List<Uri>();
            foreach(Match link in links.Matches(html ?? "")) {
                var attrs = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                foreach(Match attr in attributes.Matches(link.Value)) attrs[attr.Groups["name"].Value] = WebUtility.HtmlDecode(attr.Groups["value"].Value);
                if(!attrs.TryGetValue("rel",out var rel) || !rel.Split(new[] {' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Any(part => part.Equals("icon",StringComparison.OrdinalIgnoreCase) || part.Equals("apple-touch-icon",StringComparison.OrdinalIgnoreCase))) continue;
                if(attrs.TryGetValue("href",out var href) && Uri.TryCreate(page,href,out var icon) && (icon.Scheme == Uri.UriSchemeHttps || icon.Scheme == Uri.UriSchemeHttp) && string.IsNullOrEmpty(icon.UserInfo) && !result.Contains(icon)) result.Add(icon);
            }
            var fallback = new Uri(page.GetLeftPart(UriPartial.Authority)+"/favicon.ico");
            if(!result.Contains(fallback)) result.Add(fallback);
            return result.Take(8).ToList();
        }
        private string CachePath(Uri page)
        {
            if(string.IsNullOrEmpty(cacheDirectory)) return null;
            using(var hash = SHA256.Create()) return Path.Combine(cacheDirectory,BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(page.AbsoluteUri))).Replace("-","")+".png");
        }
        private static BitmapSource Decode(byte[] bytes)
        {
            using(var stream = new MemoryStream(bytes)) {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 128; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
            }
        }
        private async Task<Tuple<byte[],Uri>> Download(Uri uri,int limit,CancellationToken cancellation)
        {
            using(var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation)) {
                timeout.CancelAfter(TimeSpan.FromSeconds(12));
                using(var response = await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false)) {
                    if(!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > limit) return null;
                    using(var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using(var output = new MemoryStream()) {
                        var buffer = new byte[8192]; int count;
                        while((count = await input.ReadAsync(buffer,0,buffer.Length,timeout.Token).ConfigureAwait(false))>0) {
                            if(output.Length+count>limit) return null;
                            output.Write(buffer,0,count);
                        }
                        return Tuple.Create(output.ToArray(),response.RequestMessage?.RequestUri ?? uri);
                    }
                }
            }
        }
        public Task<BitmapSource> GetAsync(string target,CancellationToken cancellation)
        {
            var page = PageUri(target); if(page == null) return Task.FromResult<BitmapSource>(null);
            var request = requests.GetOrAdd(page.AbsoluteUri,key => new Lazy<Task<BitmapSource>>(() => Task.Run(() => LoadThrottled(page,cancellation),cancellation)));
            return request.Value;
        }
        private async Task<BitmapSource> LoadThrottled(Uri page,CancellationToken cancellation)
        {
            await downloads.WaitAsync(cancellation).ConfigureAwait(false);
            try { return await Load(page,cancellation).ConfigureAwait(false); }
            finally { downloads.Release(); }
        }
        private async Task<BitmapSource> Load(Uri page,CancellationToken cancellation)
        {
            var path = CachePath(page);
            try {
                if(path != null && File.Exists(path) && File.GetLastWriteTimeUtc(path)>DateTime.UtcNow.AddDays(-7)) {
                    try { return Decode(File.ReadAllBytes(path)); } catch(Exception ex) when(ex is IOException || ex is NotSupportedException || ex is FileFormatException || ex is ArgumentException) { }
                }
                IList<Uri> candidates;
                try { var html = await Download(page,256*1024,cancellation).ConfigureAwait(false); candidates = Discover(html == null ? "" : Encoding.UTF8.GetString(html.Item1),html?.Item2 ?? page); }
                catch(Exception ex) when(ex is HttpRequestException || ex is IOException || ex is OperationCanceledException) { cancellation.ThrowIfCancellationRequested(); candidates = Discover("",page); }
                foreach(var icon in candidates) {
                    cancellation.ThrowIfCancellationRequested();
                    try {
                        var data = await Download(icon,2*1024*1024,cancellation).ConfigureAwait(false); if(data == null) continue;
                        var image = Decode(data.Item1);
                        if(path != null) {
                            try { Directory.CreateDirectory(cacheDirectory); using(var output = File.Create(path)) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); encoder.Save(output); } }
                            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException) { }
                        }
                        return image;
                    } catch(Exception ex) when(ex is HttpRequestException || ex is IOException || ex is FileFormatException || ex is NotSupportedException || ex is ArgumentException || ex is InvalidOperationException || ex is OperationCanceledException) { cancellation.ThrowIfCancellationRequested(); }
                }
                return null;
            }
            catch(OperationCanceledException) { throw; }
            catch(Exception ex) { LandingPageExtension.logger.Debug("Website icon unavailable: "+page.Host+" ("+ex.GetType().Name+")."); return null; }
        }
        public void Dispose() => client.Dispose();
    }
}
