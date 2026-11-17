# Animated backgrounds

Choose **Dashboard pages > Page background > Image / GIF / video...**, or browse from the extension's Background settings tab. GIFs and videos are also included when selecting a background folder. A folder containing a single supported file keeps that file instead of falling back to a game image.

Expand **GIF / video playback** for:

- Play/pause animation, looping and restart.
- Playback speed, from 0.25x to 2x.
- Pause when Playnite loses focus, enabled by default. Minimized, hidden and fully transparent backgrounds do not keep playing; changing pages releases outgoing sessions.
- Video mute and volume. Videos start muted by default.
- GIF maximum frame rate (default 24 FPS) and decoded dimension (default 1920 pixels, with an Original resolution option).

Framing, flips, opacity, blur, tint, edge fades and vignette apply to animated backgrounds too. **Keep background out of Playnite sidebar** also applies to GIFs/videos when using the global background; it fits all layers inside the native content area. Original size uses native media dimensions, even when a GIF's decoded output is limited for performance. Lowering GIF resolution can therefore reduce sharpness when the result is enlarged; choose Original resolution for maximum detail. Appearance/playback settings are independent per page and saved in profiles. Media files themselves are referenced, not embedded in profiles.

## Performance

Still images remain the lightest option. GIFs require CPU frame decoding/compositing and pixel uploads; higher dimensions and frame rates increase the work. StartPage reuses a single GIF output bitmap and pixel buffer rather than retaining every decoded frame, skips frames to respect its rate limit, and waits without rendering while paused. The native decoder and compressed source also consume memory. Very large GIFs are rejected safely: the source limit is 64 MiB, at most 16 million logical pixels, 8192 pixels per dimension and 5000 frames.

Videos use WPF's Windows media pipeline. MP4/H.264 is a practical starting point for larger animations; actual resource use and playback support depend on the file, Windows codecs and hardware. The picker also accepts M4V, WMV, AVI, MOV, MKV and WebM, but a container extension does not guarantee that its video codec is installed. Prefer shorter, moderate-resolution files and keep pause-when-inactive enabled. Video decoding resolution is controlled by the source/Windows decoder; the GIF resolution slider does not downscale video decoding.

GIF timing, one-shot playback and loop/restart are supported. Remote HTTP/HTTPS GIFs are downloaded with size/time limits, and Windows media playback can open supported remote video URLs. Sites requiring authentication may not work.

## Troubleshooting

Enable **Settings > Debug**, then reproduce the issue. Open the log folder from that tab. The dedicated file is normally:

`<Playnite configuration folder>/ExtensionsData/a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5/diagnostics/startpage-debug.log`

Logs include media readiness, decoded GIF dimensions/buffer size, playback state and periodic GIF average/maximum frame decode timings. Invalid media and missing codecs produce an error message in the background and a logged warning rather than an unhandled playback exception.

This implementation covers dashboard/global backgrounds. Animated media inside the separate Image widget remains a roadmap task.
