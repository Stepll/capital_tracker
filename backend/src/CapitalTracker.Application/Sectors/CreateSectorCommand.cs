using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;

namespace CapitalTracker.Application.Sectors;

public record CreateSectorCommand(string Name) : IRequest<SectorDto>;

public class CreateSectorCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateSectorCommand, SectorDto>
{
    public async Task<SectorDto> Handle(CreateSectorCommand request, CancellationToken cancellationToken)
    {
        var sector = new Sector { Id = Guid.NewGuid(), Name = request.Name };
        db.Sectors.Add(sector);
        await db.SaveChangesAsync(cancellationToken);
        return new SectorDto(sector.Id, sector.Name);
    }
}
