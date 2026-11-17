$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName System.Net.Http
$root = Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'source\StartPage\bin\Release\StartPagePlugin.dll'))
Add-Type -ReferencedAssemblies 'System.Net.Http.dll' -TypeDefinition @'
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
public sealed class WebsiteIconFixtureHandler : HttpMessageHandler
{
    public readonly Dictionary<string,byte[]> Responses = new Dictionary<string,byte[]>();
    public readonly List<string> Requests = new List<string>();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        lock(Requests) Requests.Add(request.RequestUri.AbsoluteUri);
        byte[] body;
        var found = Responses.TryGetValue(request.RequestUri.AbsoluteUri,out body);
        return Task.FromResult(new HttpResponseMessage(found ? HttpStatusCode.OK : HttpStatusCode.NotFound) {
            Content = new ByteArrayContent(body ?? new byte[0]), RequestMessage = request
        });
    }
}
'@
$directory = Join-Path $env:TEMP ('StartPage-icons-test-'+[guid]::NewGuid())
$token = [Threading.CancellationToken]::None
$folder = [LandingPage.Utils.ShortcutIcons]::Folder
if(!$folder -or !$folder.IsFrozen -or $folder.PixelWidth -lt 16) { throw 'Windows folder stock icon missing or not frozen' }
if(![LandingPage.Utils.ShortcutIcons]::Website) { throw 'Windows website fallback icon missing' }
$page = [LandingPage.Utils.WebsiteIconCache]::PageUri('https://user:password@example.test/page?q=secret#section')
if($page.AbsoluteUri -ne 'https://example.test/page') { throw 'Favicon page request retains credentials/query/fragment' }
$candidates = [LandingPage.Utils.WebsiteIconCache]::Discover('<link href="/assets/icon.png?x=1&amp;y=2" rel="shortcut icon"><link rel=icon href="javascript:bad"><link rel="apple-touch-icon" href="/apple.png">',$page)
if($candidates.Count -ne 3 -or $candidates[0].AbsoluteUri -ne 'https://example.test/assets/icon.png?x=1&y=2' -or $candidates[2].AbsoluteUri -ne 'https://example.test/favicon.ico') { throw 'Favicon discovery/attribute order/entity decoding/fallback failed' }
$handler = [WebsiteIconFixtureHandler]::new()
$handler.Responses.Add('https://example.test/page',[Text.Encoding]::UTF8.GetBytes('<link rel="icon" href="/unsupported.svg"><link href="assets/site.png" rel="icon">'))
$handler.Responses.Add('https://example.test/unsupported.svg',[Text.Encoding]::UTF8.GetBytes('<svg/>'))
$handler.Responses.Add('https://example.test/assets/site.png',[IO.File]::ReadAllBytes((Join-Path $root 'source\StartPage\icon.png')))
$cache = [LandingPage.Utils.WebsiteIconCache]::new($directory,$handler)
try {
    $task = $cache.GetAsync($page.AbsoluteUri,$token)
    if(![object]::ReferenceEquals($task,$cache.GetAsync($page.AbsoluteUri,$token))) { throw 'Duplicate icon requests were not shared' }
    $icon = $task.GetAwaiter().GetResult()
    if(!$icon -or !$icon.IsFrozen -or $handler.Requests.Count -ne 3) { throw 'Declared favicon or invalid-image fallback failed' }
    if(@(Get-ChildItem -LiteralPath $directory -Filter *.png).Count -ne 1) { throw 'Decoded website icon was not cached locally' }
} finally { $cache.Dispose() }
$offlineHandler = [WebsiteIconFixtureHandler]::new()
$cache = [LandingPage.Utils.WebsiteIconCache]::new($directory,$offlineHandler)
try {
    if(!$cache.GetAsync($page.AbsoluteUri,$token).GetAwaiter().GetResult() -or $offlineHandler.Requests.Count -ne 0) { throw 'Cached favicon needs another network request' }
} finally { $cache.Dispose() }
$fallbackHandler = [WebsiteIconFixtureHandler]::new()
$fallbackHandler.Responses.Add('https://fallback.test/favicon.ico',[IO.File]::ReadAllBytes((Join-Path $root 'source\StartPage\icon.png')))
$cache = [LandingPage.Utils.WebsiteIconCache]::new($null,$fallbackHandler)
try {
    if(!$cache.GetAsync('https://fallback.test/page',$token).GetAwaiter().GetResult()) { throw '/favicon.ico fallback did not load' }
    if($null -ne $cache.GetAsync('file:///C:/test',$token).GetAwaiter().GetResult()) { throw 'Non-web target accepted' }
    if($null -ne $cache.GetAsync('https://missing.test/',$token).GetAwaiter().GetResult()) { throw 'Missing favicon did not return a safe fallback result' }
} finally { $cache.Dispose() }
'PASS: native Windows folder icon, declared favicon discovery, invalid-image/root fallback, request deduplication, frozen decoded images and offline disk cache.'
