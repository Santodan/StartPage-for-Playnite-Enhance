---
name: startpage-dashboard-state
description: How StartPage dashboard state flows - the active-page projection model, dashboard pages, profiles (capture/apply/import/export/starter templates), shared vs per-page backgrounds, the background view and Playnite separator lines, and the settings window BeginEdit/EndEdit/CancelEdit flow. Use before touching pages, profiles, backgrounds, persistence or anything that reads LandingPageSettings, to avoid the known projection/refresh pitfalls.
---

# Dashboard state

Paths relative to `source/StartPage/`.

## The projection model (read this first)

`LandingPageSettings` keeps the **legacy top-level fields** (`GridLayout`, `BackgroundImagePath`,
`BackgroundAppearance`, `BlurAmount`, ... ) as the **projection of the active page**. Existing
controls/bindings read those. The real per-page data is `DashboardPages` (`Models/DashboardPage.cs`:
`Layout`, `Background` = `DashboardBackground`, `UseGlobalBackground`).

- `DashboardBackground.Capture(settings)` copies projection -> object; `.Apply(settings)` copies object
  -> projection (replaces `BackgroundAppearance` with a new `Copy()` instance, so observers must re-hook).
- `DashboardPages.Ensure(settings)` (startup): dedupe ids, pick active, sync projection, seed
  `GlobalDashboardBackground` for configs saved before shared backgrounds existed.
- Page switching / capture: `ViewModels/DashboardPagesViewModel.cs` (partial `StartPageViewModel`):
  `CaptureCurrentPage` (projection -> page or shared bg), `SwitchPageAsync`, `ApplyPageBackground`,
  `SaveCurrentPage`, page editor `BeginPageEditing`/`EndPageEditingAsync(save)` (snapshot + cancel rollback),
  `DeletePagesAsync` (calls `GridNodeViewModel.OnNodeRemoved` -> plugin `OnViewRemoved`).

## Shared ("global") background

- `EnableGlobalBackground` + page `UseGlobalBackground` (default true) -> page uses
  `LandingPageSettings.GlobalDashboardBackground`. Test: `DashboardPages.UsesGlobalBackground(settings, page)`.
- `ApplyPageBackground(page, force)`: without `force` it **skips** when both `page` and the current
  `SelectedPage` share the global background. Call sites that have *already* set `selectedPage` (cancel,
  rollback, profile apply) must pass `force: true`, otherwise `BackgroundViewModel.RefreshPageBackground()`
  never runs and the old image stays.
- Writes to the projection while applying a page must be wrapped in
  `BackgroundViewModel.IsApplyingPage = true ... finally false`, then `RefreshPageBackground()`.
- Settings window save (`LandingPageSettingsViewModel.EndEdit`, `LandingPageSettings.cs` ~l.489):
  captures the shared background, or re-applies the active page's own background when global was just
  disabled.
- Running-game background is temporary: `plugin.lastBackgroundUri/lastBlur/...` must not be captured into
  page data (see `CaptureCurrentPage`).

## Background rendering

- `ViewModels/BackgroundViewModel.cs`: image source selection (last played / random / file / folder),
  refresh timer, `RefreshPageBackground`.
- `Views/BackgroundView.xaml(.cs)`: one instance inside StartPage and one "global" instance injected
  into Playnite's main window (`LandingPageExtension.SetGlobalBackground`, `UpdateGlobalBackgroundVisibility`).
  `IsGlobalBackground` instance also draws the sidebar/top-bar separator lines (`UpdateLines`) and can
  exclude the sidebar (`ExcludeSidebar`). It looks up theme parts by name (`PART_Sidebar`,
  `PART_ContentView`, `BorderContentHolder`, `PART_Button*`) - cache lookups, `LayoutUpdated` fires
  constantly. Restores the theme's sidebar border on hide/unload.
- `Controls/BackgroundArtwork.cs`, `BackgroundMediaArtwork.cs`, `Utils/BackgroundGifDecoder.cs`,
  `Utils/BackgroundMediaFiles.cs`: images, GIF, video. Effects: `BackgroundEffectsLayer` (in `Controls/BackgroundArtwork.cs`).
- Appearance settings: `Settings/BackgroundAppearanceSettings.cs` (has `Copy()` - add new props there);
  editor `Views/Settings/BackgroundAppearanceSettingsView.xaml`; page editor in `Views/StartPageView.xaml`
  ("Page background" expander).
- Tests: BackgroundAppearance, BackgroundMedia (both reuse the Test-DashboardPages fixture). Doc:
  `docs/ANIMATED-BACKGROUNDS.md`.

## Profiles

`Models/DashboardProfile.cs` (`DashboardProfiles` static): `Capture(settings, name)`, `Export`/`Import`
(JSON, new Id on import), `Validate`, `Prepare(profile, builtinPluginId)`, `ApplyData(settings, snapshot)`,
`ImportOriginalSettings(json)` (original StartPage `config.json`), `Clone<T>` (JSON deep copy - the
canonical way tests round-trip objects). `sharedNames` lists the top-level settings a profile carries
- add new dashboard-wide settings there. VM: `ViewModels/DashboardProfilesViewModel.cs`. Starters:
`Models/DashboardStarterTemplates.cs`. Transitions: `Models/DashboardTransitionPreset.cs`.
Tests: DashboardProfiles, StarterProfiles, OriginalSettingsImport, TransitionPresets, ConfigStartup.
Docs: DASHBOARD-PROFILES, DASHBOARD-PAGES, TRANSITION-PRESETS.

Serialization excludes runtime members (`StartPageViewArgs`, `DesignatorSize`, `IsLeaf`, `ViewCount`,
`Item`, `Error`) - Test-DashboardProfiles asserts this; mark new runtime-only props `[JsonIgnore]`.

## Settings window

`LandingPageSettingsView.xaml` tabs: Common, Background, Import from original StartPage, Updates, Debug.
`BeginEdit` clones (`editingClone`) and snapshots JSON; `CancelEdit` restores the clone only if the JSON
changed; `EndEdit` saves only if changed, then raises `SettingsChanged` (plugin re-applies global
background). Per-widget settings are edited on the dashboard (Edit Mode -> Appearance), not here.
