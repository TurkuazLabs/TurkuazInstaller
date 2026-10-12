# 📄 Dosya Yolu: /config/velopack-e2e.psd1
# 📌 Amac: Gercek Velopack E2E kalite kapisi icin tool ve fixture sabitlerini merkezi config olarak tanimlar
# 📌 Modul - Config PowerShell
# Version: 1.3.0
# Aciklama: vpk surumu, test package kimligi, iki test surumu ve marker dosyasi ayarlarini tek yerde tutar
# Bagimli Oldugu Katman: Config | Tool

@{
    VpkVersion = "1.2.161"
    PackId = "turkuazlabs.turkuazinstaller.e2e"
    PackTitle = "TurkuazInstaller Velopack E2E"
    MsiProbePackageId = "turkuazlabs.turkuazinstaller.e2e.msi.scope.lab"
    MsiProbeTitle = "TurkuazInstaller MSI Scope LAB ONLY"
    MsiProbeInstallPrefix = "TurkuazInstaller-MSI-LAB"
    MsiInstallDirectoryProperty = "VELOPACK_INSTALLDIR"
    MsiProbeScopes = @(
        @{
            Name = "PerUser"
            SpecialFolder = "LocalApplicationData"
            Subdirectory = "Programs"
            RequiresElevation = $false
        }
        @{
            Name = "PerMachine"
            SpecialFolder = "ProgramFiles"
            Subdirectory = ""
            RequiresElevation = $true
        }
    )
    MainExecutable = "TurkuazInstaller.Velopack.E2E.Fixture.exe"
    VersionOne = "1.0.0"
    VersionTwo = "2.0.0"
    VersionMarkerFile = "e2e-version.txt"
    UserDataDirectory = "UserData"
    UserDataMarkerFile = "e2e-user-state.txt"
}
