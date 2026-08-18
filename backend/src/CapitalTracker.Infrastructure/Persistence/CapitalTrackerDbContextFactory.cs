using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CapitalTracker.Infrastructure.Persistence;

/// <summary>
/// Builds a DbContext for `dotnet ef` without going through the Api's host.
///
/// Without this, scaffolding a migration boots Program.cs, which fails fast on a missing
/// Anthropic:ApiKey — so adding a migration would require an LLM key that has nothing to
/// do with the schema. The eager validation is worth keeping for real startups; this just
/// keeps design time out of its way.
///
/// The connection string is only used to pick the provider — `migrations add` never opens
/// a connection. `database update` does, and for that the Api applies migrations on
/// startup anyway.
/// </summary>
public class CapitalTrackerDbContextFactory : IDesignTimeDbContextFactory<CapitalTrackerDbContext>
{
    public CapitalTrackerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=capital_tracker;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<CapitalTrackerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new CapitalTrackerDbContext(options);
    }
}
