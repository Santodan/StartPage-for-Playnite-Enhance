# Dashboard pages and the toolbar widget

## Add and configure a toolbar

1. Enter StartPage **Edit Mode**.
2. Select a panel and choose **Add view > StartPage > Toolbar**.
3. Open **Appearance > Toolbar settings** for that panel.
4. Under **Toolbar items**, choose an item by its actual icon preview and name, then click **Add item**. Change its dropdown to replace it, enter an optional custom label, or use the up/down/remove buttons.
5. Under **Toolbar layout**, choose horizontal/vertical layout, icons, labels, text size, and spacing. Horizontal toolbars wrap when necessary.
6. Leave Edit Mode to use the toolbar normally.

The initial items are previous page, page selector, next page, and manage pages. Multiple toolbar widgets have independent settings. A blank custom label uses the default/native title. If both icons and labels are disabled, buttons retain text when needed to stay identifiable. Use the panel's existing **Appearance settings** for colors, backgrounds, borders, and spacing.

## Dashboard pages

Use the toolbar's selector or arrows to change pages. Arrows are disabled at the first/last page. Choose **Manage dashboard pages** to create, rename, reorder, or delete pages, or to configure the current page's background.

Without a toolbar, right-click StartPage's background and select **Manage dashboard pages...**, **Previous dashboard page**, or **Next dashboard page**. The old fixed top-left page bar is no longer used.

Your original layout becomes **Main**. New pages start empty and copy the current page's background settings independently. Add your preferred widgets and toolbar to each page. Layouts, widget settings, background settings, page order, and the selected page persist across restarts. Switching pages exits Edit Mode.

The page manager remains inside Playnite. Close it with Done, Escape, or a click outside it. Deletion requires confirmation and leaves at least one page; it removes that page's widget configuration, not games or local images.

## Native toolbar items

The widget exposes these native Playnite toolbar controls:

- Main menu.
- Search button and library search box.
- View settings, filter presets, grouping, and sorting dropdowns.
- Details, grid, and list view switches.
- Random game selection, including selection from the current view.
- Explorer panel, filters panel, and notifications panel.
- Playnite updates.
- Global operation progress, status text, and cancel action.
- Extension buttons supplied to Playnite's native toolbar.

Native actions use Playnite's own commands and availability checks. Dropdowns open the existing menus at the widget button and restore their original placement/context afterward. Original toolbar controls remain in their original locations.

Some items operate on the library view rather than the dashboard layout. A native command can be disabled in a context where Playnite does not allow it. This widget does not bypass those restrictions.

## Missing items and themes

The native toolbar is discovered from the current desktop theme. If a theme omits a native control, its menu/search binding may be unavailable; the chosen item stays saved and is disabled rather than crashing or disappearing from configuration. Basic commands can fall back to the desktop view model when available.

Use **Refresh native items** in the toolbar editor after the native toolbar or extensions change. Extension buttons are identified by their supplied titles because Playnite does not give them persistent IDs. If a title changes, including after changing language, select the replacement item in the editor.

Extensions that permit only one dashboard widget instance remain limited to one across pages. Toolbar, clock, text, image, and game-shelf widgets support independent multiple instances.


### Matching the native icon toolbar

The picker and widget use the native toolbar's icon templates and extension-provided font glyphs, images, and vector icons. Original native controls stay in place. In **Toolbar layout**, keep **Show icons** enabled and disable **Show labels** for the icon-only appearance of the Playnite toolbar.

Extension actions appear as **Extension: <name>** with an icon preview. If the native toolbar initializes after the editor opens, use **Refresh native items**. Existing choices remain selected when refreshed.


## Groups and alignment

Open **Toolbar settings > Groups and alignment**. The initial **Main** group contains existing items. Center that group to center a single icon row, or add groups and choose left/center/right and top/center/bottom alignment independently. Assign each toolbar item using its **Group** dropdown. Groups can fill weighted shares of available space, fit their contents, or use fixed pixel sizes; items keep their configured order inside each group. Removing a group moves its items to a remaining group.

