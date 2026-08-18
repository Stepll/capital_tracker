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
    PricingMode PricingMode,
    bool ExcludeFromAiAnalysis,
    // Null when an analysis can be run right now. Surfaced on read so the button
    // can be disabled before the click rather than after a wasted round trip.
    DateTime? NextAnalysisAvailableAt,
    // Set once the holding is deleted. This query is the only one that looks past the
    // soft-delete filter, so the page still opens — links to it have to keep working —
    // and renders read-only. Every other handler reads the filtered set, where a deleted
    // holding simply isn't there.
    DateTime? DeletedAt);
