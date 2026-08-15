using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalTracker.Infrastructure.Persistence.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.HasIndex(r => new { r.Date, r.Currency }).IsUnique();
        builder.Property(r => r.Currency).HasMaxLength(3);
        builder.Property(r => r.RateToUah).HasPrecision(18, 6);
    }
}
