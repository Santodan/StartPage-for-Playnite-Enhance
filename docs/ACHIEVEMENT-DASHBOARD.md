# Achievement dashboard

StartPage offers its own configurable achievement widgets alongside the widgets supplied by PlayniteAchievements. They share the selected provider's achievement data, while keeping their presentation and game targets independent.

## StartPage achievement widgets

Enter Edit Mode and choose **Add view > StartPage**:

- **Achievement progress cards**: a wrapping card gallery or vertical rows with progress rings, bars, both, or neither.
- **Achievement finish next**: incomplete games ordered by fewest remaining achievements by default. Change the sort or narrow the list with minimum progress and maximum remaining achievements.
- **Game achievements list**: individual locked/unlocked achievements for the selected, hovered, hovered-or-selected or pinned game(s), with icons, descriptions and unlock dates.
- **Achievement goals**: pin games and set an individual target percentage for each. A target is reached when the current unlocked percentage meets it. These goals do not change the provider's pins or Playnite completion status.

Open **Appearance > Achievement widget settings**. Each instance independently controls content/filtering, maximum games (0 means all), layout, card/artwork size, title/detail sizes, B/I styling, text alignment, visible title/count/percentage/remaining fields, ring size, bar/ring thickness, spacing, padding, rounded corners, border width, background opacity and colors. Text colors have inline pickers beside size/style; other palette colors are inside collapsed **Colors** sections. Text uses the theme by default, with optional separate title/detail colors. The progress accent changes when a game is fully unlocked or its goal is reached.

In **Achievement goals > Appearance > Achievement widget settings > Game source**, choose **Selected library game**, **Hovered StartPage game**, **Hovered game, otherwise selected game**, or **Pinned games (saved goals)**. Selected/hovered modes follow the same context as artwork widgets and use the shared shelf/random fallback when no game is available. Set the default target percentage for these dynamic games; a saved per-game target takes priority. Source changes update from cached achievement data without waiting for the ten-second poll. Existing goal widgets keep pinned mode by default.

Expand **Goals** in the Achievement goals settings, choose **Add game goal**, search for a game and enter its target percentage (1 to 100). Remove individual goals with **Remove goal**. A game with no available achievement data is omitted; missing data is never shown as a fabricated 0% result.

The built-in **Achievement dashboard** starter arranges progress cards and finish-next rows beneath the toolbar. Add goals as a separate widget wherever you want. It does not require the provider to export StartPage widget types, but does require achievement data from your selected provider.

Provider data is read on a worker and shared in a ten-second cache. Hidden widgets stop their refresh timers; appearance edits reuse the current snapshot. Unchanged refreshes keep the visuals intact. Cover images are decoded at display size and cached per widget. Game artwork/counts update on the next visible refresh, normally within ten seconds. Clicking a card selects its game in Playnite outside Edit Mode.

Settings and goals persist in StartPage layouts/profiles. Each instance has independent settings. Progress refers to unlocked / total achievements; it does not infer completion from a Playnite status or the presence of a capstone. The progress/finish-next/goals widgets show game-level summaries; the separate Game achievements list shows individual achievements. Deadlines and historical game-completion dates are not tracked.

## Optional provider dashboard

The following comparison is based on the local Santodan PlayniteAchievements fork; other releases may expose fewer widgets or different settings.

## Add the dashboard

1. Enable PlayniteAchievements and restart Playnite so it can register its StartPage widgets.
2. Open **Manage dashboard profiles**, then **Starter profiles**.
3. Choose **PlayniteAchievements dashboard** and **Apply**. Keep **Add profile pages** enabled to preserve existing pages.
4. Enter StartPage Edit Mode and click the panel's top-right **settings gear**. The unified window includes the external widget's own settings and StartPage panel appearance controls.

The starter contains an auto-height StartPage toolbar, a large Game summaries panel, and Recent unlocks, Achievement statistics and Activity calendar panels. It appears only when one enabled provider advertises all four supported multiple-instance widget types. It does not install or enable extensions. The existing **Achievements** starter remains available without this dependency.

## Existing capabilities to reuse

| Wanted feature | PlayniteAchievements capability |
| --- | --- |
| Per-game achievement progress | Game summaries, including progress and earned/total counts. |
| Recently completed games | Game summaries with the completed progress filter and Recent unlock sorting. This sorts by the most recent unlock; it is not a separate completion-date history. |
| Closest to completion | Game summaries source **Finish next**, with closest-to-completion, fewest-remaining or easiest-remaining ranking, minimum progress and maximum remaining settings. |
| Pinned goals | Game collections in Game summaries; achievement collections in the achievement grid. These are existing pinning/collection features, not a new target-date or target-count goal tracker. |
| Achievement totals, distribution and history | Existing Statistics, Pie, Scores, Timeline and Activity calendar widgets. Add other provider widgets from its Add view group as needed. |

The starter leaves provider defaults intact. It does not silently select Finish next, change collections or copy private provider settings. Labels may be localized by PlayniteAchievements.

## External provider data ownership and profiles

External widget data, completion rules, collection membership and ranking remain owned by PlayniteAchievements. The new StartPage widgets read the selected provider cache using StartPage's shared read-only reader. Neither dashboard writes to the achievement database. Achievement progress is distinct from Playnite's library Completion Status used by Library statistics.

