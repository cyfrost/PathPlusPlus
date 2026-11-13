# Path++ — Windows 11 audit: issues and fix plan

> **Status:** this audit describes v0.1. Everything in Phases 0–7 is implemented in v1.0 (see the README). Phase 8 (top-level Windows 11 menu via a signed MSIX package) and code signing are still open. File and line links below point at the v0.1 sources as of commit caa1a31.

*Audit date: 2026-11-14. Scope: every source file, project file, installer script, bundled binary and the copy installed in `C:\Program Files\Path++`.*

## TL;DR

Path++ v0.1 is a 2018 .NET 3.5 WinForms app, plus a 2010 third‑party COM context‑menu DLL (KuShellExtension), packaged with NSIS. On Windows 11 (and on any managed "business" machine) it fails in three ways:

1. **It can quietly damage your PATH.** Every add or remove rewrites `Path` as `REG_SZ` instead of `REG_EXPAND_SZ`. Entries such as `%SystemRoot%\system32` and `%USERPROFILE%\AppData\Local\Microsoft\WindowsApps` then stop expanding. **Don't use Add/Remove from PATH until this is fixed.** Both of your PATH values are still `REG_EXPAND_SZ` today, so no damage has happened yet.
2. **Changes don't show up.** The extension writes to the registry directly and never broadcasts `WM_SETTINGCHANGE`. Explorer, and every terminal you open from it, keeps the old PATH until you sign out.
3. **Windows 11 hides it or gets in its way.** The menu is a legacy `IContextMenu` handler, so it only appears under "Show more options". Every click triggers a UAC prompt because of `requireAdministrator`. On a standard‑user business account, approving that prompt with admin credentials makes "Current user" changes land in the **admin's** profile. The console window can't be hidden when Windows Terminal is the default terminal.

The code is small: about 700 lines of real logic. **I recommend a focused rewrite of the core onto .NET Framework 4.8.1**, not patching. It ships with Windows 11, so nothing extra needs installing. Add a proper PATH store layer, per‑user registry context‑menu verbs with no COM DLL, and an installer that doesn't need admin. Detailed plan in Part 2.

---

## What I verified, and how

| Check | Result |
|---|---|
| Shipped binaries vs. source | `res/Path++.exe` and `res/pathpp_ext.exe` are byte‑identical (SHA‑256) to `bin/Release` outputs, so the report applies to what you installed |
| Installed copy | `C:\Program Files\Path++` contains exactly those binaries |
| Signing | All binaries are **unsigned**: exes, CommandLine.dll, both KuShellExtension DLLs and the setup exe |
| Runtime | Both exes target .NET 3.5 / CLR 2 (`supportedRuntime v2.0.50727`). .NET 3.5 *is* enabled on this machine, but it's an optional feature that's off by default on Windows 11 |
| PATH value kinds now | HKLM and HKCU `Path` are both `REG_EXPAND_SZ` (healthy) |
| CLI arg parsing (run with harmless `proc` target) | `-v "C:\"` → parse failure, usage dumped, exit code 0. `-t bogus` → **silently wrote to User scope**, exit 0. Missing `-t` → usage, exit 0 |

> ⚠️ Side effect of testing: the `-t bogus` probe created a `PPTEST=foo` user variable. It's been deleted and HKCU `Path` is untouched.

---

## Part 1 — Issues

Severity: **S1** = data loss or core feature broken · **S2** = feature broken in common cases · **S3** = wrong or fragile behaviour · **S4** = hygiene.

### A. PATH data integrity (Explorer extension)