Labels and editor text use the theme's text brush. Native icons prefer the active theme's templates and original icon visuals; missing templates use Playnite's corresponding default icon-font glyphs. Page management has its own page icon, distinct from view settings.

Filter and notification entries are toolbar toggle buttons that invoke the native panel controls. Refreshing native items cannot write a missing selection back to your configuration. Selected temporarily unavailable extension entries retain their last-known title/icon in the editor. New extension identifiers use their activation-provider assembly and item position; older title-based choices are upgraded when resolved, so changing a button title does not discard the selected extension.

Native toolbar buttons can initialize after the dashboard. The widget retries unresolved entries while loaded, and Refresh native items also refreshes the widget itself. Saved extension selections do not need to be re-added after late initialization. Repeated editor labels explicitly use the active theme text style and brush.

Extension buttons also fall back to the loaded extension?s SDK toolbar items, independently of the theme?s native toolbar controls. Their original icons and activation callbacks are used. Refresh native items clears the provider cache. If an entry still cannot resolve, the Playnite debug log records its saved ID and discovered extension IDs for diagnosis.


## Choose how an extension opens

Each extension item has **Open extension** under **Toolbar settings > Toolbar items**:

- **Automatic (overlay when supported)** preserves PlayniteAchievements' existing overlay action. Other extensions open their real extension page when one is available.
- **Extension page (when available)** opens the native extension page, including for PlayniteAchievements, instead of its toolbar overlay action.

An extension without a page keeps its native action (for example, a service-specific dialog). No extension controls are reparented or forced into an unsupported overlay. Page routing uses Playnite's existing page-selection command and is resolved when clicked.

A provider's native toolbar **Visible** flag controls whether its original icon is shown; it does not disable an item explicitly selected for this widget. The widget respects actual command availability. The previous generic ?unavailable in the current Playnite theme/context? tooltip could be triggered by that visibility flag; extension items now have a more specific unavailable-action/page message when no usable route exists.


## Add sidebar-only extensions

The picker includes both **Extension: ...** top-toolbar actions and **Sidebar: ...** entries. Use the Sidebar entries for extensions such as Aniki Helper that do not publish a top-toolbar button. Click **Refresh native items** after installing this build if the editor is already open.

Sidebar entries use their provided icons and the native sidebar navigation/action. They always open the corresponding page/action, so the overlay/page selector is shown only for top-toolbar extension actions. An extension can appear twice when it offers both a top-toolbar action and a sidebar page; these are separate choices. Sidebar selection identifiers do not depend on the displayed title.


## Group sizing and appearance

Under **Toolbar settings > Groups and alignment**, expand a group's **Group size**:

- **Fill available space:** shares remaining room with the other Fill groups. A share of 2 gets twice the room of a share of 1 along the toolbar direction.
- **Fit contents:** sizes the group to its items, padding, and border.
- **Fixed size:** uses the entered width or height in pixels. Very small sizes can clip controls.

Width and height are independent. In a horizontal toolbar, width determines each group's column; in a vertical toolbar, height determines each group's row. Alignment positions the contents within the group.

In **Group appearance**, enable a background and use the color picker, opacity slider/numeric field, border color picker, border width, corner radius, internal padding, and outer spacing. Background opacity affects only the background, not icons or labels. Fixed width/height includes padding and border; outer spacing is added outside. **Reset group appearance** clears decoration/spacing while preserving its sizing, alignment, and items.

## Delete several pages without opening them

Open **Manage dashboard pages > Delete pages**. Tick the pages in the list; this does not navigate to them. Choose **Delete selected pages...**, then confirm with **Delete**. At least one page must remain, so selecting every page disables deletion. Removing inactive pages keeps the current page and its live widgets in place; removing the active page switches to a surviving page first. Press **Done** to save the deletions. Closing or dismissing the manager without Done restores the pending changes.

