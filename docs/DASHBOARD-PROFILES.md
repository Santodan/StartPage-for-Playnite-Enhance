# Complete dashboard profiles

Open **Dashboard profiles...** from StartPage's background right-click menu. You can also add **Manage dashboard profiles** to a Toolbar widget. The manager opens inside Playnite.

## Save and apply

1. Enter a name under **Save current dashboard** and click **Save new profile**.
2. Continue editing your dashboard. The saved profile stays unchanged.
3. Select a saved profile and click **Apply selected profile** to restore its snapshot.

Applying replaces the current dashboard pages and first saves an independent **Before applying ...** backup profile. To undo an application, select and apply that backup. Each application creates its own backup, and failed applications attempt to restore the backup automatically.

The manager allows renaming profiles. Deleting requires confirmation and removes only the saved profile; it leaves the current dashboard intact. Close with Done, Escape, or a click outside the manager.

## What is included

- All named pages, their order, and the selected page.
- Layout splits, alignments, sizes, and panel appearance.
- Clock, text, image, and toolbar settings, including toolbar groups and their appearance.
- Per-page background sources/images/folders, blur, opacity, noise, and refresh settings.
- Referenced game-shelf instances, metadata styling, display layouts, counts, sorting, and all include/exclude/advanced/pattern rules.
- Shared dashboard presentation, achievement-provider selection, recent-achievement display counts, and Most Played options.

Profiles contain independent copies. Applying a profile creates new built-in widget IDs and remaps their shelf settings, so subsequent edits cannot change the saved copy. External widget IDs are retained so their providers can continue finding existing settings, and existing live controls are reused when available.

Existing layout-only presets remain separate. Saving a layout preset now also takes an independent copy rather than referencing the active layout.

## Import and export

**Export selected JSON...** writes the selected saved snapshot, not unsaved changes to the active dashboard. Save a new profile first if you want to export current edits.

**Import JSON...** accepts version 1 `StartPage.DashboardProfile` files, validates their structure/settings, and adds them to the saved list. It does not replace your dashboard until you click Apply. Invalid or unsupported files leave the current dashboard intact. Layout-only preset JSON is a different format and should be imported through the existing layout-preset settings.

## External extensions and local assets

External extensions keep control of their own private settings and data. Profiles save their placement, identity, and StartPage appearance; they do not export credentials, achievement caches, private widget configuration, or install extensions. Their callbacks are used when hiding/opening controls; arbitrary extension content is not serialized.

A missing extension or unavailable widget type appears as a placeholder while preserving its saved placement/appearance. Enable or install its provider and restart Playnite to restore it.

Local image files and folders are referenced, not embedded or copied. Keep the paths available, or select replacement files after import. Missing local references are reported after applying. Nothing deletes or moves your images.

Shelf metadata selections use Playnite IDs. A profile shared with another library may need its tag/category/platform/source selections adjusted. Operational settings such as startup behavior and other extensions' configuration are not changed by applying a profile.


## Starter profiles

Expand **Starter profiles** in the manager to choose:

| Starter | Included layout |
| --- | --- |
| Minimal | Home page with toolbar, clock, welcome note, and recently played shelf. |
| Achievements | Recent unlocks alongside game cards with progress and trophy metadata. Uses your selected achievement provider. |
| Library | Library and Backlog pages with recent/favorite/unplayed/installed shelves and navigation on both pages. |

Each card shows a schematic preview for every page, including shelf styles. These diagrams represent layout structure rather than screenshots; your theme, artwork, library, window size, and achievement data determine the rendered dashboard.

**Add profile** puts an independent copy in the saved list and leaves the current dashboard unchanged. **Apply** adds a copy and applies it using the add-pages option, first saving a backup of your current dashboard. All widgets remain editable afterward. To share a customized starter, save it as a new profile and export that saved copy.

Starters do not reference local images or require library-specific tags. They retain your selected achievement provider and relevant notification/global-background preferences. The Achievements starter can have empty sections until its provider has data; it does not download achievements or change provider configuration.


## Keep existing pages and delete multiple profiles

**Add profile pages to the existing dashboard** is checked by default and applies to saved profiles and starter cards. Apply appends independent pages without changing your existing pages, active page, shared appearance settings, or running widget controls. Each appended page retains its profile layout, background and widget/shelf configuration. Repeated applications get fresh page/widget identities and numbered page names when needed. Shared settings, including the achievement provider, remain those of your current dashboard.

Uncheck the option to replace the entire dashboard and load the profile's shared settings. Both modes save an automatic backup first.

The saved profiles list supports Ctrl-click and Shift-click selection. Choose **Delete selected profiles...**, then **Delete** to remove all selected saved snapshots together. This leaves the active dashboard intact. Apply, rename and export use the active profile selection.
