using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Holdings;

public record ValuationPointDto(DateOnly Date, decimal Value);

public record HoldingDetailDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    AccountType AccountType,
    string Name,
    string? Symbol,
    decimal? Quantity,
    string? Notes,
    string Currency,
    decimal CurrentValue,
    Guid? SectorId,
    string? SectorName,
    DateTime CreatedAt,
    List<ValuationPointDto> ValuationHistory,
    Dictionary<string, string> Attributes,
    // Only the keys — never ciphertext or plaintext values. The client asks
    // for one value at a time via RevealSecretAttributeQuery, on demand.
    List<string> SecretAttributeKeys,
    bool ExcludeFromAiAnalysis,
    // Null when an analysis can be run right now. Surfaced on read so the button
    // can be disabled before the click rather than after a wasted round trip.
    DateTime? NextAnalysisAvailableAt);
