namespace CapitalTracker.Application.Insights;

public enum InsightStreamEventType
{
    Phase,
    Completed,
    Failed
}

/// <summary>
/// One SSE frame. Every outcome — including "too soon" and "not found" — travels as an
/// event on a 200 response rather than an HTTP status code: the response has already
/// started streaming by the time most of these are known, and the modal is the only
/// consumer, so there is nothing to gain from a status code it would have to special-case.
/// </summary>
public record InsightStreamEvent
{
    public required InsightStreamEventType Type { get; init; }

    public InsightPhase? Phase { get; init; }
    public string? Detail { get; init; }

    public AiInsightDto? Insight { get; init; }

    public InsightErrorCode? ErrorCode { get; init; }

    /// <summary>Set only on <see cref="InsightErrorCode.Cooldown"/>.</summary>
    public DateTime? RetryAt { get; init; }

    public static InsightStreamEvent AtPhase(InsightPhase phase, string? detail = null) =>
        new() { Type = InsightStreamEventType.Phase, Phase = phase, Detail = detail };

    public static InsightStreamEvent Completed(AiInsightDto insight) =>
        new() { Type = InsightStreamEventType.Completed, Insight = insight };

    public static InsightStreamEvent Failed(InsightErrorCode code, DateTime? retryAt = null) =>
        new() { Type = InsightStreamEventType.Failed, ErrorCode = code, RetryAt = retryAt };
}
