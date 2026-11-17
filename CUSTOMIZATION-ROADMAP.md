# StartPage Enhanced customization roadmap

Development roadmap toward the first stable release. Checked items are implemented and remain open to bug reports and feedback; unchecked items are planned, without promised release dates. See the [README](README.md) for installation, compatibility and reporting guidance.

Suggestions and bug reports are welcome through [GitHub Issues](https://github.com/Santodan/StartPage-for-Playnite-Enhance/issues).

1. [x] **Per-widget appearance.** Optional background color/opacity, border color/width, corner radius, internal padding, outer spacing, shadow, custom title, title visibility/color/size/alignment, and reset to theme defaults. Includes optional widget text, small bitmap icon, chart, and selection/highlight colors, including FusionZ row/header highlights used by DuplicateHider. Settings follow the widget and persist with its layout. Existing layouts retain their appearance until customization is enabled.
2. [x] **Customizable clock.** Size, color, alignment, date visibility/format, 12/24-hour time, optional seconds, and optional clock animation.
3. [x] **Text widget.** Custom headings and notes with font size, color, alignment, and wrapping.
4. [x] **Image widget.** Local images, banners, and logos with fit/fill choices and optional links.
5. [x] **Shelf display styles.** Horizontal cover rows, wrapping grids, compact lists, and wide artwork cards; configure styles per shelf.
6. [x] **Shelf customization.** Per-shelf hover animations: dock-style neighbor magnification, zoom, lift, tilt, bounce, optional glow and dimming, with strength, reach, timing, easing, artwork overflow and reset controls. Implemented.
7. [x] **Shelf metadata and badges.** Selectable titles, playtime, last played, platform, favorites, installation status, and achievement progress where available.
8. [x] **Advanced shelf rules.** Never played, inactivity and date-added ranges, include/exclude rules, and match-all/match-any selection.
9. [x] **Multiple dashboard pages and toolbar widget.** Named pages with independent layouts/backgrounds. A placeable toolbar supports configurable native Playnite toolbar items, a page selector, previous/next arrows, and page management.
10. [x] **Complete dashboard profiles.** Save/import/export layout, appearance, widget settings, backgrounds, and shelf configuration together. Ensure saved presets are independent snapshots and handle missing extensions gracefully.
11. [x] **Starter profiles.** Refreshed Minimal, Achievements, and Library layouts, plus Game spotlight, Game inspector, and Library insights with previews.
12. [x] **Dashboard transitions.** Optional None/Fade/Slide transitions with duration and direction controls. Prepare the destination before revealing it and coordinate the page/background change.
13. [x] **Featured game widget.** Pinned or automatically selected game, large artwork, description, play button, playtime, and optional achievement progress.
14. [x] **Selected/hovered game detail widgets.** Separate placeable widgets for a game's title/artwork, description, activity, game details, library information, scores, links, and actions. Each widget can follow Playnite's selected game or the game under the mouse, with a shared selected-or-hovered mode for synchronized layouts. Reuse Featured Game section fields/appearance controls, allow independent placement and resizing through dashboard panels, and persist choices in profiles. See scope below.
15. [x] **Library statistics widget.** Choose counters for total, installed, unplayed, completed games, and total playtime. Implemented.
16. [x] **Library charts widget.** Independent multiple-instance graphs for game counts and recorded playtime, bar/doughnut/pie styles, configurable breakdowns, label placement, legends and colorful palettes. Implemented.
17. [x] **Customizable achievement widgets and dashboard.** Independent StartPage progress cards, finish-next games and pinned game percentage goals, with card/ring/bar layouts, filters, typography, colors and borders. Read existing provider data and keep the optional PlayniteAchievements dashboard integration. Implemented; awaiting user testing.
18. [x] **Shortcut widget.** Independent game, application, folder and website buttons with configurable labels/icons; add, duplicate, reorder and remove entries; wrap/horizontal/vertical layouts; compact typography, sizing and color controls; native game/application/Windows folder icons and automatic website favicons with optional overrides. Applications support separate arguments and working directories; missing targets are disabled, and Edit Mode prevents activation. Saved with dashboard profiles. Implemented; awaiting user testing.
19. [x] **Divider/spacer widget.** Independent horizontal/vertical separator lines and blank spacers; solid/dashed/dotted styles, thickness, percentage length, end insets, position/alignment, opacity and themed/custom colors. Preferred spacing for auto-sized panels, bounded drawing in small panels, settings reset and profile persistence. Implemented; awaiting user testing.
21. [ ] **Background options** Add more options to control the background image.
22. [ ] **Consistent, compact widget settings (ongoing until the first stable release).** Review every built-in widget and its appearance/settings editor, then apply the same formatting throughout: labeled color swatches opening themed popup pickers with custom-color toggles; text-element dropdowns showing only the selected text editor; compact size and formatting controls; two-column layouts for related fields and full-width sliders where appropriate. Standardize section grouping, labels, spacing and theme colors while preserving each widget's existing options, saved settings and default appearance. Verify the editors at different window sizes and check settings persistence. Keep this step open throughout development and review new or changed editors as features are added. External providers retain their own settings UI.
23. [ ] **Startup animations.** Optional, customizable animations when StartPage first opens, with effect, duration and timing controls; coordinate with dashboard loading and respect reduced-motion preferences.
24. [ ] **Animated GIF and video support.** Support animated GIFs and videos in media widgets and dashboard backgrounds, with playback, looping, fit/fill and video mute/volume controls. Pause playback when hidden and release media resources when no longer needed.
25. [ ] **Extension settings menu review.** Review the extension-wide settings menu, reorganize related options into clear sections, improve labels and help text, and remove redundant controls while preserving saved preferences and access to widget-specific settings.

## First change: testing in Playnite

- Install the latest locally built `.pext` package from `source/StartPage/bin/` and restart Playnite when prompted.
- Enter StartPage Edit Mode and click **Appearance** in the upper-left corner of a populated panel (or right-click it and choose **Panel appearance...**). An overlay inside StartPage shows all controls, without a separate operating-system window. Close it with Done, Escape, or a click outside the editor.
- Enable **Use custom appearance**. Expand each color picker to choose palette swatches, adjust RGB/alpha channels, or enter a color name/hex value. Try opacity, borders, corners, padding, spacing, and shadow.
- Enable **Show title** and try a custom title, size, color, and alignment. A blank title uses the widget name.
- Leave Edit Mode and restart Playnite to check persistence. Move/split the panel and check that appearance follows the widget.
- Background opacity supports fractional steps of 0.01 and direct numeric entry. Done appears immediately above Reset.
- Enable the widget text/icon/chart/highlight checkboxes and test each picker. Disable customization to restore original widget colors. External widgets that draw their own content may need specific support.
- Invalid colors should show validation feedback without replacing the last valid value.
- **Reset appearance to theme defaults** disables customization and clears its appearance settings; title visibility remains controlled by **Show title**.

Build verification does not replace visual testing in Playnite. Rounded corners affect the panel background/border; widget content is not cropped to rounded corners.

## Second change: testing in Playnite

- Install the rebuilt package and restart Playnite. Open the clock panel's **Appearance** overlay; **Clock settings** appears at the top.
- Try time/date sizes, horizontal/vertical alignment, and 12-hour, 24-hour, and system formats.
- Toggle seconds and date visibility. Choose long, short, ISO, or a custom .NET date format such as `dddd, d MMMM yyyy`.
- Enable **Use separate time and date colors** and select each color with its picker. These take priority over the general widget text color and do not require **Use custom appearance**.
- Toggle **Animate at 13:37** to control the clock's existing animation.
- Restart Playnite and check persistence. **Reset clock settings** restores clock defaults without changing panel appearance.
- Build and runtime checks passed; user testing completed and the user authorized the commit.

## Third change: testing in Playnite

- Enter Edit Mode, choose an empty panel, and select **Add view > StartPage > Text**. Multiple Text widgets are supported.
- Open **Appearance > Text settings** to edit multiline text, font size, bold/italic, wrapping, horizontal/vertical alignment, and an optional custom color.
- Text uses the theme color by default. A custom text color takes priority over the general widget text color.
- Use **Appearance settings** for backgrounds, borders, and spacing. Resetting text settings leaves panel appearance unchanged.
- Add two text widgets and verify that editing one does not change the other. Restart Playnite to check persistence.
- Release build and runtime checks passed; user testing completed and the user authorized the commit.

## Fourth change: testing in Playnite

- Enter Edit Mode and choose **Add view > StartPage > Image**. Multiple independent Image widgets are supported.
- Open **Appearance > Image settings** and browse for a local PNG, JPEG, BMP, GIF, TIFF, or ICO. GIFs display a still frame.
- Try **Fit**, **Fill**, **Stretch**, and **Original size**. Fill/original-size content is clipped to the widget panel. Image opacity supports fractional steps and direct numeric entry.
- Optionally enter an `https://` or `http://` website link. Outside Edit Mode, clicking the image opens it in the default browser.
- General icon-color settings leave image-widget artwork unchanged. Use Appearance settings for panel backgrounds, borders, and spacing.
- Test two independent image widgets and restart Playnite to verify persistence. Missing/corrupt files show a message; **Clear image** and **Reset image settings** are available.
- Local files are referenced in place, not copied into settings or presets. Keep them available at their saved paths.
- Release build and runtime checks passed; user testing completed and the user authorized the commit.

## Fifth change: testing in Playnite

- Open a Game Shelves widget. Open an individual shelf's existing settings popup (the button beside its shelf label).
- Choose **Display style**: Horizontal cover row, Wrapping cover grid, Compact list, or Wide artwork cards.
- The default cover row retains the existing appearance. Grid/list/wide styles have a per-shelf **Card width** setting; wide cards use landscape artwork and fall back to covers when no background is set.
- Verify that each shelf can use its own style. Try resizing the panel, scrolling, grouping, filters, sorting, game details, selecting a game, and double-clicking to play.
- Grid/list styles scroll vertically; cover rows and wide cards scroll horizontally. Settings persist when Playnite restarts.
- Missing artwork uses the standard placeholder. Switching layouts does not refresh game data unnecessarily.
- Release build and rendered-view checks passed, including grouped grids; user testing completed and the user authorized the commit.

## Sixth change: testing in Playnite

- Open an individual shelf's settings popup and expand **Metadata and badges**.
- Choose **Info placement > Over artwork (card overlay)** to place details on cover rows, cover grids, or wide cards. Customize the overlay background with its color picker and opacity slider. Compact lists keep details beside the image.
- Expand **Text style** and enable **Customize metadata text** for separate title/info colors and sizes, alignment, bold, italic, and wrapping. These colors take priority over the general widget text color.
- Expand **Translucent background** to show/hide the backdrop, pick its color, enter opacity, and adjust padding/corner radius.
- Select **Default**, **Show**, or **Hide** for game titles. Toggle playtime, last played date, platform, favorites, installation status, and achievement progress independently for each shelf.
- Metadata works with cover rows, cover grids, compact lists, and wide cards. Existing metadata defaults remain unchanged.
- For storefronts, enable **Source/library icon** and optionally **Source/library name** (Steam, Epic, GOG, etc.). These use the game's library integration; missing icons fall back to the source name. Platforms remain a separate option.
- Toggle **Platform icons** independently of **Platform names**. Icons come from Playnite platform settings; a missing icon falls back to its name. Enable **Trophy beside achievement progress** and adjust **Metadata icon size**. Trophy color follows the metadata text color.
- Favorite labels appear for favorites only. Missing platforms and unavailable achievement data stay hidden. Unplayed games show **Last played: Never**.
- Achievement progress reads the provider selected in StartPage settings: SuccessStory JSON or the PlayniteAchievements cache. Access is read-only; progress refreshes about every 10 seconds while visible.
- Try changing a game's favorite/installation status, restart Playnite, and check the options persist. Use the appearance palette to recolor text and progress highlights.
- Shelf settings can scroll when the expanded options exceed the available height.
- Release build and rendered-view/provider checks passed; user testing completed and the user authorized the commit.

## Seventh change: testing in Playnite

- Open an individual shelf's settings popup and expand **Advanced rules**.
- Enable **Never played only**, set a minimum inactivity age, or set minimum/maximum days since added. Date endpoints are inclusive calendar days; zero means today. Missing dates do not match enabled date rules.
- Inactivity omits never-played games unless **Include never-played games in inactivity rule** is enabled. Never played means zero recorded playtime and no last played date.
- Under **Filters**, expand a group (categories, tags, genres, platforms, sources, features, or completion statuses) and use its separate **Include** and **Exclude** pickers. Both lists can be populated at once; exclusions always win. Existing selections saved with the earlier exclude mode are preserved as separate exclusions.
- Choose **All groups** or **Any group** for active include groups. Optionally require all selected values within multi-value groups. Exclusions always veto matches; date/activity and installed/favorite/hidden rules always apply.
- Example: to find installed games you have not played for 90 days, enable Installed only and minimum inactivity 90. To combine tag rules, choose a required tag under Tags > Include and an unwanted tag under Tags > Exclude.
- Empty selections remain unrestricted. Existing shelves keep their previous all-groups/any-value behavior until these options change. Reset filters also resets advanced rules and clears separate exclusions.
- Shelf settings use collapsible sections except **Game count and sorting** and **Shelf actions**, which stay visible. The popup follows Playnite behind other applications when focus changes. Close it with an outside click inside Playnite, **Close**, **Escape**, or its shelf toggle. **Game count and sorting** accepts counts/skips above 30 and offers **Show all matching games**; the skipped-game count still applies.
- The Playnite source badge now uses the default icon embedded in the Playnite executable when no library icon is available.
- Automated checks: `powershell.exe -STA -ExecutionPolicy Bypass -File tests/Test-AdvancedShelfRules.ps1` after a Release build.
- Release build, rule tests, pattern tests, and rendered shelf regression checks passed; user testing completed and the user authorized the commit.

Advanced shelf rules also support separate include/exclude wildcard patterns for game names and recorded installation directories. One pattern per line; `*` matches any characters, `?` one character; case is ignored. Path separators are normalized. These restrictions always apply independently of ID-group All/Any mode, exclusions win, and reset clears all patterns.

Pattern filters now cover 21 fields, with independent include/exclude boxes, extended glob sets/ranges, optional regular expressions with timeout, literal/contains/starts/ends helpers, missing-value matching, and inline validation. See [the shelf pattern guide](docs/SHELF-PATTERNS.md). The pattern panel has a `?` button pointing to that guide on the fork?s GitHub master branch; publication awaits an explicitly requested commit/push.


## Eighth change: testing in Playnite

- Your existing layout becomes **Main**, retaining widget instance IDs and settings. The fixed top-left page bar has been replaced by a configurable widget.
- Right-click the StartPage background and choose **Manage dashboard pages...**, or use the toolbar widget's page-management button, to open the page manager inside Playnite. Expand **Create a page**, enter a name, and choose **Create empty page**. The new page starts empty and copies the current page's background settings independently.
- Add widgets using the existing Edit Mode. Each page retains its layout and per-widget appearance/settings. Clocks now support multiple independent instances; so do text, images, and game shelves.
- In **Page name and order**, rename the current page and move it left/right in the selector. The most recently selected page is restored on restart.
- Expand **Page background** to select game-artwork sources, a local image, or an image folder, and adjust blur, overlay opacity, noise, and refresh interval for that page. Existing background settings/context-menu controls also edit the current page. Clearing a background never retains another page's image.
- Switch between two differently configured pages, then restart Playnite and verify layouts, backgrounds, shelf filters, and selection. Shelf settings on unvisited pages are retained.
- Page switching ends Edit Mode and sends existing widget hidden/closed and shown/opened callbacks. Visited page controls are cached, without removing their settings. Providers that allow only one widget instance remain limited to one across all pages; remove it from its old page before adding it elsewhere.
- Expand **Delete this page** and confirm deletion to remove the current page and its widget settings. At least one page remains. Page deletion does not delete games or local images.
- Close the manager with **Done**, **Escape**, or a click outside it. Changes save when leaving the manager, switching pages, leaving Edit Mode/StartPage, or shutting down.
- Automated checks: `powershell.exe -STA -ExecutionPolicy Bypass -File tests/Test-DashboardPages.ps1` after a Release build. Tests use an in-memory API and a unique temporary settings directory, never your installed Playnite configuration.
- Release build, page migration/persistence/lifecycle/UI tests, and existing filter regressions passed. User testing completed and the user authorized the commit.


Settings-opening crash fix: validation-only `ShelveProperties.Error` and its `IDataErrorInfo` indexer are excluded from both Playnite and Json.NET serialization. The reported `Item` cloning crash was reproduced with the installed Playnite serializer and verified fixed through `BeginEdit`/`CancelEdit`. Run `powershell.exe -STA -ExecutionPolicy Bypass -File tests/Test-SettingsSerialization.ps1` for this regression; it automatically uses an x86 process to load Playnite's assembly and does not touch installed settings.


### Toolbar widget: added to step 8

- Enter Edit Mode, select a panel, and choose **Add view > StartPage > Toolbar**. Multiple independent toolbars are supported. Leave Edit Mode to interact with the toolbar.
- The default toolbar has **previous page**, **page selector**, **next page**, and **manage pages**. Arrows stop at the first/last page. Navigation controls explicitly allow hit testing in Playnite window chrome.
- Open **Appearance > Toolbar settings > Toolbar items** to add/remove/reorder items and customize their labels. Choose native main menu, search/search box, view settings, filter presets, grouping, sorting, details/grid/list views, random selection, explorer, filters, notifications, updates, progress/status/cancel, and extension toolbar buttons exposed by Playnite.
- **Toolbar layout** configures horizontal/vertical direction, icons/labels, text size, and spacing. **Appearance settings** supplies the existing colors/background/border customization. Resetting toolbar settings restores its default items without affecting panel appearance.
- Native items reuse existing commands, bindings, and dropdown menus without moving the original controls. Unavailable controls/providers remain saved and disabled. Some native controls depend on the active Playnite theme/context; extension item IDs follow their supplied titles.
- A new empty page does not force a toolbar into its layout. Right-click **Manage dashboard pages...**, **Previous dashboard page**, and **Next dashboard page** remain available without a widget.
- Page initialization now clears loading flags in a finally block so failed loading does not permanently disable navigation. The previous settings-cloning crash fix remains included.
- Tests cover native command forwarding/CanExecute, dropdown anchoring and restoration, search/filter/progress bindings, extension buttons, clickable page selection, settings refresh, independent serialization, and page lifecycle. See [dashboard and toolbar usage](docs/DASHBOARD-PAGES.md).

Toolbar native-icon follow-up: both the item picker and widget now render actual native templates and extension font/image/vector icons. The editor refreshes previews in place without clearing selections; native toolbar discovery also searches other application windows when needed. Use Show icons on / Show labels off to match the native icon-only toolbar. Tests cover preserving an extension font icon and leaving its original control in place.

Toolbar refinements: theme-colored labels, actual theme templates with Playnite icon-font fallbacks, a distinct page-management icon, independent horizontal/vertical alignment per configurable group, native filter/notification button behavior, and refresh-safe extension selections with provider-based IDs. Existing items start in the Main group. Regression tests cover theme colors/resources, groups/persistence, notification panel activation, temporary provider loss, and renamed extension titles.

Toolbar follow-up: explicit theme style/foreground is applied to labels inside repeated item templates. Refresh native items now requests a render of the configured toolbar, and a loaded-only retry resolves saved native/extension entries when Playnite initializes its toolbar later. Tests realize the repeated Group/Custom label rows and simulate both explicit refresh and automatic late extension loading.

Extension-icon fallback: the catalog now queries loaded extensions through the SDK GetTopPanelItems API when native visual buttons cannot be found. Provider metadata, the original activation callback, and Playnite?s icon resolver supply working icons/actions without a native toolbar control. Provider results are cached, empty/not-ready results can retry, and explicit refresh clears the cache. Unresolved selected extension IDs are logged with discovered IDs. Regression coverage removes native extension controls entirely and verifies the SDK-supplied icon and action in the rendered widget.

Per-extension launch mode: Automatic retains the supported PlayniteAchievements native overlay action and routes other extensions to native pages when available. Extension page mode bypasses that overlay. Extensions without pages retain their native action. Toolbar Visible preferences no longer disable explicitly configured buttons. Tests cover hidden native icons with working actions, automatic fallback to a real page, explicit page selection, native-action fallback without a page, mode persistence, and both PlayniteAchievements modes using an isolated fixture assembly.

Sidebar coverage: toolbar choices now include native sidebar entries and SDK GetSidebarItems providers, not only GetTopPanelItems. Sidebar-only extensions such as Aniki Helper are selectable with their own icons and native navigation/action. The list uses Sidebar: labels and keeps top-toolbar actions separate; sidebar items always use their page/action. SDK metadata fills gaps in partially initialized native sidebars. Tests cover sidebar-only discovery/icons, direct native page routing, title-independent IDs, and persistence.

Group sizing/appearance: each toolbar group now independently supports Fill/Fit contents/Fixed width and height, weighted fill shares, optional background with a color picker and opacity, border color picker/width, corner radius, internal padding, outer spacing, and appearance reset. Background opacity leaves icons/text unchanged. Tests cover pixel/auto/star sizing, rendered decoration, persistence/reset, and actual Playnite settings cloning.


## Ninth change: complete dashboard profiles

- Right-click the StartPage background and choose **Dashboard profiles...**, or add **Manage dashboard profiles** to a Toolbar widget.
- **Save new profile** captures all pages, the selected page, layouts, panel appearance, clocks/text/images/toolbars (including group appearance and extension launch modes), page backgrounds, shared dashboard presentation settings, and referenced shelf settings/rules.
- Each profile is an independent snapshot. Editing the current dashboard or an applied profile does not update the stored snapshot. Saving an existing layout preset now also clones its layout instead of retaining a live reference.
- Import/export use a versioned JSON dashboard profile. Import validates the format and configuration before adding it, and does not apply it automatically. Rename and delete saved profiles from the manager; deleting a saved profile leaves the current dashboard intact.
- **Apply selected profile** saves a named backup of the current dashboard first, then replaces dashboard pages. Select that backup to restore the previous dashboard. Application failures attempt to restore the saved backup automatically.
- Built-in widget instance IDs are remapped together with shelf configuration so profiles are independent. Existing external widget identities and live controls are retained/reused where available; their private extension settings are not exported or changed.
- Missing extensions/view types retain placeholders and saved layout/appearance data instead of clearing their view configuration. Enable/install them and restart Playnite to restore their controls. Local images/folders remain referenced in place and missing local assets are reported after applying.
- Profile import includes only supported dashboard settings; startup preferences, operational settings, profile lists, and other extensions' configuration are excluded. Imported filter IDs refer to the originating library's metadata; reselect missing metadata when sharing across libraries.
- Verify save, edit, apply, backup restore, import/export, missing extensions, local images, and restart persistence in Playnite. Tests cover immutable snapshots, all widget/shelf/page data, safe JSON validation, ID remapping, automatic backup, runtime apply, unavailable-provider placeholders, external lifecycle reuse, and Playnite's actual settings serializer.
- Commands: `powershell.exe -STA -ExecutionPolicy Bypass -File tests/Test-DashboardProfiles.ps1`, `tests/Test-DashboardPages.ps1`, and `tests/Test-SettingsSerialization.ps1` after a Release build. Test fixtures use a temporary configuration directory and never overwrite installed settings.
- Release build and regression tests passed; user testing completed and the user authorized the commit.


## Tenth change: starter profiles with previews

- Open **Dashboard profiles...** from the background menu or toolbar and expand **Starter profiles**.
- **Minimal:** one Home page with toolbar navigation, clock, welcome note, and recently played shelf.
- **Achievements:** one page with recent unlocks and game cards showing achievement progress/trophy metadata, using the achievement provider currently selected in StartPage. Missing achievement data leaves relevant sections empty; no external provider data/settings are modified.
- **Library:** Library and Backlog pages, with recent games, additions, favorites, never-played games, and an installed list. Both pages have navigation and profile-management toolbar items.
- Every card previews each page's actual layout tree and shelf styles. These are schematic previews; installed theme, artwork, window dimensions, and library/provider data determine the final appearance.
- **Add profile** creates an independent saved copy without changing the running dashboard. **Apply** adds a copy and applies it using the existing automatic backup mechanism. Copies get their own page/widget IDs and shelf settings; editing or applying a copy never alters the template recipe.
- Starters use built-in widgets, game-artwork backgrounds, and no hardcoded local files or library-specific metadata filters. Current notification/global-background preferences, ignore tags, and achievement-provider selection are respected when the gallery is generated. Saved copies can be exported through the existing JSON profile workflow.
- Tests: `tests/Test-StarterProfiles.ps1` covers recipe validity, shelf mapping, portability, independent generations, profile round-trip, and distinct rendered previews. `tests/Test-DashboardPages.ps1` covers four gallery page previews, command bindings, adding without applying, and applying with a backup. Run using Windows PowerShell STA after a Release build.
- Release build and regression tests passed; user testing completed. Step 10 and its dashboard fixes were committed as `af89018` with user authorization.
- Profile crash fix: normalize missing/null Most Played options, detach removed widgets from settings/activity events, and discard refresh results after removal or settings replacement. Regression coverage applies a starter from a second page containing Most Played, including older profiles with null options.

Profile manager follow-up: Apply now defaults to appending independent profile pages, preserving existing page/control identities, the active page and shared settings. Unchecking the option retains full replacement. Saved profiles support Ctrl/Shift multi-selection and bulk deletion with confirmation. Regression tests cover repeated append, unique identities/names, navigation, preservation and bulk deletion through the real WPF list.

Dashboard smoothness follow-up (committed as `af89018`): retain decoded local backgrounds in a bounded 64 MiB / eight-image cache with file-change invalidation; bind cached backgrounds directly without asynchronous binding queue delays; debounce navigation saves until 750 ms after the last switch, while explicit saves and closing StartPage flush immediately. Existing page roots, shelf models and layout templates remain reused.


## Eleventh change: dashboard transitions (awaiting user testing)

- Open **Manage dashboard pages... > Dashboard transitions** from the background menu or toolbar.
- Choose None, Fade (default), or Slide; set duration from 50 to 1000 ms (default 200 ms).
- Slide direction supports Automatic based on page order, Left, Right, Up, or Down.
- The outgoing dashboard is captured before background/widget changes and stays visible while the destination prepares. Then the selected animation reveals it. Snapshot resolution is bounded to 2048 pixels on its longest side and released after completion/interruption.
- Global backgrounds are included in the outgoing snapshot. Page management/profile/settings overlays remain outside the animated surface.
- Windows animation preferences are respected by default. Uncheck **Follow Windows animation preference** to explicitly enable animations when Windows has disabled them.
- Preferences are global navigation settings and persist in Playnite settings; applying a profile does not overwrite them. Navigation saves begin after the transition finishes.
- Tests cover real fade/slide completion, all directions, None, Windows preference/override, interruption cleanup, command release and Playnite settings cloning. Step 11 and its follow-up fixes were committed as `69bd2f6` after user testing and approval.


Dashboard performance follow-up: Game Shelves reuses its loaded results on unchanged revisits, keeps lightweight library notifications while hidden, and defers hidden-page refreshes until needed. Library/settings changes and day changes invalidate the cached results. A widget refresh now shares one library snapshot across its shelves and avoids redundant game lookups for already-required queries. Refreshes are serialized per widget. Per-widget opening times over 100 ms are logged alongside dashboard/background timings. Awaiting user measurements on the installed build; no fixed load-time improvement is claimed.

Additional transition customization has been requested for a follow-up after loading performance is reviewed; keep step 11 open for user feedback before proceeding to step 12.

Extension debug logging follow-up: the dashboard-only diagnostics controls were removed from page management and moved to **Extensions > StartPage > Debug**. The opt-in `startpage-debug.log` captures the extension's logger at every level with exception details, lifecycle callbacks, and widget/dashboard/cache/background timings. The Debug tab shows its full path and opens its folder. Existing dashboard-only files are retained. No separate commit has been made for this follow-up.


Step 11 transition customization follow-up: added Slide and fade, Zoom in/out and Wipe alongside the existing Fade/Slide/None modes. Custom presets independently configure incoming/outgoing opacity, X/Y percentage movement, scale and delay, with Linear/Quadratic/Cubic/Sine easing and optional reverse movement for backward navigation. Users can preview without changing pages, save/load/delete independent named presets, and import/export validated versioned JSON. See `docs/TRANSITION-PRESETS.md` and `docs/transition-presets/gentle-slide.json`. Animation parameters remain bounded; no script engine or blur effects were added. Awaiting user testing; no commit authorized for this follow-up.


## Twelfth change: featured game widget (awaiting user testing)

- In Edit Mode, add **Featured game** to an empty panel. Multiple independent instances are supported.
- Open the widget's **Appearance > Featured game settings** to choose a pinned game, Last played, Recently added, Random, Random favorite, or Selected library game.
- The native searchable picker pins a specific game. Automatic sources can filter installed/hidden games; explicit pins override those filters. Missing/deleted pins show a selection prompt rather than a different game.
- Random selections persist across cached page visits; **Another game** rerolls when alternatives exist. Library/selection changes and day changes invalidate relevant cached selection.
- Configure description visibility/length, playtime, last played, source name/icon, optional achievement progress, Play/Install and Open in library buttons.
- Configure background/cover artwork, artwork opacity, overlay color picker/opacity, title/text sizes, optional title/text color pickers, content width and alignment. Text follows the theme by default. Missing artwork leaves usable text/actions. Descriptions are displayed as plain text, without a browser.
- The achievement section uses the selected StartPage provider and is omitted if data is unavailable. Profiles preserve per-widget settings; pinned game IDs refer to the current Playnite library and may need reselection when sharing.
- Cached revisits avoid rescanning an unchanged selection. Hidden widgets defer rendering. Removed widgets detach settings/database/selection listeners.
- Tests cover selection/filter/missing-pin/random behavior, description conversion, live rendered configuration, provider registration, settings editor, cache reuse, cleanup and actual Playnite settings cloning. Step 12 was committed as `52e1eae` after user testing and approval.


## Shelf interaction follow-up (awaiting user testing)

Optional per-shelf Dock magnification with neighboring-card falloff, Zoom, Lift, pointer-driven Tilt and Bounce, plus Classic/None choices. Controls include magnification, dock radius, lift, tilt, response duration/easing, dimming, glow color/radius, artwork overflow beyond widget bounds, Windows animation preference and reset. Hover settings do not refresh game data. Render-container state is restored on leave/unload. See `docs/SHELF-HOVER-EFFECTS.md`. This is now roadmap step 6 (Shelf customization) and remains uncommitted. The next pending feature is step 14 (Selected/hovered game detail widgets); Library statistics moves to step 15.

Appearance organization follow-up: all existing appearance color pickers now sit in collapsed Colors sections (general panel, clock/text/featured widgets, toolbar groups, shelf metadata/background/glow). Hover space now uses measured per-item/group layout margins rather than theme-dependent ListBox padding, and adapts resting card size to fixed-height viewport space. Bounds tests cover magnification and reduced shelf height. Awaiting user testing; no commit made.

Featured game sections follow-up: Appearance > Featured game settings now offers individual title visibility and optional Game details (platforms, genres, release date, developers, publishers), Library information (completion, categories, tags, features, date added, installation status, version), Scores (user, critic, community), and Links sections. Each detail field and section heading can be toggled; missing data is omitted. Detail rows use metadata formatting, long content scrolls, and profile snapshots retain all choices. Existing content defaults are preserved. Built and verified with selection/profile round-trip and live WPF section/field/theme/link rendering checks; uncommitted.

Featured section positioning follow-up: Section layout offers opt-in custom positioning, individual width/height and horizontal/vertical coordinates for Title, Description, Activity, Game details, Library information, Scores, Links and Actions. Edit sections on the widget reveals drag handles and resize grips; Done sections locks interaction. The dashboard edit overlay yields input during section editing. Initial custom placement uses the current stacked geometry, positions are saved with profiles, editing is temporary, numeric changes update without rebuilding the game widget, and Reset restores automatic layout. Overflow remains scrollable. Implemented.

## Step 14: selected/hovered game detail widgets (awaiting user testing)

- Expose Featured Game sections as independent dashboard widgets: title/artwork, description, activity (playtime, last played, source, achievements), game details (platforms, genres, release date, developers, publishers), library information (completion, categories, tags, features, date added, installation, version), scores, links, and game actions.
- Choose the game source per widget: **Selected game**, **Hovered game**, or **Hovered game, otherwise selected game**. Use a shared game context so widgets configured alike stay synchronized.
- Track hover from StartPage game shelves. Confirm supported native Playnite views during implementation; use native hover information only where it can be obtained reliably. This mode previews a hovered game without changing the actual library selection.
- Configure hover activation delay to avoid updating every widget while sweeping over covers. On leaving a game, use the selected game in fallback mode; offer keeping the last hovered game in hover-only mode. Show a clear empty state when no game is available.
- Reuse section field toggles and theme-aware text/color/icon controls. Each widget has independent dashboard position, size, appearance and profile persistence; users can compose a game details page without one large Featured Game widget.
- Subscribe only while visible, share/cache game context and metadata, cancel stale work, and update only affected content. Preserve the shelf hover performance improvements; avoid rebuilding dashboard layouts or decoding unchanged artwork on pointer movement.
- Verify synchronized hover/selection updates, fallback/empty states, independent settings, profile round-trips, page-switch cleanup and rapid-hover performance. Build for user testing before any commit or next roadmap step.

Step 14 is implemented for user testing. The preceding customization work was committed as 51147c8; step 14 remains uncommitted. Library statistics (step 15) must wait for user approval.

Section editing lifecycle follow-up: the explicit Edit sections on widget button starts editing and closes Appearance automatically. Done sections stays above the content; Escape and exiting dashboard Edit Mode also stop section editing and cancel active drag handles. Section editing is cleared on widget unload/page hiding. Position and size choices remain saved when editing stops. No commit made.

Section drag surface follow-up: in section editing, a transparent move handle covers the full section, including text and buttons, while the corner resize grip remains above it. Leaving section editing removes the move surface so normal content interaction returns. Hit tests cover starting a move over title text and reaching the resize grip. Implemented.

Featured artwork and edit-mode follow-up: Artwork sizing offers Fit entire image (Uniform, uncropped) or Fill widget (UniformToFill, original behavior), saved with widget profiles. Edit sections now has an entry button on the dashboard edit overlay; section controls are hidden outside dashboard Edit Mode, and leaving that mode locks sections. Starting from Appearance enters dashboard Edit Mode before closing the panel. Implemented.

Additional Featured Game artwork sizing: Fill, Fit, Fit without enlarging small images, Stretch, and Original size. Fit/Fill profiles retain their prior settings. All modes update live and persist through profiles; Stretch may distort proportions, and original-size images can be cropped when larger than the widget. Implemented.

Section controls and focus follow-up: one right-aligned Edit sections / Done sections toggle now lives above the dashboard edit overlay; the left duplicate and covered widget toggle are removed. The button stays clickable in both section states and is hidden outside dashboard Edit Mode. Application deactivation no longer exits Edit Mode, preserving dashboard/section editing across Alt+Tab. Implemented.

Section editing geometry follow-up: labels, outlines and resize grips overlay the content instead of adding a temporary header row or border thickness. Entering/leaving section editing preserves text position, wrapping and section geometry. Initial automatic-layout capture no longer adds editor-only header space. Implemented.

Featured game source follow-up: the existing Featured Game widget now supports Hovered StartPage game and Hovered game, otherwise selected game, alongside Selected library game. Hover follows Game Shelves, Most Played and the legacy StartPage game view without changing library selection. A configurable 0-1000 ms delay defaults to 150 ms; hover-only mode can retain the last hovered game to keep widget actions usable. Combined mode returns to the selected game on pointer leave. Shared owner-aware hover context ignores stale leave events, and targeted game lookups avoid full library scans. Timers stop while hidden/closed and settings persist in profiles. This does not implement roadmap step 14's separate widgets. Implemented.

Featured descriptions follow-up: length 0 now means the full description; positive limits accept values through the numeric input's integer range rather than the previous 2,000-character cap. Show description remains the explicit hide control. Optional description images are displayed in order alongside native text, with failed/unsupported images omitted. Overflow is scrollable in the widget or individual custom sections. Scrolling settings add optional automatic looping, speed, and pause-on-hover; auto scroll stops during dashboard editing, inactivity and hiding/removal. Native text/image rendering does not execute description HTML scripts. Implemented.

Description sizing/scrolling correction: description images no longer decode at an artificial fixed width; they keep natural size and shrink only when the section is narrower. Automatic scrolling uses physical pixel offsets with accumulated sub-pixel progress, previews in general dashboard Edit Mode, pauses during section editing, and follows application activation rather than only the host window's active flag. Debug logs report running/paused reasons and viewport/overflow values. Native image sizing at several widget widths and an actual DispatcherTimer scrolling cycle are covered by regression checks. Implemented.

Featured section appearance follow-up: Shared section appearance applies a global style within each Featured Game widget. Individual section appearance offers a full override per section, with opt-in styling, background color/opacity, border color/width, corner radius, padding, automatic-layout spacing, shadow and optional text color. Color pickers are inside collapsed Colors sections; reset controls restore defaults, and unchecking an override returns to the shared style. Styles update live in automatic and custom layouts, persist independently in profiles, and survive resetting section geometry. Existing widgets retain their old appearance until enabled. Built and checked with actual WPF style inheritance/override/reset, per-section text palette and profile cloning tests; uncommitted.

The user approved the completed shelf animation and Featured Game customization changes for commit. Step 14 (separate selected/hovered game detail widgets) is the next implementation step.

## Step 14 implementation and testing

- Add any of the nine **Game:** widgets: Artwork, Title, Description, Activity, Details, Library information, Scores, Links or Actions. Each occupies its own dashboard panel; split, resize and arrange panels using the normal dashboard editor.
- Open a widget's Appearance and expand **Game detail settings**. All default to **Hovered game, otherwise selected game** and share the existing preview context. Choose Selected, Hovered, combined, or Pinned per instance. Matching sources/delays stay synchronized; pinned instances remain independent.
- Each widget renders only its own section and exposes relevant field toggles, text/layout/scrolling or artwork options. Per-widget panel appearance supplies backgrounds, borders, colors and spacing. Description supports images and length 0 for all text. Artwork defaults to a fitted cover, with the existing image sizing choices.
- Hover comes from StartPage shelves and Most Played, not the native library's unsupported hover state. Native library selection is supported. Empty game contexts show instructions rather than random data; missing metadata is omitted. All controls and state survive profile import/export and actual Playnite settings cloning.
- Rendering reuses the Featured Game implementation and shared context/background image cache. Selected/hovered changes use direct game lookups; unrelated game updates are ignored by detail widgets. Hidden widgets stop timers and removed widgets release event subscriptions.
- Regression checks cover all nine registrations, actual ancestor settings binding, isolated sections/artwork, settings editors, synchronized hover/selection fallback, no full-library scans, independent profile/settings serialization and cleanup. See `docs/GAME-DETAIL-WIDGETS.md` for usage. Awaiting testing in Playnite; do not commit step 14 until asked.

Step 14 preview latency follow-up: the supplied log showed millisecond image decoding beginning seconds after hover. Hover timers and coalesced refresh callbacks now run at Loaded priority rather than background priority; selected/hovered/pinned lookups avoid settings cloning and worker scheduling. Unchanged game previews reuse rendered sections, while database/filter changes force fresh content. Debug logging adds shared Game preview hover context and per-widget Game preview update timings (request-to-start including configured delay, lookup, render, reuse). Regression tests verify all nine previews update before input/background work, indexed lookups complete synchronously, and same-game previews retain their visual sections. Built and tested; uncommitted.

Step 14 description image follow-up: newly added Description widgets enable images by default; existing per-widget choices remain intact. The parser now supports lazy image attributes and srcset, including empty/unsupported placeholder src values before a valid image URL. Debug logs report the toggle, parsed image count and load failures. Implemented.

Step 14 accepted by the user for commit: includes nine independent game detail widgets, responsive preview updates, description images and Playnite library HTML formatting in both description renderers. Step 15 (Library statistics) is now authorized to begin.

## Step 15: Library statistics (awaiting user testing)

Step 14 was committed as `c907e0f` after user approval. Step 15 adds an independent Library statistics widget with selectable counters, hidden/installed scope, explicit completion status choices, wrapping tiles/rows, text sizes, alignment, width and spacing. General panel appearance handles backgrounds/borders/colors. The shared library index updates incrementally and avoids repeat enumeration on dashboard visits. Profiles preserve per-widget choices and unrelated widget profiles omit statistics settings. See `docs/LIBRARY-STATISTICS.md` for usage and counter definitions. Build and verify before user testing; do not commit this step or start step 16 until authorized.

Step 15 verification: Release package built; counter/scope/empty-library/update/removal tests, independent profile cloning, actual Playnite settings serialization and WPF dashboard tests passed. WPF checks cover registration, ancestor settings binding, the completion status editor, library addition/playtime refresh, shared index across instances, cached revisits and removal cleanup. Step 15 remains uncommitted for user testing.

Step 15 follow-up: dashboard page management now holds edits until Done; outside click, Escape and Close discard pending settings/names/page additions/deletions. Draft names do not appear in the live selector. New dashboards contain an auto-height toolbar above an empty star-sized content panel. Library statistics adds native WPF bar/doughnut/pie charts, seven breakdowns, titles, legend/percent controls, group limits and a six-color picker palette. Unconfigured completion now shows a dash with the exact setup location rather than the vague Not configured label. Changes remain uncommitted for user testing.

Step 15 chart separation follow-up: added a multiple-instance Library chart widget with independent settings/profile persistence. New Library statistics defaults to counters; existing combined settings remain usable. Chart metrics include game counts and recorded playtime, with per-game and playtime-range breakdowns plus existing metadata groups. Optional average-playtime and played-game counters were added. Labels support grouped top/bottom/left/right legends, horizontal/vertical alignment and flow, beside-segment leader labels and above-bar labels. Shared caches avoid repeated library enumeration; per-game aggregates are collected only when that breakdown is requested. Awaiting user testing; no commit authorized.

Completed counter correction: Completed is now detected automatically from the Playnite status named Completed; Beaten/Played are separate. Required checkbox setup and the setup message were removed. Custom mapping is an opt-in advanced override for renamed statuses/custom groups. Automatic detection also supplies completion playtime and chart groups. No commit made.

Library charts is now its own roadmap step 16; remaining pending steps move to 17–21. Library Statistics renders counters only and no longer exposes graph settings. Library Charts hides custom completion mapping and always uses the automatic Completed definition. Bar labels now use explicit grid rows/columns for distinct beside/above positions. The default palette is colorful; custom colors override it. No commit made.

## Step 16: Library charts (awaiting user testing)

Add independent Library chart widgets for counts or recorded playtime, with bar/doughnut/pie styles, nine breakdowns, customizable titles/colors/labels and profile persistence. Default charts use a colorful six-color palette; custom colors are optional. Label modes include a positioned/aligned grouped legend, beside-bar labels and above-bar labels, or outside circular labels with leader lines. Completion mapping controls are excluded from charts. Library Statistics is counters-only. Achievement dashboard is now the next pending step, 17. Build and test this step before requesting a commit or starting the next feature.

Period playtime and circular-label correction: Library Statistics offers current-calendar today/week/month/year counters using shared GameActivity history, asynchronous calculation, configurable week start, scope filters and session boundary splitting. Missing history is explicit. Library chart outside labels now follow their segment angles; the above-bar/inside-slice mode renders circular labels inside the colored slice instead of reusing outside labels. Built for user testing; no commit authorized.

Label/layout follow-up for steps 15?16: removed the chart-editor explanatory label and completion-mapping section/footer from Statistics. Label fonts, bold/italic/underline and optional color pickers are available. A Library chart widget can contain additional independently configurable charts with add/duplicate/remove/reorder and chart-column controls. Statistics supports explicit counter grid rows, columns and spans, including period counters. Nested settings persist in profiles and removed charts clean up listeners. No commit made.

Unified Charts editor: renamed Additional charts to Charts and included the first chart in that single list. All per-chart options are inside the selected-chart editor, with no separate default editor. Any chart may be reordered/deleted, including the first; the last chart cannot be removed. Saved chart settings are preserved. No commit made.

Game preview empty-state follow-up: Featured Game and separate game-detail widgets use the first matching game from shelves on the current dashboard when their source has no game, then a shared stable random match if no shelf game is usable. Shelf refresh/order notifications update these previews without repeat library scans. Hover instructions are removed; explicit pins and source filters remain respected. WPF regression checks cover synchronized shelf/random fallbacks and cache reuse. No commit made.

Background-import crash fix: FeaturedGameView previously read WPF Settings/UsesHover directly from worker-thread database events, breaking Xbox imports and install-size scans. Database, selection, hover, fallback, settings and refresh callbacks now defer to the owning dispatcher before accessing UI state, without synchronously waiting for it. Closed widgets ignore queued work. Release build and worker-thread regression tests for all nine game-detail widgets passed. No commit made.

Chart geometry follow-up: vertical bars, explicit width/height/scale, pie/doughnut rotation, measured automatic outside fallback for labels that do not fit, global/per-series label offsets, and connector visibility/style/thickness/color controls. Charts keep label fonts independent of geometry size. Built for testing; no commit made.

Direct chart editing follow-up: rotation uses a slider; chart frames expose resize grips and labels can be dragged in chart-edit mode, with saved offsets and live connector updates. The dashboard edit overlay yields input while editing charts; Done charts and leaving Edit Mode lock controls. Inside text fit tries multiple wrapping widths before falling back outside. General Appearance defaults collapsed when specialized widget sections exist, expanded otherwise. No commit made.

Chart interaction correction: wheel input routes to the chart viewport, pixel scroll offsets survive redraws, and the viewport participates in window-chrome hit testing. Chart action buttons have explicit spacing/minimum size. Dragged inside labels create/update connector lines when outside their slice and remove them when back inside. No commit made.

Nested chart scrolling fix: non-leaf layout content and chart hosts stretch within their allocated cells; the chart viewport is capped to its actual host slot height. A nested toolbar/column dashboard regression without a direct chart-height override confirms the widget has its own scrollbar and reaches its final chart. Release build and dashboard checks passed; no commit made.

Library charts rendering follow-up: replaced hand-drawn graphs with LiveCharts 0.9.7, following PlayniteAchievements' glossy fills, subtle separators, Cartesian axes, themed hover tooltips and aligned separate legends. Added glossy/flat colors, axis/grid visibility, optional chart animations and separator width. Retained library aggregation, multiple charts, saved settings, label offsets/connectors and dashboard editing. New charts use a right-side legend; saved placements remain intact. PlayniteAchievements was read-only. Implemented.

Library chart scrolling follow-up: one collection scrollbar with full-height additional charts, measure-constrained viewport without a host ActualHeight feedback binding, legend/widget/dashboard wheel handoff at boundaries, stable settings binding across vertical splits, and debounced Debug layout snapshots (chart counts, positions, extents and offsets). Regression coverage includes visible final-chart bounds, fixed-height dashboards and live splits. Implemented.

Chart interaction follow-up: rotation updates native pie/doughnut geometry in place with normalized angles; label work is coalesced on chart/size/drag events instead of every WPF layout pass. Added segment border color pickers and shared border width for slices/bars. Chart edit frames stretch to normal plot width, and dashboard edit mode no longer zooms content out. Doughnut hole sizing uses the rendered radius. Implemented.

Statistics typography follow-up: compared native Playnite font/size/rendering preferences and theme resources. Added independent value fonts/styles/colors/opacity, dynamic theme-size presets for values and labels, label strike-through/line height/opacity, wrapping/trimming and opt-in text rendering overrides. Value/label colors remain in Colors; defaults and profiles are preserved. Implemented.

Chart edit performance follow-up: edit overlays and label/global/individual offsets update in place, including drag completion, with no native chart rebuild. Metadata-only chart renames do not redraw. Real visual/data updates arriving during a drag are deferred and applied once afterward. Added drag rebuild-count diagnostics and plot-identity/render-count regressions. Implemented.

Statistics catalog expansion: 39 opt-in counters for collection size, represented metadata, favorites, launch counts, percentages, playtime summaries and metadata coverage, plus nine independently configurable text count/playtime breakdowns (source/platform/genre/category/tag/feature/developer/publisher/completion status). Shared cached aggregation, existing typography and counter grid placement are retained; older layouts preserve positions and new options remain disabled. Implemented.

Selected source/platform statistics follow-up: source and platform breakdowns can show only one saved selection, including explicit zero counts. Added independent name/icon/heading visibility and icon size for selected and all-entry modes. Icons use provider IDs/library icons, platform metadata, Playnite fallback for no source and neutral placeholders for missing icons; per-widget caching and tooltip names are retained. Implemented.

Counter placement editor follow-up: only enabled base/additional/period/breakdown statistics appear, with live updates and grid-only enabled position inputs. Disabled statistics retain their saved positions, and editor subscriptions detach on closure. Implemented.

Statistic colors follow-up: every base/additional/period statistic and breakdown has collapsed Colors controls with independent global/custom value and label colors. Shared controls are labeled Global colors. Overrides persist with profiles and survive grid-position resets; explicit text colors take precedence over panel styling. Picker controls load lazily on opening Colors. Implemented.

November 14 follow-up: retain chart controls during host/widget resizing; move placement fields into each statistic/breakdown with a top-level grid switch; unify the global statistic text color control; replace formatting descriptions with styled B/I/U/S toggles across statistics/text/shelf settings. Added a startup-only safeguard for the logged automatic normal-window expansion to monitor bounds, with manual/maximized/DPI/display exclusions and an opt-out in Common settings. Implemented.

## Step 17: Achievement dashboard integration (awaiting user testing)

Compared the local PlayniteAchievements fork catalog, showcase options and projection code. Game summaries already cover per-game progress, completed games, recent unlock sorting, Finish next suggestions and game collections; achievement grids support achievement collections. Statistics, charts, scores, timelines and calendars are also provided. StartPage does not add parallel achievement widgets or a second cache/pinning system.

A new **Achievement dashboard** starter is offered in Manage dashboard profiles when one enabled provider advertises all four required, multiple-instance views. It places Game summaries beside Recent unlocks, Achievement statistics and Activity calendar, below an auto-height toolbar. The existing portable Achievements starter remains available. Configure provider-specific filters, sources, collections and styling through each widget's Settings menu; the starter does not write private provider settings. See [the integration guide](docs/ACHIEVEMENT-DASHBOARD.md). No commit or release update without approval.

Step 17 verification: Release build/package 0.17.0, starter discovery/profile round-trip tests, actual Playnite settings serialization and full WPF dashboard regression passed. Published installer manifest and GitHub release remain unchanged until release authorization.

Step 17 scope correction: the user requested additional StartPage-owned widgets with independent customization, not only an external-provider layout. Added Achievement progress cards, Achievement finish next and Achievement goals, plus a built-in Achievement dashboard starter. The optional external starter is now named PlayniteAchievements dashboard. Each new instance saves its own content/layout/text/color/card style and per-game percentage goals; uses shared read-only achievement snapshots; and pauses polling while hidden. See the integration guide for controls and limitations. No commit or release update authorized.

Step 17 native-widget verification: card/ring/bar rendering, progress/remaining filters, goals and target changes, independent profile cloning, shared SuccessStory snapshots, real ancestor settings/reset binding, hidden/removal cleanup and actual Playnite serializer passed. Full existing dashboard regression also passed. Build 0.17.0 is ready for user testing.

Achievement goals source follow-up: selected library game, hovered StartPage game, hovered-or-selected and existing pinned goals; immediate cached-context updates, shared shelf/random fallback and a default target percentage with per-game overrides. Source/default-target profile persistence and selection precedence checks passed; Release package rebuilt.

Step 17 achievement-list follow-up: independent Game achievements list widget with selected/hovered/combined/pinned sources, full locked/unlocked provider definitions, filtering/sorting, optional icons/descriptions/state/unlock dates, shared card typography/colors/borders, asynchronous cached reads and stale-result protection. No commit authorized.

Achievement list responsiveness follow-up: retain the last hovered game by default with an opt-out, cache database discovery, select game IDs before achievement joins, virtualize row-mode text/icons and report read/build timings to Debug. Read-only provider tests, a 1,000-row virtualization/scroll test and real-context hover-retention/lifecycle checks passed.

Recent Achievements customization: preserve its original widget ID, migrate legacy total/per-game limits into panel settings, move display controls to Appearance, add unlimited counts/date range/grouping/content toggles and shared row/card/text/icon/color/border controls. Provider selection is available under Common; old number fields and provider-only widget popup are replaced. Both provider feeds filter scope and apply per-game caps before filling the total. Migration, refill/unlimited/date-range, WPF binding/lifecycle and actual serializer checks passed.

Startup configuration fix: achievement-settings model getters no longer access the plugin singleton/settings while deserialization is in progress. Legacy recent counts migrate explicitly after loading, including saved profiles/presets; existing per-widget choices remain intact. The supplied nine-page/nine-profile configuration reproduces the original failure and now loads/round-trips using Playnite's actual serializer without an initialized settings view model. The supplied and installed configuration files were not modified.

Recent Achievements UI scope correction: restore its original XAML list, group headers/progress bars, compact rounded-icon rows, hover effect and expandable descriptions. Keep new settings in Appearance, add grouping by game/day/source/platform/none and group order, plus game/source/platform text filters. Typography/icon/color/detail additions are optional; card/row replacement controls are hidden for this existing widget. Other new achievement widgets retain their own UI.

Recent UI correction verification: actual original XAML renders compact 35-pixel icon rows, title/description, expansion toggles and game progress headers. Optional row visibility/typography and source/none grouping passed. Both provider feeds, serialization and the supplied nine-page/nine-profile startup configuration passed; package rebuilt for visual testing.

Unified widget settings: replace the Appearance text button and provider-only popup with one top-right gear opening the in-app movable/resizable editor. Audit retained built-in Game Shelves/Most Played settings and SDK-advertised external settings in a dedicated section; Recent Achievements controls remain integrated. Shift chart/section editing buttons beside the gear. WPF checks cover opening, provider controls, detachment and non-overlapping buttons.

Unified settings verification: full dashboard regression passed; a separate SDK-provider fixture confirms existing external settings controls are reused, instantiated once per opening and detached for reopening. Release package rebuilt; no commit made.

Independent panel resizing: regroup compatible aligned rows into columns with separate height dividers, or aligned columns into rows with separate width dividers. Available from gear > Panel layout and the panel context menu; fixed toolbars remain outside the converted group. Reuse existing widgets/settings and detach obsolete layout observers rather than call provider removal. WPF geometry checks cover unchanged neighboring sizes, changed target dimensions, serialization and the actual settings button/return to edit mode.

Widget picker organization: group StartPage Add view entries into General, Achievements, Library and Game details submenus, retaining external provider menus and all original widget IDs/availability rules. WPF leaf-style checks cover nested commands, unavailable duplicate instances and adding a library chart; Release package rebuilt.

Achievement typography follow-up: compact size inputs with inline B/I controls for titles and shared detail text, plus per-text overrides for descriptions, game names, counts, percentages, remaining counts, status, dates, goals and Recent group headers. Relevant controls are shown per widget, inherit existing settings by default and persist in profiles. Render checks cover progress/finish-next/goals, game-list metadata and the original Recent template.

Compact text editors: reusable text-element dropdown showing one set of fields at a time. Applied to all achievement text roles/shared styles, clock time/date, featured title/description/metadata, statistics values/labels, shelf title/metadata and applicable game-detail text. Existing controls and bindings are retained; selection does not alter settings. UI checks cover visibility and unchanged values across selections.

Compact editor polish: use stable label/data choices rather than UI controls as dropdown items so the selected text remains visible when closed; opacity spans both columns; text color pickers are inline beside size/style without an extra Colors wrapper. Closed-selection labels, unchanged formatting values, opacity geometry and inline-picker placement are covered by WPF checks.

Widget editor consistency follow-up: all remaining built-in color expanders now use labeled popup swatches, including lazy per-statistic overrides, global chart/statistic text colors, chart series/separators/connectors and toolbar group appearance. Preserve global/custom inheritance inside the popups; single-text editors stay direct while multi-text editors use the shared selector. Remove the duplicate game-detail color switch and compact the Text widget font/style/color row. Three-column palette layout retains wrapped labels and accessible swatches.

GitHub update notifications: optional background checks after startup and every six hours, plus Settings > Updates > Check now. Experimental 0.x builds include prereleases; 1.0.0 and later use stable releases only. Compare published version tags, require a finished fork .pext asset, ignore drafts/downgrades and link notifications to the release page for manual review/install. Requests run asynchronously with cancellation, failure handling and duplicate-notification suppression per session.

## Step 18: Shortcut widget testing

- Install the locally built 0.18.0 package and restart Playnite. In Edit Mode, choose **Add view > StartPage > General > Shortcuts**, then open its gear > **Shortcut settings**.
- Add a game, an application, a folder and a website. Choose library games with the search dialog; applications/folders can be browsed or use absolute paths, including environment variables such as `%LOCALAPPDATA%`. Websites require `https://` or `http://`.
- For applications, keep the executable path separate from arguments and optionally choose a working directory. A blank working directory uses the application folder. Supported targets are `.exe`, `.com`, `.bat`, `.cmd` and Windows `.lnk` shortcuts.
- Choose custom icons, or leave the field blank for game/application icons and fallback symbols. Blank labels use the game name or target. Try duplicate, move up/down and remove. Multiple widget instances keep independent lists.
- Try wrapping, a horizontal row or a vertical list; alignment; icons above/beside labels; icon/label visibility; icon/button sizes; spacing/padding; label size/B/I/color; custom button backgrounds and borders. Popup swatches retain custom-color toggles, and palette labels wrap in three-column rows. Corner radius and border width apply to custom button backgrounds/borders.
- Leave Edit Mode to launch. Installed games launch through Playnite; uninstalled games use Playnite's install action. Missing targets are disabled with a tooltip. Do not launch shortcuts while editing.
- Restart Playnite and round-trip a profile to verify persistence. Profiles reference local paths and library game IDs; they do not copy applications, icons or games to another installation.
- Release build, in-memory WPF editor/render/lifecycle checks, launch-info validation, profile isolation and actual Playnite serialization passed. Process launch tests do not execute applications or open websites; verify actual launching manually in Playnite. No step-18 commit or release update authorized.

Shortcut icon follow-up: use the Windows stock folder icon; discover website icon links and fall back to /favicon.ico without third-party icon services. Fetch and decode in the background, deduplicate concurrent requests, limit download size/time/concurrency, keep successful icons for seven days in ExtensionsData/<runtime GUID>/cache/shortcut-icons, cancel on widget unload and retain custom-icon priority. Native icon, HTML discovery, invalid-image/fallback, cache and live GitHub favicon checks passed.

## Step 19: Divider / spacer widget testing

- Install the locally built 0.19.0 package and restart Playnite. In Edit Mode choose **Add view > StartPage > General > Divider / spacer**, then open the widget gear > **Divider / spacer settings**.
- Choose **Divider line** or **Empty spacer**, and horizontal or vertical orientation. In split dashboard layouts, drag the panel divider to choose the space occupied by the widget. **Preferred spacing** controls its cross-axis size only when the panel uses content-based/Auto sizing; it does not change neighboring panels.
- Try solid, dashed and dotted lines, thickness, percentage length and end insets. Place horizontal lines toward the top/center/bottom or vertical lines toward the left/center/right; align shorter lines at the start/center/end of their available length.
- Adjust opacity and the labeled popup color picker. Turning off custom color restores the current theme's text brush. Spacer mode draws nothing and hides the line-only controls; general Appearance settings can still provide a panel background or border if desired.
- Add multiple instances, reset one without affecting another, restart Playnite and round-trip a dashboard profile. Shrink panels to confirm separators stay within their bounds and do not force extra scrolling.
- Release build, actual rendered line-style/spacer checks, small-panel containment, settings binding/reset/lifecycle, independent profile snapshots and Playnite serialization passed. Step 19 remains uncommitted pending user testing; no release update authorized.

Adjacent-space merge follow-up: replace branch-deleting merges with directional leaf-panel merges. Merge only matching rectangular neighboring spaces, regroup the affected band across nested columns/rows, and retain every surrounding panel, cached widget and fixed toolbar. Keep the populated widget when its partner is empty; ask before removing a populated neighbor. Splitter menus no longer delete nested sections. Directional/middle-band, unequal lower subdivisions, retained-control, fixed-size and profile checks passed.

Divider connections: optional start/end extensions to the nearest aligned or crossing divider, including T-junctions across widget boundaries. Configure Connections and maximum distance per divider. A non-interactive dashboard overlay follows current layout/scroll positions without resizing panels or rebuilding widgets; connections inherit source line style/color/opacity, exclude hidden/spacer targets and clear on unload. Rendered junction, target movement/distance, endpoint switches and cleanup checks passed.

Divider connector alignment fix: render a connected divider and its extensions as one continuous stroke instead of mixing local and dashboard drawing. Transform line thickness into dashboard coordinates and retain inherited opacity; restore local drawing when disconnected or unloaded. Pixel-column checks cover fractional positioning and scaling, alongside junction/cleanup tests.

Decorative dividers: thickness is a full-width slider with a numeric input. Added divider type selection for single/double/triple lines, waves/double waves, zigzags, chevrons, circles, squares and diamonds; independent solid/dash/dot/dash-dot/dash-dot-dot/custom stroke patterns; pattern size and spacing; optional filled shapes and rounded ends. Decorative envelopes clamp to small panels, and local/connected dividers share one renderer. Existing divider settings default to the original single-line type. All ten types in both orientations, shape fill, custom strokes, slider/type binding, wave connectors and actual Playnite persistence checks passed.

Decorative junction correction: resolve perpendicular connections against the widened geometry actually painted by the target (including waves and strokes), rather than its nominal axis. Connected decorative ends taper into the junction, and repeating shapes gain a terminal link so they do not stop short. Re-resolve dependent junctions when spans change and cache target outlines per layout update. Curve-contact and rendered-junction checks cover wave-to-wave joins and all decorative source types.

Connected-divider appearance matching: Connections includes a one-time Match connected divider appearance button, with nearest/start/end target selection. Copy decoration, stroke style, thickness, opacity, colors, shape/pattern sizing, dash pattern and cap/fill settings; preserve orientation, mode, layout, insets, length and connection choices. Disable matching without a target, update availability as connections change and detach editor listeners on closure. Actual button/endpoint, independent-copy and preserved-placement tests passed.
