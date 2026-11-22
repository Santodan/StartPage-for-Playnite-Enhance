# Low-output build + test runner for StartPage-for-Playnite-Enhance.
# Usage (from any shell):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .claude/skills/startpage-recipes/scripts/run-tests.ps1 [-Build] [-Only Name1,Name2] [-Verbose]
#   -Build    rebuild the solution in Release first (errors/warnings only)
#   -Only     test names without "Test-" / ".ps1", e.g. -Only BackgroundAppearance,DashboardPages
#   -ShowTail lines of output to print for each failing script (default 6)
# Prints one line per failure plus a summary; exit code = number of failing scripts.
param([switch]$Build, [string[]]$Only, [int]$ShowTail = 6)
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
$tests = Get-ChildItem (Join-Path $repo 'tests\Test-*.ps1')
if ($Only) {
    # Via -File, "-Only A,B" arrives as one string; accept commas either way.
    $names = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() -replace '^Test-','' -replace '\.ps1$','' } | Where-Object { $_ })
    $tests = @($tests | Where-Object { $names -contains ($_.BaseName -replace '^Test-','') })
    if ($tests.Count -ne $names.Count) { "unknown test name in: $($names -join ', ')"; exit 101 }
}
$failed = 0
foreach ($test in $tests) {
    # Tests load net462 assemblies: they MUST run under Windows PowerShell 5.1, never pwsh 7.
    $out = & powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File $test.FullName 2>&1
    if ($LASTEXITCODE -ne 0) {
        $failed++
        "FAIL $($test.Name)"
        $out | Select-String 'FullyQualifiedErrorId|Exception|throw ' | Select-Object -First 2 | ForEach-Object { '   ' + $_.Line.Trim() }
        $out | Select-Object -Last $ShowTail | ForEach-Object { '   | ' + "$_".Trim() }
    }
    $out | Select-String '^NOTE:' | ForEach-Object { "   $($test.BaseName): $($_.Line)" }
}
"passed $($tests.Count - $failed) / $($tests.Count)"
exit $failed
