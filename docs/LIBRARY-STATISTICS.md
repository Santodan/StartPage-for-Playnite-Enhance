# Library statistics and charts

## Library statistics: counters

In dashboard Edit Mode add **StartPage > Library statistics**. Configure it in **Appearance > Library statistics settings**. This widget shows counters only; chart controls and graph rendering belong to the separate Library chart widget.

Choose total games, installed games, unplayed games, completed games, total playtime, average playtime and games with recorded playtime. Hide/show counters independently. Choose wrapping tiles or rows, sizes for values and labels, alignment, width and spacing. Use normal panel appearance for backgrounds, borders, corners, padding, shadow and text colors.

Hidden games are excluded by default. Optionally include them or count installed games only. All counters use this same scope. Unplayed means zero recorded playtime and no last-played date. Average playtime uses games with positive recorded duration; a dated game with no recorded time is not in that average. Hours use the current number format. Duplicate library entries count separately.

Completed automatically counts the Playnite status named **Completed**, ignoring case and surrounding spaces. It excludes Beaten and Played and does not use achievement completion. There is no custom completion-mapping section in either editor.

## Library chart: independent graphs (roadmap step 16)

Add **StartPage > Library chart**. Add multiple instances for independently placed/resized graphs and configure each under **Appearance > Library chart settings**. All settings persist independently in dashboard profiles.

Choose horizontal bars, vertical bars, doughnut or pie. Choose **Number of games** or **Recorded playtime (hours)** and a breakdown: installation, played/unplayed, completed/other, sources, platforms, genres, completion statuses, individual games or playtime ranges. Individual games with the playtime metric produces a most-played graph. Ranges are zero, under 1 hour, 1 to under 10, 10 to under 50, 50 to under 100, and 100+ hours. Playtime is recorded lifetime duration, not daily session history.

Chart completion always uses the automatic Completed definition; the custom completion-mapping controls are not part of this widget. Missing metadata has its own group. Multi-platform/genre games contribute their full time/count to each group, so percentages describe memberships. Empty/zero-time graphs display an empty-state message.

Customize title, chart height, doughnut hole size, percentages, label size/width and spacing. Maximum groups **0** shows everything; positive limits combine remaining groups as Other. The default palette cycles through blue, green, orange, purple, red and cyan. Under **Colors**, enable custom colors to edit the six palette colors with color pickers. Disabling customization restores the colorful default palette.

Under **Chart > Label placement**:

- **Grouped legend:** top, bottom, left or right, with horizontal alignment and vertical alignment for side legends. Top/bottom entries can flow horizontally or vertically.
- **Beside each segment:** labels sit beside the corresponding bar. Pie/doughnut labels sit outside their segment with colored leader lines.
- **Above bar / inside pie or doughnut slice:** bar labels occupy a separate row above their bar. Pie/doughnut labels sit inside their corresponding colored segment, without outside leader lines.

Circular labels keep their configured font size when geometry shrinks. Grouped legends are recommended for charts with many categories; side legends have their own bounded scroll area.

## Data and performance

Both widgets share a small index of the required library fields and cached aggregates. Additions, removals and updates refresh it incrementally. Dashboard revisits do not enumerate the library again. Hidden/removed widgets stop listening for redraws; the shared index remains current. Per-game aggregates are collected only for that breakdown. Charts use LiveCharts 0.9.7 in WPF, the same chart library used by PlayniteAchievements, without a browser. Animations are disabled by default. Bar charts use one point per category rather than a matrix of zero-valued stacked series. Debug mode records render durations in the extension's existing log.

## Calendar-period playtime

