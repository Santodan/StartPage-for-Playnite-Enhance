# Startup intros and dashboard reveal

StartPage has two optional, independent startup features. Both are off by default; existing configurations keep opening as before.

- **Startup intro**: plays your own image, GIF or video over Playnite (or only over StartPage) when Playnite starts or StartPage opens, and optionally when you switch to a specific dashboard page.
- **Dashboard reveal**: animates the dashboard widgets into view once they have loaded (fade, rise, zoom, cascading or rippling panels, spotlight, curtain). When an intro is playing, the reveal waits behind it and runs as the intro exits.

## Startup intro

The global intro is in the extension settings, **Startup intro** tab. Each dashboard page can have its own intro in **Manage dashboard pages > Page intro**. Page intros are saved with the page, so dashboard profiles carry them (and import/export them) together with the page.

### Media

- **A file** or **Random file from a folder** (a different supported file each time).
- Images (PNG, JPEG, BMP, TIFF, WebP), GIFs and Windows-supported videos (MP4, M4V, WMV, AVI, MOV, MKV and WebM with installed codecs). GIFs decode off the UI thread; videos use Windows playback, as for animated backgrounds.
- **Framing and playback**: fit (fit/fill/stretch/original size), zoom, horizontal/vertical position, opacity, flips, playback speed, loop, mute and video volume. Intros fit the whole image and play sound by default.
- **Background**: the color shown around the media (black by default).

### Cover

- **Whole Playnite window**: covers everything, including the sidebar and the top bar. When chosen for the global intro, it plays as soon as Playnite has started, even if Playnite opens on another view.
- **StartPage area only**: covers the StartPage view; the sidebar and top bar stay visible. The global intro then plays when StartPage first opens.

### Starting while Playnite loads

For the global intro set to **Whole Playnite window** and **When Playnite starts**, **Start while Playnite is loading** (on by default) opens the intro as soon as StartPage is loaded by Playnite, before Playnite's main window exists:

- It plays in its own borderless window placed where Playnite's window opens (from Playnite's saved window position; the whole monitor when Playnite was maximized), or **Full screen** when chosen.
- It runs on its own UI thread, so the video keeps playing smoothly while Playnite loads its library and widgets behind it.
- It never ends before Playnite reports that it has started (in every length mode), and in *until the dashboard has loaded* mode it also waits for the dashboard. The maximum still applies. It then reveals Playnite with the exit effect and focuses Playnite's window.
- Playnite shows its own small banner before any extension loads, so an extension cannot replace it. To skip it, add `--hidesplashscreen` to the Playnite shortcut (Properties > Target, after the `.exe` path).
- Desktop mode only; Fullscreen mode is unaffected. If the launch window cannot start, the intro plays over the window after Playnite has started, as before.

### When it plays

| Intro | Option | Plays |
| --- | --- | --- |
| Global | When Playnite starts | Once per Playnite session (whole-window intros while Playnite loads, or right after it starts; StartPage-area intros when StartPage first opens). |
| Global | Every time StartPage opens | Each time you return to StartPage from another sidebar view. |
| Page | First switch to this page each session | The first time you switch to the page in a session. |
| Page | Every switch to this page | Every time you switch to the page. |
| Page | Also play when StartPage opens on this page | When StartPage opens on that page and the global intro did not just play. |

### Length and exit

- **Until the video or GIF ends**: plays the media once. Still images and looping media use the **fixed time** instead.
- **Fixed time** (0.5-30 s).
- **Until the dashboard has loaded**: stays until the page's widgets are ready, at least **Minimum** and at most **Maximum**.
- **Minimum** and **Maximum** (up to 60 s) bound every mode except Fixed time, so an intro can never get stuck.
- **If the GIF or video ends first**: when the intro stays up longer than the clip (Playnite still loading, the minimum or fixed time, *until the dashboard has loaded*), it can **Hold the last frame** (default), **Repeat its first part** or **Repeat its last part**. **Repeated part** (1 ms to 10 s) is the length of that part; it plays in a loop until the intro exits. Not used when **Loop** is on. **Preview intro** cannot wait for Playnite or the dashboard, so it plays a GIF or video to its end and, with a repeated part, repeats that part three times.
- **Fade in** and an **exit**: fade out, zoom in/out with fade, spotlight (a hole opens from the center), curtain (splits from the middle) or cut.
- **Skip on click or key press**, with an optional "Click or press any key to skip" hint.

A missing or unplayable file never blocks Playnite: in *until the media ends* mode nothing is shown; otherwise the background color is shown for the configured time. With Windows animations disabled and **Follow Windows animation preference** checked (Dashboard transitions), intros still play but without fades or exit motion.

Use **Preview intro** in either editor to play it inside the preview box, including from the settings window. **Reset options** restores everything except the chosen file/folder and whether the intro is on.

## Dashboard reveal

Open **Manage dashboard pages** and expand **Dashboard reveal (when StartPage opens)**.

| Effect | What happens |
| --- | --- |
| Fade | The whole dashboard fades in. |
| Rise | The dashboard slides in from the chosen direction while fading in. |
| Zoom | The dashboard grows from the start scale toward its normal size, around the chosen origin. |
| Cascade | Each visible widget panel slides in and fades in, one after another, in the chosen panel order. |
| Ripple | Panels pop in from the start scale in a wave that spreads out from the chosen origin. |
| Spotlight | A growing circle reveals the dashboard from the chosen origin. |
| Curtain | The dashboard opens from the middle line outward: left/right directions open sideways, up/down open vertically. |

Options (only those used by the selected effect are shown): play the first time each session or every time StartPage opens; duration (100-3000 ms); start delay; delay between panels; easing (Smooth, Gentle, Snappy, Overshoot, Bounce, Elastic); panel order (reading, top/bottom, left/right, center outward, edges inward, random); direction; origin; movement distance; start scale; fade in the background; skip on click or key press. **Preview startup animation** plays it on the open page.

The dashboard content stays hidden until the page's widgets finished loading (at most six seconds, longer while an intro covers it). Page switches, leaving StartPage, input or a failure finish it immediately, and every opacity, transform and clip it touches is restored exactly; bound values such as the global background opacity keep their bindings. It follows **Follow Windows animation preference**.

Intro and reveal settings other than page intros are global and are not part of dashboard profiles, like page transitions. Enable Debug logging to record `Startup intro:` and `Startup animation:` diagnostics lines.
