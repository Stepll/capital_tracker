namespace CapitalTracker.Domain.Entities;

/// <summary>
/// The single owner of this instance of the app. Not a multi-tenant user model —
/// Capital Tracker is a personal, single-user application; this entity exists
/// only so credentials can be changed without a redeploy.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string DisplayCurrency { get; set; } = "UAH";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
