namespace CapitalTracker.Infrastructure.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Secret { get; set; }
    public string Issuer { get; set; } = "CapitalTracker";
    public string Audience { get; set; } = "CapitalTracker";
    public int ExpiryMinutes { get; set; } = 60 * 24 * 7; // one week — personal single-user app
}
