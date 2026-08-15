using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// Stub for now, same as GenerateInsightCommand — writes a placeholder so the
/// UI mechanics are real end-to-end. Unlike the sector-level version, this
/// one is scoped to a single asset and previews the context a real analysis
/// would use: the holding's own attributes (e.g. a real-estate developer,
/// address) alongside its sector, if either is set. Wiring in the actual
/// pipeline later only needs to replace this handler's body.
/// </summary>
public record GenerateHoldingInsightCommand(Guid HoldingId) : IRequest<AiInsightDto>;

public class GenerateHoldingInsightCommandHandler(IApplicationDbContext db)
    : IRequestHandler<GenerateHoldingInsightCommand, AiInsightDto>
{
    public async Task<AiInsightDto> Handle(GenerateHoldingInsightCommand request, CancellationToken cancellationToken)
    {
        var holding = await db.Holdings.SingleAsync(h => h.Id == request.HoldingId, cancellationToken);

        var summary = "AI-аналіз цього активу ще не підключено. Ця функція з'явиться незабаром — " +
                       "поки що це заглушка, щоб перевірити, як виглядатиме готовий звіт.";

        if (holding.Attributes.Count > 0)
        {
            var context = string.Join(", ", holding.Attributes.Select(kv => $"{kv.Key}: {kv.Value}"));
            summary += $" Контекст, який врахує майбутній аналіз: {context}.";
        }

        var insight = new AiInsight
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            SectorId = holding.SectorId,
            Summary = summary,
            SourceUrls = [],
        };
        db.AiInsights.Add(insight);
        await db.SaveChangesAsync(cancellationToken);

        return new AiInsightDto(
            insight.Id, insight.SectorId, null, insight.HoldingId,
            insight.GeneratedAt, insight.Summary, insight.SourceUrls);
    }
}