For external widgets, saved StartPage profiles contain widget placement, IDs and panel appearance, but not private PlayniteAchievements options. Configure the widgets after applying the starter. Exporting a profile to another PC does not export those private options or achievement data. If the provider is later disabled, saved panels retain their positions and show the existing missing-provider placeholder.

## Test this step

- Apply the starter as additional pages and confirm your original pages remain present.
- Configure Game summaries for Finish next, then for completed games, and check provider settings open correctly.
- Select a game or achievement collection in the corresponding provider widget.
- Change panel appearance, save a StartPage profile, restart Playnite and check the layout and provider-owned settings.
- With PlayniteAchievements disabled, confirm the ordinary portable starters remain available and existing external panels are preserved.

## Game achievements list

Add **StartPage > Game achievements list** in Edit Mode. It defaults to **Hovered game, otherwise selected game**; the **Game source** section offers the same choices as Achievement goals. For pinned mode, add games under **Pinned games** (goal percentages are not used by this widget).

Under **Achievement list**, choose All, Locked or Unlocked and provider/name/recent-unlock/locked-first/unlocked-first sorting. Maximum achievements defaults to 0 (all); positive limits apply after filtering. Toggle icons, achievement names, descriptions, game name, locked/unlocked state and unlock date independently. Layout/typography and Colors control row/card appearance using the same sizing, borders and text styling as the other achievement widgets. The list scrolls within its panel.

Reads full definitions from the selected PlayniteAchievements or SuccessStory cache, including locked achievements without a recorded user row. Per-game reads run in the background and are cached for ten seconds. Switching hovered/selected games discards late results for the previous game. Hidden/removed widgets ignore pending results. Icons use the provider's cached paths or image URLs; absent icons leave the text usable. Settings persist with the panel/profile.

### Hover retention and performance diagnostics

**Game source > Keep last hovered game when leaving the shelf** is enabled by default for goals and the achievement list. Hover-based sources keep the last game when the pointer leaves; disable this to return to selected-game/shared fallback behavior. Selected-only and pinned sources are unaffected.

Row-mode achievement lists virtualize their contents, creating icons and text only for visible rows. Database location discovery is cached, and queries identify the game's internal IDs before joining its achievement records. Card mode retains its wrapping layout.

Enable Debug logging in the extension settings to capture `Achievement list loaded` entries in the existing `diagnostics/startpage-debug.log`. `readMs` measures the data wait; `buildMs` measures list preparation (visible-row layout/icon decoding can happen afterward). Each entry identifies the game, entry count, source and hover-retention setting. Cached unchanged lists retain their existing visuals.

## Existing Recent Achievements widget

The original **Recent Achievements** UI is retained: compact rows, rounded icons, game headers with progress bars, hover animation and click-to-expand descriptions. New controls are in **Appearance > Achievement widget settings**; they do not replace it with the new achievement-card UI.

- Total/per-game limits and last-N-days filter (0 means unlimited/any date).
- Group by game, unlock day, source, platform, or none. Platform grouping uses the game's combined platform names. Groups can sort by latest unlock or name; entries sort by recent unlock or achievement name.
- Filter by game/source/platform name, hidden/installed/favorite state. These filters apply before limits so later eligible games can refill the count.
- Show/hide icons, names, descriptions and game-group progress. Optionally show the game name or unlock date in each row.
- Optional icon sizing and typography. Both are disabled by default, retaining the original 35-pixel icons and themed text. Optional title/description/progress colors use pickers in Colors.

Existing limits migrate into panel settings after loading. New per-widget settings persist in layouts/profiles without changing the legacy shared values. The old two number fields are removed from extension settings; **Common > Achievement provider** selects the provider for all StartPage achievement widgets. The original widget ID is preserved. The new progress/goals/game-achievement-list widgets keep their separate presentation controls.


Legacy recent-limit migration runs after configuration loading; loading saved achievement widget settings does not require the plugin settings view model to exist yet. Per-profile legacy limits are retained independently, and preparing an old profile does not modify the saved original.

The panel's top-right settings gear now opens the unified editor. References to Appearance in this guide mean its appearance/specialized settings sections in that window.

## Text formatting controls

Title and shared detail-size fields use compact numeric inputs with **B** (bold) and **I** (italic) beside them. Under **Text formatting**, choose a text element from the dropdown and disable **Use shared style** to set a specific text type's size, bold/italic and optional color. Only types used by that widget are shown: counts/percentage/remaining for progress and finish-next cards; goal targets for Goals; descriptions/game names/status/dates for the game list; and descriptions/game names/dates/group headers for Recent Achievements. Text color pickers sit beside size/style; other palette controls remain in collapsed Colors sections. Existing layouts inherit their previous style until an override is enabled.

The achievement text editor now shows one selected element at a time. The dropdown includes Title, Shared detail style and the relevant individual text types; size, B/I and Colors controls belong to that selection. Switching entries preserves all other text settings.

Text colors use a compact swatch beside size and B/I. Click it to open a Playnite-themed popup with **Use custom color**, a palette, RGB/transparency sliders and hex input. Disable custom color to use the theme while keeping your saved color. Card background opacity spans the full width below the two-column dimension fields. The selected text element remains visible in the closed dropdown.
