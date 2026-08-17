using CapitalTracker.Application.Insights;

namespace CapitalTracker.Application.Common.Interfaces;

/// <summary>
/// Runs one analysis of a holding, reporting progress as it goes. The single seam
/// between the use case and the LLM — everything provider-specific (the Anthropic SDK,
/// prompt text, market-data lookup, response parsing) lives behind it in Infrastructure,
/// so this layer never references a model vendor and the handler stays unit-testable.
/// </summary>
public interface IHoldingAnalysisGenerator
{
    /// <summary>
    /// Yields <see cref="GenerationEventKind.Phase"/> events while working and exactly one
    /// terminal event — <see cref="GenerationEventKind.Result"/> or
    /// <see cref="GenerationEventKind.Failed"/>. Implementations report failure as an event
    /// rather than an exception; only cancellation propagates.
    /// </summary>
    IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        HoldingAnalysisRequest request,
        CancellationToken cancellationToken);
}
