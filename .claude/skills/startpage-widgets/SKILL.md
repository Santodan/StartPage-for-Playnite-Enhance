---
name: startpage-widgets
description: File map of every StartPage dashboard widget (ViewId -> view, settings class, Appearance editor, test script, doc) plus checklists for adding a widget or a widget setting. Use before changing, debugging or adding any widget, shelf, toolbar, chart, achievement, featured/game-detail, divider, shortcut, image, text or clock feature, to open the right files directly instead of searching.
---

# Widget map

Paths are relative to `source/StartPage/`. Tests are `tests/Test-<Name>.ps1`, docs are `docs/`.
All built-in widgets share PluginId `a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5`.

## How a widget is wired (4 places)

1. **Registry** `LandingPageExtension.GetAvailableViews` (~l.750): `StartPageViewArgsBase { ViewId, Name, Description, AllowMultipleInstances }`.
2. **Factory** `LandingPageExtension.GetStartPageView(id, instanceId)` (~l.840) creates the control;
   `OnViewRemoved(viewId, instanceId)` (~l.925) disposes it and removes per-instance state.
3. **Per-instance settings** live on `Models/Layout/ViewProperties.cs` (`TextSettings`, `ClockSettings`,
   `DividerSettings`, `ShortcutSettings`, `ImageSettings`, `ToolbarSettings`, `AchievementSettings`,
   `LibraryStatisticsSettings`, `FeaturedGameSettings`, `GameDetailSettings`), so they persist inside the
   page layout `GridNode` and copy with profiles automatically. Exception: shelves (below).
4. **Appearance editor**: `Views/PanelAppearanceView.xaml` shows one editor per ViewId via
   `DataTrigger Binding="{Binding GridNode.ViewProperties.ViewId}" Value="..."`. Common panel styling
   (background/border/title) is in the same view; `Views/Settings/SectionAppearanceSettingsView` is reused
   by featured/detail sections. Legacy gear-settings come from `GetStartPageViewSettings` (only
   MostPlayed, GameShelves, RecentAchivements).

## Widgets

| ViewId | View | Settings / model | Editor (`Views/Settings/`) | Tests | Doc |
| --- | --- | --- | --- | --- | --- |
| `GameShelves` | `Views/ShelvesView`, `ShelveView`, `ShelfGameInfo.cs`, `Controls/ShelfHover.cs` | `Settings/ShelvesSettings.cs`, `Models/ShelveProperties.cs`, `Models/AdvancedShelfRules.cs`; VMs `ShelvesViewModel`, `ShelveViewModel` | `ShelvesSettingsView`, `Views/AdvancedShelfRulesView` | AdvancedShelfRules, ShelfPatterns, ShelfHover | SHELF-PATTERNS, SHELF-HOVER-EFFECTS |
| `ToolbarWidget` | `Views/ToolbarWidgetView.cs`, `Controls/NativeToolbarCatalog.cs` | `Settings/ToolbarWidgetSettings.cs` (`Defaults()` = 7 items) | `ToolbarSettingsView` | DashboardPages | DASHBOARD-PAGES |
| `TextWidget` | `Views/TextWidgetView` | `Settings/TextWidgetSettings.cs` | `TextWidgetSettingsView` | SettingsSerialization, DashboardProfiles | - |
| `ImageWidget` | `Views/ImageWidgetView` | `Settings/ImageWidgetSettings.cs` | `ImageWidgetSettingsView` | DashboardProfiles, DashboardPages | - |
| `DigitalClock` | `Views/ClockView`, `Models/Clock.cs` | `Settings/ClockSettings.cs` | `ClockSettingsView` | DashboardProfiles | - |
| `DividerWidget` | `Views/DividerWidgetView.cs`, `Controls/DividerRenderer.cs`, `DividerConnectionLayer.cs` | `Settings/DividerWidgetSettings.cs` | `DividerWidgetSettingsView` | DividerWidget, AdjacentPanelMerge | - |
| `ShortcutWidget` | `Views/ShortcutWidgetView.cs`, `Utils/ShortcutLauncher.cs`, `ShortcutIcons.cs`, `WebsiteIconCache.cs` | `Settings/ShortcutWidgetSettings.cs` | `ShortcutWidgetSettingsView` | ShortcutWidget, WebsiteIcons | - |
| `AchievementProgress`, `AchievementFinishNext`, `AchievementGoals`, `GameAchievementList`, `RecentAchivements` (sic - keep the typo, it is a persisted id) | `Views/AchievementWidgetView.cs` (Recent wraps `RecentAchievementsView`) | `Settings/AchievementWidgetSettings.cs` (`IsWidget`, `Create(id)`); data `PlayniteAchievementsProvider.cs`, `ViewModels/SuccessStory/*`, `ViewModels/PlayniteAchievements/*` | `AchievementWidgetSettingsView.cs`, `AchievementsSettingsView` | AchievementWidgets | ACHIEVEMENT-DASHBOARD |
| `LibraryStatistics`, `LibraryChart` | `Views/LibraryStatisticsView.cs` (`chartOnly` ctor flag), `Controls/LibraryChartPlot.cs` | `Settings/LibraryStatisticsSettings.cs`, `Models/LibraryStatistics.cs`, `Models/PeriodPlaytime.cs` | `LibraryStatisticsSettingsView` | LibraryStatistics, LibraryChartPlot, PeriodPlaytime, DashboardPages | LIBRARY-STATISTICS |
| `FeaturedGame` | `Views/FeaturedGameView.cs`, `Controls/FeaturedSectionFrame.cs` | `Settings/FeaturedGameSettings.cs`, `Models/FeaturedGameSelection.cs`, `Models/FeaturedDescription.cs` | `FeaturedGameSettingsView` | FeaturedGame, LibraryDescription | FEATURED-GAME |
| `GameDetail{Artwork,Title,Description,Activity,Details,Library,Scores,Links,Actions}` | `FeaturedGameView(detail)` | `Models/GameDetailWidgets.cs` (`Definitions`, `Find`, `CreateSettings`) -> `FeaturedGameSettings` | `GameDetailSettingsView` | GameDetailWidgets | GAME-DETAIL-WIDGETS |
| `MostPlayed` | `Views/MostPlayedView` | `ViewModels/GameActivity/MostPlayedViewModel.cs`, `Models/GameActivity/MostPlayedOptions.cs` (global, on `LandingPageSettings`) | `MostPlayedSettingsView` | - | - |
| `WeeklyActivity` | `Views/GameActivityView` | `ViewModels/GameActivity/GameActivityViewModel.cs` | - | - | - |

