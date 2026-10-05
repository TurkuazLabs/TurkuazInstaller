// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Prerequisites/PrerequisiteIds.cs
// 📌 Amac: Community runtime tarafinda desteklenen prerequisite kimliklerini merkezi olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Manifest prerequisite id degerlerinin magic string olarak dagilmasini engeller
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Prerequisites;

public static class PrerequisiteIds
{
    public const string DotNetDesktopRuntime =
        "dotnet-desktop-runtime";

    public const string WindowsBuild =
        "windows-build";

    public const string Architecture =
        "architecture";
}
