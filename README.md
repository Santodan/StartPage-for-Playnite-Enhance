

<p align="center">
  <img src="source/icon.png" width="128" alt="StartPage Enhanced icon" />
</p>

# StartPage-for-Playnite-Enhance

<p align="center">
  <a href="https://github.com/Santodan/StartPage-for-Playnite-Enhance/releases"><img src="https://img.shields.io/github/v/release/Santodan/StartPage-for-Playnite-Enhance?include_prereleases&amp;sort=date&amp;style=for-the-badge&amp;logo=github&amp;label=Release&amp;color=00A8E8" alt="Latest release, including experimental releases" /></a>
  <a href="https://github.com/JosefNemec/PlayniteSDK"><img src="https://img.shields.io/badge/Playnite%20SDK-6.1.0-6C63FF?style=for-the-badge" alt="Playnite SDK 6.1.0" /></a>
  <a href="https://github.com/Santodan/StartPage-for-Playnite-Enhance/releases"><img src="https://img.shields.io/github/downloads/Santodan/StartPage-for-Playnite-Enhance/total?style=for-the-badge&amp;label=Total%20downloads&amp;color=00BE8E" alt="Total release downloads" /></a>
  <a href="https://github.com/Santodan/StartPage-for-Playnite-Enhance/releases"><img src="https://img.shields.io/github/downloads/Santodan/StartPage-for-Playnite-Enhance/v0.21.0/total?style=for-the-badge&amp;label=Latest%20release%20downloads&amp;color=8B5CF6" alt="Latest release downloads" /></a>
    <a href="https://github.com/Santodan/StartPage-for-Playnite-Enhance/stargazers"><img src="https://img.shields.io/github/stars/Santodan/StartPage-for-Playnite-Enhance?style=for-the-badge&amp;logo=github&amp;label=Stars&amp;color=F5A000" alt="GitHub stars" /></a>
</p>

> **Disclaimer:** I'm not a developer. All changes and enhancements in this fork were made using AI, I'm just a troubleshooting guy that try to test everything as much as possible. The original StartPage extension and SDK remain credited to felixkmh.

