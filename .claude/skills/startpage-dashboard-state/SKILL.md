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
`Models/DashboardStarterTemplates.cs`. Transitions: `Settings/PageTransitionSettings.cs` bundles effect/duration/direction/custom preset/media; the global one is `LandingPageSettings.GlobalTransition` (DontSerialize; stored in the original Dashboard* properties, which forward to it), a page's own is `DashboardPage.Transition` + `UseOwnTransition` (null until first used; saved with the page, so profiles carry it); `DashboardPages.EffectiveTransition(settings, destination)` picks one at switch time. Shared editor `Views/Settings/TransitionSettingsView` (extension settings Transitions tab, page manager via `Views/StartPageView.PageTransition.cs` with copy from/to global and use on all dashboards; saved presets shared). Presets: `Models/DashboardTransitionPreset.cs`; effects and the GIF/video run in `Controls/TransitionPlayer.cs` (outgoing/incoming elements + `Controls/TransitionMediaLayer.cs`; placements revealed by the effect, instead of it, over it), used by `StartPageView` (snapshot -> page, `Player`) and by `Controls/TransitionPreviewBox.cs` (two sample pages; the editor's `PreviewInBox` mode in the extension settings). Media options: `Settings/TransitionMediaSettings.cs` (`Media.Fit` is the Size option, default Fit). `AfterEnd`/`AfterEndRepeatMs` (also on `StartupIntroSettings`) map to `BackgroundMediaArtwork.EndBehavior`/`EndRepeatMs`: hold the last frame or repeat the first/last part after a non-looping clip ends (MediaFinished still fires once; `RepeatCount` counts repeats; previews use `TransitionMediaLayer.PreviewRepeats` = 3 and `StartupIntroSettingsView.PlanPreview` to show the whole clip plus three repeats; test MediaEndRepeat). Never await an already-finished media task in a wait loop (it spins the UI thread). The launch intro window (`Utils/LaunchIntro.cs`) is not topmost unless `LaunchAboveOtherApps`: a 30 ms timer on its thread moves Playnite-thread windows below it (`KeepAbovePlaynite`, async SetWindowPos) and Playnite is only activated at the end if the intro still had the focus (test LaunchIntroOrder). Tests: DashboardPages, SettingsLayout, SettingsWindow, TransitionMediaFit. Startup animation (global, not in profiles): `Settings/StartupAnimationSettings.cs`
(`LandingPageSettings.StartupAnimation`), engine `Controls/StartupAnimator.cs` (only animates opacity, restores raw local
transform/origin/clip), hookup `Views/StartPageView.Startup.cs` (waits for `IsSelectedPageInitialized`), editor in the page
manager; test StartupAnimation, doc STARTUP-ANIMATIONS. Startup intros: `Settings/StartupIntroSettings.cs` (global
`LandingPageSettings.StartupIntro`, per page `DashboardPage.Intro` - travels with profiles), player `Controls/IntroPresenter.cs`,
adorner host + session bookkeeping `Utils/IntroHost.cs` (coverage = window root or StartPage view), early launch intro
`Utils/LaunchIntro.cs` (plugin ctor, own STA thread/window, held until `PlayniteReady`), launch hook
`LandingPageExtension.PlayStartupIntroAtLaunch`, open/page-switch hooks `Views/StartPageView.Intro.cs`, editor
`Views/Settings/StartupIntroSettingsView` (settings tab + page manager); test StartupIntro.
Tests: DashboardProfiles, StarterProfiles, OriginalSettingsImport, TransitionPresets, ConfigStartup.
Docs: DASHBOARD-PROFILES, DASHBOARD-PAGES, TRANSITION-PRESETS.

Serialization excludes runtime members (`StartPageViewArgs`, `DesignatorSize`, `IsLeaf`, `ViewCount`,
`Item`, `Error`) - Test-DashboardProfiles asserts this; mark new runtime-only props `[JsonIgnore]`.

## Settings window

`LandingPageSettingsView.xaml` tabs (step 24): General, Covers and shelves (global cover options + defaults copied into
new shelves), Background, Transitions (global transition, step 25.9), Startup intro, Updates and tools (updates, original-StartPage import, debug). Legacy options
that no longer affect the widget dashboard (ShowClock, ShowMostPlayedGames, EnableGameActivity, ShowRecentAchievements,
KeepInMemory, global NumberOfGames) stay in `LandingPageSettings` for old configs but have no control. Test: SettingsWindow.
`BeginEdit` clones (`editingClone`) and snapshots JSON; `CancelEdit` restores the clone only if the JSON
changed; `EndEdit` saves only if changed, then raises `SettingsChanged` (plugin re-applies global
background). Per-widget settings are edited on the dashboard (Edit Mode -> Appearance), not here.
