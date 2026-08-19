using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// Analyses the portfolio as a whole — composition, concentration, currency exposure —
/// rather than one asset at a time. Same streaming contract as the per-holding command,
/// same cooldown reasoning, and its own cooldown window: the two scopes answer different
/// questions and neither should block the other.
/// </summary>
public record StreamPortfolioInsightCommand(Guid UserId) : IStreamRequest<InsightStreamEvent>;

public class StreamPortfolioInsightCommandHandler(
    IApplicationDbContext db,
    IPortfolioAnalysisGenerator generator,
    IOptions<InsightsOptions> insightsOptions,
    ILogger<StreamPortfolioInsightCommandHandler> logger)
    : IStreamRequestHandler<StreamPortfolioInsightCommand, InsightStreamEvent>
{
    public async IAsyncEnumerable<InsightStreamEvent> Handle(
        StreamPortfolioInsightCommand request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Cheap guards before anything billable, and the cooldown clock is the last stored
        // portfolio insight — nothing is written unless a run succeeds, so a failed attempt
        // costs no waiting time.
        var lastGeneratedAt = await db.AiInsights
            .Where(i => i.Scope == InsightScope.Portfolio)
            .MaxAsync(i => (DateTime?)i.GeneratedAt, cancellationToken);

        var cooldown = TimeSpan.FromHours(insightsOptions.Value.CooldownHours);
        if (lastGeneratedAt is not null && DateTime.UtcNow - lastGeneratedAt.Value < cooldown)
        {
            yield return InsightStreamEvent.Failed(InsightErrorCode.Cooldown, lastGeneratedAt.Value.Add(cooldown));
            yield break;
        }

        yield return InsightStreamEvent.AtPhase(InsightPhase.Preparing);

        var analysisRequest = await InsightGenerationPipeline.TryAsync(
            () => BuildRequestAsync(request.UserId, cancellationToken), logger, "building the portfolio request");
        if (analysisRequest is null)
        {
            yield return InsightStreamEvent.Failed(InsightErrorCode.Internal);
            yield break;
        }

        // An empty portfolio — or one where every asset is opted out — has nothing to say,
        // and saying it costs a model call. Refused here rather than after the bill.
        if (analysisRequest.Holdings.Count == 0)
        {
            yield return InsightStreamEvent.Failed(InsightErrorCode.Empty);
            yield break;
        }

        await foreach (var e in InsightGenerationPipeline.RunAsync(
            generator.GenerateAsync(analysisRequest, cancellationToken),
            result => InsightGenerationPipeline.TryAsync(
                () => SaveAsync(result), logger, "saving the portfolio analysis"),
            cancellationToken))
        {
            yield return e;
        }
    }

    private async Task<PortfolioAnalysisRequest> BuildRequestAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        var converter = await CurrencyConverter.LoadAsync(db, cancellationToken);

        // Flat fetches folded in C# — same reasoning as the dashboard summary. Deleted
        // holdings are filtered out globally, which is what we want here: the analysis is
        // about what is held now.
        var holdings = await db.Holdings.ToListAsync(cancellationToken);
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, cancellationToken);
        var snapshots = await db.ValuationSnapshots.ToListAsync(cancellationToken);
        var holdingInsights = await db.AiInsights
            .Where(i => i.Scope == InsightScope.Holding)
            .ToListAsync(cancellationToken);

        var snapshotsByHolding = snapshots.ToLookup(s => s.HoldingId);
        var insightsByHolding = holdingInsights
            .Where(i => i.HoldingId is not null)
            .ToLookup(i => i.HoldingId!.Value);

        var analysable = holdings
            .Where(h => !h.ExcludeFromAiAnalysis && accounts.ContainsKey(h.AccountId))
            .ToList();

        var summaries = analysable
            .Select(h =>
            {
                var account = accounts[h.AccountId];
                var latest = snapshotsByHolding[h.Id].OrderByDescending(s => s.Date).FirstOrDefault();
                var currency = latest?.Currency ?? account.Currency;
                var value = latest?.Value ?? 0m;

                return new PortfolioHoldingSummary(
                    h.Name,
                    h.Symbol,
                    account.Type,
                    account.Name,
                    value,
                    currency,
                    converter.Convert(value, currency, user.DisplayCurrency),
                    h.Quantity,
                    insightsByHolding[h.Id]
                        .OrderByDescending(i => i.GeneratedAt)
                        .FirstOrDefault()
                        ?.ToDto().Facts ?? []);
            })
            .OrderByDescending(h => h.ValueInDisplayCurrency)
            .ToList();

        // Only the most recent portfolio analysis, so the model can mark what is genuinely
        // new — same reasoning as the per-holding one.
        var previous = (await db.AiInsights
                .Where(i => i.Scope == InsightScope.Portfolio)
                .ToListAsync(cancellationToken))
            .OrderByDescending(i => i.GeneratedAt)
            .FirstOrDefault();

        return new PortfolioAnalysisRequest(
            user.DisplayCurrency,
            summaries.Sum(h => h.ValueInDisplayCurrency),
            summaries,
            holdings.Count - analysable.Count,
            previous is null ? null : new PreviousAnalysis(previous.GeneratedAt, previous.ToDto().Facts));
    }

    private async Task<AiInsightDto> SaveAsync(AnalysisResult result)
    {
        var insight = new AiInsight
        {
            Id = Guid.NewGuid(),
            Scope = InsightScope.Portfolio,
            // No holding: this analysis is about the shape of the whole portfolio, and the
            // archive tells the two apart by scope rather than by which key is set.
            HoldingId = null,
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

        // CancellationToken.None on purpose: the model call is already paid for, so a
        // client that walked away mid-save should still get the result stored.
        await db.SaveChangesAsync(CancellationToken.None);

        return insight.ToDto();
    }
}
