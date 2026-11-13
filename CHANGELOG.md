# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-11-14

A complete rewrite for Windows 10 and 11. The full list of problems this fixes is in [docs/2026-audit.md](docs/2026-audit.md).

### Added
- **PATH editor.** Shows your PATH and the system PATH side by side. Missing folders, duplicates, unknown `%variables%` and cross-scope entries are flagged. You can add, edit, remove, reorder and clean up entries before saving.
- **`pathpp` command-line tool** with `add`, `remove`, `list`, `dedupe`, `get`, `set`, `unset`, `vars`, `backups` and `restore`, and meaningful exit codes.
- **Automatic backups** before every change, with restore from the editor or the CLI.
- **Installer** with a no-admin "just for me" install or an all-users install, optional Explorer menu, optional `pathpp` on PATH, and a clean uninstall.
- **Explorer menu** on drives and folder backgrounds as well as folders and files.

### Fixed
- PATH was rewritten as `REG_SZ`, which broke `%SystemRoot%`, `%USERPROFILE%` and similar entries. It is now always `REG_EXPAND_SZ`.
- Changes didn't reach new terminals until sign-out. Path++ now broadcasts `WM_SETTINGCHANGE`.
- Every action triggered a UAC prompt, and approving it with another account's credentials changed that account's PATH. User-scope changes no longer need admin, and Path++ refuses them when running as a different account.
- Removing a folder failed when its case or trailing backslash differed from the PATH entry.
- Drive roots (`C:\`) and parent folders were passed incorrectly from Explorer.
- A console or Windows Terminal window appeared behind every dialog.
- The CLI replaced the whole PATH, silently wrote unknown targets to user scope, broke values containing spaces when elevating, and always exited with code 0.

### Removed
- The KuShellExtension COM DLL (GPL-3.0, 2010). The menu is now plain registry verbs.
- .NET Framework 3.5 and the CommandLineParser 1.9 dependency.
- Committed `bin/`, `obj/` and `packages/` folders.

## [0.1] - 2018-01-22

Initial release.
