# StartPage customization roadmap

Implement one numbered item at a time. Commit only when the user explicitly requests a commit. Build a Release package after each item and wait for user testing and approval before starting the next item. Do not push unless requested.

1. [x] **Per-widget appearance.** Optional background color/opacity, border color/width, corner radius, internal padding, outer spacing, shadow, custom title, title visibility/color/size/alignment, and reset to theme defaults. Includes optional widget text, small bitmap icon, chart, and selection/highlight colors, including FusionZ row/header highlights used by DuplicateHider. Settings follow the widget and persist with its layout. Existing layouts retain their appearance until customization is enabled.
2. [x] **Customizable clock.** Size, color, alignment, date visibility/format, 12/24-hour time, optional seconds, and optional clock animation.
3. [x] **Text widget.** Custom headings and notes with font size, color, alignment, and wrapping.
4. [x] **Image widget.** Local images, banners, and logos with fit/fill choices and optional links.
5. [x] **Shelf display styles.** Horizontal cover rows, wrapping grids, compact lists, and wide artwork cards; configure styles per shelf.
6. [x] **Shelf metadata and badges.** Selectable titles, playtime, last played, platform, favorites, installation status, and achievement progress where available.
7. [ ] **Advanced shelf rules.** Never played, inactivity and date-added ranges, include/exclude rules, and match-all/match-any selection.
8. [ ] **Multiple dashboard pages.** Named pages with independent layouts/backgrounds and convenient page switching.
9. [ ] **Complete dashboard profiles.** Save/import/export layout, appearance, widget settings, backgrounds, and shelf configuration together. Ensure saved presets are independent snapshots and handle missing extensions gracefully.
10. [ ] **Starter profiles.** Minimal, Achievements, and Library layouts with previews.
11. [ ] **Featured game widget.** Pinned or automatically selected game, large artwork, description, play button, playtime, and optional achievement progress.
12. [ ] **Library statistics widget.** Choose counters for total, installed, unplayed, completed games, and total playtime.
13. [ ] **Achievement dashboard.** Per-game completion, recently completed games, closest-to-completion games, and pinned goals, using available achievement-provider data.
14. [ ] **Shortcut widget.** Configurable game, application, folder, and website buttons with labels and icons.
15. [ ] **Divider/spacer widget.** Configurable separator lines and empty space for composing sections.

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
