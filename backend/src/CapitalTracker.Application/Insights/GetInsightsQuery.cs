using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Insights;

/// <summary>Sector-scoped insights only — see GetHoldingInsightsQuery for the per-asset feed.</summary>
public record GetInsightsQuery : IRequest<List<AiInsightDto>>;

public class GetInsightsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetInsightsQuery, List<AiInsightDto>>
{
    public async Task<List<AiInsightDto>> Handle(GetInsightsQuery request, CancellationToken cancellationToken)
    {
        // Fetched flat and ordered client-side — see the note in GetHoldingByIdQuery.
        var insights = await db.AiInsights.Where(i => i.SectorId != null).ToListAsync(cancellationToken);
        var sectorNames = await db.Sectors.ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        return insights
            .OrderByDescending(i => i.GeneratedAt)
            .Select(i => i.ToDto(sectorNames.GetValueOrDefault(i.SectorId!.Value, "?")))
            .ToList();
    }
}
