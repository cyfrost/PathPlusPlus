Section $(SEC_CORE) Core
	
   SectionIn RO
	
	
   SetOutPath "$INSTDIR"
   SetOverwrite ifnewer
  nsExec::ExecToLog '"$SYSDIR\cmd.exe" /C "taskkill.exe -im Path++.exe /f"'
  ;add installation resources here
  File res\License.txt
  File res\Path++.exe
  File res\CommandLine.dll
  File res\pathpp_ext.exe
  
  SetOutPath "$INSTDIR\ShellExt_bin\"
  File res\ShellExt_bin\config.xml
  File res\ShellExt_bin\KuShellExtension.dll
  File res\ShellExt_bin\KuShellExtension64.dll
  File res\ShellExt_bin\LICENSE.txt
  File res\ShellExt_bin\PROG_AUTHOR.txt
  File res\ShellExt_bin\ShellExt_Install.cmd
  File res\ShellExt_bin\ShellExt_Uninstall.cmd
  
  ExecWait '$INSTDIR\ShellExt_bin\ShellExt_Uninstall.cmd'
  
SetShellVarContext current
CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\${MAIN_EXECUTABLE_FILENAME}"
CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\Uninstall ${PRODUCT_NAME}.lnk" "$INSTDIR\uninst.exe"
  

SectionEnd

Section $(SEC_EXPLORER) Explorer
;enables win explorer integration
;MessageBox MB_OK "gonna add"

ExecWait '$INSTDIR\ShellExt_bin\ShellExt_Install.cmd'

;nsExec::ExecToLog '$SYSDIR\cmd.exe /c "$INSTDIR\EasyRun.exe /e true"'
;nsExec::ExecToLog '"$SYSDIR\cmd.exe" /C "$INSTDIR\EasyRun.exe /e"'
;MessageBox MB_OK "did add"
SectionEnd


;tentative section
Function createshortcut

SetShellVarContext current
CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\${MAIN_EXECUTABLE_FILENAME}" ""


FunctionEnd

