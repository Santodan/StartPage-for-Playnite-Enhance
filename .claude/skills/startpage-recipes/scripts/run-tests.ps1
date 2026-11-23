# Low-output build + test runner for StartPage-for-Playnite-Enhance.
# Usage (from any shell):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .claude/skills/startpage-recipes/scripts/run-tests.ps1 [-Build] [-Only Name1,Name2]
#   -Build        rebuild the solution in Release first (errors/warnings only)
#   -Only         test names without "Test-" / ".ps1", e.g. -Only BackgroundAppearance,DashboardPages
#   -ShowTail     lines of output to print for each failing script (default 6)
#   -StallMinutes stop a test that prints nothing for this long and report it as STALLED (default 4, 0 = never)
#   -StatusSeconds interval for the one-line progress status (default 30, 0 = off)
# Prints one line per failure plus a summary; exit code = number of failing scripts.
# While running it shows a progress bar (interactive consoles) and periodic status lines:
#   [2/11 DashboardPages] 20/55 checks, 3:10 elapsed, ~5:20 left (all tests ~9:40 left)
# Progress counts the test's PASS lines; durations are remembered in %TEMP%\StartPageTests\timings.json.
param([switch]$Build, [string[]]$Only, [int]$ShowTail = 6, [double]$StallMinutes = 4, [int]$StatusSeconds = 30)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
if ($Build) {
    Push-Location (Join-Path $repo 'source\StartPage')
    try {
        $out = msbuild StartPage.sln /p:Configuration=Release /t:Rebuild /v:minimal /nologo 2>&1
        $bad = $out | Select-String -Pattern ' error | warning CS'
        if ($bad) { $bad | Select-Object -First 20 | ForEach-Object { $_.Line }; if ($out | Select-String ' error ') { 'BUILD FAILED'; exit 100 } }
        else { 'build ok' }
    } finally { Pop-Location }
}
$tests = @(Get-ChildItem (Join-Path $repo 'tests\Test-*.ps1'))
if ($Only) {
    # Via -File, "-Only A,B" arrives as one string; accept commas either way.
    $names = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() -replace '^Test-','' -replace '\.ps1$','' } | Where-Object { $_ })
    $tests = @($tests | Where-Object { $names -contains ($_.BaseName -replace '^Test-','') })
    if ($tests.Count -ne $names.Count) { "unknown test name in: $($names -join ', ')"; exit 101 }
}

# Remembered durations (seconds) per test, for the overall estimate.
$timingDir = Join-Path $env:TEMP 'StartPageTests'; $timingFile = Join-Path $timingDir 'timings.json'
$timings = @{}
try { if (Test-Path $timingFile) { (Get-Content $timingFile -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $timings[$_.Name] = [double]$_.Value } } } catch { }
function Format-Span([double]$seconds) { if ($seconds -lt 0) { $seconds = 0 }; $s = [TimeSpan]::FromSeconds([Math]::Round($seconds)); if ($s.TotalHours -ge 1) { $s.ToString('h\:mm\:ss') } else { $s.ToString('m\:ss') } }
function Remaining-Known([int]$from) { $sum = 0.0; $unknown = 0; for ($i = $from; $i -lt $tests.Count; $i++) { $n = $tests[$i].BaseName; if ($timings.ContainsKey($n)) { $sum += $timings[$n] } else { $unknown++ } }; @($sum,$unknown) }

