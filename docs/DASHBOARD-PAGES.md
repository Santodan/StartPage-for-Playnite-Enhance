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
