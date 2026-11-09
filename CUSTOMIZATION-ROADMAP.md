# StartPage customization roadmap

Implement one numbered item at a time. Commit only when the user explicitly requests a commit. Build a Release package after each item and wait for user testing and approval before starting the next item. Do not push unless requested.

1. [x] **Per-widget appearance.** Optional background color/opacity, border color/width, corner radius, internal padding, outer spacing, shadow, custom title, title visibility/color/size/alignment, and reset to theme defaults. Includes optional widget text, small bitmap icon, chart, and selection/highlight colors, including FusionZ row/header highlights used by DuplicateHider. Settings follow the widget and persist with its layout. Existing layouts retain their appearance until customization is enabled.
2. [x] **Customizable clock.** Size, color, alignment, date visibility/format, 12/24-hour time, optional seconds, and optional clock animation.
3. [x] **Text widget.** Custom headings and notes with font size, color, alignment, and wrapping.
4. [ ] **Image widget.** Local images, banners, and logos with fit/fill choices and optional links.
5. [ ] **Shelf display styles.** Horizontal cover rows, wrapping grids, compact lists, and wide artwork cards; configure styles per shelf.
6. [ ] **Shelf metadata and badges.** Selectable titles, playtime, last played, platform, favorites, installation status, and achievement progress where available.
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
