; Path++ installer (NSIS 3.x)
;
; Build:  makensis /DVERSION=1.0.0 /DSOURCE_DIR=<folder with the built binaries> /DOUTPUT_FILE=<setup.exe> PathPlusPlus.nsi
; (build.ps1 does this for you.)
;
; - Per-user install by default (no admin needed): %LOCALAPPDATA%\Programs\Path++
; - Administrators may choose an all-users install: C:\Program Files\Path++
; - The Explorer menu is plain registry verbs: no DLL is loaded into Explorer, so uninstall is always clean.

Unicode true
ManifestDPIAware true
ManifestSupportedOS all
SetCompressor /SOLID lzma

!ifndef VERSION
  !define VERSION "1.0.0"
!endif
!ifndef SOURCE_DIR
  !error "Pass /DSOURCE_DIR=<folder containing PathPlusPlus.exe and pathpp.exe>"
!endif
!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "Path++-Setup-${VERSION}.exe"
!endif

!define PRODUCT_NAME "Path++"
!define PRODUCT_PUBLISHER "cyfrost"
!define PRODUCT_URL "https://github.com/cyfrost/PathPlusPlus"
!define APP_EXE "PathPlusPlus.exe"
!define CLI_EXE "pathpp.exe"
!define SETTINGS_KEY "Software\PathPlusPlus"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\PathPlusPlus"
!define LEGACY_UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Path++"

Name "${PRODUCT_NAME}"
OutFile "${OUTPUT_FILE}"
BrandingText "${PRODUCT_NAME} ${VERSION}"
ShowInstDetails show
ShowUninstDetails show

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "FileDescription" "${PRODUCT_NAME} Setup"
VIAddVersionKey "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright (c) 2018-2026 ${PRODUCT_PUBLISHER}"

; ---------------------------------------------------------------- per-user / all-users

!define MULTIUSER_EXECUTIONLEVEL Highest
!define MULTIUSER_MUI
!define MULTIUSER_INSTALLMODE_COMMANDLINE
!define MULTIUSER_INSTALLMODE_DEFAULT_CURRENTUSER
!define MULTIUSER_USE_PROGRAMFILES64
!define MULTIUSER_INSTALLMODE_INSTDIR "${PRODUCT_NAME}"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_KEY "${SETTINGS_KEY}"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_VALUENAME "InstallDir"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_KEY "${SETTINGS_KEY}"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_VALUENAME "InstallDir"

!include "MultiUser.nsh"
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"
!include "x64.nsh"

; ---------------------------------------------------------------- pages

!define MUI_ICON "..\src\PathPlusPlus.App\app.ico"
!define MUI_UNICON "..\src\PathPlusPlus.App\app.ico"
!define MUI_ABORTWARNING
!define MUI_COMPONENTSPAGE_SMALLDESC

!define MUI_WELCOMEPAGE_TITLE_3LINES
!define MUI_WELCOMEPAGE_TEXT "Setup will install ${PRODUCT_NAME} ${VERSION}.$\r$\n$\r$\n${PRODUCT_NAME} lets you add folders to PATH and manage environment variables from the Explorer right-click menu, a PATH editor, or the 'pathpp' command.$\r$\n$\r$\nNo administrator rights are needed for a just-for-me install."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${SOURCE_DIR}\LICENSE.txt"
!insertmacro MULTIUSER_PAGE_INSTALLMODE
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE_3LINES
!define MUI_FINISHPAGE_TEXT "${PRODUCT_NAME} is installed.$\r$\n$\r$\n- Right-click a folder (on Windows 11: Show more options) and choose ${PRODUCT_NAME}.$\r$\n- Open '${PRODUCT_NAME}' from the Start menu to edit PATH.$\r$\n- In a new terminal, run 'pathpp help'."
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; ---------------------------------------------------------------- init

Function .onInit
  SetRegView 64
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP "${PRODUCT_NAME} ${VERSION} requires Windows 10 or Windows 11."
    Abort
  ${EndIf}

  ; .NET Framework 4.8 (built into Windows 10 1903+ and Windows 11).
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 528040
    MessageBox MB_OK|MB_ICONSTOP "${PRODUCT_NAME} needs .NET Framework 4.8 or later.$\r$\n$\r$\nInstall it with Windows Update, or from https://dotnet.microsoft.com/download/dotnet-framework/net48, then run Setup again."
    Abort
  ${EndIf}

  ; MULTIUSER_INIT picks the default folder for the install mode, which would discard a folder
  ; given with /D=... on the command line, so keep that one.
  StrCpy $9 $INSTDIR
  !insertmacro MULTIUSER_INIT
  ${If} $9 != ""
    StrCpy $INSTDIR $9
  ${EndIf}
  Call WarnAboutLegacyVersion
FunctionEnd

; Path++ 0.1 (2018) used a COM shell extension and a different uninstall key.
Function WarnAboutLegacyVersion
  SetRegView 32
  ReadRegStr $0 HKLM "${LEGACY_UNINSTALL_KEY}" "UninstallString"
  SetRegView 64
  ${If} $0 != ""
    MessageBox MB_YESNO|MB_ICONEXCLAMATION|MB_DEFBUTTON2 \
      "${PRODUCT_NAME} 0.1 (the 2018 version) is still installed.$\r$\n$\r$\nPlease uninstall it first from Settings > Apps, then sign out and back in (or restart Explorer) so its old Explorer extension is unloaded.$\r$\n$\r$\nContinue installing anyway?" \
      /SD IDYES IDYES +2
    Abort
  ${EndIf}
