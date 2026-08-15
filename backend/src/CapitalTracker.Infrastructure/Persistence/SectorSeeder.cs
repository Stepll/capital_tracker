using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalTracker.Infrastructure.Persistence;

/// <summary>
/// Seeds a starter list of sectors so the dropdown isn't empty on first use.
/// The user can add their own afterward — this just saves typing the obvious
/// ones (only runs if the table is empty, so it never fights user edits).
/// </summary>
public class SectorSeeder(CapitalTrackerDbContext db, ILogger<SectorSeeder> logger)
{
    private static readonly string[] DefaultSectors =
    [
        "Технології", "Нерухомість", "Фінанси", "Енергетика",
        "Охорона здоров'я", "Споживчі товари", "Промисловість", "Інше",
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Sectors.AnyAsync(cancellationToken))
            return;

        db.Sectors.AddRange(DefaultSectors.Select(name => new Sector { Id = Guid.NewGuid(), Name = name }));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} default sectors.", DefaultSectors.Length);
    }
}
