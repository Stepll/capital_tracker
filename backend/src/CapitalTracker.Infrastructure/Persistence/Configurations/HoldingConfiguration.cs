using System.Text.Json;
using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalTracker.Infrastructure.Persistence.Configurations;

public class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        var dictionaryComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            d => d.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key, kv.Value)),
            d => new Dictionary<string, string>(d));

        builder.Property(h => h.Attributes)
            .HasColumnType("jsonb")
            .HasConversion(
                d => JsonSerializer.Serialize(d, JsonSerializerOptions.Default),
                s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, JsonSerializerOptions.Default)
                    ?? new Dictionary<string, string>())
            .Metadata.SetValueComparer(dictionaryComparer);

        builder.Property(h => h.SecretAttributes)
            .HasColumnType("jsonb")
            .HasConversion(
                d => JsonSerializer.Serialize(d, JsonSerializerOptions.Default),
                s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, JsonSerializerOptions.Default)
                    ?? new Dictionary<string, string>())
            .Metadata.SetValueComparer(dictionaryComparer);

        // Same default-safe rule as AccountConfiguration: twelve places read Holdings,
        // and one forgotten filter would put a deleted asset back into the net worth.
        // Deleting an account stamps its holdings too, so this single predicate covers
        // both cases without a join into Account.
        builder.HasQueryFilter(h => h.DeletedAt == null);
    }
}
