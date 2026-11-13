# Path++

[![build](https://github.com/cyfrost/PathPlusPlus/actions/workflows/build.yml/badge.svg)](https://github.com/cyfrost/PathPlusPlus/actions/workflows/build.yml)
[![license](https://img.shields.io/badge/license-MIT-orange.svg)](LICENSE)

Add folders to `PATH` and manage environment variables on Windows **safely**: from the Explorer right-click menu, a PATH editor, or the `pathpp` command.

Works on **Windows 10 and 11**. Needs .NET Framework 4.8, which is built into Windows 10 1903+ and Windows 11. The binaries are AnyCPU and contain no native shell extension, so they should also run on ARM64 devices, though that hasn't been tested yet.

## Download

Download the installer from the [`dist/`](dist/) folder, or get the installer or portable zip from the [latest release](https://github.com/cyfrost/PathPlusPlus/releases/latest).

- **Just for me** (default): no administrator rights needed. Installs to `%LOCALAPPDATA%\Programs\Path++`.
- **All users** (administrators only): installs to `C:\Program Files\Path++`.

> Upgrading from Path++ 0.1 (2018)? Uninstall it first from *Settings → Apps*, then sign out and back in so its old Explorer extension is unloaded.

## Using it

### Explorer

Right-click a folder, the background of a folder, a drive, or a file, and choose **Path++**:

| On a folder | On a file |
|---|---|
| Add to PATH… | Add containing folder to PATH… |
| Remove from PATH… | Remove containing folder from PATH… |
| Create environment variable… | Create variable from this file's path / containing folder… |
| Open PATH editor | Open PATH editor |
| Windows Environment Variables… | Windows Environment Variables… |

On Windows 11 the menu is under **Show more options** (or press <kbd>Shift</kbd>+<kbd>F10</kbd>). Windows 11 only puts entries from MSIX-packaged apps in the compact menu.

### PATH editor

Start menu → **Path++**. Your PATH and the system PATH appear side by side. Missing folders, duplicates, unknown `%variables%` and entries present in both are flagged. You can add, edit, remove, reorder and clean up entries, then **Save**. Saving the system PATH asks for administrator approval once.

### Command line

Open a new terminal after installing:

```text
pathpp add <folder> [--machine] [--prepend] [--force]
pathpp remove <folder> [--machine | --all]
pathpp list [--user | --machine] [--plain]
pathpp dedupe [--machine]
pathpp get|set|unset <NAME> [value] [--machine]
pathpp vars [--machine]
pathpp backups
pathpp restore <number | file>
```

`pathpp add .` adds the current folder. Commands are idempotent, so adding something that's already there is a no-op. Exit codes: `0` ok, `1` failed, `2` invalid input, `3` not found, `4` cancelled, `5` not permitted, `6` changed by another program.

## Safety guarantees

- **`%VARIABLES%` keep working.** PATH is always written as `REG_EXPAND_SZ`, and entries you didn't touch are kept byte-for-byte.
- **Changes apply immediately to new programs.** Path++ broadcasts `WM_SETTINGCHANGE`, the same as the Windows dialog.
- **No admin for your own PATH.** Only system-wide changes trigger UAC, and only for that one change.
- **Right account.** If Path++ runs as a different account than the person signed in, for example after entering an IT admin's credentials, it refuses user-scope changes rather than editing the wrong profile.
- **Backups before every change.** Restore them from *Backups…* in the editor or with `pathpp restore`. User backups are stored in `%LOCALAPPDATA%\Path++\Backups`, system backups in `%ProgramData%\Path++\Backups`, which only administrators can modify.
- **No lost updates.** The editor refuses to save if another program changed PATH after it was loaded.
- **Nothing runs inside Explorer.** The menu is plain registry verbs with no shell-extension DLL.

## Building

Requirements: Visual Studio 2022+ or the .NET SDK, plus [NSIS 3](https://nsis.sourceforge.io/) for the installer. The .NET Framework 4.8 reference assemblies are restored from NuGet.

```powershell
.\build.ps1                 # build, run tests, create artifacts\Path++-Setup-<version>.exe and a portable zip
.\build.ps1 -VerifyFormat   # also fail if 'dotnet format' would change anything (what CI runs)
.\build.ps1 -UpdateDist     # also refresh the installer kept in dist\
```

| Path | Output | Purpose |
|---|---|---|
| `src/PathPlusPlus.Core` | `PathPlusPlus.Core.dll` | Registry access, PATH parsing and matching, backups, elevation |
| `src/PathPlusPlus.App` | `PathPlusPlus.exe` | PATH editor, Explorer dialogs, elevated helper, menu registration |
| `src/PathPlusPlus.Cli` | `pathpp.exe` | Command-line tool |
| `tests/PathPlusPlus.Tests` | | xUnit tests (`dotnet test`) |
| `installer/` | | NSIS installer script |
| `assets/` | | Icon shared by the executables and the installer |
| `dist/` | `Path++-Setup-<version>.exe` | The current installer, kept in the repository |

See [CONTRIBUTING.md](CONTRIBUTING.md) for coding standards and [CHANGELOG.md](CHANGELOG.md) for release notes.

## License

Copyright (c) 2018-2026 cyfrost. Licensed under the [MIT License](LICENSE).
