using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Common.Interfaces;

/// <summary>
/// Narrow view of the DbContext exposed to the Application layer, so use-case
/// handlers don't take a dependency on Infrastructure/EF Core directly.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Holding> Holdings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
