// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/SelfUpdateCompleteRequest.cs
// 📌 Amac: Replacement bootstrap processinin parent kapandiktan sonra self-update tamamlamasi icin gerekli veriyi tasir
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Source, target, parent PID ve resume argumentlarini typed kontratta birlestirir
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public sealed record SelfUpdateCompleteRequest
{
    public SelfUpdateCompleteRequest(
        string sourceExecutablePath,
        string targetExecutablePath,
        int parentProcessId,
        IReadOnlyList<string> resumeArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExecutablePath);
        ArgumentNullException.ThrowIfNull(resumeArguments);

        if (parentProcessId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parentProcessId));
        }

        SourceExecutablePath = sourceExecutablePath.Trim();
        TargetExecutablePath = targetExecutablePath.Trim();
        ParentProcessId = parentProcessId;
        ResumeArguments = Array.AsReadOnly(resumeArguments.ToArray());
    }

    public string SourceExecutablePath { get; }

    public string TargetExecutablePath { get; }

    public int ParentProcessId { get; }

    public IReadOnlyList<string> ResumeArguments { get; }
}
