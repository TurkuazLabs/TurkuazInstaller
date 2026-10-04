// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Verification/VerificationResult.cs
// 📌 Amac: Artifact verification sonucunu typed ve acik durum modeli olarak temsil eder
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Hash, size, signature ve path guvenlik hatalarini ortak result modeliyle tasir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Verification;

public enum VerificationFailure
{
    None = 0,
    SizeMismatch = 1,
    HashMismatch = 2,
    SignatureInvalid = 3,
    PathRejected = 4
}

public sealed record VerificationResult
{
    private VerificationResult(bool isValid, VerificationFailure failure, string message)
    {
        IsValid = isValid;
        Failure = failure;
        Message = message;
    }

    public bool IsValid { get; }
    public VerificationFailure Failure { get; }
    public string Message { get; }

    public static VerificationResult Passed() => new(true, VerificationFailure.None, string.Empty);

    public static VerificationResult Failed(VerificationFailure failure, string message)
    {
        if (failure == VerificationFailure.None)
        {
            throw new ArgumentException("Failure reason is required.", nameof(failure));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new VerificationResult(false, failure, message.Trim());
    }
}
