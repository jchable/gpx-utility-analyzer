; installers/windows/gpx-analyzer.nsi
; NSIS 3.x installer for gpx-analyzer
;
; Build command:
;   makensis /DVERSION=x.y.z /DEXE_PATH=C:\path\to\gpx-analyzer.exe gpx-analyzer.nsi

Unicode true

!ifndef VERSION
  !define VERSION "0.0.0"
!endif

!ifndef EXE_PATH
  !error "EXE_PATH must be defined: makensis /DEXE_PATH=C:\path\to\gpx-analyzer.exe ..."
!endif

; Standard library includes (part of NSIS distribution)
!include "WordFunc.nsh"
!include "WinMessages.nsh"   ; HWND_BROADCAST + WM_SETTINGCHANGE for PATH refresh

; Declare the uninstaller variant of WordReplace (used to strip PATH on uninstall)
!insertmacro un.WordReplace

;---------------------------------------------------------------------
; StrContains — case-sensitive substring search (NSIS Wiki, kenglish_hi)
; Returns the needle if found in the haystack, otherwise "".
; Usage: ${StrContains} $out "needle" "haystack"
;---------------------------------------------------------------------
Var STR_HAYSTACK
Var STR_NEEDLE
Var STR_CONTAINS_VAR_1
Var STR_CONTAINS_VAR_2
Var STR_CONTAINS_VAR_3
Var STR_CONTAINS_VAR_4
Var STR_RETURN_VAR

Function StrContains
  Exch $STR_NEEDLE
  Exch 1
  Exch $STR_HAYSTACK
  StrCpy $STR_RETURN_VAR ""
  StrCpy $STR_CONTAINS_VAR_1 -1
  StrLen $STR_CONTAINS_VAR_2 $STR_NEEDLE
  StrLen $STR_CONTAINS_VAR_4 $STR_HAYSTACK
  loop:
    IntOp $STR_CONTAINS_VAR_1 $STR_CONTAINS_VAR_1 + 1
    StrCpy $STR_CONTAINS_VAR_3 $STR_HAYSTACK $STR_CONTAINS_VAR_2 $STR_CONTAINS_VAR_1
    StrCmp $STR_CONTAINS_VAR_3 $STR_NEEDLE found
    StrCmp $STR_CONTAINS_VAR_1 $STR_CONTAINS_VAR_4 done
    Goto loop
  found:
    StrCpy $STR_RETURN_VAR $STR_NEEDLE
    Goto done
  done:
    Pop $STR_NEEDLE
    Exch $STR_RETURN_VAR
FunctionEnd

!macro _StrContainsConstructor OUT NEEDLE HAYSTACK
  Push `${HAYSTACK}`
  Push `${NEEDLE}`
  Call StrContains
  Pop `${OUT}`
!macroend
!define StrContains '!insertmacro "_StrContainsConstructor"'

;---------------------------------------------------------------------
; General settings
;---------------------------------------------------------------------
Name                "GPX Analyzer ${VERSION}"
OutFile             "gpx-analyzer-setup-${VERSION}-win-x64.exe"
InstallDir          "$PROGRAMFILES64\GPX Analyzer"
InstallDirRegKey    HKLM "Software\GPX Analyzer" "InstallDir"
RequestExecutionLevel admin
ShowInstDetails     show
ShowUninstDetails   show

;---------------------------------------------------------------------
; Pages
;---------------------------------------------------------------------
Page directory
Page instfiles

UninstPage uninstConfirm
UninstPage instfiles

;---------------------------------------------------------------------
; Install section
;---------------------------------------------------------------------
Section "Install" SecInstall

  ; Registry writes target the native 64-bit view (the installer is 32-bit, so
  ; without this HKLM\Software entries would be redirected to WOW6432Node)
  SetRegView 64

  SetOutPath "$INSTDIR"
  File "${EXE_PATH}"

  ; Write uninstaller
  WriteUninstaller "$INSTDIR\uninstall.exe"

  ; Store install metadata
  WriteRegStr HKLM "Software\GPX Analyzer" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\GPX Analyzer" "Version"    "${VERSION}"

  ; ---- Add/Remove Programs registration ----
  WriteRegStr   HKLM \
    "Software\Microsoft\Windows\CurrentVersion\Uninstall\GPXAnalyzer" \
    "DisplayName"     "GPX Analyzer"
  WriteRegStr   HKLM \
    "Software\Microsoft\Windows\CurrentVersion\Uninstall\GPXAnalyzer" \
    "DisplayVersion"  "${VERSION}"
  WriteRegStr   HKLM \
    "Software\Microsoft\Windows\CurrentVersion\Uninstall\GPXAnalyzer" \
    "Publisher"       "GPX Analyzer Project"
  WriteRegStr   HKLM \
    "Software\Microsoft\Windows\CurrentVersion\Uninstall\GPXAnalyzer" \
    "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM \
    "Software\Microsoft\Windows\CurrentVersion\Uninstall\GPXAnalyzer" \
    "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr   HKLM \
    "Software\Microsoft\Windows\CurrentVersion\Uninstall\GPXAnalyzer" \
    "DisplayIcon"     "$INSTDIR\gpx-analyzer.exe"
  WriteRegDWORD HKLM \
    "Software\Microsoft\Windows\CurrentVersion\Uninstall\GPXAnalyzer" \
    "NoModify"        1
  WriteRegDWORD HKLM \
    "Software\Microsoft\Windows\CurrentVersion\Uninstall\GPXAnalyzer" \
    "NoRepair"        1

  ; ---- Add $INSTDIR to system PATH if not already present ----
  ReadRegStr $0 HKLM \
    "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path"

  ${StrContains} $1 "$INSTDIR" "$0"
  StrCmp $1 "" 0 PathAlreadyPresent
    WriteRegExpandStr HKLM \
      "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" \
      "Path" "$0;$INSTDIR"
    ; Notify the shell of PATH change (no reboot required)
    SendMessage ${HWND_BROADCAST} ${WM_SETTINGCHANGE} 0 "STR:Environment" \
      /TIMEOUT=5000
  PathAlreadyPresent:

SectionEnd

;---------------------------------------------------------------------
; Uninstall section
;---------------------------------------------------------------------
Section "Uninstall"

  ; Match the install section's 64-bit registry view
  SetRegView 64

  ; Remove binary and uninstaller
  Delete "$INSTDIR\gpx-analyzer.exe"
  Delete "$INSTDIR\uninstall.exe"
  RMDir  "$INSTDIR"

  ; ---- Remove $INSTDIR from system PATH ----
  ReadRegStr $0 HKLM \
    "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path"
  ; Three passes to handle: middle/end (;entry), start (entry;), alone (entry)
  ${un.WordReplace} "$0" ";$INSTDIR"  "" "+" $1
  ${un.WordReplace} "$1" "$INSTDIR;"  "" "+" $2
  ${un.WordReplace} "$2" "$INSTDIR"   "" "+" $3
  WriteRegExpandStr HKLM \
    "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" \
    "Path" "$3"
  SendMessage ${HWND_BROADCAST} ${WM_SETTINGCHANGE} 0 "STR:Environment" \
    /TIMEOUT=5000

  ; Remove ARP entry and registry key
  DeleteRegKey HKLM \
    "Software\Microsoft\Windows\CurrentVersion\Uninstall\GPXAnalyzer"
  DeleteRegKey HKLM "Software\GPX Analyzer"

SectionEnd