In **Library statistics settings > Playtime by period**, enable **today**, **this week**, **this month** and/or **this year** independently. Choose the week start (by default the system culture's first weekday). These are current calendar periods, not rolling 24-hour/7-day/30-day/365-day windows. Hidden/installed filters apply to all four.

Period totals use the GameActivity session history already consumed by StartPage's activity widgets. Install/use GameActivity to record sessions; missing history shows an explanation and a dash instead of inventing zero totals from lifetime playtime. Existing recorded history loads asynchronously and is shared with the activity widgets. Sessions are allocated by their overlapping duration in local calendar periods, including midnight/month/year boundaries. Future time is excluded. Older offset-less GameActivity timestamps follow its existing UTC convention.

History updates refresh these counters, and visible counters also refresh each minute and on day changes. Hidden/removed widgets stop their period timer and detach their history listeners. Lifetime counters remain available without GameActivity.

## Label text

Under **Layout and text**, choose label font, size, bold, italic, underline and alignment. **Use theme label font** restores the theme font. Under the collapsed **Colors** section, enable a custom label color and use its color picker. This applies to chart labels/legends (including inside-slice labels) and statistic captions. Value size remains independent. A custom label color takes priority over automatic inside-slice contrast and general panel text coloring.

## Multiple charts within one widget

Open **Library chart settings**. This is the single manager for all charts, including the first one. Select a chart to edit its graph, metric, scope, labels and colors below the list. There are no separate default-chart controls outside the chart manager.

Use **Add chart**, **Duplicate**, **Remove**, and move buttons to manage the list. Any chart can be reordered or removed, including the first, but at least one must remain; Remove is disabled for the final chart. **Chart columns** controls the widget's overall chart grid, with rows added as needed. Each chart's list name is independent of its displayed title. Existing saved chart settings/order are retained.

## Statistic rows and columns

Enable **Place counters in a grid** in **Counter grid** at the top. Set minimum row/column counts, then expand each statistic's own **Position** section for its row, column and optional spans. Positions start at 1; the grid expands to fit enabled counters. Turn counters on/off through the existing Counters/Playtime by period sections. Disabled counters keep their positions. Avoid assigning the same cells to counters unless overlap is intended. Grid mode uses stacked value/caption tiles; the older Rows option applies outside grid mode. Reset counter positions restores the default three-column placement.

Chart collections, label typography/colors and nested counter placements persist independently with dashboard profiles. Additional chart controls are reused during redraws, share the library cache, and detach listeners when removed.

## Chart geometry and movable labels

Each selected chart offers **Horizontal bars**, **Vertical bars**, **Pie** and **Doughnut**. Configure **Chart width** (0 fits the available area), **Chart height**, and **Chart scale** to enlarge or shrink the geometry without scaling label fonts. **Pie / doughnut rotation** changes the start angle in degrees while keeping text upright.

Above-bar placement positions labels above their actual LiveCharts bars. Inside-slice placement tests against the rendered slice/ring geometry; labels that cannot fit move outside with connectors. Font size is not silently reduced.

Under **Label placement**, set **Outside label distance** and global horizontal/vertical label offsets. **Individual label offsets** lists each rendered series and permits per-label adjustment; offsets are saved with that chart. Reset label offsets clears both global and individual offsets.

Connector controls offer show/hide, straight/elbow style and thickness. Under **Colors**, optionally set a connector color with the color picker. Otherwise connectors use their series color. Connector endpoints follow the corresponding outside label as it moves.

## Edit directly on the dashboard

Under Library chart settings, **Edit charts on widget** enters dashboard Edit Mode and closes Appearance. Alternatively enter dashboard Edit Mode and use the widget's **Edit charts** toggle. Each chart displays an outline and a bottom-right resize grip. Drag the grip to change width and height; numeric size inputs remain optional for precision.

In this mode, drag a label directly to change its individual saved offset. Outside connector endpoints follow the label live. Dragging also works for inside and legend labels. The widget avoids rebuilding visuals during a drag. **Done charts** locks the controls; leaving dashboard Edit Mode hides handles and stops direct manipulation. This edit-mode flag is temporary and excluded from profiles; geometry and label offsets are saved.

Pie/doughnut rotation is a slider in the selected chart's Chart section. Inside placement checks the text bounds against the actual LiveCharts slice. It remains distinct from Beside on sufficiently large charts; small slices still place labels outside when they cannot fit, as requested.

General Appearance settings starts collapsed when the widget has its own settings section. Editors that only have general Appearance keep it expanded.

Chart scrolling uses pixel offsets and routes wheel input to the chart widget, including through the dashboard edit overlay. Redraws retain the current scroll position. Action buttons reserve spacing when they wrap to another row. Inside labels moved beyond their slice gain a connector automatically, including during dragging; returning them inside removes the connector. Connector visibility/color/style settings still apply.

Nested dashboard layout containers pass their allocated height through to the chart panel. The chart widget bounds its own viewport to that panel and shows an independent vertical scrollbar when the chart collection exceeds it. All charts are reachable inside the widget rather than relying on the outer page scrollbar. A regression layout with a fixed-height dashboard, top row and split columns verifies scrolling to the final chart.


## PlayniteAchievements-style rendering

Library charts now use **LiveCharts 0.9.7**, following the rendering approach in PlayniteAchievements: diagonal glossy fills, subtle slice separators, themed hover tooltips, real Cartesian axes and separate legends. Existing chart data, saved settings and chart order are retained. PlayniteAchievements is not modified or required to run StartPage.

New charts default to a doughnut with a vertical legend on the right. Legends align category names, values and percentages in separate columns. Hovering a legend entry highlights its corresponding bar/slice. Segment borders can be customized independently of series fills.

Select a chart in **Appearance > Library chart settings** to customize it:

- **Chart:** style, metric, breakdown, title, axes, grid lines, optional update animations, separator-width slider, size, rotation and doughnut hole.
- **Label placement:** grouped legend or individual labels, legend position/alignment, offsets and connector lines.
- **Colors:** glossy/flat fills, palette color pickers, label color and connector color.
- **Layout and text:** label font, size, weight and other text options.

Chart animations default to off, as in PlayniteAchievements. Dashboard transition settings remain separate. Use **Edit charts on widget** to resize charts and move individual labels directly.


## Chart scrolling diagnostics

The chart collection uses one widget scroll viewer. Additional charts contribute their full content height and do not create nested widget scrollbars. Scroll the widget under the pointer; at the top/bottom boundary the wheel passes to the dashboard. Side legends get the wheel first if they can scroll, then pass it to the chart collection. Splitting or moving the widget retains its chart settings.

Enable **StartPage settings > Debug** to capture `Library chart layout` records in the existing `startpage-debug.log`. Records include configured/rendered chart counts, chart columns, widget/viewport dimensions, content extent, offset/range, scrollbar visibility, each chart's row/column/top/height, and the surrounding dashboard's scrolling range. These are debounced; hidden widgets stop the diagnostic timer. This helps distinguish missing chart settings from collapsed content or an inaccessible scroll range.


## Rotation, segment borders and editing size

Rotation updates the existing pie/doughnut in place. Positive and negative angles are normalized to a full turn; 0 and 360 are equivalent. Labels/connectors refresh with the rotated geometry. Dragging other sliders coalesces redraws, and label positioning is refreshed by chart/size/drag changes rather than every WPF layout pass.

Under the selected chart's **Chart** section, **Segment border width (0 = none)** controls the outline thickness of pie/doughnut slices and bars. Under **Colors**, enable **Use custom segment border color** and use the color picker. These settings are independent per chart and persist in profiles.

Chart resize frames stretch to the same allocated area as normal charts. Dashboard Edit Mode no longer applies the previous 5% zoom-out, keeping artwork dimensions and pointer coordinates consistent when editing. Doughnut holes follow the actual rendered radius, including narrow charts and label margins.


## Statistic text customization and Playnite comparison

Playnite's desktop configuration exposes font families, small/normal/large/larger/largest sizes, text formatting and text rendering. Its themes use dynamic font/size/color resources and WPF text styles. Library Statistics now exposes the corresponding useful controls directly; no theme files are modified. See [Playnite's theme font documentation](https://api.playnite.link/docs/tutorials/themes/various.html).

Open **Appearance > Library statistics settings > Layout and text**:

| Area | Controls |
| --- | --- |
| Statistic values | Installed font or theme font, bold, italic, underline, strike-through, custom size or theme size preset, opacity |
| Labels | Existing font/style/size controls plus theme size presets, strike-through, opacity and line height (0 = automatic) |
| Wrapping and text rendering | Wrap/no-wrap, no trimming/character ellipsis/word ellipsis, inherited Playnite rendering or local Ideal/Display formatting and Auto/ClearType/Grayscale/Aliased rendering |

**Colors** remains a collapsed section. Value colors can follow the theme/panel, match labels, or use their own color picker. Label colors keep their existing independent color picker. Explicit custom colors take priority over the general panel text color. Matching labels follows custom label colors when enabled and otherwise follows the theme/panel.

Theme-size presets reference Playnite's dynamic `FontSizeSmall`, `FontSize`, `FontSizeLarge`, `FontSizeLarger` and `FontSizeLargest` resources. Custom size fields are disabled while a theme preset is selected. Theme-font buttons clear only the corresponding font-family override. Rendering overrides are disabled while inheriting Playnite's rendering preferences.

Existing profiles keep their current numeric sizes, colors and wrapping behavior; new text controls default to theme inheritance where applicable. Changes apply to all enabled counters, including calendar-period playtime. These additional counter controls are hidden from Library Charts. All choices persist independently per statistics widget and in dashboard profiles.


## Editing without chart rebuilds

Entering/leaving chart editing only toggles the existing handles, outlines and drag cursors. Dragging a label, releasing it, adjusting global/individual label offsets or resetting offsets moves existing text and connector visuals without recreating the plot. Renaming a chart in the settings list does not redraw its graph.

Changes that affect chart content or styling still refresh the graph. If one arrives during a label drag, it is deferred and applied once after the drag finishes. Resizing still updates chart geometry. Debug logs include `Chart label drag completed` with elapsed time, `chartRebuilds` and whether a separate rebuild was deferred; a position-only drag should report `chartRebuilds=0`. Render records include the chart name and a per-view render counter.


## More counters and text breakdowns

Open **Appearance > Library statistics settings**. All additions are opt-in and use the same hidden/installed filters, typography, colors and counter grid as the existing statistics.

Under **More statistics**, expand a category:

| Category | Additional counters |
| --- | --- |
| Library | Not installed, hidden games, favorites, distinct sources/platforms/genres/categories/tags/features/developers/publishers/completion statuses represented, largest named source (name and game count) |
| Playtime and progress | Played games with time or a last-played date, games with no recorded launches, recorded launch total, installed/played/completed/favorite percentages, median playtime among games with recorded time, longest playtime, average across all games, playtime of installed/completed/uninstalled games, most-played game |
| Metadata coverage | Games without a source/platform/genre/category/tags/features/completion status/release date, or missing cover/background/icon/description fields |

There are **39 additional counters**, beyond the original counters and four calendar-period playtime counters. Distinct metadata counts exclude unassigned (`Guid.Empty`) values and count only IDs used by games in the selected scope, not all registered integrations or unused metadata. Hidden-game count is therefore zero when hidden games are excluded. Missing artwork checks whether its metadata field is empty; it does not scan files or download images. Description coverage checks whether its field is blank, without keeping description HTML in the index.

Launch totals use Playnite's recorded `PlayCount`, which may be absent for imported history; zero recorded launches does not prove a game was never played elsewhere. Played percentage uses the existing played definition (recorded time or a last-played date). Completed counters continue using the automatic Completed-status definition. Playtime summaries are lifetime recorded duration, distinct from the session-based calendar counters. Medians exclude zero-time games; empty numeric statistics show zero, and the most-played/largest-source counters explain when no matching records exist.

Under **Breakdowns by source and metadata**, enable any of nine independent text breakdowns:

- Source, platform, genre, category, tag, feature, developer, publisher and completion status.
- Select game count or lifetime recorded playtime for each breakdown.
- Include or omit unassigned games, show percentages, and optionally limit to the largest N entries. **0 = all**; there is no fixed maximum.
- Entries sort by value descending, then name. Percentages use all matching memberships after the unassigned filter, before the optional top-N limit; a limited list may total less than 100%.

Developers and publishers share Playnite's company database but are aggregated separately according to each game's developer/publisher assignments. Games with multiple metadata values contribute once to each assigned group, so genre/platform/tag memberships can exceed the game total. Duplicate copies of the same ID on one game are counted once.

Breakdowns appear as styled counter tiles or rows, not charts. In each breakdown's **Position** section, choose its position/span as a block; its entries flow within that block using the widget's layout settings. Newly introduced positions are appended safely when loading older profiles, preserving existing positions. Counter selections and per-breakdown metric/filter/limit choices persist independently with dashboard profiles. Disabling or removing a widget releases its option listeners on closure, and the shared data index refreshes incrementally on library changes.


## One selected source or platform, with name and/or icon

Open **Library statistics settings > Breakdowns by source and metadata > By source** (or **By platform**):

1. Enable **Show this breakdown**.
2. Enable **Show only the selected source / platform** and choose the entry in the dropdown.
3. Choose **Games** or **Recorded playtime**.
4. Set **Show name text**, **Show source / platform icon**, **Icon size**, and **Show breakdown heading** independently.

Name only, icon only, both, or value only are supported. The number remains visible even when name/heading are hidden. A selected entry with no matching games shows **0** (or **0.0 h** using the local number format), rather than disappearing. The chosen source/platform ID is saved in profiles; the dropdown metadata itself is not serialized. Existing all-source/all-platform breakdowns remain available by disabling selected-only mode, and icon/name preferences apply to those entries too.

Source icons come from the corresponding library extension, using the provider ID recorded on matching games, with source-name matching as a fallback. The unassigned/Playnite source uses the Playnite application icon. Platform icons come from Playnite's platform metadata. If no icon exists, a neutral badge with a descriptive tooltip is used. Loaded icons are cached per widget; source/platform names remain available in tooltips even when hidden visually.

Counts use the widget's hidden/installed scope. A selected platform counts games assigned to that platform; selected sources count actual source IDs, rather than plugin IDs or the generic PC platform. Percentages remain relative to the full applicable breakdown, rather than becoming 100% just because one entry was selected.


Position controls live inside each statistic/breakdown. They appear when the top-level grid switch is enabled; there is no separate list of placement editors. Disabling a statistic preserves its saved row, column, spans and colors.


## Global and per-statistic colors

The statistics editor now calls the shared color section **Global colors**. Expand any statistic (including additional counters and calendar periods), then its collapsed **Colors** section. Breakdowns have the same Colors section within their settings.

- **Use global value color** inherits the widget's global value-color mode. Turn it off to enable that statistic's value color picker.
- **Use global label color** inherits the widget's global label/theme color. Turn it off to enable that statistic's label color picker.
- The two choices are independent. In global **Same color as labels** mode, an inherited value follows that statistic's effective label color, including a local label override.

For a breakdown, its override applies to its entry values/names and heading. Original source/platform image icons keep their artwork colors. Explicit per-statistic text colors are preserved against general panel text coloring.

Existing statistics default to both global options enabled. Per-statistic overrides persist in profiles, remain saved when a statistic is disabled, and survive **Reset counter positions**. Color picker bodies are created only when their Colors section is first expanded, keeping the many-statistic editor lighter.


## Current editor layout and resizing

**Counter grid** appears first and contains only the grid switch, minimum rows/columns and position reset. Every base/additional/period statistic and breakdown contains its own collapsed **Position** section, visible while grid mode is enabled. Its fields load only on expansion and edit the same persisted placement data as before.

**Global colors** now has one shared text-color picker and enable toggle, rather than separate redundant global value/label pickers. Editing it applies that color to inherited values and labels together. Old profile color modes are preserved until the global control is used; individual statistic overrides remain independent. Charts retain their own label-color control.

Text formatting uses compact B (bold), I (italic), U (underline) and S (strike-through) toggles with styled letters and descriptive tooltips. This also applies to text-widget and shelf metadata formatting where those options are available.

Resizing the host or widget no longer rebuilds chart views, recreates series or recalculates library aggregates. Existing WPF/LiveCharts controls reflow to their allocated dimensions. Data, style and explicit chart-setting changes still update the visuals as needed.
