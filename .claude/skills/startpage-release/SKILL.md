---
name: startpage-release
description: Release checklist for StartPage-for-Playnite-Enhance - which files carry the version, the InstallerManifest changelog format, README badge/install path, and where the .pext is produced. Use only when the owner explicitly asks to prepare a release or bump the version.
---

# Release (only when asked)

## Version number rule (owner's instruction, until v1.0.0)

- The version follows the **last completed roadmap step** in `CUSTOMIZATION-ROADMAP.md`: `0.<step>.0`.
  Finishing step 23 makes the release `0.23.0`; finishing step 24 makes it `0.24.0`. Use the step number as
  rendered in the roadmap list (the list is numbered 1, 2, 3, ... without gaps).
- Fixes or small additions released before the next step is completed bump only the patch: `0.23.1`, `0.23.2`.
- Never reuse or skip back: if a version already exists as a tag/release, bump the patch.
- `1.0.0` is the first stable release (owner decides when); after it, use normal semantic versioning.
- Tags are `vX.Y.Z`; the package is `santodan_StartPage_Enhanced_Plugin_X_Y_Z.pext`.

Experimental `0.x.y` releases are published as GitHub **prereleases** (the update checker offers them until 1.0.0).
Reference commit: `git show 5fdb7a1` ("Prepare experimental 0.21.0 release") plus the version lines of
`e8a6f45`.

## Files to update together

| File | Change |
| --- | --- |
| `source/StartPage/extension.yaml` | `Version: X.Y.Z` |
| `source/StartPage/Properties/AssemblyInfo.cs` | `AssemblyVersion` and `AssemblyFileVersion` `"X.Y.Z.0"` |
| `InstallerManifest/santodan_StartPage_Enhanced_Plugin.yaml` | New entry at the **top** of `Packages:` (below) |
| `README.md` | Install step path `santodan_StartPage_Enhanced_Plugin_X_Y_Z.pext`. The "Latest release downloads" badge uses `github/downloads-pre/.../latest/total` and follows the newest release automatically - do not put a version in it (a tag badge pushed before its release exists gets cached by GitHub as "Repo or release not found"). |
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
