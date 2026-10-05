# 📄 Dosya Yolu: /config/velopack-e2e.psd1
# 📌 Amac: Gercek Velopack E2E kalite kapisi icin tool ve fixture sabitlerini merkezi config olarak tanimlar
# 📌 Modul - Config PowerShell
# Version: 1.0.0
# Aciklama: vpk surumu, test package kimligi, iki test surumu ve marker dosyasi ayarlarini tek yerde tutar
# Bagimli Oldugu Katman: Config | Tool

@{
    VpkVersion = "1.2.161"
    PackId = "TurkuazLabs.TurkuazInstaller.E2E"
    PackTitle = "TurkuazInstaller Velopack E2E"
    MainExecutable = "TurkuazInstaller.Velopack.E2E.Fixture.exe"
    VersionOne = "1.0.0"
    VersionTwo = "2.0.0"
    VersionMarkerFile = "e2e-version.txt"
}
