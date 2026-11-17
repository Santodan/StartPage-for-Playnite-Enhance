# Offline fixtures: no installed settings or network writes.
$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path $PSScriptRoot -Parent) 'source\StartPage\bin\Release'
Add-Type -AssemblyName System.Net.Http
[void][Reflection.Assembly]::LoadFrom((Join-Path $build 'Newtonsoft.Json.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $build 'StartPagePlugin.dll'))
Add-Type -ReferencedAssemblies 'System.Net.Http.dll' -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
public sealed class UpdateFixtureHandler : HttpMessageHandler
{
    public readonly List<string> Requests = new List<string>();
    public readonly Queue<string> Pages = new Queue<string>();
    public HttpStatusCode Status = HttpStatusCode.OK;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(request.Headers.UserAgent.Count == 0 || request.Headers.Accept.Count == 0) throw new Exception("Missing GitHub headers");
        Requests.Add(request.RequestUri.ToString());
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Pages.Dequeue()) });
    }
}
'@
function Release($tag,$pre=$false,$draft=$false,$package=$true) {
    $assets = @(); if($package) { $assets = @(@{name='santodan_StartPage_Enhanced_Plugin_0_18_0.pext';size=1200;state='uploaded'}) }
    @{tag_name=$tag;prerelease=$pre;draft=$draft;assets=$assets;html_url='https://example.invalid/untrusted'}
}
function Json($entries) { ConvertTo-Json -InputObject @($entries) -Depth 8 -Compress }
$installed = [version]'0.17.0.0'
$fixture = Json @((Release 'v0.16.0'),(Release 'v0.18.0' $true),(Release 'v9.0.0' $false $true),(Release 'v10.0.0' $false $false $false),(Release 'bad-tag'))
$selected = [LandingPage.Utils.GitHubUpdateChecker]::SelectUpdate($fixture,$installed)
if($selected.Tag -ne 'v0.18.0' -or !$selected.Prerelease -or $selected.Url -ne 'https://github.com/Santodan/StartPage-for-Playnite-Enhance/releases/tag/v0.18.0') { throw 'Experimental selection or safe release URL failed' }
foreach ($current in @([version]'1.0.0',[version]'2.0.0')) {
    $fixture = Json @((Release 'v3.0.0' $true),(Release 'v2.1.0'),(Release 'v4.0.0-beta.1'))
    if([LandingPage.Utils.GitHubUpdateChecker]::SelectUpdate($fixture,$current).Tag -ne 'v2.1.0') { throw 'Stable versions include prereleases' }
}
$fixture = Json @((Release 'v0.18.0-rc.2' $true),(Release 'v0.18.0-rc.10' $true),(Release 'v0.18.0-beta' $true))
if([LandingPage.Utils.GitHubUpdateChecker]::SelectUpdate($fixture,$installed).Tag -ne 'v0.18.0-rc.10') { throw 'Numeric prerelease identifiers sorted incorrectly' }
$fixture = Json @((Release 'v0.18.0-rc.10' $true),(Release 'v0.18.0'),(Release 'v0.17.0'))
if([LandingPage.Utils.GitHubUpdateChecker]::SelectUpdate($fixture,$installed).Tag -ne 'v0.18.0') { throw 'Stable release did not outrank its prereleases' }
if($null -ne [LandingPage.Utils.GitHubUpdateChecker]::SelectUpdate((Json @((Release 'v0.17.0'),(Release 'v0.16.0'))),$installed)) { throw 'Offered an installed version or downgrade' }
$unfinished = Release 'v0.99.0'; $unfinished.assets[0].state='new'
if($null -ne [LandingPage.Utils.GitHubUpdateChecker]::SelectUpdate((Json @($unfinished)),$installed)) { throw 'Unfinished package was offered' }
'PASS: prerelease/stable policy, version ordering, drafts/assets, downgrade prevention and safe links.'
$handler = [UpdateFixtureHandler]::new()
$firstPage = @(1..99 | ForEach-Object { Release 'v0.16.0' }); $firstPage += Release 'v0.18.0' $true
$handler.Pages.Enqueue((Json $firstPage)); $handler.Pages.Enqueue((Json @((Release 'v0.19.0' $true))))
$checker = [LandingPage.Utils.GitHubUpdateChecker]::new($handler)
try {
    $result = $checker.CheckAsync($installed,[Threading.CancellationToken]::None).GetAwaiter().GetResult()
    if($result.Tag -ne 'v0.19.0' -or $handler.Requests.Count -ne 2 -or $handler.Requests[1] -notmatch 'page=2$') { throw 'Pagination did not select highest version' }
} finally { $checker.Dispose() }
$handler = [UpdateFixtureHandler]::new(); $handler.Status = [Net.HttpStatusCode]::Forbidden; $handler.Pages.Enqueue('[]')
$checker = [LandingPage.Utils.GitHubUpdateChecker]::new($handler)
try {
    $failed = $false; try { [void]$checker.CheckAsync($installed,[Threading.CancellationToken]::None).GetAwaiter().GetResult() } catch { $failed=$true }
    if(!$failed) { throw 'API failure was reported as success' }
} finally { $checker.Dispose() }
$handler = [UpdateFixtureHandler]::new(); $cancel = [Threading.CancellationTokenSource]::new(); $cancel.Cancel()
$checker = [LandingPage.Utils.GitHubUpdateChecker]::new($handler)
try {
    $failed = $false; try { [void]$checker.CheckAsync($installed,$cancel.Token).GetAwaiter().GetResult() } catch { $failed=$true }
    if(!$failed) { throw 'Cancelled update check completed normally' }
} finally { $checker.Dispose(); $cancel.Dispose() }
'PASS: paginated HTTP requests/headers, request failures and shutdown cancellation.'
