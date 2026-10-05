// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/BootstrapHandoffArguments.cs
// 📌 Amac: Bootstrap self-update handoff ve internal startup protocol argument sabitlerini tek noktada tutar
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Parent, replacement ve normal bootstrap processleri arasinda magic string kullanilmasini engeller
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Platform.Windows.Tools;

public static class BootstrapHandoffArguments
{
    public const string BeginSelfUpdate =
        "--self-update-replacement";

    public const string CompleteSelfUpdate =
        "--complete-self-update";

    public const string Source =
        "--source";

    public const string Target =
        "--target";

    public const string ParentProcessId =
        "--parent-pid";

    public const string ResumeArgument =
        "--resume-arg";

    public const string CleanupSource =
        "--cleanup-source";
}
