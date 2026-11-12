# Game shelf hover effects

Open an individual Game Shelves shelf's settings menu and expand **Hover animations**. Settings apply to that shelf and persist in dashboard profiles.

- **Classic:** the previous subtle 5% hover zoom (the default).
- **None:** no movement or magnification; optional glow/dimming still work.
- **Dock magnification:** cards near the pointer grow smoothly, with a falloff across neighboring games. The focused item is raised above nearby cards. Horizontal rows use horizontal distance; grids/lists use two-dimensional proximity.
- **Zoom:** enlarge the hovered card.
- **Lift:** enlarge and raise it.
- **Tilt:** enlarge it and rotate slightly toward the pointer.
- **Bounce:** a short overshoot before settling into the enlarged/raised state.

Set maximum magnification (1-1.5), dock reach in card widths (0.5-4), lift distance, tilt angle, response duration and Smooth/Linear/Spring easing. Classic uses the original 1.05 scale and 100 ms response.

Optional **Dim other games** emphasizes the active card; dock mode fades this emphasis by proximity. **Glow** adds a configurable colored outline/shadow around the focused card, with a color picker and bounded radius.

**Allow enlarged artwork outside the widget** draws enlarged cards in a dashboard overlay. Normal card sizes, shelf dimensions and spacing stay unchanged. Artwork can extend over nearby widgets, while remaining within the dashboard window. Settings and notification panels remain above it. Disable this option to keep the animation inside the normal shelf drawing area. The whole card, including metadata, scales together.

Windows animation preferences are respected by default: disabling animations changes focus size/appearance instantly instead of animating it. Uncheck that option to explicitly enable motion. Close the settings menu and hover games to try changes. **Reset hover animations** restores Classic and disables glow/dimming.

Effects operate on realized WPF item containers and animate render transforms/opacity. They do not change game filters or query the library. Neighbor updates are coalesced to at most one update per WPF render frame, and original transforms, opacity, effects, stacking order and padding are restored when containers unload. Glow is optional because it adds rendering work.

Color pickers are grouped under collapsed **Colors** sections in the appearance editors, including hover glow colors.

Grouped shelves keep their normal header and card layout. Cover-row artwork uses the configured cover width and aspect ratio. Hover effects never shrink cards or reserve extra layout space.

Disable **Show game details popup on hover** in an individual shelf's settings to hide the floating game information panel. The option does not remove card metadata, play/info buttons, or hover animations. The global shelf details preference still applies when this option is enabled.

For stuttering, enable logging in extension **Settings > Debug**, hover across several games, then move the pointer outside the shelf. The existing `diagnostics/startpage-debug.log` includes `Shelf hover performance` summaries: frame gaps over 34 ms, maximum frame gap, update CPU time, input queue delay, focus changes, overlay creation/removal counts and WPF rendering tier. `Shelf hover enter` records detail/background update time. Frame samples stay in memory; summaries are written after leaving the shelf, without per-frame disk writes. Compare a run with the details popup enabled to one with it disabled.

Hovering a game updates the background only when its source is **Last hovered**. Disabling the details popup also skips updating its target and metadata model. Diagnostic frame counts ignore duplicate WPF callbacks with the same rendering timestamp.

**Animation direction** selects **Up** for a shelf at the bottom or **Down** for one at the top. Magnification grows from the bottom or top edge respectively, and Dock/Lift/Bounce movement follows that direction. Direction is saved per shelf and in profiles; reset restores Up.
