;Language strings English
LangString SEC_CORE ${LANG_ENGLISH} "Path++ Core (Required)"
LangString SEC_EXPLORER ${LANG_ENGLISH} "Path++ Shell Extension"


LangString DESC_CORE ${LANG_ENGLISH} "The core components of Path++ package"
LangString DESC_EXPLORER ${LANG_ENGLISH} "Enables/Disables the Windows Explorer integration module of Path++"


;Assign language strings to sections
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
!insertmacro MUI_DESCRIPTION_TEXT ${CORE} $(DESC_CORE)
!insertmacro MUI_DESCRIPTION_TEXT ${EXPLORER} $(DESC_EXPLORER)
!insertmacro MUI_FUNCTION_DESCRIPTION_END
