using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalTracker.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        // Soft-deleted accounts are invisible by default everywhere, rather than by each
        // query remembering to ask. Reads that genuinely want them — the history series,
        // the archive — opt in with IgnoreQueryFilters() and say why.
        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
