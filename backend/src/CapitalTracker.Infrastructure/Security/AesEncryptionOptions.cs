namespace CapitalTracker.Infrastructure.Security;

public class AesEncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>Base64-encoded 32-byte (AES-256) key. Generate with e.g. `openssl rand -base64 32`.</summary>
    public required string Key { get; set; }
}
