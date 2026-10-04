// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/BootstrapEnvironmentSnapshot.cs
// 📌 Amac: Bootstrap prerequisite kontrolu icin platform bilgilerini typed snapshot olarak tasir
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Windows durumu, OS surumu ve CPU mimarisini Application servisine platform bagimsiz iletir
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public sealed record BootstrapEnvironmentSnapshot(
    bool IsWindows,
    Version OperatingSystemVersion,
    BootstrapCpuArchitecture Architecture);