FunctionEnd

Function un.onInit
  SetRegView 64
  !insertmacro MULTIUSER_UNINIT
FunctionEnd

; Appends " --all-users" (shell) / " --machine" (pathpp) for an all-users install.
!macro ScopeArgs SHELL_VAR CLI_VAR
  ${If} $MultiUser.InstallMode == "AllUsers"
    StrCpy ${SHELL_VAR} " --all-users"
    StrCpy ${CLI_VAR} "--machine"
  ${Else}
    StrCpy ${SHELL_VAR} ""
    StrCpy ${CLI_VAR} "--user"
  ${EndIf}
!macroend

; ---------------------------------------------------------------- sections

Section "${PRODUCT_NAME} (required)" SecCore
  SectionIn RO
  SetOutPath "$INSTDIR"
  SetOverwrite on

  File "${SOURCE_DIR}\${APP_EXE}"
  File "${SOURCE_DIR}\${APP_EXE}.config"
  File "${SOURCE_DIR}\${CLI_EXE}"
  File "${SOURCE_DIR}\${CLI_EXE}.config"
  File "${SOURCE_DIR}\PathPlusPlus.Core.dll"
  File "${SOURCE_DIR}\LICENSE.txt"
  File "${SOURCE_DIR}\README.txt"

  WriteUninstaller "$INSTDIR\uninstall.exe"
  CreateShortcut "$SMPROGRAMS\${PRODUCT_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0 SW_SHOWNORMAL "" "Edit PATH and environment variables"

  WriteRegStr SHCTX "${SETTINGS_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "URLInfoAbout" "${PRODUCT_URL}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE},0"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe" /$MultiUser.InstallMode'
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /$MultiUser.InstallMode /S'
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "NoRepair" 1
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "EstimatedSize" 400
SectionEnd

Section "Explorer right-click menu" SecShell
  !insertmacro ScopeArgs $1 $2
  DetailPrint "Registering the Explorer menu..."
  ExecWait '"$INSTDIR\${APP_EXE}" --register-shell$1' $0
  ${If} $0 != 0
    DetailPrint "Could not register the Explorer menu (exit code $0)."
  ${EndIf}
  WriteRegDWORD SHCTX "${SETTINGS_KEY}" "ShellMenu" 1
SectionEnd

Section "Add the 'pathpp' command to PATH" SecCliPath
  !insertmacro ScopeArgs $1 $2
  DetailPrint "Adding $INSTDIR to the PATH..."
  nsExec::ExecToLog '"$INSTDIR\${CLI_EXE}" add "$INSTDIR" $2'
  Pop $0
  ${If} $0 == 0
    WriteRegDWORD SHCTX "${SETTINGS_KEY}" "CliOnPath" 1
  ${Else}
    DetailPrint "Could not add $INSTDIR to PATH (exit code $0)."
  ${EndIf}
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} "The PATH editor, the Explorer dialogs and the 'pathpp' command-line tool."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecShell} "Adds a ${PRODUCT_NAME} submenu when you right-click folders and files (on Windows 11: under Show more options)."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCliPath} "Lets you run 'pathpp' from any new terminal window."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ---------------------------------------------------------------- uninstall

Section "Uninstall"
  !insertmacro ScopeArgs $1 $2

  ReadRegDWORD $0 SHCTX "${SETTINGS_KEY}" "CliOnPath"
  ${If} $0 == 1
  ${AndIf} ${FileExists} "$INSTDIR\${CLI_EXE}"
    DetailPrint "Removing $INSTDIR from the PATH..."
    nsExec::ExecToLog '"$INSTDIR\${CLI_EXE}" remove "$INSTDIR" $2'
    Pop $0
  ${EndIf}

  ${If} ${FileExists} "$INSTDIR\${APP_EXE}"
    DetailPrint "Removing the Explorer menu..."
    ExecWait '"$INSTDIR\${APP_EXE}" --unregister-shell$1' $0
  ${EndIf}

  Delete "$SMPROGRAMS\${PRODUCT_NAME}.lnk"

  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\${APP_EXE}.config"
  Delete "$INSTDIR\${CLI_EXE}"
  Delete "$INSTDIR\${CLI_EXE}.config"
  Delete "$INSTDIR\PathPlusPlus.Core.dll"
  Delete "$INSTDIR\LICENSE.txt"
  Delete "$INSTDIR\README.txt"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"

  DeleteRegKey SHCTX "${UNINSTALL_KEY}"
  DeleteRegKey SHCTX "${SETTINGS_KEY}"

  ${If} ${FileExists} "$INSTDIR\*.*"
    DetailPrint "Some files are in use and were left in $INSTDIR. Close Path++ and delete the folder."
  ${EndIf}
  DetailPrint "Backups of your PATH were kept in %LOCALAPPDATA%\Path++\Backups (and %ProgramData%\Path++\Backups)."
SectionEnd
