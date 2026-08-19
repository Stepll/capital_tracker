using CapitalTracker.Application.Insights;

namespace CapitalTracker.Application.Common.Interfaces;

/// <summary>
/// The portfolio-level twin of <see cref="IHoldingAnalysisGenerator"/>: same event
/// contract, same result shape, different subject. Kept as a separate interface rather
/// than a generic one because the two take unrelated request types and nothing would
/// share a call site.
/// </summary>
public interface IPortfolioAnalysisGenerator
{
    IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        PortfolioAnalysisRequest request,
        CancellationToken cancellationToken);
}
