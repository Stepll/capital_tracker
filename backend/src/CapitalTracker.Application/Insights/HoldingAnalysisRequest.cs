using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// Everything the analysis pipeline is allowed to know about a holding.
///
/// This type is the privacy boundary, and it enforces it structurally rather than
/// by convention: there is simply no member capable of carrying a holding's
/// SecretAttributes, so no future edit to the generator or the prompt builder can
/// leak them by accident. Keep it that way — if secrets ever seem necessary here,
/// that is a design problem, not a missing property.
/// </summary>
public record HoldingAnalysisRequest(
    string Name,
    string? Symbol,
    AccountType AccountType,
    string AccountName,
    string? SectorName,
    decimal? Quantity,
    string Currency,
    decimal CurrentValue,
    string? Notes,
    IReadOnlyDictionary<string, string> Attributes,
    PreviousAnalysis? Previous);

/// <summary>
/// The most recent analysis of this holding, fed back in so the model can mark facts
/// as already-known instead of re-reporting them. Only the latest one — the token cost
/// of a full history buys nothing, and "new since when?" stops being answerable.
/// </summary>
public record PreviousAnalysis(DateTime GeneratedAt, IReadOnlyList<AnalysisFactDto> Facts);

/// <summary>What the model produced, before it becomes a persisted AiInsight.</summary>
public record HoldingAnalysisResult(string Summary, IReadOnlyList<AnalysisFactDto> Facts);
