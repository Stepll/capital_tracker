using System.Security.Cryptography;
using System.Text;
using CapitalTracker.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Infrastructure.Security;

/// <summary>
/// AES-256-GCM (authenticated encryption — tampering is detected, not just
/// hidden). Output layout: base64(nonce[12] ++ tag[16] ++ ciphertext).
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesEncryptionService(IOptions<AesEncryptionOptions> options)
    {
        _key = Convert.FromBase64String(options.Value.Key);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException(
                "Encryption:Key must decode to exactly 32 bytes (AES-256). " +
                "Generate one with: openssl rand -base64 32");
        }
    }

    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var output = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(output, 0);
        tag.CopyTo(output, NonceSize);
        ciphertext.CopyTo(output, NonceSize + TagSize);
        return Convert.ToBase64String(output);
    }

    public string Decrypt(string ciphertext)
    {
        var input = Convert.FromBase64String(ciphertext);
        var nonce = input[..NonceSize];
        var tag = input[NonceSize..(NonceSize + TagSize)];
        var encrypted = input[(NonceSize + TagSize)..];
        var plaintextBytes = new byte[encrypted.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, encrypted, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
