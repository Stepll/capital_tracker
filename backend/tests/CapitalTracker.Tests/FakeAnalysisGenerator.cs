using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;

namespace CapitalTracker.Tests;

/// <summary>
/// Stands in for the Anthropic call. Records the request it was handed — which is how the
/// tests assert what does and doesn't reach the model — and replays a scripted outcome.
/// </summary>
public class FakeAnalysisGenerator : IHoldingAnalysisGenerator
{
    private readonly Func<IEnumerable<AnalysisGenerationEvent>> _script;

    public FakeAnalysisGenerator(Func<IEnumerable<AnalysisGenerationEvent>>? script = null) =>
        _script = script ?? (() => [AnalysisGenerationEvent.Completed(new HoldingAnalysisResult("ok", []))]);

    public HoldingAnalysisRequest? ReceivedRequest { get; private set; }

    public int CallCount { get; private set; }

    public async IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        HoldingAnalysisRequest request,
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

    /// <summary>A generator whose model call blows up — nothing should be persisted.</summary>
    public static FakeAnalysisGenerator Failing() =>
        new(() => [AnalysisGenerationEvent.Failed(InsightErrorCode.Upstream)]);
}
