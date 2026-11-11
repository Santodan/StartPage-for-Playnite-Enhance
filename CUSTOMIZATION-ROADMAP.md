# StartPage customization roadmap

Implement one numbered item at a time. Commit only when the user explicitly requests a commit. Build a Release package after each item and wait for user testing and approval before starting the next item. Do not push unless requested.

1. [x] **Per-widget appearance.** Optional background color/opacity, border color/width, corner radius, internal padding, outer spacing, shadow, custom title, title visibility/color/size/alignment, and reset to theme defaults. Includes optional widget text, small bitmap icon, chart, and selection/highlight colors, including FusionZ row/header highlights used by DuplicateHider. Settings follow the widget and persist with its layout. Existing layouts retain their appearance until customization is enabled.
2. [x] **Customizable clock.** Size, color, alignment, date visibility/format, 12/24-hour time, optional seconds, and optional clock animation.
3. [x] **Text widget.** Custom headings and notes with font size, color, alignment, and wrapping.
4. [x] **Image widget.** Local images, banners, and logos with fit/fill choices and optional links.
5. [x] **Shelf display styles.** Horizontal cover rows, wrapping grids, compact lists, and wide artwork cards; configure styles per shelf.
6. [x] **Shelf metadata and badges.** Selectable titles, playtime, last played, platform, favorites, installation status, and achievement progress where available.
7. [x] **Advanced shelf rules.** Never played, inactivity and date-added ranges, include/exclude rules, and match-all/match-any selection.
8. [x] **Multiple dashboard pages and toolbar widget.** Named pages with independent layouts/backgrounds. A placeable toolbar supports configurable native Playnite toolbar items, a page selector, previous/next arrows, and page management.
9. [x] **Complete dashboard profiles.** Save/import/export layout, appearance, widget settings, backgrounds, and shelf configuration together. Ensure saved presets are independent snapshots and handle missing extensions gracefully.
10. [x] **Starter profiles.** Minimal, Achievements, and Library layouts with previews.
11. [ ] **Dashboard transitions.** Optional None/Fade/Slide transitions with duration and direction controls. Prepare the destination before revealing it and coordinate the page/background change. Begin only after the user approves dashboard switching smoothness.
12. [ ] **Featured game widget.** Pinned or automatically selected game, large artwork, description, play button, playtime, and optional achievement progress.
13. [ ] **Library statistics widget.** Choose counters for total, installed, unplayed, completed games, and total playtime.
14. [ ] **Achievement dashboard.** Per-game completion, recently completed games, closest-to-completion games, and pinned goals, using available achievement-provider data.
15. [ ] **Shortcut widget.** Configurable game, application, folder, and website buttons with labels and icons.
16. [ ] **Divider/spacer widget.** Configurable separator lines and empty space for composing sections.
17. [ ] **More achievements settings.** Remove the Achievement limits and add more settings to the achievements.
18. [ ] **Background options** Add more options to control the background image.

## First change: testing in Playnite

- Install `source/StartPage/bin/felixkmh_StartPage_Plugin_2_14_1.pext` and restart Playnite when prompted.
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
- Release build and regression tests passed; awaiting user testing. Step 10 remains uncommitted.
- Profile crash fix: normalize missing/null Most Played options, detach removed widgets from settings/activity events, and discard refresh results after removal or settings replacement. Regression coverage applies a starter from a second page containing Most Played, including older profiles with null options.

Profile manager follow-up: Apply now defaults to appending independent profile pages, preserving existing page/control identities, the active page and shared settings. Unchecking the option retains full replacement. Saved profiles support Ctrl/Shift multi-selection and bulk deletion with confirmation. Regression tests cover repeated append, unique identities/names, navigation, preservation and bulk deletion through the real WPF list.

Dashboard smoothness follow-up (awaiting user testing): retain decoded local backgrounds in a bounded 64 MiB / eight-image cache with file-change invalidation; decode through the asynchronous background binding; debounce navigation saves until 750 ms after the last switch, while explicit saves and closing StartPage flush immediately. Existing page roots, shelf models and layout templates remain reused. No transition animation is implemented yet.
