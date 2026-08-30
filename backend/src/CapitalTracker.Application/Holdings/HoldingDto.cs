using CapitalTracker.Application.Common;

namespace CapitalTracker.Application.Holdings;

public record HoldingDto(
    Guid Id,
    Guid AccountId,
    string Name,
    string? Symbol,
    string Currency,
    decimal CurrentValue,
    DateTime CreatedAt,
    DateOnly? LastValuedOn,
    // Set once the position has been sold out — filled in alongside ValuationAge below.
    DateOnly? ClosedOn = null,
    // Left null by the EF projection, which knows nothing about the account type or the
    // holding's position: HoldingQueries.WithValuationAge fills it in memory afterwards,
    // and both callers of the projection go through it so neither can forget.
    ValuationAgeDto? ValuationAge = null);
