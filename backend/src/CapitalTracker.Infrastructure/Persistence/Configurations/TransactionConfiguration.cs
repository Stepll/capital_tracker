using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalTracker.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        // Inherited from the Holding.Quantity column this table replaced — fractional
        // shares and eight-decimal crypto both have to survive a round trip.
        builder.Property(t => t.Quantity).HasPrecision(28, 10);

        // Money, so the same fixed precision as ValuationSnapshot.Value.
        builder.Property(t => t.UnitPrice).HasPrecision(18, 2);

        builder.Property(t => t.Currency).HasMaxLength(3);

        // Both lists read by holding and order by date; this replaces the plain FK index.
        builder.HasIndex(t => new { t.HoldingId, t.Date });

        // Deliberately no query filter, exactly like ValuationSnapshot: a soft-deleted
        // holding keeps its history, and its page still shows it.
    }
}
