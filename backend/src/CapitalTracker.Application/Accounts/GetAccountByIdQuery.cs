using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Holdings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Accounts;

public record GetAccountByIdQuery(Guid Id) : IRequest<AccountDetailDto?>;

public class GetAccountByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountByIdQuery, AccountDetailDto?>
{
    public async Task<AccountDetailDto?> Handle(GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (account is null)
            return null;

        // Ordered client-side: EF Core can't translate an OrderBy applied after
        // a Select into a constructor-based DTO like HoldingDto, and holding
        // counts per account are small enough that this is a non-issue.
        var holdings = await HoldingQueries.WithValuationAgeAsync(
            db,
            (await HoldingQueries.WithCurrentValue(db, request.Id).ToListAsync(cancellationToken))
                .OrderBy(h => h.CreatedAt)
                .ToList(),
            account,
            cancellationToken);

        var converter = await CurrencyConverter.LoadAsync(db, cancellationToken);
        var total = holdings.Sum(h => converter.Convert(h.CurrentValue, h.Currency, account.Currency));

        // Worthless holdings are left out rather than drawn as slivers — and it keeps a
        // holding with no valuation at all (HoldingDto falls back to an empty currency)
        // away from the converter.
        var allocation = holdings
            .Where(h => h.CurrentValue > 0m)
            .Select(h => new AccountAllocationItemDto(
                h.Id, h.Name, converter.Convert(h.CurrentValue, h.Currency, account.Currency)))
            .OrderByDescending(a => a.Value)
            .ToList();

        // Past the filter deliberately, exactly like the dashboard's series: a holding sold
        // in March was still part of this account in February, and dropping it would redraw
        // that month as if it never had been.
        var everHeld = await db.Holdings
            .IgnoreQueryFilters()
            .Where(h => h.AccountId == account.Id)
            .ToListAsync(cancellationToken);

        var everHeldIds = everHeld.Select(h => h.Id).ToList();
        var snapshots = await db.ValuationSnapshots
            .Where(s => everHeldIds.Contains(s.HoldingId))
            .ToListAsync(cancellationToken);

        var history = ValuationHistory.Build(everHeld, snapshots, converter, account.Currency)
            .Select(p => new ValuationPointDto(p.Date, p.Value))
            .ToList();

        return new AccountDetailDto(
            account.Id, account.Name, account.Type, account.Currency, account.CreatedAt,
            total, holdings, allocation, history);
    }
}
