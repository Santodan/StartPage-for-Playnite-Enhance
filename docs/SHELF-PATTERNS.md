# Shelf pattern filters

Use **Shelf settings ? Advanced rules ? Pattern filters** to build shelves from game names, installation directories, launch actions, and metadata. Expand the field you want and enter patterns in its **Include** and/or **Exclude** box. The **?** button opens this guide on GitHub.

## How rules combine

- Enter **one pattern per line**. Blank lines and surrounding whitespace are ignored.
- Matching is **case-insensitive**, independent of your Windows language.
- A blank box adds no restriction.
- Within one Include box, **any line** can match. For a multi-value field, **any recorded value** can match.
- Different active fields **must all match**. For example, an included directory and an included publisher both apply.
- **Any exclusion wins**, even if an inclusion also matches.
- These restrictions always apply alongside the normal filters, date/activity rules, and ignored-game settings. The **All groups / Any group** selector applies to the selected ID groups under Filters; it does not loosen pattern restrictions.
- **Reset filters** clears all pattern boxes.
- Input is saved and applied after a short typing delay. Invalid expressions display an error beneath the box and match no games until corrected. Regular expressions have a short timeout; a timed-out game is omitted, including for exclusion rules.

## Simple wildcards

Without a prefix, patterns match the **whole value**. Everything except `*` and `?` is literal, including brackets and parentheses.

| Pattern | Meaning |
| --- | --- |
| `*` | Any nonempty value |
| `*Zelda*` | Contains Zelda |
| `Final Fantasy*` | Starts with Final Fantasy |
| `*Edition` | Ends with Edition |
| `DOOM` | Exactly DOOM |
| `DOOM ?` | DOOM followed by a space and exactly one character |
| `Space Quest [IV]` | Exactly that title, including the brackets |

`*` matches zero or more characters. `?` matches exactly one character. Wildcards do not match an absent/blank value; use `missing:` for that.

### Directory and path matching

Use a path followed by a separator and `*` to include everything beneath that directory:

```text
D:\Games\*
```

This includes `D:\Games\Example` and `D:\Games\Steam\Example`, but not `D:\GamesOther\Example`. A star crosses directory boundaries; there is no separate `**` syntax. Forward and backward slashes are equivalent for plain wildcard and `glob:` path patterns. They remain exact characters for `regex:`, `literal:`, `contains:`, `starts:`, and `ends:` patterns.

Paths match **the strings recorded in Playnite**. They do not scan your drives, expand environment variables or Playnite variables such as `{InstallDir}`, resolve shortcuts, or resolve relative paths. A bare directory without `*` matches only that exact recorded directory, including its trailing separator if present.

## More precise patterns

A prefix at the start of a line chooses a different matching method. Prefixes are case-insensitive. Unrecognized prefixes are treated as ordinary wildcard text.

| Prefix | Example | Meaning |
| --- | --- | --- |
| `contains:` | `contains:Quest` | Contains the literal text Quest |
| `starts:` | `starts:Super` | Starts with the literal text Super |
| `ends:` | `ends:Edition` | Ends with the literal text Edition |
| `literal:` | `literal:What?` | Exact literal text, including the question mark |
| `glob:` | `glob:*[0-9]*` | Extended wildcards with character sets and ranges |
| `regex:` | `regex:^(Doom|Quake)\b` | A .NET regular expression; searches within the value unless anchored |
| `missing:` | `missing:` | No nonblank value is recorded for this field |

Use `literal:` to match text that would otherwise be recognized as a prefix. Use `glob:` or `regex:` when significant leading/trailing spaces are needed. Each line is limited to 2,048 characters.

### Extended wildcards (`glob:`)

Extended wildcards match the whole value and support ordinary `*` and `?`, plus:

| Pattern | Meaning |
| --- | --- |
| `glob:*[0-9]*` | Contains a digit |
| `glob:Game [ABC]` | Game A, Game B, or Game C |
| `glob:Game [!0-9]` | Game followed by one character that is not a digit |
| `glob:Game [A-Z]` | Game followed by a letter; matching ignores case |
| `glob:*[?]` | Ends in a literal question mark |
| `glob:*[*]*` | Contains a literal asterisk |

Ranges use `-`; put it first or last in a set to match a literal hyphen. `!` at the start of a set negates it. Empty, unclosed, or invalid sets display an error. For literal bracket-heavy text, use a plain wildcard pattern or `literal:` instead. Backslashes remain path separators, not wildcard escape characters.

### Regular expressions (`regex:`)

Regular expressions are optional. They use .NET syntax with case-insensitive, culture-independent matching. `.` also matches newlines. Unlike wildcards, a regex searches within a value unless you anchor it.

```text
regex:^(Doom|Quake)\b
regex:\b(remaster|remastered|remake)\b
regex:^D:\\Games\\(Steam|GOG)\\
```

