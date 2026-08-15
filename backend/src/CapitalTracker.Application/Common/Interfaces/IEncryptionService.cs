namespace CapitalTracker.Application.Common.Interfaces;

/// <summary>
/// Encrypts/decrypts sensitive values (e.g. holding credentials) at the
/// application layer, so ciphertext — never plaintext — is what reaches the
/// database. Distinct from password hashing (<c>IPasswordHasher</c>): these
/// values must be recoverable for display, not just verifiable.
/// </summary>
public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
