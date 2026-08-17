using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalTracker.Infrastructure.Persistence.Configurations;

public class AiInsightConfiguration : IEntityTypeConfiguration<AiInsight>
{
    // Deliberately NOT JsonSerializerOptions.Default (which HoldingConfiguration uses
    // for its string dictionaries). Facts carry enums, and the default options write
    // those as integers — so the stored jsonb would silently change meaning the first
    // time anyone reordered FactCategory. Names cost a few bytes and survive edits.
    private static readonly JsonSerializerOptions FactsJson = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public void Configure(EntityTypeBuilder<AiInsight> builder)
    {
        // Both FKs are optional (SectorId/HoldingId are nullable — exactly one is
        // set per insight), so EF Core's default convention is Restrict rather
        // than the Cascade a required FK gets. Without this, deleting a holding
        // (directly, or via its account cascading) fails with a FK violation
        // the moment it has any insight history.
        builder.HasOne(i => i.Holding)
            .WithMany()
            .HasForeignKey(i => i.HoldingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Sector)
            .WithMany()
            .HasForeignKey(i => i.SectorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(i => i.Facts)
            .HasColumnType("jsonb")
            .HasConversion(
                f => JsonSerializer.Serialize(f, FactsJson),
                s => JsonSerializer.Deserialize<List<AnalysisFact>>(s, FactsJson) ?? new List<AnalysisFact>())
            // AnalysisFact is a mutable class with no value semantics, so comparing
            // by reference would miss edits and snapshotting by reference would let
            // the change tracker mutate its own baseline. Round-tripping through JSON
            // is the cheap correct option for a list this small.
            .Metadata.SetValueComparer(new ValueComparer<List<AnalysisFact>>(
                (a, b) => JsonSerializer.Serialize(a, FactsJson) == JsonSerializer.Serialize(b, FactsJson),
                f => JsonSerializer.Serialize(f, FactsJson).GetHashCode(),
                f => JsonSerializer.Deserialize<List<AnalysisFact>>(
                    JsonSerializer.Serialize(f, FactsJson), FactsJson)!));
    }
}
