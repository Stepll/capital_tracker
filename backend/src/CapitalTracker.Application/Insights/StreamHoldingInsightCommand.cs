using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// Generates a fresh analysis for one holding, streaming progress as it goes.
///
/// A stream request rather than a plain one because a run takes 1-3 minutes and the
/// UI needs to show what is happening. Note that MediatR keeps IStreamPipelineBehavior
/// separate from IPipelineBehavior — if this project ever grows a validation or logging
/// behaviour, it will NOT apply here unless it is registered as the stream variant.
/// </summary>
public record StreamHoldingInsightCommand(Guid HoldingId) : IStreamRequest<InsightStreamEvent>;

public class StreamHoldingInsightCommandHandler(
    IApplicationDbContext db,
    IHoldingAnalysisGenerator generator,
    IOptions<InsightsOptions> insightsOptions,
    ILogger<StreamHoldingInsightCommandHandler> logger)
    : IStreamRequestHandler<StreamHoldingInsightCommand, InsightStreamEvent>
{
    public async IAsyncEnumerable<InsightStreamEvent> Handle(
        StreamHoldingInsightCommand request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // SingleOrDefault, not Single: this method is lazy, so a thrown exception would
        // surface on the first MoveNextAsync — after the controller has already committed
        // SSE headers and can no longer turn it into a status code.
        var holding = await db.Holdings.SingleOrDefaultAsync(h => h.Id == request.HoldingId, cancellationToken);
        if (holding is null)
        {
            yield return InsightStreamEvent.Failed(InsightErrorCode.NotFound);
            yield break;
        }

        if (holding.ExcludeFromAiAnalysis)
        {
            yield return InsightStreamEvent.Failed(InsightErrorCode.Excluded);
            yield break;
        }

        // Every cheap guard runs before anything billable. Because only successful runs
        // are persisted, "last stored insight" IS the cooldown clock — a failed or
        // cancelled attempt leaves no row and therefore costs the user no waiting time.
        var lastGeneratedAt = await db.AiInsights
            .Where(i => i.HoldingId == holding.Id)
            .MaxAsync(i => (DateTime?)i.GeneratedAt, cancellationToken);

        var cooldown = TimeSpan.FromHours(insightsOptions.Value.CooldownHours);
        if (lastGeneratedAt is not null && DateTime.UtcNow - lastGeneratedAt.Value < cooldown)
        {
            yield return InsightStreamEvent.Failed(InsightErrorCode.Cooldown, lastGeneratedAt.Value.Add(cooldown));
            yield break;
        }

        yield return InsightStreamEvent.AtPhase(InsightPhase.Preparing);

        // C# forbids `yield return` inside a try that has a catch, so anything that can
        // throw is wrapped in a helper returning null on failure and yielded outside it.
        // Without this a database hiccup would drop the connection with no frame at all,
        // leaving the modal to guess what happened.
        var analysisRequest = await TryAsync(() => BuildRequestAsync(holding, cancellationToken), "building the analysis request");
        if (analysisRequest is null)
        {
            yield return InsightStreamEvent.Failed(InsightErrorCode.Internal);
            yield break;
        }

        await foreach (var e in generator.GenerateAsync(analysisRequest, cancellationToken))
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

                    var saved = await TryAsync(() => SaveAsync(holding, e.Result!), "saving the analysis");
                    yield return saved is null
                        ? InsightStreamEvent.Failed(InsightErrorCode.Internal)
                        : InsightStreamEvent.Completed(saved);
                    yield break;
            }
        }

        // The generator finished without a terminal event — treat as a failed run so the
        // client never sits waiting on a stream that has nothing left to send.
        yield return InsightStreamEvent.Failed(InsightErrorCode.Upstream);
    }

    /// <summary>
    /// Runs a step that touches the database, turning failure into null so the caller can
    /// yield an event for it. Cancellation is rethrown — that is the client hanging up,
    /// not an error, and the controller handles it.
    /// </summary>
    private async Task<T?> TryAsync<T>(Func<Task<T>> step, string what) where T : class
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
            logger.LogError(ex, "Holding analysis failed while {What}.", what);
            return null;
        }
    }

    private async Task<HoldingAnalysisRequest> BuildRequestAsync(Holding holding, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleAsync(a => a.Id == holding.AccountId, cancellationToken);

        var sectorName = holding.SectorId is null
            ? null
            : (await db.Sectors.SingleOrDefaultAsync(s => s.Id == holding.SectorId, cancellationToken))?.Name;

        // Flat fetch, sorted in memory — same reasoning as GetHoldingByIdQuery.
        var latest = (await db.ValuationSnapshots
                .Where(v => v.HoldingId == holding.Id)
                .ToListAsync(cancellationToken))
            .OrderBy(v => v.Date)
            .LastOrDefault();

        // Only the most recent analysis: enough to tell new findings from repeats,
        // without spending tokens on history the model would have to reconcile.
        var previous = (await db.AiInsights
                .Where(i => i.HoldingId == holding.Id)
                .ToListAsync(cancellationToken))
            .OrderByDescending(i => i.GeneratedAt)
            .FirstOrDefault();

        return new HoldingAnalysisRequest(
            holding.Name,
            holding.Symbol,
            account.Type,
            account.Name,
            sectorName,
            holding.Quantity,
            latest?.Currency ?? account.Currency,
            latest?.Value ?? 0m,
            holding.Notes,
            holding.Attributes,
            previous is null
                ? null
                : new PreviousAnalysis(previous.GeneratedAt, previous.ToDto().Facts));
    }

    private async Task<AiInsightDto> SaveAsync(Holding holding, HoldingAnalysisResult result)
    {
        var insight = new AiInsight
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            // Explicit, not inferred from the FK being set: the archive groups by scope,
            // and the portfolio and market analyses coming next carry no holding at all.
            Scope = InsightScope.Holding,
            Summary = result.Summary,
            Facts = result.Facts
                .Select(f => new AnalysisFact
                {
                    Claim = f.Claim,
                    Category = f.Category,
                    Polarity = f.Polarity,
                    Confidence = f.Confidence,
                    IsNew = f.IsNew,
                    SourceName = f.SourceName,
                    SourceUrl = f.SourceUrl,
                    SourceDate = f.SourceDate,
                })
                .ToList(),
            SourceUrls = result.Facts
                .Select(f => f.SourceUrl)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct()
                .ToList()!,
        };

        db.AiInsights.Add(insight);

        // CancellationToken.None on purpose: the model call is already paid for by this
        // point, so a client that walked away mid-save should still get the result stored
        // rather than throwing away tokens that have been billed either way.
        await db.SaveChangesAsync(CancellationToken.None);

        return insight.ToDto();
    }
}
