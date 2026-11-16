# Fork identity and compatibility

- Name: **StartPage-for-Playnite-Enhance**
- Maintainer: **Santodan**
- Package ID: `santodan_StartPage_Enhanced_Plugin`
- Development version: **0.16.0** (not a stable release).
- Package: `santodan_StartPage_Enhanced_Plugin_0_16_0.pext`.

## Replacing the original

Disable the original StartPage before enabling this fork. They share runtime plugin GUID `a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5` and must not run together. Back up Playnite settings before switching; do not delete the original extension data. Keeping this GUID preserves saved dashboard provider references and the existing extension settings location.

The package ID and display name identify the fork; they do not replace the runtime GUID used by Playnite and dashboard layouts.

## External widgets

StartPage discovers loaded plugins implementing `StartPage.SDK.IStartPageExtension`. Providers return their views through that interface; they do not send widgets to the package name. The public SDK assembly names, interfaces and versions are preserved, as are the built-in provider GUID and internal StartPage catalog name. Existing compatible providers should continue to appear without recompilation.

A third-party extension that explicitly checks the original textual package ID or installation directory may need its own update. Such checks are outside the standard SDK contract; compatibility with every provider cannot be guaranteed without testing it in Playnite.

Original SDK and project authorship remain credited to felixkmh. The MIT license attribution is retained.

## Release metadata

The add-on and installer manifests belong to this fork. The current development package is 0.16.0, matching roadmap step 16. Its installer URL targets the `v0.16.0` experimental prerelease in Santodan/StartPage-for-Playnite-Enhance. This corrects the initial release numbering to match the implemented roadmap step. Building locally does not publish a release or register the add-on in the Playnite catalog.

## Importing an original configuration

Open the extension settings and choose **Import from original StartPage ? Choose original config.json...**. The tab shows the usual file location: `<Playnite ExtensionsDataPath>/a6a3dcf6-9bfe-426c-afb0-9f49409ae0c5/config.json`. Portable Playnite normally keeps this under its `ExtensionsData` directory; installed Playnite uses its configured user data directory.

The importer creates a saved profile containing the original single-page panel layout, shelf instances and filters, background choices, Most Played options, achievement limits, and shared appearance settings. It does not replace the current dashboard or modify the selected file. Save the extension settings to keep the imported profile; Cancel discards it. Open **Dashboard profiles** from StartPage's right-click menu to preview it, add its page, or apply it using the existing automatic backup behavior.

This fork uses the same settings location as the original, so settings might already have migrated automatically. For an earlier original setup, select a backup of its config rather than the currently active fork config. Background artwork files are referenced rather than copied; external widgets require their original extensions, and game/tag IDs assume the same library. Game data, external provider data, startup preferences and legacy layout presets are not imported by this dashboard importer. Unrelated JSON, malformed files and invalid layouts are rejected before a profile is added.
