using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// Every analysis ever produced, newest first — portfolio-level and per-holding alike,
/// including analyses of holdings that have since been deleted. Runs cost real money,
/// so nothing here is ever filtered away for tidiness.
/// </summary>
public record GetInsightsQuery : IRequest<List<AiInsightDto>>;

public class GetInsightsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetInsightsQuery, List<AiInsightDto>>
{
    public async Task<List<AiInsightDto>> Handle(GetInsightsQuery request, CancellationToken cancellationToken)
    {
        // Fetched flat and ordered client-side — see the note in GetHoldingByIdQuery.
        var insights = await db.AiInsights.ToListAsync(cancellationToken);

        // Past the soft-delete filter on purpose: an analysis of a deleted holding is
        // exactly what this feed exists to keep, and it needs the name to label it.
        var holdings = await db.Holdings
            .IgnoreQueryFilters()
            .ToDictionaryAsync(h => h.Id, cancellationToken);

        return insights
            .OrderByDescending(i => i.GeneratedAt)
            .Select(i => i.ToDto(i.HoldingId is null ? null : holdings.GetValueOrDefault(i.HoldingId.Value)))
            .ToList();
    }
}
