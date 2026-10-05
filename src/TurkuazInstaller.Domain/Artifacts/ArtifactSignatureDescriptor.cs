// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Artifacts/ArtifactSignatureDescriptor.cs
// 📌 Amac: Artifact icin manifest tarafindan talep edilen signature dogrulama politikasini tasir
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Signature deklarasyonu mevcutsa runtime dogrulamasini zorunlu hale getirir
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Artifacts;

public sealed record ArtifactSignatureDescriptor(
    ArtifactSignatureAlgorithm Algorithm);
