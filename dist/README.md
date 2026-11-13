# Path++ installer

`Path++-Setup-<version>.exe` is the current release, kept in the repository so it can be downloaded directly. The same file is attached to each [GitHub release](https://github.com/cyfrost/PathPlusPlus/releases).

- Check the download against `SHA256SUMS.txt`:
  ```powershell
  (Get-FileHash .\Path++-Setup-1.0.0.exe -Algorithm SHA256).Hash
  ```
- The installer isn't code-signed yet, so SmartScreen may show a warning. Choose *More info* → *Run anyway*.
- To install without a prompt for just the current user, run:
  ```powershell
  Path++-Setup-1.0.0.exe /S /CurrentUser
  ```

The installer is built by `.\build.ps1 -UpdateDist` from `installer/PathPlusPlus.nsi`. Don't edit files here by hand.
