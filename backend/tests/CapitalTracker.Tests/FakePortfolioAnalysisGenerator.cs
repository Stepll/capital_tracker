using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;

namespace CapitalTracker.Tests;

/// <summary>
/// Portfolio twin of <see cref="FakeAnalysisGenerator"/>: records what reached the model
/// and replays a scripted outcome.
/// </summary>
public class FakePortfolioAnalysisGenerator : IPortfolioAnalysisGenerator
{
    private readonly Func<IEnumerable<AnalysisGenerationEvent>> _script;

    public FakePortfolioAnalysisGenerator(Func<IEnumerable<AnalysisGenerationEvent>>? script = null) =>
        _script = script ?? (() => [AnalysisGenerationEvent.Completed(new AnalysisResult("ok", []))]);

    public PortfolioAnalysisRequest? ReceivedRequest { get; private set; }

    public int CallCount { get; private set; }

    public async IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        PortfolioAnalysisRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ReceivedRequest = request;
        CallCount++;

        foreach (var e in _script())
        {
            yield return e;
        }

        await Task.CompletedTask;
    }
}
