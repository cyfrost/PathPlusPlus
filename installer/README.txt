Path++ - manage PATH and environment variables on Windows 10/11
=================================================================

Explorer
  Right-click a folder, a folder's background, a drive or a file and choose
  "Path++". On Windows 11 the menu is under "Show more options" (Shift+F10).

PATH editor
  Start menu > Path++. Edit your user PATH and the system PATH side by side,
  spot missing folders and duplicates, reorder, and save.

Command line (open a new terminal after installing)
  pathpp add <folder> [--machine] [--prepend]
  pathpp remove <folder> [--machine | --all]
  pathpp list
  pathpp help

Safety
  - User-scope changes never need administrator rights.
  - System-wide changes ask for approval once, only for that change.
  - %VARIABLES% in PATH are preserved (REG_EXPAND_SZ).
  - Every change is backed up first: Path++ > Backups, or "pathpp backups".
    User backups:   %LOCALAPPDATA%\Path++\Backups
    System backups: %ProgramData%\Path++\Backups

Project page: https://github.com/cyfrost/PathPlusPlus
