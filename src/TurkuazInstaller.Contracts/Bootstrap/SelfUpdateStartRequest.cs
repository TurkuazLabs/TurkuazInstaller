// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/SelfUpdateStartRequest.cs
// 📌 Amac: Mevcut bootstrap processinden dogrulanmis replacement bootstrapa self-update handoff istegini tasir
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Current executable, replacement executable ve resume argumentlarini typed kontratla aktarir
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public sealed record SelfUpdateStartRequest
{
    public SelfUpdateStartRequest(
        string currentExecutablePath,
        string replacementExecutablePath,
        IReadOnlyList<string> resumeArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementExecutablePath);
        ArgumentNullException.ThrowIfNull(resumeArguments);

        CurrentExecutablePath = currentExecutablePath.Trim();
        ReplacementExecutablePath = replacementExecutablePath.Trim();
        ResumeArguments = Array.AsReadOnly(resumeArguments.ToArray());
    }

    public string CurrentExecutablePath { get; }

    public string ReplacementExecutablePath { get; }

    public IReadOnlyList<string> ResumeArguments { get; }
}
