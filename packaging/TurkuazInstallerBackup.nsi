; Dosya Yolu: REPO_ROOT/packaging/TurkuazInstallerBackup.nsi
; Amac: Birincil Turkuaz setup ile ayni offline paketi kullanan zorunlu NSIS yedegini olusturmak
; Modul - NSIS Script
; Version: 0.1.0
; Aciklama: NSIS yedegi harici signer trust policy ile primary setup CLI'sine devreder
; Bagimli Oldugu Katman: Packaging

!include "LogicLib.nsh"

Unicode true
Name "Turkuaz Installer Setup (NSIS Backup)"
OutFile "${OUTPUT_PATH}"
InstallDir "$LOCALAPPDATA\Programs\TurkuazLabs\TurkuazInstaller"
RequestExecutionLevel user
SetCompressor /SOLID lzma

Page directory
Page instfiles

Section "Install"
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File /oname=TurkuazSetup.exe "${PRIMARY_SETUP_PATH}"
  File /oname=manifest-trust.yaml "${TRUST_STORE_PATH}"

  ExecWait '"$PLUGINSDIR\TurkuazSetup.exe" install-self --package-id "${PACKAGE_ID}" --trust-store "$PLUGINSDIR\manifest-trust.yaml" --target "$INSTDIR" --data-root "$LOCALAPPDATA\TurkuazInstaller" --operation install' $0
  ${If} $0 != 0
    SetErrorLevel $0
    Abort "Turkuaz setup failed with exit code $0."
  ${EndIf}
SectionEnd
