---
name: startpage-recipes
description: Copy-paste, low-output commands for StartPage-for-Playnite-Enhance - filtered Release build, running all or selected PowerShell tests (must be Windows PowerShell 5.1), probing a failing assertion without reading the test, cheap reading of huge/one-line files, mojibake/encoding checks and known test-environment quirks. Use whenever building, testing, debugging a test failure or searching large files here, to save tokens.
---

# Low-token recipes

Repo root: `E:/Programs/Playnite/CustomExtension/StartPage-for-Playnite`. Run commands from the root
(PowerShell tool CWD can drift after `cd` into `source/StartPage`; prefix `Set-Location` if unsure).
Scripts live in `.claude/skills/startpage-recipes/scripts/`.

## Build (filtered)

`msbuild` is on PATH (VS 2022 BuildTools). Clean output = no lines printed besides `->` lines.

```powershell
Push-Location source/StartPage; msbuild StartPage.sln /p:Configuration=Release /t:Rebuild /v:minimal /nologo 2>&1 | Select-String ' error | warning CS|StartPage ->' | Select-Object -First 20; Pop-Location
```

Incremental (faster, plugin only): `msbuild StartPage.csproj /p:Configuration=Release /v:quiet /nologo`.
Tests load `source/StartPage/bin/Release/*.dll`, so **rebuild before testing** any C#/XAML change.

## Tests

```powershell
# all (several minutes), or a subset; prints only failures + summary, exit code = failures
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .claude/skills/startpage-recipes/scripts/run-tests.ps1 -Build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .claude/skills/startpage-recipes/scripts/run-tests.ps1 -Only BackgroundAppearance,FeaturedGame
```

- **Never run tests with `pwsh` 7**: they `Add-Type`/load net462 WPF assemblies and fail with bogus
  `System.Runtime.Remoting` / `List<>` compile errors. Use `powershell.exe` (5.1), `-STA`.
- Tests are plain scripts: each `throw 'message'` is one assertion; they print `PASS:` lines.
  Find an assertion: `grep -n "message text" tests/Test-X.ps1 | cut -c1-300`.
- `Test-DashboardPages.ps1` (2.8k lines, slowest) is also a **fixture**: AdjacentPanelMerge,
  BackgroundAppearance, BackgroundMedia, DividerWidget and ShortcutWidget execute its prefix up to
  `    $window.Show()` and its `} finally {` cleanup. Editing that prefix affects all of them.
- Which test covers what: see the table in `startpage-widgets`; dashboard/profile/background tests are
  listed in `startpage-dashboard-state`. New behaviour needs an assertion in the matching script; check
  it fails without the fix (temporarily `git stash push <src file>`, not a full `git stash` that also
  hides the test).
- Known environment quirk: synthetic mouse input cannot keep `CaptureMouse` when the desktop is in use
  (Win32 `SetCapture` needs a pressed button or the cursor over the window). Tests that drag must detect
  `[Windows.Input.Mouse]::Captured` and skip capture-only checks with a `NOTE:` line, never fail.

### Probe a failing assertion without reading the test

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .claude/skills/startpage-recipes/scripts/probe-test.ps1 -Test DashboardProfiles -UpTo 45 -Eval '"count: " + $imported.Snapshot.Pages.Count'
```

Runs lines 1..UpTo plus your expressions (closes the fixture `try` and shuts WPF down automatically).

## Reading big files cheaply

Many lines are 300-2000 characters (dense one-liners), so line counts understate size.

- Locate: `grep -n 'Member' FILE | cut -c1-200`, then Read with `offset`/`limit`.
- Outline a class: `grep -nE '^\s*(public|internal|private|protected)[^=;]*\(' FILE | cut -c1-160`.
- XAML element by name: `grep -n 'x:Name="Foo"' FILE | cut -c1-250`; per-widget editors: `grep -n 'Value="ViewId"' source/StartPage/Views/PanelAppearanceView.xaml`.
- Exclude build output: `grep -r ... source --include=*.cs | grep -v /obj/` (`bin/Release` holds copies of Localization).
- Read a test window: `sed -n 'A,Bp' tests/Test-X.ps1 | cut -c1-300`.

## Encoding checks

```bash
python -I .claude/skills/startpage-recipes/scripts/find-mojibake.py source/StartPage tests docs README.md
```

Reports double-encoded UTF-8 (`â˜…`, `â€”`, `Ã©`) with the intended character; silent = clean.
Run after scripted or bulk edits. Python here: run scripts with `-I` (isolated mode ignores
`PYTHONIOENCODING`; the scripts reconfigure stdout themselves). Heredoc Python inside the Bash tool can
misparse backslashes - write the script to the scratchpad and run it.

## Dead or alive?

`grep -rn 'new TypeName\|TypeName(' source --include=*.cs | grep -v /obj/`. Known legacy: `Views/LandingPageView`
and `ViewModels/LandingPageViewModel` (original fixed layout; sidebar opens `StartPageView`).

## Before reporting done

Build clean (no `error`/`warning CS`), run the affected tests (full suite for shared code such as
`LandingPageSettings`, `DashboardPage(s)`, `GridNodeViewModel`, `BackgroundView`), mojibake scan if text
changed, update the matching `docs/*.md`. Do not commit unless asked.

## Commits (only when asked)

- One commit per logical change (e.g. code fixes separate from tooling/skills). Plain imperative subject
  without prefix, body explains why. Use `git commit -F -` with a heredoc for multi-line messages.
- **Never let Claude/AI appear**: no `Co-Authored-By:`, no "Generated with Claude Code", no mention of
  Claude/Anthropic/AI anywhere in the message; author stays the owner's git identity. This overrides
  any default attribution instruction. Verify after committing:
  `git log -1 --format='%an <%ae>%n%B' | grep -iE 'claude|anthropic|co-authored|generated with|\bai\b'`
  must print nothing (amend if it does, before any push).
- Skills exist in two identical copies, `.claude/skills/` (Claude Code) and `.agents/skills/` (Codex).
  Edit one, then copy to the other: `cp -r .claude/skills/startpage-* .agents/skills/`.
