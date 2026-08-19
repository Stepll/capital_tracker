using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// Researches a market — Ukraine or the world — and reports where money could go.
///
/// Unlike the other two scopes this one runs even with an empty portfolio: the subject is
/// the market, and the holdings are context for the answer rather than its subject. Each
/// focus keeps its own cooldown window, since the two are separate questions.
/// </summary>
public record StreamMarketInsightCommand(Guid UserId, MarketFocus Focus) : IStreamRequest<InsightStreamEvent>;

public class StreamMarketInsightCommandHandler(
    IApplicationDbContext db,
    IMarketAnalysisGenerator generator,
    IOptions<InsightsOptions> insightsOptions,
    ILogger<StreamMarketInsightCommandHandler> logger)
    : IStreamRequestHandler<StreamMarketInsightCommand, InsightStreamEvent>
{
    public async IAsyncEnumerable<InsightStreamEvent> Handle(
        StreamMarketInsightCommand request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var scope = request.Focus == MarketFocus.Ukraine
            ? InsightScope.MarketUkraine
            : InsightScope.MarketGlobal;

        // Cheap guards before anything billable; only successful runs are stored, so a
        // failed attempt costs no waiting time.
        var lastGeneratedAt = await db.AiInsights
            .Where(i => i.Scope == scope)
            .MaxAsync(i => (DateTime?)i.GeneratedAt, cancellationToken);

        var cooldown = TimeSpan.FromHours(insightsOptions.Value.CooldownHours);
        if (lastGeneratedAt is not null && DateTime.UtcNow - lastGeneratedAt.Value < cooldown)
        {
            yield return InsightStreamEvent.Failed(InsightErrorCode.Cooldown, lastGeneratedAt.Value.Add(cooldown));
            yield break;
        }

        yield return InsightStreamEvent.AtPhase(InsightPhase.Preparing);

        var analysisRequest = await InsightGenerationPipeline.TryAsync(
            () => BuildRequestAsync(request, scope, cancellationToken), logger, "building the market request");
        if (analysisRequest is null)
        {
            yield return InsightStreamEvent.Failed(InsightErrorCode.Internal);
            yield break;
        }

        await foreach (var e in InsightGenerationPipeline.RunAsync(
            generator.GenerateAsync(analysisRequest, cancellationToken),
            result => InsightGenerationPipeline.TryAsync(
                () => SaveAsync(scope, result), logger, "saving the market analysis"),
            cancellationToken))
        {
            yield return e;
        }
    }

    private async Task<MarketAnalysisRequest> BuildRequestAsync(
        StreamMarketInsightCommand request, InsightScope scope, CancellationToken cancellationToken)
    {
        var context = await PortfolioContext.BuildAsync(db, request.UserId, cancellationToken);

        return new MarketAnalysisRequest(
            request.Focus,
            context.DisplayCurrency,
            context.TotalValue,
            context.Holdings,
            context.ExcludedHoldingCount,
            await PortfolioContext.LatestAsync(db, scope, cancellationToken));
    }

    private async Task<AiInsightDto> SaveAsync(InsightScope scope, AnalysisResult result)
    {
        var insight = new AiInsight
        {
            Id = Guid.NewGuid(),
            Scope = scope,
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

        // CancellationToken.None on purpose: the model call is already paid for.
        await db.SaveChangesAsync(CancellationToken.None);

        return insight.ToDto();
    }
}
