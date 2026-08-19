using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// The middle of every generation run, shared by the holding and portfolio handlers:
/// map generator events onto client events, persist exactly one successful result, and
/// never leave a client waiting on a stream that has nothing left to send.
///
/// Shared because the subtle parts are identical and easy to get wrong twice — the
/// terminal-event guarantee above all.
/// </summary>
internal static class InsightGenerationPipeline
{
    public static async IAsyncEnumerable<InsightStreamEvent> RunAsync(
        IAsyncEnumerable<AnalysisGenerationEvent> generation,
        Func<AnalysisResult, Task<AiInsightDto?>> saveAsync,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var e in generation.WithCancellation(cancellationToken))
        {
            switch (e.Kind)
            {
                case GenerationEventKind.Phase:
                    yield return InsightStreamEvent.AtPhase(e.Phase!.Value, e.Detail);
                    break;

                case GenerationEventKind.Failed:
                    yield return InsightStreamEvent.Failed(e.ErrorCode ?? InsightErrorCode.Upstream);
                    yield break;

                case GenerationEventKind.Result:
                    yield return InsightStreamEvent.AtPhase(InsightPhase.Saving);

                    var saved = await saveAsync(e.Result!);
                    yield return saved is null
                        ? InsightStreamEvent.Failed(InsightErrorCode.Internal)
                        : InsightStreamEvent.Completed(saved);
                    yield break;
            }
        }

        // The generator finished without a terminal event — treated as a failed run so the
        // client never sits waiting on a stream that will produce nothing more.
        yield return InsightStreamEvent.Failed(InsightErrorCode.Upstream);
    }

    /// <summary>
    /// Runs a step that touches the database, turning failure into null so the caller can
    /// yield an event for it — C# forbids `yield return` inside a try that has a catch.
    /// Cancellation is rethrown: that is the client hanging up, not an error.
    /// </summary>
    public static async Task<T?> TryAsync<T>(Func<Task<T>> step, ILogger logger, string what)
        where T : class
    {
        try
        {
            return await step();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis failed while {What}.", what);
            return null;
        }
    }
}
