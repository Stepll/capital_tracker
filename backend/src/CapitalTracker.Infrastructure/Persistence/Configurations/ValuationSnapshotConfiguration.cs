using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalTracker.Infrastructure.Persistence.Configurations;

public class ValuationSnapshotConfiguration : IEntityTypeConfiguration<ValuationSnapshot>
{
    public void Configure(EntityTypeBuilder<ValuationSnapshot> builder)
    {
        // "One valuation per holding per day" was app convention only, enforced by an
        // upsert doing SingleOrDefaultAsync. Production already drifted — one holding
        // ended up with three rows on the same date, after which that same
        // SingleOrDefaultAsync throws and its page can no longer be updated at all.
        // With the price job about to become a second writer, the invariant needs to
        // live in the database.
        builder.HasIndex(v => new { v.HoldingId, v.Date }).IsUnique();

        builder.Property(v => v.Currency).HasMaxLength(3);

        // Money, so fixed precision — matches ExchangeRate.RateToUah and Holding.Quantity,
        // both of which already declare theirs. Also keeps the stored value identical to
        // what the price job rounds to before saving.
        builder.Property(v => v.Value).HasPrecision(18, 2);
    }
}