`MostPlayed` / `WeeklyActivity` return `null` unless the GameActivity extension data folder
(`ExtensionsDataPath/afbb1a0d-04a1-4d0c-9afa-c6e42ca855b4/GameActivity`) exists.

Shared infrastructure: `Controls/WidgetPalette.cs` (Add-widget catalog), `Controls/PopupColorPicker.cs` /
`ColorPicker.xaml` (color fields), `Controls/TextSettingsSelector.cs`, `Controls/SectionAppearanceBorder.cs`,
`Controls/FloatingPanel.cs` (Test-FloatingPanels), `Controls/ContentAutoScroll.cs` (Test-ContentAutoScroll).

Editor layout kit `Controls/SettingsLayout.cs` (Test-SettingsLayout): `SettingsField` (label above a control),
`SettingsHint`, `SettingsSlider` (label + full-width slider + numeric box), `TextFormatBar` (size + B/I + popup
color swatch), `SettingsColumns` (2 columns, stacks below 150 px each; `SettingsColumns.Span`). Use it for new or
reworked editors. Converted: Text, Clock, Image, Divider, Shortcut, Toolbar, Library statistics/charts (targeted); the rest are listed in `CUSTOMIZATION-ROADMAP.md`
("Consistent, compact widget settings"). Add each converted editor to the fit/unchanged-on-open loop in the test. Inner kit bindings go through
`SettingsLayout.Bind` (proxy + `SetCurrentValue`); never bind inner controls TwoWay straight to the owner's DP, or
template-applied bindings (DataTemplate rows) get replaced on the first write-back.

## Shelves are special

Shelf settings are **not** on `ViewProperties`: they live in
`LandingPageSettings.ShelveInstanceSettings[instanceId]` (dictionary keyed by the node InstanceId),
created on first view in `ShelvesViewModel` ctor, saved from `shelvesViewModels` in
`LandingPageExtension` (~l.704), captured by `DashboardProfile` as `Snapshot.Shelves`, and removed in
`OnViewRemoved`. Any new shelf-level feature must survive profile capture/apply and page deletion.

## Checklist: add a setting to an existing widget

1. Property with default in the widget's `Settings/*.cs` (pattern: `private T x = default; public T X { get => x; set => SetValue(ref x, Clamp(value)); }`).
   If the class has a `Copy()` method, add the property there too (e.g. `BackgroundAppearanceSettings.Copy`, easy to forget).
2. Control in the editor (`Views/Settings/<Widget>SettingsView.xaml`), bound to the property.
3. Render it in the view; react to `PropertyChanged` if the view caches visuals.
4. Persisted-compat: old JSON without the property must load with the old behaviour.
5. Test: extend the widget's test (round-trip via `[LandingPage.Models.DashboardProfiles]::Clone(...)`), run it.
6. Update the widget doc.

## Checklist: add a new widget

Registry entry + factory branch + `OnViewRemoved` cleanup (if it holds timers/subscriptions) +
`ViewProperties` settings property (lazy `?? (x = new ...)` getter so old layouts load) +
`PanelAppearanceView.xaml` editor trigger + `StartPage.csproj` `<Compile>`/`<Page>` items +
a `tests/Test-<Widget>.ps1` + README feature table / doc + `CUSTOMIZATION-ROADMAP.md`.
