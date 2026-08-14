using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CapitalTracker.Infrastructure.Auth;

/// <summary>
/// Ensures the single owner account exists. Credentials come from configuration
/// (InitialUser:Email / InitialUser:Password, e.g. via env vars) so they never
/// live in source control; the password is only ever stored hashed.
/// </summary>
public class UserSeeder(
    CapitalTrackerDbContext db,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    ILogger<UserSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(cancellationToken))
            return;

        var email = configuration["InitialUser:Email"];
        var password = configuration["InitialUser:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No users exist and InitialUser:Email/Password are not configured — skipping seed.");
            return;
        }

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.Hash(password),
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded initial user {Email}.", email);
    }
}
