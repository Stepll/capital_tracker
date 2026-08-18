using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Tests;

/// <summary>
/// In-memory stand-in for the real DbContext. IApplicationDbContext exposes DbSet&lt;T&gt;
/// rather than IQueryable&lt;T&gt;, so handlers need an actual context — a hand-rolled fake
/// isn't an option.
/// </summary>
public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IApplicationDbContext
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<ValuationSnapshot> ValuationSnapshots => Set<ValuationSnapshot>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<AiInsight> AiInsights => Set<AiInsight>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    public static TestDbContext Create() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The production configurations live in Infrastructure, which this project
        // deliberately doesn't reference. Value converters are provider-agnostic, so
        // mirroring them here is enough to make the JSON-backed collections round-trip
        // — which is the point: the fact list has to survive a save/load to be worth
        // asserting on.
        modelBuilder.Entity<Holding>().Property(h => h.Attributes).HasConversion(
            d => JsonSerializer.Serialize(d, Json),
            s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, Json) ?? new());

        modelBuilder.Entity<Holding>().Property(h => h.SecretAttributes).HasConversion(
            d => JsonSerializer.Serialize(d, Json),
            s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, Json) ?? new());

        // Mirrored for the same reason as the converters above: the soft-delete filters
        // are what keep a deleted holding out of every total, so a test context without
        // them would happily prove behaviour production doesn't have.
        modelBuilder.Entity<Account>().HasQueryFilter(a => a.DeletedAt == null);
        modelBuilder.Entity<Holding>().HasQueryFilter(h => h.DeletedAt == null);

        modelBuilder.Entity<AiInsight>().Property(i => i.Facts).HasConversion(
            f => JsonSerializer.Serialize(f, Json),
            s => JsonSerializer.Deserialize<List<AnalysisFact>>(s, Json) ?? new());
    }
}
