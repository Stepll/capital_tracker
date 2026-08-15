using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Settings;

public record UpdateDisplayCurrencyCommand(Guid UserId, string Currency) : IRequest<SettingsDto>;

public class UpdateDisplayCurrencyCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateDisplayCurrencyCommand, SettingsDto>
{
    public async Task<SettingsDto> Handle(UpdateDisplayCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (!SupportedCurrencies.All.Contains(request.Currency))
            throw new ArgumentException(
                $"Currency must be one of: {string.Join(", ", SupportedCurrencies.All)}");

        var user = await db.Users.SingleAsync(u => u.Id == request.UserId, cancellationToken);
        user.DisplayCurrency = request.Currency;
        await db.SaveChangesAsync(cancellationToken);
        return new SettingsDto(user.DisplayCurrency, SupportedCurrencies.All);
    }
}
