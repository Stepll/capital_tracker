using CapitalTracker.Application.Insights;

namespace CapitalTracker.Application.Common.Interfaces;

/// <summary>
/// Researches a market and reports where money could go, in the same event and result
/// shape as the other two generators.
/// </summary>
public interface IMarketAnalysisGenerator
{
    IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        MarketAnalysisRequest request,
        CancellationToken cancellationToken);
}
