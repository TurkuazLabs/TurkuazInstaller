// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsCredentialManagerReader.cs
// 📌 Amac: Windows Credential Manager generic credential blobunu Win32 CredRead ile guvenli sekilde okur
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Missing credential icin null, diger Win32 hatalari icin exception dondurur; blob byte bufferini okuma sonrasi sifirlar
//
// Bagimli Oldugu Katman: Tool

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsCredentialManagerReader
    : IWindowsCredentialReader
{
    private const uint GenericCredentialType = 1;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobBytes = 16384;

    public string? ReadGenericSecret(
        string targetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            targetName);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows Credential Manager is only available on Windows.");
        }

        if (
            !CredRead(
                targetName,
                GenericCredentialType,
                0,
                out var credentialPointer))
        {
            var error =
                Marshal.GetLastWin32Error();

            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(
                error,
                "Windows Credential Manager read failed.");
        }

        try
        {
            var credential =
                Marshal.PtrToStructure<NativeCredential>(
                    credentialPointer);

            if (credential.CredentialBlobSize == 0)
            {
                return null;
            }

            if (
                credential.CredentialBlob == IntPtr.Zero ||
                credential.CredentialBlobSize >
                    MaximumCredentialBlobBytes ||
                credential.CredentialBlobSize % 2 != 0)
            {
                throw new InvalidDataException(
                    "Windows generic credential secret blob is invalid.");
            }

            var buffer =
                new byte[
                    checked(
                        (int)credential.CredentialBlobSize)];

            try
            {
                Marshal.Copy(
                    credential.CredentialBlob,
                    buffer,
                    0,
                    buffer.Length);

                var secret =
                    Encoding.Unicode
                        .GetString(
                            buffer)
                        .TrimEnd(
                            '\0')
                        .Trim();

                return string.IsNullOrWhiteSpace(
                    secret)
                    ? null
                    : secret;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    buffer);
            }
        }
        finally
        {
            CredFree(
                credentialPointer);
        }
    }

    [DllImport(
        "advapi32.dll",
        EntryPoint = "CredReadW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint flags,
        out IntPtr credential);

    [DllImport(
        "advapi32.dll",
        EntryPoint = "CredFree",
        SetLastError = false)]
    private static extern void CredFree(
        IntPtr credential);

    [StructLayout(
        LayoutKind.Sequential)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
