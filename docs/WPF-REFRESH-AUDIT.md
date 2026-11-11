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
