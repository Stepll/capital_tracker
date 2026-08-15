using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Settings;

public record GetSettingsQuery(Guid UserId) : IRequest<SettingsDto>;

public class GetSettingsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSettingsQuery, SettingsDto>
{
    public async Task<SettingsDto> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleAsync(u => u.Id == request.UserId, cancellationToken);
        return new SettingsDto(user.DisplayCurrency, SupportedCurrencies.All);
    }
}
