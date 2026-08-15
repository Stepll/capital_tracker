namespace CapitalTracker.Application.Holdings;

public record HoldingDto(
    Guid Id,
    Guid AccountId,
    string Name,
    string? Symbol,
    string Currency,
    decimal CurrentValue,
    DateTime CreatedAt);