Build your own Playnite desktop dashboard with configurable pages, game shelves, widgets, statistics and charts. This fork extends [StartPage by felixkmh](https://github.com/felixkmh/StartPage-for-Playnite), retaining its layout system and support for external widget providers.

[Roadmap](CUSTOMIZATION-ROADMAP.md) - [Report a bug](https://github.com/Santodan/StartPage-for-Playnite-Enhance/issues/new) - [Suggest a feature](https://github.com/Santodan/StartPage-for-Playnite-Enhance/issues) - [Releases](https://github.com/Santodan/StartPage-for-Playnite-Enhance/releases)

## Shortcut widget

In dashboard Edit Mode, choose **Add view > StartPage > General > Shortcuts**. Its gear opens **Shortcut settings**, where you can add games, applications, folders and websites, set labels/icons, reorder entries and adjust layout, typography and button colors. Application arguments and working directory have separate fields. Shortcuts activate outside Edit Mode; unavailable targets show a disabled button with an explanatory tooltip. Each widget keeps its own list, saved with dashboard profiles. Blank custom-icon fields use native game/application/Windows folder icons, or fetch the website favicon asynchronously. Website icons are cached locally for seven days; sites without a usable favicon keep a Windows web icon.

## Divider / spacer widget

Choose **Add view > StartPage > General > Divider / spacer** to add a horizontal/vertical separator or leave an area blank. Its gear offers lines, double/triple lines, waves, zigzags, chevrons and repeating circles/squares/diamonds. Configure stroke patterns, thickness with a slider/numeric input, pattern height/spacing, shape fill, length, insets, positioning, opacity and color. Drag dashboard dividers to allocate space; preferred spacing applies to auto-sized panels. Each instance saves independently with dashboard profiles. Under **Connections**, enable **Connect ends to nearby dividers** to bridge widget spacing and form continuous lines or T-junctions; select the start/end and maximum distance. **Match connected divider's appearance** copies a connected divider's styling once while preserving orientation and placement; choose nearest/start/end if needed.

## Background customization

Open **Dashboard pages > Page background**, or the extension's **Background** tab. Enable **Keep background out of Playnite sidebar** to confine global backgrounds to the theme's content area. Use **Image framing** for sizing, zoom, positioning, flips and opacity; **Background effects** for tint, edge fades and vignette; and **Colors** for their popup color pickers. All sizing modes use full-resolution rendering and high-quality image scaling. Settings are independent per dashboard page and included in profiles. Resetting these controls keeps the chosen image and existing blur/noise settings. GIF and video backgrounds are supported. Choose **Image / GIF / video...**, then open **GIF / video playback** for loop, speed, sound and performance controls. See [animated backgrounds](docs/ANIMATED-BACKGROUNDS.md) for formats, defaults and troubleshooting.

## Update notifications

Open **Add-ons > Extension settings > StartPage-for-Playnite-Enhance > Updates** to enable or disable automatic GitHub checks, or press **Check now**. Checks run 30 seconds after startup and every six hours without blocking dashboard loading. Available updates appear in Playnite notifications; clicking one opens the release page to review the notes and download the `.pext`. Installation remains manual, alongside Playnite's existing add-on update mechanism.

Experimental **0.x** builds include published prereleases. From **1.0.0**, the checker accepts stable releases only. Only newer version tags with a finished extension package are offered; drafts, source-only releases and older versions are ignored. No GitHub account or token is required. Network/API failures leave the extension usable and are recorded in its logs; a manual check also shows a status message.

## Install or move from the original StartPage

1. Download the experimental `.pext` package from [Releases](https://github.com/Santodan/StartPage-for-Playnite-Enhance/releases). For a local build, use `source/StartPage/bin/santodan_StartPage_Enhanced_Plugin_0_21_0.pext`.
2. Disable the original StartPage before enabling this fork. **They must not run together:** the shared runtime identity preserves settings and external widget compatibility.
3. Open the `.pext` with Playnite and restart when prompted.
4. Open StartPage from Playnite's sidebar. Enter Edit Mode from the dashboard's background menu to split panels and add widgets.

The fork has its own package ID, `santodan_StartPage_Enhanced_Plugin`. It keeps the original runtime GUID and public StartPage SDK interfaces, so your original configuration may already be available. Do not delete the original settings data when switching.

To recover an earlier configuration, open **Add-ons --> Extension settings --> StartPage-for-Playnite-Enhance --> Import from original StartPage**, choose the original `config.json`, and save settings. The import becomes a saved dashboard profile; it does not replace your current pages. Open **Dashboard profiles...** from the dashboard background menu to use it.

See [fork identity, import scope and compatibility](docs/FORK-IDENTITY.md) for the details. This extension targets Playnite's desktop interface and SDK API 6.1.0.

## What you can customize

| Area | Features |
| --- | --- |
| Dashboard layout | Split panels, resize widgets, arrange multiple named pages and give each page its own background. |
| Widget appearance | Backgrounds, opacity, borders, corners, spacing, shadows, titles, text and supported content colors, with color pickers and theme defaults. |
| Game shelves | Cover rows, grids, compact lists and wide cards; sorting, grouping, include/exclude filters and advanced wildcard rules. |
| Shelf metadata | Titles, source icons, playtime, last played, favorites, installation status and achievement progress, including artwork overlays. |
| Shelf interactions | Dock magnification, zoom, lift, tilt, bounce, glow and dimming; animation direction and optional details popups. |
| Toolbar | Native Playnite and extension items, page navigation, labels, configurable groups, alignment and group appearance. |
| Clock, text and images | Configurable clock/date formats, text styling, image sizing and optional links. |
| Featured game | Selected, hovered, pinned or automatically chosen game, with artwork, formatted description, activity and actions. |
| Game detail widgets | Independent artwork, title, description, activity, details, library information, scores, links and actions, synchronized to the same game. |
| Library statistics | Library totals, playtime, progress, metadata coverage and source/platform breakdowns; per-counter layout, text and colors. |
| Library charts | Multiple bar, vertical bar, pie and doughnut charts with configurable data, size, rotation, legends, labels and segment borders. |
| Transitions | Fade, slide, zoom, wipe and custom presets with previews and JSON import/export. |
| Startup intros | Your own image, GIF or video when Playnite starts or StartPage opens, over the whole window or only StartPage, plus per-page intros when switching dashboards; length, exit effects, framing, sound and skip controls. An optional dashboard reveal animates the widgets in afterwards. |
| Profiles | Save, import, export, append or restore complete dashboards, with independent snapshots and automatic backups when applying. |

Some data depends on other extensions. Weekly Activity, Most Played and period playtime statistics use **GameActivity** history. Achievement widgets and progress use the achievement provider selected in settings, with data supplied by that provider. Playtime charts grouped by source use Playnite's recorded lifetime playtime.

## Start with a profile

Right-click the dashboard background, choose **Dashboard profiles...**, then expand **Starter profiles**.

| Profile | Layout |
| --- | --- |
| Minimal | Clock, welcome note and a dock-style recently played shelf. |
| Achievements | Recent unlocks and game cards with achievement progress and trophy metadata. |
| Library | Overview counters and recent/favorite shelves, plus a separate Backlog page. |
| Game spotlight | A featured game follows hover/selection, with a dock-style shelf below it. |
| Game inspector | Separate artwork, title, full description with images, activity and actions follow the same game. |
| Achievement dashboard | Customizable StartPage achievement progress cards and finish-next games. |
| PlayniteAchievements dashboard (when available) | PlayniteAchievements game summaries, recent unlocks, statistics and activity calendar. |
| Library insights | Library and period playtime counters beside source and installation charts. |

Each profile includes navigation and can be customized afterward. **Add profile** saves a copy without changing the current dashboard. The add-pages option lets you try a profile alongside existing pages. Refreshing the starter gallery does not overwrite your saved profiles.

## Roadmap

The [full customization roadmap](CUSTOMIZATION-ROADMAP.md) tracks implemented features and planned work.

Implemented areas include widget appearance, shelf customization, dashboard pages and profiles, transitions, featured/selected-game widgets, library statistics and charts.

Planned areas include:

- Achievement dashboards with completion tracking and goals.
- Shortcut widgets for games, applications, folders and websites.
- Divider and spacer widgets.
- More achievement display settings.
- More background controls.

Priorities may change based on experimental-release feedback. There are no release dates promised for individual items.

## Guides

- [Dashboard pages and toolbar](docs/DASHBOARD-PAGES.md)
- [Dashboard profiles and starter templates](docs/DASHBOARD-PROFILES.md)
- [Achievement dashboard and existing provider features](docs/ACHIEVEMENT-DASHBOARD.md)
- [Shelf patterns and typed filter selection](docs/SHELF-PATTERNS.md)
- [Shelf hover effects](docs/SHELF-HOVER-EFFECTS.md)
- [Featured game](docs/FEATURED-GAME.md)
- [Selected/hovered game detail widgets](docs/GAME-DETAIL-WIDGETS.md)
- [Library statistics and counter definitions](docs/LIBRARY-STATISTICS.md)
- [Transitions and custom JSON presets](docs/TRANSITION-PRESETS.md)
- [Startup intros and dashboard reveal](docs/STARTUP-ANIMATIONS.md)
- [Fork compatibility and original-settings import](docs/FORK-IDENTITY.md)

## Feedback and troubleshooting

When reporting an issue, include:

- Extension and Playnite versions, plus your theme.
- Steps to reproduce, expected behavior and actual behavior.
- Relevant widget/profile settings, a screenshot or a short recording.
- A debug log for crashes, slow loading or refresh problems, when available.

Enable **Extension settings > Debug > Enable StartPage debug logging**, reproduce the issue, then click **Open log folder**. Disable logging when finished. To capture startup, enable it before restarting Playnite.

The log is `startpage-debug.log` inside:

```text
<Playnite ExtensionsDataPath>/a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5/diagnostics/
```

Portable installations normally keep `ExtensionsData` under the Playnite directory. Logs rotate at 4 MiB and keep one previous file. Review logs and screenshots before sharing them, since they can contain game names or local paths.

## External widgets and attribution

Compatible extensions can provide widgets through the original StartPage SDK. Keep their providers installed and enabled. A missing provider cannot be restored by importing a dashboard profile. See the [original SDK integration guide](https://github.com/felixkmh/StartPage-for-Playnite/wiki/Integrating-with-other-Extensions); compatibility with every third-party provider is not guaranteed.

Original StartPage and SDK work by **felixkmh**; fork enhancements maintained by **Santodan**. The original attribution is retained under the [MIT license](LICENSE).