New Toolbar widgets include previous page, page selection, next page, Manage dashboard pages, Manage dashboard profiles, quick Add page and quick Remove page by default. Profiles use a stacked-layouts bookmark icon. Existing customized toolbar item lists are preserved; add Manage dashboard profiles from their settings if needed.

## Quick toolbar page actions

New toolbar widgets include **Add dashboard page** (+) and **Remove current dashboard page** (-). Add creates and saves a page immediately with a fitted toolbar above an empty content panel. Remove opens a small in-app confirmation, then switches to a surviving page and saves the removal. Cancel or Escape leaves the page unchanged. The final remaining page cannot be removed. Both quick actions are disabled while switching pages or editing pages in the manager. Existing toolbar item lists are preserved; add these actions through Toolbar settings.

## Organize toolbar items by group

In **Toolbar settings > Toolbar items**, select a group to show only its items. **Add item** uses that group, and the arrows reorder items within it. Each item still has a Group selector for moving it to another group; it immediately leaves the current list. Groups and alignment remains available for naming, sizing, styling and ordering the groups. Removing a group moves its items to a remaining group.

## Reorder pages without opening them

Open **Manage dashboard pages > Page order**, select a page in the list, and choose **Move up**, **Move down**, **Move to first** or **Move to last**. Selecting this list does not open the page. The order controls the toolbar page selector and previous/next navigation. Press **Done** to save; dismissing the manager restores the previous order. Moving a page to first does not rename it or change the existing behavior of reopening the last active dashboard.

## Unified widget settings

In Edit Mode, use the gear in the top-right corner of a populated panel. It replaces the Appearance text button and the separate widget settings popup. The same movable/resizable in-app window contains specialized controls, panel appearance, and any settings advertised by the widget provider.

Game Shelves and Most Played keep their existing editors in this window. External provider controls are included through the StartPage SDK rather than recreated. Recent Achievements already uses its appearance settings; its shared provider choice remains in Common settings. Individual shelf filter/appearance menus still configure each shelf separately.

The chart/section editing buttons sit beside the gear. Right-click > Widget settings opens the same window. Empty panels have no settings gear.

## Independent panel resizing

For a layout with aligned top/bottom rows, enter Edit Mode and open a widget's **gear > Panel layout > Make column heights independent**. The editor closes and each column gets its own horizontal divider. Drag the divider in the right column to extend its upper widget downward while retaining the left column's heights. The lower-right widget uses the remaining height in that column.

**Make row widths independent** provides the corresponding arrangement for aligned columns: each row gets its own vertical divider. Both actions are also in the panel's right-click menu.

These actions regroup existing panels rather than recreate widgets. They preserve widget identities, settings, cached controls and initial proportions, including a fixed/auto-height toolbar above the content. Resized proportions persist in the dashboard and profiles. The options appear in Panel layout only when the selected widget belongs to a compatible aligned group with matching subdivisions; other flexible siblings outside the group prevent conversion. This uses nested splits; it does not create overlapping/free-positioned panels.

## Widget picker folders

In Edit Mode, **Add view > StartPage** groups widgets into **General**, **Achievements**, **Library**, and **Game details**. The game-detail entries (artwork, title, description, activity, details, library information, scores, links, actions and Featured game) are together below Library. Existing extension-provider menus keep their own entries. Disabled items still indicate single-instance widgets that are already placed on a page.

## Compact text editors

Where a widget has multiple text elements, its formatting editor uses a **Text element** dropdown and shows only that element's controls. This applies to achievement widgets, clock time/date, featured-game title/description/metadata, statistic values/labels, shelf title/metadata and applicable standalone game text. Widgets with just one applicable text element keep a direct editor. Selection changes only which controls are visible; formatting values, inheritance, theme defaults and the rendered widget are preserved.
