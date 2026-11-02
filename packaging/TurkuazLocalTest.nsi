; Dosya Yolu: REPO_ROOT/packaging/TurkuazLocalTest.nsi
; Amac: Imzasiz ve sadece kullaniciya ozel yerel installer testi
; Modul - NSIS Script
; Version: 0.1.0
; Aciklama: Guvenli imzali release akisindan bagimsiz lokal TEST derleme
; Bagimli Oldugu Katman: Packaging | QA

!ifndef SOURCE_DIR
  !error "SOURCE_DIR is required"
!endif
!ifndef OUTPUT_PATH
  !error "OUTPUT_PATH is required"
!endif
!ifndef APP_NAME
  !error "APP_NAME is required"
!endif
!ifndef PACKAGE_ID
  !error "PACKAGE_ID is required"
!endif

Unicode true
Name "${APP_NAME} - LOCAL TEST ONLY"
OutFile "${OUTPUT_PATH}"
InstallDir "$LOCALAPPDATA\TurkuazInstaller\LocalTests\${PACKAGE_ID}"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails show
Page directory
Page instfiles

Section "LOCAL TEST ONLY"
  IfFileExists "$INSTDIR\*.*" 0 +2
  Abort "Local test directory already contains files; refusing to overwrite."
  SetOutPath "$INSTDIR"
  File /r "${SOURCE_DIR}\*"
SectionEnd
