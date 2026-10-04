// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/BootstrapHandoffArguments.cs
// 📌 Amac: Self-update handoff protocol argument sabitlerini tek noktada tutar
// 📌 Modul - Tool CSharp
// Version: 0.6.0
// Aciklama: Parent ve replacement bootstrap arasindaki internal CLI protocolunde magic string kullanilmasini engeller
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Platform.Windows.Tools;

public static class BootstrapHandoffArguments
{
    public const string CompleteSelfUpdate = "--complete-self-update";
    public const string Source = "--source";
    public const string Target = "--target";
    public const string ParentProcessId = "--parent-pid";
    public const string ResumeArgument = "--resume-arg";
    public const string CleanupSource = "--cleanup-source";
}
