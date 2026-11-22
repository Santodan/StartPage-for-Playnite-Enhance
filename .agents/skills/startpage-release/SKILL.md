---
name: startpage-release
description: Release checklist for StartPage-for-Playnite-Enhance - which files carry the version, the InstallerManifest changelog format, README badge/install path, and where the .pext is produced. Use only when the owner explicitly asks to prepare a release or bump the version.
---

# Release (only when asked)

Versioning: experimental `0.x.0` (e.g. `0.21.0`); prereleases are offered by the update checker until 1.0.0.
Reference commit: `git show 5fdb7a1` ("Prepare experimental 0.21.0 release") plus the version lines of
`e8a6f45`.

## Files to update together

| File | Change |
| --- | --- |
| `source/StartPage/extension.yaml` | `Version: X.Y.Z` |
| `source/StartPage/Properties/AssemblyInfo.cs` | `AssemblyVersion` and `AssemblyFileVersion` `"X.Y.Z.0"` |
| `InstallerManifest/santodan_StartPage_Enhanced_Plugin.yaml` | New entry at the **top** of `Packages:` (below) |
| `README.md` | "Latest release downloads" badge tag `vX.Y.Z`; install step path `santodan_StartPage_Enhanced_Plugin_X_Y_Z.pext` |
| `CUSTOMIZATION-ROADMAP.md` | Move finished items if the owner tracks them there |

```yaml
  - Version: X.Y.Z
    RequiredApiVersion: 6.1.0
    ReleaseDate: YYYY-MM-DD
    PackageUrl: https://github.com/Santodan/StartPage-for-Playnite-Enhance/releases/download/vX.Y.Z/santodan_StartPage_Enhanced_Plugin_X_Y_Z.pext
    Changelog:
      - Added <feature, user-facing wording>.
      - Fixed <bug, user-facing wording>.
```

Changelog bullets: one line each, past tense, start with Added/Improved/Fixed, no internal class names.
Draft them from `git log --format=%s <previous-release-commit>..HEAD`.

## Build the package

Release build runs `Toolbox.exe pack` (post-build event in `StartPage.csproj`, looks in
`%LocalAppData%\Playnite` then `E:\Programs\Playnite`) and writes
`source/StartPage/bin/santodan_StartPage_Enhanced_Plugin_X_Y_Z.pext`. Build + full tests with
`startpage-recipes` before reporting the release ready. Tagging/publishing on GitHub is the owner's step.
