using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Insights;

public record GetHoldingInsightsQuery(Guid HoldingId) : IRequest<List<AiInsightDto>>;

public class GetHoldingInsightsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetHoldingInsightsQuery, List<AiInsightDto>>
{
    public async Task<List<AiInsightDto>> Handle(GetHoldingInsightsQuery request, CancellationToken cancellationToken)
    {
        var insights = await db.AiInsights
            .Where(i => i.HoldingId == request.HoldingId)
            .ToListAsync(cancellationToken);

        return insights
            .OrderByDescending(i => i.GeneratedAt)
            .Select(i => new AiInsightDto(i.Id, i.SectorId, null, i.HoldingId, i.GeneratedAt, i.Summary, i.SourceUrls))
            .ToList();
    }
}
