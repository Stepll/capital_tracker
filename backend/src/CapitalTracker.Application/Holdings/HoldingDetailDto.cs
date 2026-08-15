namespace CapitalTracker.Application.Holdings;

public record ValuationPointDto(DateOnly Date, decimal Value);

public record HoldingDetailDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Name,
    string? Symbol,
    string Currency,
    decimal CurrentValue,
    Guid? SectorId,
    string? SectorName,
    DateTime CreatedAt,
    List<ValuationPointDto> ValuationHistory);
