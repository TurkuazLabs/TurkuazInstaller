// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Artifacts/ArtifactSignatureAlgorithm.cs
// 📌 Amac: Artifact signature algoritmasini typed domain degeri olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Stable v1 runtime tarafinda desteklenen Authenticode signature politikasini magic string kullanmadan temsil eder
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Artifacts;

public enum ArtifactSignatureAlgorithm
{
    Authenticode = 0
}
