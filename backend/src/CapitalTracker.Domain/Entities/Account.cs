using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Domain.Entities;

/// <summary>
/// A source of holdings: a brokerage account, bank account, a real-estate object,
/// a cash stash or a crypto wallet.
/// </summary>
public class Account
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public AccountType Type { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the owner deleted this account, or null while it is open. Soft for the same
    /// reason as <see cref="Holding.DeletedAt"/>, and deleting an account stamps the same
    /// timestamp onto its holdings rather than relying on a database cascade — a cascade
    /// here would hard-delete live holdings and their history.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public List<Holding> Holdings { get; set; } = [];
}