| # | Sev | Issue | Where |
|---|---|---|---|
| A1 | **S1** | `RegistryKey.SetValue("Path", string)` writes **REG_SZ**, destroying `REG_EXPAND_SZ`. Any `%VAR%` entries (Windows' own `%SystemRoot%…`, `%USERPROFILE%…WindowsApps`, `%JAVA_HOME%\bin`…) stop expanding in new processes. Machine scope can break `where`, `powershell`, `ssh`, `wbem` for every user. | [PathVariableFunctions.cs:368](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L368), [:371](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L371) |
| A2 | **S1** | No `WM_SETTINGCHANGE("Environment")` broadcast after registry writes. Explorer and new terminals don't see the change until you sign out, so it looks like "nothing happened". | same |
| A3 | S2 | Add never checks for duplicates (`IsDirectoryPresentInPATH` exists but isn't called), so repeat clicks pile up copies. | [PathVariableFunctions.cs:40-108](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L40) |
| A4 | S2 | Matching is exact and case‑sensitive, with no normalisation. `C:\Tools` doesn't match `c:\tools`, `C:\Tools\` or `%USERPROFILE%\Tools`, so Remove says "No instances found" for entries that are there. | [:149](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L149), [:181](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L181), [:340](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L340) |
| A5 | S2 | The "is present" check trims each entry but the removal loop doesn't, so an entry with stray spaces is found but never removed. The result is a contradictory "No instances found". | [:340](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L340) vs [:149](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L149) |
| A6 | S3 | Remove re‑serialises the whole value and keeps empty segments (`;;`). It always adds a trailing `;` and changes the value even when only one entry was meant to go. | [:139-218](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L139) |
| A7 | S3 | No backup or undo before PATH writes. There's also no read‑modify‑write guard, so a concurrent installer change can be lost. | whole file |
| A8 | S3 | Return values of `WriteChangesToPATH` are ignored in the "both scopes" path, so "Success!" is shown even if the HKLM write failed. | [:226-246](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/PathVariableFunctions.cs#L226) |
| A9 | S3 | Every helper swallows exceptions into a MessageBox and returns `false`. Callers can't tell a failure from "not found", and errors can show twice. | throughout |
| A10 | S3 | "Add as new Environment Variable" uses `Environment.SetEnvironmentVariable`, which also writes REG_SZ, so values containing `%VAR%` never expand. Naming the variable `Path` replaces your **whole PATH** with one folder; only a warning box protects you. | [NewEnvVariable.cs:91-122](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/NewEnvVariable.cs#L91) |

### B. Windows 11 / business‑machine compatibility

| # | Sev | Issue | Where |
|---|---|---|---|
| B1 | **S1** | **`requireAdministrator` manifest.** Every menu click raises UAC, even user‑scope actions. On a standard‑user business account, "over‑the‑shoulder" elevation runs the app **as the admin account**, so HKCU, `Environment.UserName` and "Current user (…)" all refer to the admin. Your own PATH is never changed. Where users can't elevate at all, nothing works. | [app.manifest](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/app.manifest) |
| B2 | **S1** | **The Windows 11 context menu hides legacy `IContextMenu` handlers.** KuShellExtension items only appear under *Show more options* / Shift+F10. Only `IExplorerCommand` handlers registered through a packaged (MSIX or sparse‑package) identity reach the top‑level menu. | KuShellExtension*.dll |
| B3 | S2 | **The console window can't be hidden.** The extension is a console‑subsystem exe that hides itself with `GetConsoleWindow`+`ShowWindow`. On Windows 11 the default terminal is Windows Terminal, where `GetConsoleWindow` returns a pseudo‑console handle, so a Terminal window or tab pops up and stays open behind each dialog. Even under conhost it flashes. | [Main_Program.cs:111-127](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/Main_Program.cs#L111), csproj `OutputType=Exe` |
| B4 | S2 | **.NET 3.5 dependency.** It's off by default on Windows 11, and turning it on usually needs Windows Update or an admin. On WSUS/Intune‑managed machines it's often blocked (error 0x800F0954). On a machine without it, nothing launches. | both `app.config` files, both csproj |
| B5 | S2 | **Unsigned code everywhere.** SmartScreen warns on the setup exe. Smart App Control, WDAC or AppLocker policies (common on business machines) may block the exes outright, or stop the unsigned 2010 COM DLL loading into `explorer.exe`, in which case the menu silently disappears. | all binaries |
| B6 | S3 | No ARM64 support. `ShellExt_Install.cmd` only checks for `AMD64`, so Windows on ARM devices (Surface, Copilot+ PCs) get no menu. | [ShellExt_Install.cmd](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/res/ShellExt_bin/ShellExt_Install.cmd) |
| B7 | S3 | Hard‑coded `C:\Windows\System32\rundll32.exe` / `control.exe` breaks if Windows isn't on C:. The XP branch is dead code. | [Main_Program.cs:184](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/Main_Program.cs#L184) |
| B8 | S3 | No DPI awareness declared, so the WinForms dialogs are bitmap‑stretched and blurry at 125–200 % scaling, the Windows 11 laptop default. | app.manifest (commented out) |

### C. Explorer‑extension argument handling and UI

| # | Sev | Issue | Where |
|---|---|---|---|
| C1 | S2 | **Drive roots break parsing.** `"%1"` for `C:\` becomes `-v "C:\"`, and the `\"` escapes the quote. I verified this: usage is printed, nothing happens, exit code 0. | config.xml; verified via CLI |
| C2 | S2 | "Parent directory" uses `Substring(0, LastIndexOf('\'))`. `C:\foo` becomes `C:`, which is drive‑*relative* and not `C:\`. A path with no backslash throws. UNC roots (`\\server\share\x`) produce `\\server\share` correctly only by luck. | [Main_Program.cs:137](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/Main_Program.cs#L137), [:155](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/Main_Program.cs#L155), [:160](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/Main_Program.cs#L160) |
| C3 | S3 | The scope combo box is matched by its *display text*. If the box is editable, or the user name changes during elevation, neither branch matches and clicking OK does nothing. | [AddDirectoryToPATH.cs:80-96](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/AddDirectoryToPATH.cs#L80), [NewEnvVariable.cs:101-104](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/NewEnvVariable.cs#L101) |
| C4 | S3 | The default variable name is the folder name, which may contain spaces, `=` or start with a digit. It isn't validated, so the user gets a raw .NET exception message. | [NewEnvVariable.cs:39](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++%20Explorer%20Extension/Path++%20Explorer%20Extension/NewEnvVariable.cs#L39) |
| C5 | S3 | Esc asks "Are you sure you want to exit?". Cancelling a dialog shouldn't need confirming. `Application.Exit()` is also called from inside modal dialogs that were never `Application.Run`. | all three forms |
| C6 | S3 | Remove isn't offered for files' own folders on the `folder` class, and there's no background (`Directory\Background`) menu, the most natural place for "add *this* folder". | config.xml |
| C7 | S4 | There's no way to *view* or reorder PATH, or find dead entries. The README's pitch ("view and manage") is only met by opening the Windows dialog. | — |

### D. `Path++.exe` (CLI)

| # | Sev | Issue | Where |
|---|---|---|---|
| D1 | **S1** | There's no append or remove. `-n Path -v X` **replaces your whole PATH** with `X`. For the tool's main purpose, the CLI is dangerous. | [Main_Program.cs:106](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++/Path++/Main_Program.cs#L106) |
| D2 | S2 | Any `-t` other than `proc`/`mach` silently goes to User scope (verified with `bogus`). `usr` only "works" by accident. | [:81-86](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++/Path++/Main_Program.cs#L81) |
| D3 | S2 | The elevated re‑launch builds an unquoted command line, `" -n " + name + " -v " + value`, so values with spaces (`C:\Program Files\…`) are mangled. The child's error flashes in a new console and vanishes. | [ElevatedPermissions.cs:59](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++/Path++/ElevatedPermissions.cs#L59) |
| D4 | S2 | The parent prints "completed successfully" whatever the child did. The child's exit code is never read, and nothing sets one. **The exit code is always 0**, even on parse errors (verified), so scripts can't detect failure. | [Main_Program.cs:92-94](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++/Path++/Main_Program.cs#L92), [ElevatedPermissions.cs:72-74](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++/Path++/ElevatedPermissions.cs#L72) |
| D5 | S3 | Declining UAC throws `Win32Exception (1223)`, which is reported as "Access denied … must be granted with Elevated access", the wrong message. | [Main_Program.cs:96-99](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++/Path++/Main_Program.cs#L96) |
| D6 | S3 | A console app shows errors in a **MessageBox**, which blocks forever in non‑interactive or SSH sessions. | [Main_Program.cs:113](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++/Path++/Main_Program.cs#L113) |
| D7 | S3 | `-t proc` is meaningless: it sets the variable in Path++'s own process, which then exits. | — |
| D8 | S4 | The help text example doesn't match the command ("foobar"/"fooVal" vs `foo`/`bar`). "build" is `DateTime.Now`, so it shows *today's* date, not the build date. | [:63-66](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/Path++/Path++/Main_Program.cs#L63) |
| D9 | S4 | CommandLineParser 1.9.71 (2013) is obsolete; its v2 API is entirely different. | packages.config |

### E. Installer (NSIS) and shell‑extension packaging

| # | Sev | Issue | Where |
|---|---|---|---|
| E1 | S2 | `RequestExecutionLevel none` means no manifest level, so it relies on UAC's *installer detection* heuristic. Enterprise/domain‑joined machines **disable** that heuristic by default. There the installer runs un‑elevated, Program Files and HKLM writes are virtualised or fail, and `regsvr32` fails silently (`/s`). | [Installer.nsi:1](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/Installer.nsi#L1) |
| E2 | S2 | The **Start‑menu shortcut launches `Path++.exe`**, a console CLI with three required args. It flashes and closes, which looks broken. | [Components.nsi:28](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/Components.nsi#L28) |
| E3 | S2 | `config.xml` hard‑codes `C:\Program Files\Path++\pathpp_ext.exe`, which breaks for any other install location or a virtualised install. | [config.xml:24](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/res/ShellExt_bin/config.xml#L24) |
| E4 | S3 | `ExecWait` calls use unquoted paths containing spaces (`C:\Program Files\…`), which only works through CreateProcess's ambiguous‑path fallback. The `.cmd` files call `C:\Windows\System32\regsvr32` from a 32‑bit process, so WOW64 redirects it. They should use `$SYSDIR`/`Sysnative` with explicit quoting. | [Components.nsi:24](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/Components.nsi#L24), [:38](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/Components.nsi#L38), [Installer.nsi:123](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/Installer.nsi#L123) |
| E5 | S3 | The uninstaller doesn't restart Explorer and doesn't schedule the deletes for reboot. `explorer.exe` keeps the DLL loaded, so `Delete`/`RMDir` fail and files are left behind. The `$SMPROGRAMS\Path++` folder is never removed. | [Installer.nsi:116-167](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/Installer.nsi#L116) |
| E6 | S3 | The "previous install" check reads 64‑bit HKLM from a 32‑bit installer without `SetRegView`, and runs the old uninstaller with `nsExec::Exec` without waiting on or checking its result. | [Installer.nsi:84-98](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/Installer.nsi#L84) |
| E7 | S3 | `InstallDir $PROGRAMFILES` is overridden by `$PROGRAMFILES64` in `.onInit`, and the directory can't be chosen. There's no per‑user (no‑admin) install option. | [Installer.nsi:22](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/Installer.nsi#L22), [:80](https://github.com/cyfrost/PathPlusPlus/blob/caa1a31a5d974e1c48dae9f258226377f98c75dc/src/NSIS%20Installer/Installer.nsi#L80) |
| E8 | S3 | **Licensing:** KuShellExtension is **GPL‑3.0** and is bundled inside an MIT project's installer. Aggregate distribution is arguably fine, but it's unmaintained (2010), unsigned, and runs inside every Explorer process. Removing it fixes B2, B5 and B6 too. | res/ShellExt_bin |

### F. Repository and build hygiene

| # | Sev | Issue |
|---|---|---|
| F1 | S4 | `bin/`, `obj/`, `packages/`, `.csproj.user` and the built `setup_Path++-0.1.exe` are committed (about 60 of the 118 tracked files). There's no `.gitignore`. |
| F2 | S4 | Two separate `.sln` files and duplicated `packages/` folders, with no single build command. |
| F3 | S4 | No tests, no CI. The README "build passing" badge is static and the "XP through 10" claim is stale. |
| F4 | S4 | The Explorer extension ships as `pathpp_ext.exe` but builds as `Path++ Explorer Extension.exe`, renamed by hand. |
| F5 | S4 | Copy‑pasted MIT headers say "FOR AND CLAIM", a typo for "FOR ANY CLAIM". Assembly copyright has a double space. |

---

## Part 2 — Fix plan

### Guiding decisions

- **Runtime: .NET Framework 4.8.1, WinForms.** It's in‑box on Windows 10 22H2 and 11, so nothing extra to install, and the existing forms port almost unchanged. .NET 8 would mean a runtime install or a 60 MB self‑contained exe, for no real benefit here.
- **No admin unless the user picks *Machine* scope.** Run `asInvoker`. Elevate a small helper **only** for the HKLM write.
- **No COM DLL.** Use static registry verbs under `HKCU\Software\Classes`: per‑user, no admin, no DLL inside Explorer, works on x64 and ARM64. The Windows 11 top‑level menu (sparse package + `IExplorerCommand`) is an optional later phase, because it needs a code‑signing certificate.
- **One shared core library** used by the CLI, the dialogs and the tests.

### Phase 0 — Safety first (½ day). Do this before you use the tool again.

1. Write `PathStore.Write()` so it always uses `RegistryValueKind.ExpandString` for `Path` and preserves the existing kind for other variables (fixes A1, A10).
2. Broadcast after every write: `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, "Environment", SMTO_ABORTIFHUNG, 5000, out _)` (A2).
3. Before every write, save a timestamped backup of the raw value to `%LOCALAPPDATA%\Path++\backups\` (A7).
4. CLI: refuse `-n Path` without `--force` (D1). Reject unknown `-t` values (D2).

### Phase 1 — Core library `PathPlusPlus.Core` (1–2 days)

- `PathStore`: `Read(scope)` returns raw string + kind (`DoNotExpandEnvironmentNames`), and `Write(scope, string)` does kind preservation, broadcast and backup. Accept a `Scope` enum `{User, Machine}`, never magic ints or combo‑box text (C3).
- `PathList`: splits on `;` and keeps the original text of untouched entries exactly. Compares by a normalised key: expand `%VAR%`, `Path.GetFullPath`, trim whitespace and trailing `\` except on roots, case‑insensitive (A4, A5, A6).
- Operations `Add` (no duplicates, optional "prepend"), `Remove` (returns count per scope), `Contains`, `Dedupe`, `FindMissing`.
- Fix parent‑directory handling with `Path.GetDirectoryName`, and keep roots as roots (C2).
- Validate variable names: non‑empty, no `=`, ≤ 255 chars. Warn when PATH exceeds 2047 chars, which some legacy tools truncate, and refuse above 32 767 (C4).
- Return typed results or throw typed exceptions. **Only the UI layer shows dialogs** (A8, A9).
- **Unit tests (xUnit or MSTest)** against an injected fake registry. Cover: `%SystemRoot%` round‑trip, mixed case, trailing slash, empty segments, duplicates, drive root, UNC, a value that's only `;`.

### Phase 2 — Elevation model (1 day)

- Both exes use the `asInvoker` manifest with DPI awareness (`PerMonitorV2`) and the Common‑Controls v6 dependency (B1, B8).
- User scope: write directly, never elevate.
- Machine scope: re‑launch self with `Verb="runas"` and a single argument, a **path to a temp JSON request file** (no command‑line quoting at all, which fixes D3). Wait for exit and propagate the exit code (D4). Map `Win32Exception 1223` to "Cancelled by user" (D5).
- If the process is elevated *and* its user SID differs from the interactive session owner's (`WTSQueryUserToken` / explorer's token), **refuse user‑scope writes** and explain why (B1, over‑the‑shoulder case).

### Phase 3 — Explorer integration without a COM DLL (1 day)

- Build `pathpp_ext` as `OutputType=WinExe`, so there's no console window under Windows Terminal or conhost. Delete the `GetConsoleWindow`/`ShowWindow` hack (B3).
- Register cascading verbs in **HKCU** (per‑user, no admin) using `ExtendedSubCommandsKey` / `SubCommands` under `Directory\shell`, `Directory\Background\shell` and `*\shell`. Pass `"%V"` (works for background, roots and UNC). Strip a trailing `\"` in our own arg parser to cope with drive roots (C1, C6).
- `pathpp_ext.exe --register` / `--unregister` subcommands, so the installer just calls them. Use the exe's real location, not a hard‑coded path (E3).
- Remove KuShellExtension and its GPL notice entirely (B2 partial, B5, B6, E8).
- Items still sit under "Show more options" on Windows 11. That's acceptable for v1 and documented in the README. Users who want the classic menu can set `HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32` (empty default); mention this but don't set it automatically.

### Phase 4 — Usable UI (1–2 days)

- A real main window as the Start‑menu target (E2): a list of User and Machine entries with status icons (missing folder, duplicate, unexpanded variable), plus Add, Remove, Move up/down, Dedupe, Restore backup, and "Open Windows dialog" (C7).
- Context‑menu dialogs: scope radio buttons instead of a text combo box. "Machine" shows the UAC shield. Esc cancels without a confirmation prompt (C5). After success, show a toast‑style message ("Open a **new** terminal to use it").
- "Open Environment Variables" via `Environment.SystemDirectory` + `rundll32.exe sysdm.cpl,EditEnvironmentVariables` (B7).

### Phase 5 — CLI redesign (½ day)

```
pathpp add    <dir> [--machine] [--prepend]
pathpp remove <dir> [--machine|--all]
pathpp list   [--machine|--user] [--check]
pathpp dedupe [--machine|--user]
pathpp set    <NAME> <value> [--machine]       # refuses Path without --force
pathpp unset  <NAME> [--machine]
pathpp restore <backup-file>
```

- Use the `System.CommandLine` or CommandLineParser 2.x API (D9). Non‑zero exit codes, errors to stderr, no MessageBoxes (D4, D6). Drop `proc` (D7).

### Phase 6 — Installer and distribution (1 day)

- Keep NSIS, or move to Inno Setup, whose per‑user/per‑machine dialog is simpler. Either way:
  - Per‑user install by default to `%LOCALAPPDATA%\Programs\Path++` with `RequestExecutionLevel user`. Optional all‑users mode with an explicit `admin` level (E1, E7).
  - Call `pathpp_ext --register`/`--unregister` with quoted paths (E4). Without a COM DLL there's nothing locked by Explorer, so uninstall is clean. Remove the Start‑menu folder (E5).
  - Uninstall key in HKCU (per‑user) or HKLM 64‑bit view (all users), with `SetRegView 64` (E6).
- Also publish a portable zip and a `winget` manifest.
- **Code signing.** Ideally an Azure Trusted Signing or OV certificate to clear SmartScreen and Smart App Control (B5). At minimum, document "Unblock" / the SmartScreen "Run anyway" step.

### Phase 7 — Repo, CI, docs (½ day)

- Add a `.gitignore` (VisualStudio template) and `git rm -r --cached` all `bin/ obj/ packages/ *.user` and the setup exe (F1).
- A single `PathPlusPlus.sln` with `Core`, `Cli`, `Shell` (WinForms), `Tests`. SDK‑style csproj targeting `net481`, `PackageReference` instead of `packages.config` (F2, F4).
- A GitHub Actions `windows-latest` workflow: build, test, build installer, upload artifacts, attach to Release on tag (F3).
- README: Windows 10/11 only, the "Show more options" note, CLI reference, how backups and restore work.

### Optional Phase 8 — Top‑level Windows 11 menu

- A small C++/WinRT (or C# via COM‑hosting) `IExplorerCommand` DLL plus a **sparse MSIX package** with `windows.fileExplorerContextMenus` registration, installed alongside the Win32 app. It needs a signing certificate trusted on the machine, which is why it's last. Without one, managed devices will reject the package.

### Suggested order and effort

| Order | Phase | Effort | Unblocks |
|---|---|---|---|
| 1 | 0 Safety | ½ d | Using the tool without risk |
| 2 | 1 Core + tests | 1–2 d | Everything else |
| 3 | 2 Elevation | 1 d | Business/standard‑user machines |
| 4 | 3 Explorer verbs | 1 d | Menu works without a DLL or admin |
| 5 | 6 Installer | 1 d | Painless install and uninstall |
| 6 | 4 UI, 5 CLI | 2 d | "Manage" rather than just "add" |
| 7 | 7 Repo/CI | ½ d | Repeatable releases |
| — | 8 Win11 top‑level menu | 2–3 d + cert | Polish |

About **7–9 working days** for phases 0–7.

### Until then: safe workaround

Use the built‑in editor (`Win + R` → `rundll32 sysdm.cpl,EditEnvironmentVariables`), or PowerShell. Both preserve `REG_EXPAND_SZ`:

```powershell
$k = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
$p = $k.GetValue('Path', '', 'DoNotExpandEnvironmentNames')
$k.SetValue('Path', ($p.TrimEnd(';') + ';C:\your\dir'), 'ExpandString')
```

PowerShell writes the value but doesn't broadcast, so open a new Explorer window or sign out. Better still, just reopen the built‑in dialog and press OK, which broadcasts.
