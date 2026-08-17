using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// Stub for now — writes a placeholder AiInsight so the UI mechanics (click,
/// store, show in the feed) are real end-to-end. The actual pipeline (pull
/// news for the sector's holdings, prompt an LLM, cache the result) comes
/// later; wiring it in only needs to replace this handler's body.
/// </summary>
public record GenerateInsightCommand(Guid SectorId) : IRequest<AiInsightDto>;

public class GenerateInsightCommandHandler(IApplicationDbContext db)
    : IRequestHandler<GenerateInsightCommand, AiInsightDto>
{
    public async Task<AiInsightDto> Handle(GenerateInsightCommand request, CancellationToken cancellationToken)
    {
        var sector = await db.Sectors.SingleAsync(s => s.Id == request.SectorId, cancellationToken);

        var insight = new AiInsight
        {
            Id = Guid.NewGuid(),
            SectorId = sector.Id,
            Summary = "AI-аналіз цього сектору ще не підключено. Ця функція з'явиться незабаром — " +
                      "поки що це заглушка, щоб перевірити, як виглядатиме готовий звіт.",
            SourceUrls = [],
        };
        db.AiInsights.Add(insight);
        await db.SaveChangesAsync(cancellationToken);

        return insight.ToDto(sector.Name);
    }
}
