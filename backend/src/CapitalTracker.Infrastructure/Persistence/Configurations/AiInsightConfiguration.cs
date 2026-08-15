using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalTracker.Infrastructure.Persistence.Configurations;

public class AiInsightConfiguration : IEntityTypeConfiguration<AiInsight>
{
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
    }
}
