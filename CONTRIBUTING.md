# Contributing to Path++

Thanks for helping! Path++ edits a part of Windows that every program depends on, so changes are held to a high bar for safety.

## Getting started

Requirements:
- Windows 10 or 11.
- Visual Studio 2022 or later, or just the .NET SDK. The .NET Framework 4.8 reference assemblies come from NuGet automatically.
- [NSIS 3](https://nsis.sourceforge.io/), only if you want to build the installer.

```powershell
git clone https://github.com/cyfrost/PathPlusPlus.git
cd PathPlusPlus
dotnet test PathPlusPlus.sln     # build and run the tests
.\build.ps1                      # full build: tests, installer and portable zip in .\artifacts
```

## Before you open a pull request

1. Run `dotnet format PathPlusPlus.sln` and commit the result. CI runs `dotnet format --verify-no-changes`.
2. Run `dotnet test PathPlusPlus.sln`. The build treats warnings as errors, with the .NET analyzers (`latest-recommended`) and the `.editorconfig` style rules on.
3. Add or update tests for any behaviour change, especially anything in `PathPlusPlus.Core`.
4. If you changed a window, check it at 100 % and 150 % display scaling.

## Coding standards

- One type per file, and the file is named after the type.
- Private fields are `_camelCase`. Constants and static readonly fields are `PascalCase`.
- Registry, PATH and elevation logic lives in `PathPlusPlus.Core` and is unit-tested against `IEnvironmentStore` fakes. The UI and CLI only call `OperationRunner`.
- Never write PATH with `Environment.SetEnvironmentVariable` or a bare `RegistryKey.SetValue(name, value)`. Both write `REG_SZ`, which breaks `%VARIABLES%`. Go through `EnvironmentService`.
- UI text, colours, spacing and button sizes come from `Ui` (`src/PathPlusPlus.App/Ui.cs`), so windows stay consistent.

## Project layout

| Path | Contents |
|---|---|
| `src/PathPlusPlus.Core` | Registry access, PATH parsing and matching, backups, elevation |
| `src/PathPlusPlus.App` | PATH editor, Explorer dialogs, elevated helper, Explorer menu registration |
| `src/PathPlusPlus.Cli` | `pathpp` command-line tool |
| `tests/PathPlusPlus.Tests` | xUnit tests |
| `installer/` | NSIS installer script |
| `assets/` | Icon shared by the executables and the installer |
| `dist/` | The released installer, kept in the repository for direct download |

## Releasing

1. Update `<Version>` in `Directory.Build.props` and add a section to `CHANGELOG.md`.
2. Run `.\build.ps1 -UpdateDist` to rebuild and refresh `dist/`.
3. Merge, then tag `vX.Y.Z`. The GitHub Actions workflow builds and attaches the installer to a GitHub release.
