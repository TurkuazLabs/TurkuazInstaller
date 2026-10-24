// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/EncryptedSignedThemePackReader.cs
// Amac: AES-256-GCM ile muhurlu tema zarfini, dis yetkili anahtarla bellekte acmak
// Modul - CSharp Security Tool
// Version: 0.2.0-qa
// Aciklama: Anahtar dis yetkili provider'dan gelir; cozulmus TPK ayrica CMS pin ile dogrulanir
// Bagimli Oldugu Katman: Infrastructure | Trust Policy | Cryptography

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Infrastructure.Branding;

/// <summary>
/// A production implementation must authorize the requested product/key ID with a
/// remote authenticated issuer or similarly protected external mechanism.
/// Implementations must return an OWNED fresh 32-byte array; the reader destroys it.
/// Never derive keys from executable bytes, per-user settings or theme package.
/// </summary>
public interface IThemeDecryptionKeyProvider
{
    ValueTask<byte[]?> ResolveKeyAsync(
        PackageId expectedPackageId, string keyId, CancellationToken cancellationToken);
}

/// <summary>
/// .tpe v1 envelope, big-endian:
/// "TURQTPK1" (8), keyId length (1), ASCII keyId (1..64),
/// nonce (12), ciphertext byte count (4), ciphertext, GCM tag (16).
/// All header bytes up through ciphertext length are authenticated AAD.
/// The decrypted plaintext MUST be a separately CMS-signed .tpk.
/// </summary>
public sealed class EncryptedSignedThemePackReader
{
    private static readonly byte[] Magic = "TURQTPK1"u8.ToArray();
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int MaximumKeyIdLength = 64;
    private const int HeaderMinLength = 8 + 1 + 1 + NonceLength + 4;
    private const int MaxEncryptedFileBytes =
        SignedThemePackReader.MaxArchiveBytes + HeaderMinLength + MaximumKeyIdLength + TagLength;

    private readonly SignedThemePackReader _signedReader;
    private readonly IThemeDecryptionKeyProvider _keys;

    public EncryptedSignedThemePackReader(
        SignedThemePackReader signedReader,
        IThemeDecryptionKeyProvider keys)
    {
        _signedReader = signedReader ?? throw new ArgumentNullException(nameof(signedReader));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
    }

    public async Task<VerifiedThemePack> ReadAsync(
        Stream envelope, PackageId expectedProduct,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(expectedProduct);
        cancellationToken.ThrowIfCancellationRequested();

        if (!envelope.CanRead || !envelope.CanSeek ||
            envelope.Length < HeaderMinLength + TagLength ||
            envelope.Length > MaxEncryptedFileBytes)
        {
            throw new InvalidDataException("Encrypted theme envelope is outside its size limits.");
        }

        envelope.Position = 0;
        var start = new byte[9];
        await envelope.ReadExactlyAsync(start, cancellationToken);
        if (!start.AsSpan(0, Magic.Length).SequenceEqual(Magic) ||
            start[8] is < 1 or > MaximumKeyIdLength)
        {
            throw new InvalidDataException("Unsupported encrypted theme envelope header.");
        }

        var idLength = start[8];
        var header = new byte[9 + idLength + NonceLength + 4];
        start.CopyTo(header, 0);
        await envelope.ReadExactlyAsync(
            header.AsMemory(start.Length), cancellationToken);

        var keyIdSpan = header.AsSpan(9, idLength);
        // Intentionally restrictive ID syntax; no file paths, slashes, whitespace
        // or embedded control bytes may influence an external provider request.
        foreach (var value in keyIdSpan)
        {
            if (!((value >= 'a' && value <= 'z') ||
                  (value >= '0' && value <= '9') ||
                  value is '-' or '_'))
            {
                throw new InvalidDataException("Encrypted theme key identifier is invalid.");
            }
        }

        var id = Encoding.ASCII.GetString(keyIdSpan);
        var length = BinaryPrimitives.ReadUInt32BigEndian(
            header.AsSpan(header.Length - 4, 4));
        var actualEncryptedBytes = envelope.Length - header.Length - TagLength;
        if (length is < 1 or > SignedThemePackReader.MaxArchiveBytes ||
            actualEncryptedBytes != length)
        {
            throw new InvalidDataException("Encrypted theme length does not match envelope.");
        }

        var cipher = new byte[(int)length];
        var tag = new byte[TagLength];
        await envelope.ReadExactlyAsync(cipher, cancellationToken);
        await envelope.ReadExactlyAsync(tag, cancellationToken);

        var key = await _keys.ResolveKeyAsync(
            expectedProduct, id, cancellationToken).ConfigureAwait(false);
        if (key is null)
        {
            throw new InvalidDataException("No authorized theme decryption key is available.");
        }

        // The provider must return independently owned key bytes. No key material
        // is logged or written to disk, including when any subsequent step fails.
        byte[]? plaintext = null;
        try
        {
            if (key.Length != 32)
            {
                throw new InvalidDataException("Theme envelope requires a 256-bit key.");
            }

            plaintext = new byte[cipher.Length];
            using (var aes = new AesGcm(key, TagLength))
            {
                aes.Decrypt(
                    header.AsSpan(9 + idLength, NonceLength),
                    cipher, tag, plaintext, header);
            }

            using var signedArchive = new MemoryStream(
                plaintext, writable: false);
            return await _signedReader.ReadAsync(
                signedArchive, expectedProduct, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CryptographicException error)
        {
            throw new InvalidDataException(
                "Theme encryption authentication failed.", error);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(cipher);
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }
}


/// <summary>
/// Writes the optional .tpe envelope from an ALREADY signed theme.tpk.
/// Caller-owned key remains with trusted signing/packaging tooling; never
/// distribute it inside the .tpe, EXE or configuration beside the installer.
/// Use a unique cryptographically random nonce for each encryption operation.
/// </summary>
public static class EncryptedSignedThemePackWriter
{
    public static byte[] Encrypt(
        ReadOnlySpan<byte> signedThemePack, ReadOnlySpan<byte> key, string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        if (key.Length != 32)
        {
            throw new ArgumentException("AES-256-GCM requires exactly 32 key bytes.", nameof(key));
        }
        if (signedThemePack.Length is < 1 or > SignedThemePackReader.MaxArchiveBytes)
        {
            throw new ArgumentException("Theme archive is outside allowed size.", nameof(signedThemePack));
        }
        if (keyId.Length is < 1 or > 64 ||
            keyId.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
        {
            throw new ArgumentException("Invalid external key identifier.", nameof(keyId));
        }

        var keyBytes = Encoding.ASCII.GetBytes(keyId);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var header = new byte[8 + 1 + keyBytes.Length + 12 + 4];
        "TURQTPK1"u8.CopyTo(header);
        header[8] = checked((byte)keyBytes.Length);
        keyBytes.CopyTo(header, 9);
        nonce.CopyTo(header, 9 + keyBytes.Length);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(header.Length - 4, 4),
            checked((uint)signedThemePack.Length));

        var envelope = new byte[header.Length + signedThemePack.Length + 16];
        header.CopyTo(envelope, 0);
        using (var cipher = new AesGcm(key, 16))
        {
            cipher.Encrypt(nonce, signedThemePack,
                envelope.AsSpan(header.Length, signedThemePack.Length),
                envelope.AsSpan(header.Length + signedThemePack.Length, 16),
                header);
        }
        return envelope;
    }
}
