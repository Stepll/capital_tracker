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
        // HoldingId is optional, so EF Core's default convention here is Restrict — which
        // used to fail with a FK violation the moment a deleted holding had any analysis.
        // Holdings are soft-deleted now, so nothing triggers this at all; it is SetNull
        // rather than Cascade purely as a backstop, because a future hard delete should
        // cost the archive its link, not the analysis itself.
        builder.HasOne(i => i.Holding)
            .WithMany()
            .HasForeignKey(i => i.HoldingId)
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
