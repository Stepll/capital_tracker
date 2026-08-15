using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Sectors;

public record GetSectorsQuery : IRequest<List<SectorDto>>;

public class GetSectorsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSectorsQuery, List<SectorDto>>
{
    public Task<List<SectorDto>> Handle(GetSectorsQuery request, CancellationToken cancellationToken) =>
        db.Sectors
            .OrderBy(s => s.Name)
            .Select(s => new SectorDto(s.Id, s.Name))
            .ToListAsync(cancellationToken);
}
