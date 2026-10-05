// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsAuthenticodeArtifactSignatureVerifier.cs
// 📌 Amac: Manifestte Authenticode zorunlu olan artifactin Windows trust policy ile imza dogrulamasini yapar
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: WinVerifyTrust Generic Verify V2 kullanarak signature ve certificate trust zincirini UI olmadan fail-closed dogrular
//
// Bagimli Oldugu Katman: Tool | Service

using System.Runtime.InteropServices;
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

        var status =
            VerifyFile(
                Path.GetFullPath(
                    artifactPath));

        return Task.FromResult(
            status == 0
                ? VerificationResult.Passed()
                : VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Authenticode verification failed with status 0x{0:X8}.",
                        status)));
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
                deleteOld: false);

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
                deleteOld: false);

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
