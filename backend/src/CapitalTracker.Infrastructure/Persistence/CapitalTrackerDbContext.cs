using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Infrastructure.Persistence;

public class CapitalTrackerDbContext(DbContextOptions<CapitalTrackerDbContext> options)
    : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ValuationSnapshot> ValuationSnapshots => Set<ValuationSnapshot>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<AiInsight> AiInsights => Set<AiInsight>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CapitalTrackerDbContext).Assembly);
    }
}