In regex, escape a Windows backslash as `\\`. Do not copy wildcard syntax directly into a regex: use `.*` for any characters and `.` for one character. Patterns have a 25 ms timeout per value to keep a slow expression from blocking shelf filtering. Prefer simple patterns; avoid nested repetitions such as `(a+)+`.

### Missing metadata

Put `missing:` in Include to show only games without that field, or in Exclude to require a recorded value. You can combine it with other patterns on separate lines:

```text
missing:
*Valve*
```

For Developers, this includes games with no recorded developers **or** a developer whose name contains Valve. A collection is missing when it has no nonblank names/paths. A source with no source assigned is missing; the displayed fallback label/icon Playnite does not create a source record.

## Available fields

| Field | What is matched | Example |
| --- | --- | --- |
| Game names | The game's display name | `*Final Fantasy*` |
| Installation directories | Recorded installation directory | `D:\Games\*` |
| Sorting names | Recorded sorting name, without falling back to display name | `The Witcher*` |
| Launch paths | Paths in recorded custom game actions, including additional actions | `*retroarch.exe` |
| Launch arguments | Arguments and additional arguments in recorded custom game actions, tested separately | `*-fullscreen*` |
| Working directories | Working directories in recorded custom game actions | `D:\Emulators\*` |
| Launch action names | Names of recorded custom game actions | `*Vulkan*` |
| ROM paths | Recorded ROM file paths | `*.nes` |
| Source names | Assigned source name | `*Steam*` |
| Platform names | Any assigned platform name | `Nintendo*` |
| Tag names | Any assigned tag name | `Backlog*` |
| Category names | Any assigned category name | `*Co-op*` |
| Genre names | Any assigned genre name | `*Strategy*` |
| Feature names | Any assigned feature name | `*Controller*` |
| Developer names | Any assigned developer name | `*Valve*` |
| Publisher names | Any assigned publisher name | `*Bethesda*` |
| Series names | Any assigned series name | `*Zelda*` |
| Completion status names | Assigned completion status name | `*Unplayed*` |
| Region names | Any assigned region name | `*Europe*` |
| Age rating names | Any assigned age rating name | `*PEGI*` |
| Versions | Recorded game version | `1.2.*` |

Metadata-name rules automatically pick up newly added matching metadata. They complement the existing filters that select specific metadata entries by ID.

Launch rules inspect **recorded custom actions**, including additional/non-play actions. They do not ask library or emulator plugins to generate actions, resolve an emulator executable/profile, or execute anything. A game launched solely through a library plugin may have no recorded action path. Argument rules inspect Arguments and AdditionalArguments separately. Multiple action-related fields can match different actions; they do not require the same action to satisfy every field.

## Shelf recipes

### Games on one drive, excluding a folder

Installation directories ? Include:

```text
D:\Games\*
```

Installation directories ? Exclude:

```text
D:\Games\Demos\*
```

### Nintendo ROMs, excluding prototypes

Platform names ? Include:

```text
Nintendo*
```

ROM paths ? Include:

```text
*.nes
*.snes
*.sfc
```

Tag names ? Exclude:

```text
*Prototype*
```

### A series without remakes or demos

Series names ? Include:

```text
*Final Fantasy*
```

Game names ? Exclude:

```text
*Remake*
*Demo*
```

### Games missing developer metadata

Developer names ? Include:

```text
missing:
```

### Steam backlog from selected publishers

Source names ? Include:

```text
Steam
```

Publisher names ? Include:

```text
*Bethesda*
*Devolver*
```

Enable **Never played only** in Advanced rules. Both source and publisher restrictions must match, and either listed publisher pattern is accepted.

## Troubleshooting

- **No games:** expand the active fields, check for validation errors and contradictory include/exclude rules, and check the normal filters too.
- **Directory does not match:** check Playnite's recorded installation directory. Many uninstalled games have no path. Add a separator before the final `*` to select a directory's contents.
- **Executable does not match:** the launch may be generated by a library/emulator plugin instead of stored as a custom action.
- **Brackets behave unexpectedly:** plain patterns treat brackets literally; only `glob:` enables character sets.
- **Literal `?` or `*`:** use `literal:` for an exact value, or `glob:` with `[?]` / `[*]` inside a longer pattern.
- **Metadata does not match:** patterns use the recorded metadata names, not translated display fallbacks or the library plugin's name.
- **GitHub guide is unavailable:** the guide must exist on the repository's `master` branch. Local builds can include this help button before the document is committed and pushed.

## Typing in include/exclude dropdowns

For platform, genre, category, tag, feature, completion status and source selectors, type an existing name and press **Enter** to toggle that filter without opening the dropdown. **Ctrl+Delete** removes the matched filter. Matching ignores case and surrounding whitespace. A partial name works only when it identifies one item; ambiguous or unknown names do not change selections. Plain Delete/Backspace still edit the text. These selectors choose existing library metadata; wildcard expressions belong in the pattern fields described above.
