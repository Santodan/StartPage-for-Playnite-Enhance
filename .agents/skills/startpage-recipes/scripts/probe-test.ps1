# Run the first N lines of a test script, then print extra expressions - to inspect values at a
# failing assertion without reading the (very long) test.
# Usage:
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .claude/skills/startpage-recipes/scripts/probe-test.ps1 `
#     -Test DashboardProfiles -UpTo 45 -Eval '"items: " + $imported.Snapshot.Pages[0].Layout.Children.Count'
# -UpTo is the last line to keep (use the line BEFORE the failing `throw`, from `grep -n`).
# The probe is written next to the tests (they resolve the build via $PSScriptRoot) and deleted afterwards.
param([Parameter(Mandatory)][string]$Test, [Parameter(Mandatory)][int]$UpTo, [string[]]$Eval = @(), [int]$Tail = 15)
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$source = Join-Path $repo "tests\Test-$Test.ps1"
$lines = @(Get-Content $source)[0..($UpTo - 1)]
$body = $lines + $Eval
# WPF fixtures (Test-DashboardPages and the tests that reuse its prefix) wrap everything in a top-level
# `try {` that must be closed, and must shut the Application down or the process hangs.
if ($lines | Where-Object { $_ -match '^try\s*\{' }) { $body += '} finally { if ($app) { $app.Shutdown() } }' }
$probe = Join-Path $repo 'tests\_probe.ps1'
Set-Content -Path $probe -Value $body -Encoding UTF8
try { & powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File $probe 2>&1 | Select-Object -Last $Tail }
finally { Remove-Item $probe -ErrorAction SilentlyContinue }