$failed = 0; $index = 0
foreach ($test in $tests) {
    $name = $test.BaseName -replace '^Test-',''
    # Expected progress steps: the test's own PASS lines (at least one).
    $expected = [Math]::Max(1,@(Select-String -Path $test.FullName -Pattern "^\s*['""]PASS:" -AllMatches).Count)
    # Tests load net462 assemblies: they MUST run under Windows PowerShell 5.1, never pwsh 7. No console window.
    $info = [Diagnostics.ProcessStartInfo]::new('powershell.exe',"-NoProfile -ExecutionPolicy Bypass -STA -File `"$($test.FullName)`"")
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true; $info.WorkingDirectory = $repo
    $proc = [Diagnostics.Process]::Start($info)
    $lines = [Collections.Generic.List[string]]::new(); $passes = 0; $lastPass = ''
    $readers = @{ out = $proc.StandardOutput.ReadLineAsync(); err = $proc.StandardError.ReadLineAsync() }
    $started = [DateTime]::UtcNow; $lastOutput = $started; $lastStatus = $started; $stalled = $false
    while ($readers.Count) {
        $progressed = $false
        foreach ($key in @($readers.Keys)) {
            $task = $readers[$key]
            if (!$task.IsCompleted) { continue }
            $line = $task.Result
            if ($null -eq $line) { $readers.Remove($key); continue }
            $lines.Add($line); $lastOutput = [DateTime]::UtcNow; $progressed = $true
            if ($line -match '^\s*PASS:') { $passes++; $lastPass = $line.Trim() }
            $readers[$key] = $(if ($key -eq 'out') { $proc.StandardOutput.ReadLineAsync() } else { $proc.StandardError.ReadLineAsync() })
        }
        $now = [DateTime]::UtcNow; $elapsed = ($now - $started).TotalSeconds
        $done = [Math]::Min($passes,$expected)
        # Prefer the remembered duration (fixture-based tests print the fixture's PASS lines too); otherwise the check rate.
        $left = if ($timings.ContainsKey($test.BaseName)) { [Math]::Max(0,$timings[$test.BaseName] - $elapsed) } elseif ($done -gt 0 -and $done -lt $expected) { $elapsed / $done * ($expected - $done) } else { -1 }
        $rest = Remaining-Known ($index + 1)
        $overall = if ($left -ge 0) { ' (all tests ~' + (Format-Span ($left + $rest[0])) + $(if ($rest[1]) { " + $($rest[1]) untimed" }) + ' left)' } else { '' }
        $status = "[$($index+1)/$($tests.Count) $name] $done/$expected checks, $(Format-Span $elapsed) elapsed" + $(if ($left -ge 0) { ", ~$(Format-Span $left) left$overall" } else { '' })
        Write-Progress -Activity "Test $($index+1) of $($tests.Count): $name" -Status $status -PercentComplete ([int](100 * $done / $expected))
        if ($StatusSeconds -gt 0 -and ($now - $lastStatus).TotalSeconds -ge $StatusSeconds) { $status; $lastStatus = $now }
        if ($StallMinutes -gt 0 -and ($now - $lastOutput).TotalMinutes -ge $StallMinutes) {
            $stalled = $true; & taskkill.exe /PID $proc.Id /T /F 2>&1 | Out-Null; break
        }
        if (!$progressed) { Start-Sleep -Milliseconds 200 }
    }
    $proc.WaitForExit(10000) | Out-Null
    $seconds = ([DateTime]::UtcNow - $started).TotalSeconds
    if ($stalled) {
        $failed++
        "STALLED $($test.Name) after $passes/$expected checks: no output for $StallMinutes min, stopped."
        if ($lastPass) { "   last passed: $lastPass" }
    } elseif ($proc.ExitCode -ne 0) {
        $failed++
        "FAIL $($test.Name) ($(Format-Span $seconds), $passes/$expected checks)"
        $lines | Select-String 'FullyQualifiedErrorId|Exception|throw ' | Select-Object -First 2 | ForEach-Object { '   ' + $_.Line.Trim() }
        $lines | Select-Object -Last $ShowTail | ForEach-Object { '   | ' + "$_".Trim() }
    } else {
        $timings[$test.BaseName] = [Math]::Round($seconds,1)
    }
    $lines | Select-String '^NOTE:' | ForEach-Object { "   $($name): $($_.Line)" }
    $index++
}
Write-Progress -Activity 'StartPage tests' -Completed
try { New-Item -ItemType Directory -Force $timingDir | Out-Null; $timings | ConvertTo-Json | Set-Content $timingFile -Encoding UTF8 } catch { }
"passed $($tests.Count - $failed) / $($tests.Count)"
exit $failed
