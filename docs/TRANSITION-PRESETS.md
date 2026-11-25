# Dashboard transitions and custom presets

Open **Manage dashboard pages > Dashboard transitions** from the background menu or a toolbar page-management button. Each transition can also play an [image, GIF or video](#gif-or-video-during-the-transition).

## Global and per-page transitions

The **global transition** plays when you switch to any page that does not have its own. Edit it in the extension settings (**Transitions** tab, where **Preview transition** plays it in a box between two sample pages; **Stop** ends it) or in **Manage dashboard pages > Dashboard transitions** while the selected page has no transition of its own.

To give a page its own transition, select the page and turn on **Use its own transition when switching to this page**. The first time, it starts as a copy of the global transition; the editor then changes only that page. It plays when you switch *to* that page.

- **Copy from global** replaces the page's transition with a copy of the global one.
- **Copy to global** makes the page's transition the global one, so every page without its own uses it too.
- **Use on all dashboards** makes the transition shown in the editor the global one and turns off every page's own transition, so all dashboards use it. Pages keep their own settings, so turning their option back on restores them.

A page's own transition, including its GIF or video, is saved with the page, so dashboard profiles carry it. The global transition, the saved presets and **Follow Windows animation preference** are global and are not part of profiles. Existing configurations keep their transition as the global one, and every page uses it until you give one its own.

## Built-in transitions

Choose None, Fade, Slide, Slide and fade, Zoom in, Zoom out, or Wipe. Built-ins use the duration above the editor (50-1000 ms). Slide, Slide and fade, and Wipe use the direction selector; Automatic follows page order. All transitions honor **Follow Windows animation preference** unless you uncheck it.

The outgoing dashboard stays opaque while the destination prepares. Animation starts after loading. Transitions do not reduce initial widget loading time.

## GIF or video during the transition

Expand **GIF or video during the transition** and turn on **Play an image, GIF or video when switching pages**. Choose a file, or a folder to play a random supported file on each switch (PNG, JPEG, BMP, TIFF, WebP, GIF and Windows-supported videos). It works with every transition, including custom presets and None.

**How it plays**:

| Option | What you see |
| --- | --- |
| Revealed by the effect (default) | The chosen effect uncovers the media, it plays, then the same effect uncovers the new page. With Wipe the media comes in along the wipe line; with Fade the old page fades into it; with Slide it slides in and then slides away as the page arrives. The new page stays hidden behind the media while it plays. |
| Instead of the effect | The old page fades into the media, it plays, then it fades into the new page. The effect is not used. |
| Over the effect | The media plays on top while the effect runs underneath, then fades out. It has no background color, so it suits transparent GIFs or a lower opacity. |

With the transition set to **None**, the media plays as in **Instead of the effect**.

- **Length**: **Until the video or GIF ends**, bounded by **Longest wait for the media to end**, or **Fixed time**. Still images and looping media always use the fixed time. Short clips work best: the page switch finishes when the media does.
- **If the GIF or video ends first**: when the media stays up longer than the clip (a fixed time longer than it), it can hold the last frame (default) or keep repeating its first or last part of the chosen length. Not used when **Loop** is on. **Preview transition** plays the whole clip and then the repeated part three times, whatever the length setting.
- **Fade in/out** is used by **Instead of the effect** and **Over the effect**. **Background** fills the area around media that does not cover the page.
- **Skip the media on click** ends it early; the remaining effect still plays.
- **Size**: **Fit (whole image)** (default) scales it to the page without cropping, with the background color around it; **Fill (crop edges)** covers the page and crops; **Stretch** fills the page and distorts the image; **Original size (centered)** keeps its own pixel size in the middle.
- **Framing and playback** adds zoom, position, opacity, speed, loop, mute, volume and flips. Videos are muted by default.
- The media loads, paused, while the next page builds, and starts playing when the transition starts.
- When the page you switch to has its own intro due (**Manage dashboard pages > Page intro**), that intro plays instead of the transition media.
- **Follow Windows animation preference** applies to the media as well. **Preview transition** plays it on the current page in Manage dashboard pages, and in a preview box with two sample pages in the extension settings.

The media is part of the transition: the global transition's media plays for pages without their own, and a page with its own transition has its own media settings.

## Create and test without writing a file

1. Select **Custom preset** as the transition.
2. Expand **Custom transition editor** and name the preset.
3. Set the custom duration and easing (Linear, Quadratic, Cubic, or Sine).
4. Expand **Incoming page** and **Outgoing page** to set their start/end opacity, horizontal/vertical movement, scale, and delay independently.
5. Click **Preview transition** (in Manage dashboard pages). This animates the current dashboard with the transition being edited, without navigating. Switch to another page afterward to test it with different content.
6. Click **Save preset** to store an independent copy. Select it in **Saved transition presets**, then **Load into editor** to use it later. Deleting a saved preset leaves the current editor configuration intact.

Movement is a percentage of dashboard width/height, from -100 to 100. Negative X moves left; negative Y moves up. Scale 1 is normal size, 0.9 is 90%, and 1.1 is 110%. Scale ranges from 0.5 to 1.5. Opacity ranges from 0 (transparent) to 1 (opaque). Each page's delay ranges from 0 to 500 ms. Total animation time is the custom duration plus the larger delay. **Reverse movement for previous page** reverses X/Y movement when navigating backward; it does not reverse fade or scale.

Custom presets use their own duration and movement, independently of the built-in duration/direction fields. They animate the whole dashboard surface, not individual widgets. Saved presets are shared by the global transition and every page's own transition, and remain local settings when applying dashboard profiles.

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
