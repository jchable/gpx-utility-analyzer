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
!include "StrFunc.nsh"

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

  ; Remove binary and uninstaller
  Delete "$INSTDIR\gpx-analyzer.exe"
  Delete "$INSTDIR\uninstall.exe"
  RMDir  "$INSTDIR"

  ; ---- Remove $INSTDIR from system PATH ----
  ReadRegStr $0 HKLM \
    "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path"
  ; Three passes to handle: middle/end (;entry), start (entry;), alone (entry)
  ${WordReplace} "$0" ";$INSTDIR"  "" "+" $1
  ${WordReplace} "$1" "$INSTDIR;"  "" "+" $2
  ${WordReplace} "$2" "$INSTDIR"   "" "+" $3
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

;---------------------------------------------------------------------
; StrContains declaration — must be at global scope, outside sections
;---------------------------------------------------------------------
${StrContains}
