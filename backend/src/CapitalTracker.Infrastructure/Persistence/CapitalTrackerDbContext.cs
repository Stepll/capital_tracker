using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CapitalTracker.Infrastructure.Persistence;

public class CapitalTrackerDbContext(DbContextOptions<CapitalTrackerDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ValuationSnapshot> ValuationSnapshots => Set<ValuationSnapshot>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<AiInsight> AiInsights => Set<AiInsight>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Holding is soft-deletable while its ValuationSnapshots and Transactions are not,
        // and EF warns about that asymmetry on every model build. It is deliberate: the
        // net worth history reads snapshots of deleted holdings on purpose (see
        // GetDashboardSummaryQuery), so filtering them to match would reintroduce the
        // rewritten-past bug soft deletion was added to fix. Silenced here, once, rather
        // than left to shout in the logs where a real warning would go unnoticed.
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CapitalTrackerDbContext).Assembly);
    }
}
