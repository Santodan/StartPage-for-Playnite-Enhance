# Dashboard transitions and custom presets

Open **Manage dashboard pages > Dashboard transitions** from the background menu or a toolbar page-management button.

## Built-in transitions

Choose None, Fade, Slide, Slide and fade, Zoom in, Zoom out, or Wipe. Built-ins use the duration above the editor (50-1000 ms). Slide, Slide and fade, and Wipe use the direction selector; Automatic follows page order. All transitions honor **Follow Windows animation preference** unless you uncheck it.

The outgoing dashboard stays opaque while the destination prepares. Animation starts after loading. Transitions do not reduce initial widget loading time.

## Create and test without writing a file

1. Select **Custom preset** as the transition.
2. Expand **Custom transition editor** and name the preset.
3. Set the custom duration and easing (Linear, Quadratic, Cubic, or Sine).
4. Expand **Incoming page** and **Outgoing page** to set their start/end opacity, horizontal/vertical movement, scale, and delay independently.
5. Click **Preview transition**. This animates the current dashboard without navigating. Switch to another page afterward to test it with different content.
6. Click **Save preset** to store an independent copy. Select it in **Saved transition presets**, then **Load into editor** to use it later. Deleting a saved preset leaves the current editor configuration intact.

Movement is a percentage of dashboard width/height, from -100 to 100. Negative X moves left; negative Y moves up. Scale 1 is normal size, 0.9 is 90%, and 1.1 is 110%. Scale ranges from 0.5 to 1.5. Opacity ranges from 0 (transparent) to 1 (opaque). Each page's delay ranges from 0 to 500 ms. Total animation time is the custom duration plus the larger delay. **Reverse movement for previous page** reverses X/Y movement when navigating backward; it does not reverse fade or scale.

Custom presets use their own duration and movement, independently of the built-in duration/direction fields. They animate the whole dashboard surface, not individual widgets. Global navigation preferences and saved transition presets remain local settings when applying dashboard profiles.

## JSON files: optional sharing and external editing

JSON is the portable preset format, not a requirement for customization. The editor and saved presets are stored in StartPage's settings automatically when settings/pages are saved. There is no watched folder or required installation directory for JSON files.

- **Export editor JSON...** opens a Save dialog. Choose any folder and a `.json` filename.
- Edit that file in VS Code or another text editor, or share it with another user.
- **Import JSON...** selects a file, automatically adds it to Saved transition presets, and loads an independent copy into the editor. It selects Custom preset. Existing names receive a numbered suffix. Click Preview to test it; no separate Save preset action is required.
- Editing an exported file does not update Playnite automatically: import it again to test the changes.

A working example is [Gentle slide](transition-presets/gentle-slide.json). Copy it anywhere, import it into the editor, and preview it. The file structure is:

```json
{
  "Format": "StartPage.TransitionPreset",
  "Version": 1,
  "Preset": {
    "Name": "Gentle slide",
    "DurationMs": 220,
    "Easing": "Cubic",
    "ReverseOnPrevious": true,
    "Incoming": {
      "StartOpacity": 0, "EndOpacity": 1,
      "StartScale": 0.98, "EndScale": 1,
      "StartX": 8, "EndX": 0,
      "StartY": 0, "EndY": 0,
      "DelayMs": 0
    },
    "Outgoing": {
      "StartOpacity": 1, "EndOpacity": 0,
      "StartScale": 1, "EndScale": 1,
      "StartX": 0, "EndX": -8,
      "StartY": 0, "EndY": 0,
      "DelayMs": 0
    }
  }
}
```

Import validates the format, version, fields, numeric ranges and size (64 KiB maximum) before replacing editor data. Invalid files show an error in the editor. The format describes animation parameters; it does not run scripts or load arbitrary WPF markup.
