namespace CapitalTracker.Domain.Entities;

/// <summary>
/// Reference sector/industry, used to group holdings for AI analytics
/// (e.g. "Technology", "Real Estate", "Energy").
/// </summary>
public class Sector
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public List<Holding> Holdings { get; set; } = [];
}
