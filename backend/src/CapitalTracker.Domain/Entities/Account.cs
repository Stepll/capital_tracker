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

    public List<Holding> Holdings { get; set; } = [];
}
