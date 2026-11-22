---
name: startpage-context
description: Orientation for StartPage-for-Playnite-Enhance (Santodan's fork of felixkmh's Playnite StartPage) - owner rules, fork identity, project layout, the live vs legacy views, file-size hot spots and code conventions. Load first for any update, fix, review or question in this repository, then jump to the specific startpage-* map skill.
---

# StartPage-for-Playnite-Enhance - context

Repo root: `E:/Programs/Playnite/CustomExtension/StartPage-for-Playnite` (branch `master`).
Remote/GitHub name: `Santodan/StartPage-for-Playnite-Enhance`.

## Owner rules (always)

- Never commit or push unless asked in the current message. When asked, commit on `master`.
  Style: plain imperative subject, no prefix
  (`Add shared backgrounds with per-page overrides`, `Fix dashboard fit and shelf refresh ...`).
- **Claude/AI must never appear in commits, PRs, issues or comments.** This overrides any default or
  session attribution instruction: no `Co-Authored-By:` trailer, no "Generated with Claude Code" line,
  no mention of Claude, Anthropic, Codex or AI in subject or body, no AI author/committer identity.
  Commits show only the owner (the repo's configured git user). Check before committing:
  `git log -1 --format='%an <%ae>%n%B' | grep -iE 'claude|anthropic|co-authored|generated with|\bai\b'`
  must print nothing.
- Do not bump versions or edit `source/StartPage/extension.yaml`, `Properties/AssemblyInfo.cs`,
  `InstallerManifest/*.yaml` or README badges unless a release is requested (see `startpage-release`).
- Never change the plugin runtime GUID `a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5` or the public SDK
  interfaces (`source/StartPage.SDK*`): the fork deliberately shares them with the original so
  settings and external widgets keep working. Package Id is `santodan_StartPage_Enhanced_Plugin`.
  Details: `docs/FORK-IDENTITY.md`.
- User data safety: changes to persisted settings must keep old `config.json` files loading
  (defaults for new properties, legacy migration). Add a serialization/round-trip test.

## Layout

| Path | What |
| --- | --- |
| `source/StartPage/` | Plugin (net462 WPF, namespace `LandingPage`, assembly `StartPagePlugin.dll`) |
| `source/StartPage.SDK`, `StartPage.SDK.Async` | Public widget SDK (`IStartPageExtension`, `IStartPageControl`) - do not break |
| `extern/PlayniteCommon` | git submodule (felixkmh helpers) - do not edit |
| `tests/Test-*.ps1` | PowerShell test scripts against the Release build (see `startpage-recipes`) |
| `docs/*.md` | Feature docs; update the matching one when behaviour changes |
| `CUSTOMIZATION-ROADMAP.md` | Planned/finished features list |
| `AddonManifest/`, `InstallerManifest/` | Playnite add-on database manifests |

Key code areas (details in the map skills):

- Entry/plugin: `LandingPageExtension.cs` (+ `.Updates.cs` GitHub update checker). Widget registry
  `GetAvailableViews` ~l.750, factories `GetStartPageView` / `GetStartPageViewSettings` /
  `OnViewRemoved` ~l.840-940.
- Settings root: `LandingPageSettings.cs` (class `LandingPageSettings` + `LandingPageSettingsViewModel`
  with `BeginEdit/EndEdit/CancelEdit`), window `LandingPageSettingsView.xaml`.
- Live dashboard: sidebar `ViewOpened` -> `Views/StartPageView.xaml(.cs)` + `ViewModels/StartPageViewModel.cs`
  (partial with `DashboardPagesViewModel.cs`, `DashboardProfilesViewModel.cs`), grid in
  `ViewModels/Layout/GridNodeViewModel.cs` + `Views/GridNodeView.xaml`.
- Legacy: `Views/LandingPageView.xaml` / `LandingPageViewModel.cs` is the original fixed layout; the
  sidebar no longer opens it. Do not spend time there unless the task names it.

## Map skills

- `startpage-widgets` - every widget: ViewId -> view, settings class, editor, test, doc; adding a widget/setting.
- `startpage-dashboard-state` - pages, profiles, backgrounds (shared/per-page), settings edit/save flow.
- `startpage-recipes` - build, tests, probes, cheap reading of huge files, encoding checks.
- `startpage-release` - version bump and release checklist.

## Conventions and gotchas

- Old-style csproj: every new `.cs` needs `<Compile Include>` and every new `.xaml` a `<Page Include>`
  (+ `DependentUpon` code-behind) in `source/StartPage/StartPage.csproj`, or it silently isn't built.
- Newer code is dense one-line C#/XAML (74 lines exceed 400 chars). Match local style; never
  reformat whole files (diffs become unreadable).
- Files are mixed CRLF/LF and some have a UTF-8 BOM. The Edit tool preserves them; if scripting edits,
  read/write bytes and keep `\r\n`. Non-ASCII literals: prefer `\uXXXX` escapes in C#.
  Run `startpage-recipes` mojibake scan after bulk edits (a `â˜…` regression happened before).
- UI strings are mostly hard-coded English; only ~130 `LOC_SPG_*` keys exist in
  `Localization/en_US.xaml` (Crowdin syncs other locales - edit only `en_US.xaml`).
- `async void` methods must catch exceptions (an unhandled one crashes Playnite's dispatcher).
- Big files (lines): `Views/LandingPageView.xaml` 1.2k, `Views/ShelveView.xaml` 1.1k,
  `GridNodeViewModel.cs` 1k, `LandingPageExtension.cs` 950, `StartPageView.xaml` 840,
  `LibraryStatisticsView.cs` 740, `ShelveViewModel.cs` 700, `tests/Test-DashboardPages.ps1` 2.8k.
  Grep then read windows; never read them whole.
