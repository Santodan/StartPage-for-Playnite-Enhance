# Monitor reconnect and window sizing

Enable **StartPage settings > Debug > Enable StartPage debug logging** to record main-window bounds, native pixels, DPI scale, monitor/work area, state/restore bounds, SizeToContent, sizing constraints and display/power messages. The observer remains attached when another Playnite page is visible. Logs use the existing 4 MiB rotation.

Use **Open log folder** and share `startpage-debug.log`. For this portable installation the path is `E:\Programs\Playnite\ExtensionsData\a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5\diagnostics\startpage-debug.log`. The Debug tab shows the actual path for other installations.

## Startup enlargement safeguard

The November 14 log showed Playnite initially restoring a normal 1946x1091 window, then changing to a normal 2560x1440 window about seven seconds later, without a DPI change or recorded manual resize. The originating component has not been identified.

**Common settings > Preserve restored window size during startup** is enabled by default. It remembers the normal window bounds already restored by Playnite at application start. For the first 30 seconds, if that window automatically grows to exactly the monitor bounds, it restores the initial bounds once. It does not edit `windowPositions.json` or keep a second placement file.

The safeguard does not apply to startup in a maximized/full-monitor window, changes after 30 seconds, a different monitor/DPI, or changes after mouse/keyboard/window-management input. Display/DPI changes cancel it. It never forces a maximized or minimized window into normal state. It works independently of Debug logging and can be disabled in Common settings. A correction is recorded as `Startup window safeguard` when logging is enabled.

For monitor off/on issues outside startup, capture one test on StartPage and another on the regular library page. The startup safeguard intentionally does not override later display changes; those remain diagnostic until their cause is established.
