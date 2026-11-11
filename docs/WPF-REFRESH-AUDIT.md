# WPF refresh audit

Reviewed dashboard navigation, profiles, toolbar command bindings, background settings, shelf settings, and widget metadata refresh paths on 2026-11-11.

## Confirmed fixes

- Requery commands when page loading finishes, the selected page/profile changes, or delete confirmation is opened/cancelled. Bound buttons update without waiting for mouse/keyboard input.
- Observe page collection changes, including moves, so previous/next availability follows the current page order.
- Requery the next-background command when its source/path changes.
- Transfer page/settings observers when settings or the page collection are replaced, including profile application and settings-dialog cancellation.
- Detach background observers from the previous settings object before attaching the replacement.
- Transfer the shelf widget's settings listener when its settings object is replaced; detach it when the widget closes.
- Detach individual shelves from old filter settings when replacement settings arrive, rather than retaining old refresh callbacks.

## Verification

`tests/Test-DashboardPages.ps1` exercises actual WPF buttons without simulated mouse input for navigation, profile selection, delete confirmation/cancellation, page ordering and background commands. It also verifies exactly one background/shelf subscription after replacement and no retained shelf listener after closing.

Existing regression coverage checks toolbar refresh preserves selected extension items, live toolbar settings/group changes, page/widget lifecycle, profile application, metadata progress updates without hover, and settings serialization.

This is an audit of StartPage-owned refresh paths. External extension controls still own their command/data notifications; their internal behavior is not changed here. No periodic command polling or transition animations were added.


## Notifications and performance diagnostics

The toolbar Notifications panel item now opens StartPage's notifications, with a live count on the bell. Minimize closes the entire panel, and opening an empty panel displays "No notifications". This avoids keeping a second minimized popup alongside a bell wired to a different panel.

Settings overlays have their own Close button in the header and a larger top margin to distinguish them from Playnite's window controls.

Enable **Extensions > StartPage > Debug > Enable StartPage debug logging**, reproduce the issue, then use **Open log folder**. Leave the option enabled and restart Playnite to capture startup. The full path is shown in the Debug tab. For this installation the file is:

`E:\Programs\Playnite\ExtensionsData\a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5\diagnostics\startpage-debug.log`

The dedicated log captures all messages sent through StartPage's logger at Trace, Debug, Info, Warn and Error levels, including exception details, alongside lifecycle callbacks, widget initialization/open timings, background and dashboard timings, shelf refresh/cache hits and transition completion. It forwards messages to Playnite's normal logger as before.

Logging is opt-in. Files rotate at 4 MiB and retain one previous file. Disabling it stops dedicated logging. Old dashboard-only logs are left intact. This does not capture other extensions' internal logs or generate a dump of every method call. Existing messages can contain local paths, game/page names and exception details; review a log before sharing it.


## Supplied Library-to-Backlog trace

The user's 2026-11-11 12:18 trace showed 11,772 ms total for the first Library-to-Backlog switch. Background selection took 78 ms, but the next continuation after a Background-priority dispatcher yield did not execute until 10,455 ms. Outgoing widget hiding then completed in the same millisecond. Destination shelf work completed in about 640-655 ms and the final transition added about 218 ms. This isolates the largest observed stall to the pre-swap dispatcher wait, rather than background image decoding.

The pre-swap yield has been removed so the outgoing page can detach immediately. The presentation yield before animation now runs at Loaded priority instead of waiting behind input and render work at Background priority. Logs distinguish background application, layout attachment/initialization and transition presentation wait; redundant timing entries were removed. A regression asserts that a completed outgoing widget's hide callback runs before yielding the switch to the dispatcher. New installed-build measurements are still needed to quantify the improvement.
