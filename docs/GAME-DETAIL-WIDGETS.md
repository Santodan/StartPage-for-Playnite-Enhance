# Game detail widgets

Create a game details page from independent dashboard panels. In dashboard Edit Mode, split or add a panel and choose a **Game:** widget from StartPage:

| Widget | Content |
| --- | --- |
| Game: Artwork | Cover or background, with Fit/Fill/Stretch/Original sizing |
| Game: Title | Game name |
| Game: Description | Description text and optional images; length 0 means all |
| Game: Activity | Playtime, last played, source/icon and available achievement progress |
| Game: Details | Platforms, genres, release date, developers and publishers |
| Game: Library information | Completion status, categories, tags, features, date added, installation and version |
| Game: Scores | Available user, critic and community scores |
| Game: Links | Website links stored with the game |
| Game: Actions | Play/Install and Open in library buttons |

Resize and arrange the dashboard panels normally. For example, put Artwork on the left, Title and Description on the right, with Activity and Actions underneath. Multiple copies of any widget are supported.

## Choose the game

Open **Appearance → Game detail settings → Game source**. The default is **Hovered game, otherwise selected game**. Configure matching sources and hover delays across widgets to keep them synchronized.

- **Selected library game:** follows Playnite's current selection.
- **Hovered StartPage game:** follows covers in StartPage shelves and Most Played. Keep last hovered game is enabled by default, allowing you to move the pointer to the detail widgets and use their buttons. Disable it for an empty state on pointer leave.
- **Hovered game, otherwise selected game:** previews a hovered game, then returns to the selected game when the pointer leaves it.
- **Pinned game:** stays on an explicitly chosen game.

Hover previews do not change the actual library selection. Native library hover is not supported; native library selection is. The hover delay defaults to 150 ms to avoid updating every widget while sweeping over covers. Filters apply to selected/hovered modes; an explicit pin ignores them.

## Customize

Game detail settings show the fields relevant to that widget. Change text size/colors/alignment, description length/images, artwork sizing/opacity, or scrolling as applicable. The normal panel appearance controls provide independent backgrounds, borders, padding, spacing and other styling. All color inputs use color pickers.

Overflow is scrollable. Enable automatic scrolling if desired; it waits two seconds, pauses at the end, then loops. Pause-on-hover, speed and section-editing/application inactivity rules follow the Featured Game widget's behavior.

Settings are independent for every widget and are saved in dashboard profiles. Reset game detail settings restores that widget's defaults without changing other widgets. Missing fields are omitted, and an unavailable selected/hovered/pinned game shows an empty-state instruction.

These widgets reuse the Featured Game renderer and the shared selected/hovered context. The original Featured Game widget and its internal section layout remain available separately.

## Preview responsiveness

The source's hover delay is intentional and defaults to 150 ms; set it to 0 for immediate previews. Refreshes run above input/background work, use direct game lookups, and keep existing visuals when the chosen game is unchanged. Library-wide selection modes on the separate Featured Game widget still use a background worker.

If an update remains slow, enable extension Settings > Debug and reproduce it. The existing debug log includes `Game preview hover context` and `Game preview update` records. Each widget reports request-to-start time (including the configured hover delay), game lookup time, render time and whether existing content was reused. These timings distinguish scheduling delay from image or metadata rendering.

New **Game: Description** widgets enable description images by default. Existing widgets retain their saved choice: open **Appearance > Game detail settings > Description** and enable **Show description images**. This setting is independent of the full Featured Game widget. Debug logs report whether images are enabled, how many image tags were recognized, and image-loading failures.

Descriptions preserve HTML formatting by default using Playnite's library description renderer, including headings, paragraphs, lists, links and emphasis. Under **Description**, disable **Preserve HTML formatting** for plain text. **Show description images** remains independent. Set length to **0** for the full description; positive limits retain balanced markup. Scrolling and automatic scrolling work with formatted descriptions as well. Supported HTML/CSS matches Playnite's renderer.

## Default preview when no game is hovered/selected

When the configured preview source has no matching game, the widget uses the first matching game from the current dashboard's Game Shelves, in dashboard/shelf/display order. If there is no usable shelf game, it uses a random matching library game. The fallback is shared and cached so matching game widgets stay synchronized and do not reshuffle or scan the library on every refresh. Shelf loading/reordering updates previews when its first game becomes available.

Normal hover/selection and keep-last-hovered behavior still take priority. Hidden/installed filters apply to fallbacks; random-favorite mode still requires favorites. Explicit pinned games remain explicit. If the library has no eligible game, preview widgets remain empty instead of showing a hover instruction.
