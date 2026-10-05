// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsAuthenticodeArtifactSignatureVerifier.cs
// 📌 Amac: Authenticode artifactin Windows trust zinciri, publisher subject ve optional certificate SHA-256 pinini dogrular
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: WinVerifyTrust sonrasinda embedded signer certificate kimligini manifestte beklenen publisher policy ile fail-closed karsilastirir
//
// Bagimli Oldugu Katman: Tool | Service

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Verification;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsAuthenticodeArtifactSignatureVerifier
    : IArtifactSignatureVerifier
{
    private const uint UiNone = 2;
    private const uint RevokeNone = 0;
    private const uint ChoiceFile = 1;
    private const uint StateActionIgnore = 0;

    private static readonly Guid GenericVerifyV2 =
        new(
            "00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

    public Task<VerificationResult> VerifyAsync(
        string artifactPath,
        ArtifactSignatureDescriptor expectedSignature,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            artifactPath);
        ArgumentNullException.ThrowIfNull(
            expectedSignature);

        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(
                VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    "Authenticode verification requires Windows."));
        }

        if (
            expectedSignature.Algorithm !=
            ArtifactSignatureAlgorithm.Authenticode)
        {
            return Task.FromResult(
                VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    "Artifact signature algorithm is not supported."));
        }

        if (!File.Exists(artifactPath))
        {
            return Task.FromResult(
                VerificationResult.Failed(
                    VerificationFailure.PathRejected,
                    "Artifact file does not exist."));
        }

        var fullPath =
            Path.GetFullPath(
                artifactPath);

        var status =
            VerifyFile(
                fullPath);

        if (status != 0)
        {
            return Task.FromResult(
                VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Authenticode verification failed with status 0x{0:X8}.",
                        status)));
        }

        try
        {
            var identity =
                ReadSignerIdentity(
                    fullPath);

            if (
                !string.Equals(
                    identity.Subject,
                    expectedSignature.PublisherSubject,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(
                    VerificationResult.Failed(
                        VerificationFailure.SignatureInvalid,
                        string.Concat(
                            "Authenticode publisher mismatch. Expected '",
                            expectedSignature.PublisherSubject,
                            "', actual '",
                            identity.Subject,
                            "'.")));
            }

            if (
                expectedSignature.CertificateSha256 is not null &&
                !string.Equals(
                    identity.CertificateSha256,
                    expectedSignature.CertificateSha256,
                    StringComparison.Ordinal))
            {
                return Task.FromResult(
                    VerificationResult.Failed(
                        VerificationFailure.SignatureInvalid,
                        "Authenticode certificate SHA-256 pin mismatch."));
            }

            return Task.FromResult(
                VerificationResult.Passed());
        }
        catch (CryptographicException exception)
        {
            return Task.FromResult(
                VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    string.Concat(
                        "Authenticode signer certificate could not be read: ",
                        exception.Message)));
        }
    }

    private static SignerIdentity ReadSignerIdentity(
        string artifactPath)
    {
#pragma warning disable SYSLIB0057
        using var certificate =
            X509Certificate.CreateFromSignedFile(
                artifactPath);
#pragma warning restore SYSLIB0057

        var subject =
            certificate.Subject;

        var certificateSha256 =
            certificate
                .GetCertHashString(
                    HashAlgorithmName.SHA256)
                .ToLowerInvariant();

        return new SignerIdentity(
            subject,
            certificateSha256);
    }

    private static int VerifyFile(
        string artifactPath)
    {
        var filePathPointer =
            Marshal.StringToCoTaskMemUni(
                artifactPath);

        var fileInfoPointer =
            IntPtr.Zero;

        var trustDataPointer =
            IntPtr.Zero;

        try
        {
            var fileInfo =
                new WinTrustFileInfo
                {
                    StructureSize =
                        (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                    FilePath =
                        filePathPointer,
                    FileHandle =
                        IntPtr.Zero,
                    KnownSubject =
                        IntPtr.Zero
                };

            fileInfoPointer =
                Marshal.AllocHGlobal(
                    Marshal.SizeOf<WinTrustFileInfo>());

            Marshal.StructureToPtr(
                fileInfo,
                fileInfoPointer,
                false);

            var trustData =
                new WinTrustData
                {
                    StructureSize =
                        (uint)Marshal.SizeOf<WinTrustData>(),
                    PolicyCallbackData =
                        IntPtr.Zero,
                    SipClientData =
                        IntPtr.Zero,
                    UiChoice =
                        UiNone,
                    RevocationChecks =
                        RevokeNone,
                    UnionChoice =
                        ChoiceFile,
                    FileInfo =
                        fileInfoPointer,
                    StateAction =
                        StateActionIgnore,
                    StateData =
                        IntPtr.Zero,
                    UrlReference =
                        IntPtr.Zero,
                    ProviderFlags =
                        0,
                    UiContext =
                        0,
                    SignatureSettings =
                        IntPtr.Zero
                };

            trustDataPointer =
                Marshal.AllocHGlobal(
                    Marshal.SizeOf<WinTrustData>());

            Marshal.StructureToPtr(
                trustData,
                trustDataPointer,
                false);

            var action =
                GenericVerifyV2;

            return WinVerifyTrust(
                IntPtr.Zero,
                ref action,
                trustDataPointer);
        }
        finally
        {
            if (
                trustDataPointer !=
                IntPtr.Zero)
            {
                Marshal.FreeHGlobal(
                    trustDataPointer);
            }

            if (
                fileInfoPointer !=
                IntPtr.Zero)
            {
                Marshal.FreeHGlobal(
                    fileInfoPointer);
            }

            Marshal.FreeCoTaskMem(
                filePathPointer);
        }
    }

    private sealed record SignerIdentity(
        string Subject,
        string CertificateSha256);

    [DllImport(
        "wintrust.dll",
        ExactSpelling = true,
        SetLastError = true)]
    private static extern int WinVerifyTrust(
        IntPtr windowHandle,
        ref Guid actionId,
        IntPtr trustData);

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint StructureSize;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint StructureSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
