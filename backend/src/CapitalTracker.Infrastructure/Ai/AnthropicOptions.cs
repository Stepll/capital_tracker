namespace CapitalTracker.Infrastructure.Ai;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>
    /// Bound from configuration (`Anthropic:ApiKey`) rather than left to the SDK's
    /// ambient ANTHROPIC_API_KEY pickup, so it fails fast at startup alongside
    /// Jwt:Secret and Encryption:Key instead of on the first analysis request.
    /// </summary>
    public required string ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5";

    /// <summary>
    /// Caps thinking *and* visible output together — a tight value truncates the
    /// analysis mid-JSON rather than shortening it.
    /// </summary>
    public int MaxTokens { get; set; } = 24000;

    /// <summary>Billed at $10 per 1000 searches, so bounded rather than open-ended.</summary>
    public int MaxWebSearches { get; set; } = 8;
}
