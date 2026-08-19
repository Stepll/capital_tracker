namespace CapitalTracker.Application.Insights;

public enum GenerationEventKind
{
    Phase,
    Result,
    Failed
}

/// <summary>
/// What the generator emits as it works. Distinct from <see cref="InsightStreamEvent"/>
/// because this side carries an unsaved <see cref="AnalysisResult"/>, while the
/// client-facing side carries a persisted <see cref="AiInsightDto"/> with an id.
/// </summary>
public record AnalysisGenerationEvent
{
    public required GenerationEventKind Kind { get; init; }

    public InsightPhase? Phase { get; init; }

    /// <summary>
    /// Free text for the current phase — the model's own search query, or a snippet of
    /// its reasoning. Passed straight through to the UI untranslated, since it comes
    /// from the model rather than from us.
    /// </summary>
    public string? Detail { get; init; }

    public AnalysisResult? Result { get; init; }

    public InsightErrorCode? ErrorCode { get; init; }

    public static AnalysisGenerationEvent AtPhase(InsightPhase phase, string? detail = null) =>
        new() { Kind = GenerationEventKind.Phase, Phase = phase, Detail = detail };

    public static AnalysisGenerationEvent Completed(AnalysisResult result) =>
        new() { Kind = GenerationEventKind.Result, Result = result };

    public static AnalysisGenerationEvent Failed(InsightErrorCode code) =>
        new() { Kind = GenerationEventKind.Failed, ErrorCode = code };
}
