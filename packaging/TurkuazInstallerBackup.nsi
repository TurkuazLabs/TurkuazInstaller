; Dosya Yolu: REPO_ROOT/packaging/TurkuazInstallerBackup.nsi
; Amac: Birincil Turkuaz setup ile ayni offline paketi kullanan zorunlu NSIS yedegini olusturmak
; Modul - NSIS Script
; Version: 0.2.0
; Aciklama: NSIS yedegi kullanici tarafindan secilen operation'i primary setup'a devreder
; Bagimli Oldugu Katman: Packaging

!include "LogicLib.nsh"
!include "nsDialogs.nsh"

Unicode true
Name "Turkuaz Installer Setup (NSIS Backup)"
OutFile "${OUTPUT_PATH}"
InstallDir "$LOCALAPPDATA\Programs\TurkuazLabs\TurkuazInstaller"
RequestExecutionLevel user
SetCompressor /SOLID lzma

Var Operation
Var RadioInstall
Var RadioUpdate
Var RadioRepair
Var RadioRollback

Page directory
Page custom OperationPage OperationPageLeave
Page instfiles

Function OperationPage
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}

  ${NSD_CreateRadioButton} 0 0 100% 12u "Install this release"
  Pop $RadioInstall
  ${NSD_Check} $RadioInstall
  ${NSD_CreateRadioButton} 0 20u 100% 12u "Update to this release"
  Pop $RadioUpdate
  ${NSD_CreateRadioButton} 0 40u 100% 12u "Repair the installed version"
  Pop $RadioRepair
  ${NSD_CreateRadioButton} 0 60u 100% 12u "Roll back to the previous version"
  Pop $RadioRollback
  nsDialogs::Show
FunctionEnd

Function OperationPageLeave
  ${NSD_GetState} $RadioUpdate $0
  ${If} $0 == 1
    StrCpy $Operation "update"
    Return
  ${EndIf}
  ${NSD_GetState} $RadioRepair $0
  ${If} $0 == 1
    StrCpy $Operation "repair"
    Return
  ${EndIf}
  ${NSD_GetState} $RadioRollback $0
  ${If} $0 == 1
    StrCpy $Operation "rollback"
    Return
  ${EndIf}
  StrCpy $Operation "install"
FunctionEnd

Section "Install"
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File /oname=TurkuazSetup.exe "${PRIMARY_SETUP_PATH}"
  File /oname=manifest-trust.yaml "${TRUST_STORE_PATH}"

  ExecWait '"$PLUGINSDIR\TurkuazSetup.exe" install-self --package-id "${PACKAGE_ID}" --trust-store "$PLUGINSDIR\manifest-trust.yaml" --target "$INSTDIR" --data-root "$LOCALAPPDATA\TurkuazInstaller" --operation "$Operation"' $0
  ${If} $0 != 0
    SetErrorLevel $0
    Abort "Turkuaz setup failed with exit code $0."
  ${EndIf}
SectionEnd
