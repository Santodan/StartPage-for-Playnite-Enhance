# StartPage-for-Playnite-Enhance 0.14.0 - Experimental

The first experimental release under Santodan's enhanced fork identity. This is not a stable release; feedback and bug reports are welcome.

## Download and install

Download **santodan_StartPage_Enhanced_Plugin_0_14_0.pext**, open it with Playnite, and restart when prompted.

**Disable the original StartPage before enabling this fork.** Both share the runtime identity to preserve existing settings and external widget compatibility, and must not run together. Back up your Playnite configuration before switching; do not delete the original settings data.

## Included

- Named dashboard pages with independent layouts and backgrounds, configurable transitions and custom transition presets.
- Complete dashboard profiles with import/export, independent snapshots, automatic application backups and append-pages support.
- Six starter profiles: Minimal, Achievements, Library, Game spotlight, Game inspector and Library insights.
- Customizable widget appearance, clock, text, images, featured game and synchronized selected/hovered game detail widgets.
- Shelf display styles, metadata, achievement progress, hover animations and advanced include/exclude pattern filters.
- Typed shelf filters: Enter toggles an exact or unique-prefix match with the dropdown closed; Ctrl+Delete removes it.
- A configurable toolbar with native Playnite/extension items, navigation and styled groups.
- Library statistics and multiple charts with per-counter styling, configurable layouts, labels, legends and chart appearance.
- Import an original StartPage config.json as a saved dashboard profile from the extension settings.
- Optional debug logging, dashboard caching and refresh improvements.
- A new yellow-glow house icon and documentation for the enhanced fork.

## Compatibility and feedback

Targets Playnite's desktop interface and SDK API 6.1.0. Keep external widget providers installed and enabled. GameActivity history is needed for period playtime, Weekly Activity and Most Played; achievement data depends on the selected provider. Profiles reference local assets and library IDs rather than copying game or external-provider data.

If something goes wrong, report the extension/Playnite versions, theme, reproduction steps and a screenshot or recording. Enable **Extension settings > Debug** for diagnostics, reproduce the issue and use **Open log folder** to find startpage-debug.log. Review logs before sharing them.

Report issues: https://github.com/Santodan/StartPage-for-Playnite-Enhance/issues

Roadmap: https://github.com/Santodan/StartPage-for-Playnite-Enhance/blob/master/CUSTOMIZATION-ROADMAP.md

The maintainer is not a developer; the fork's enhancements were made using AI under their direction and testing. Original StartPage and SDK work remain credited to felixkmh under the MIT license.
