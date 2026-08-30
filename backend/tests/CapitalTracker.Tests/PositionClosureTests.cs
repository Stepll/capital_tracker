using CapitalTracker.Application.Accounts;
using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Application.Insights;
using CapitalTracker.Application.Transactions;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Tests;

public class PositionClosureTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Selling_out_stops_the_asset_counting_towards_the_account()
    {
        // The bug: the position went to zero while the value stayed at the last valuation,
        // so a sold asset kept its weight in the total, the net worth and the donut — and
        // nothing corrected it, because the price job skips a holding with no units.
        await using var db = TestDbContext.Create();
        var (account, holding) = Seed(db);
        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today.AddDays(-10), 10m, 230m));
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Sell, Today, 10m, 250m));

        var detail = await SendAsync(db, new GetAccountByIdQuery(account.Id));

        Assert.Equal(0m, detail!.TotalValue);
        Assert.Empty(detail.AllocationByHolding);
        Assert.Equal(Today, Assert.Single(detail.Holdings).ClosedOn);
    }

    [Fact]
    public async Task What_it_was_worth_before_the_sale_is_left_alone()
    {
        // The same rule soft deletion was built on: the past is not rewritten. The chart
        // drops to zero on the day of the sale, not before it.
        await using var db = TestDbContext.Create();
        var (account, holding) = Seed(db);
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(), HoldingId = holding.Id, Date = Today.AddDays(-10),
            Value = 2300m, Currency = "USD", IsManual = true,
        });
        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today.AddDays(-10), 10m, 230m));
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Sell, Today, 10m, 250m));

        var detail = await SendAsync(db, new GetAccountByIdQuery(account.Id));

        Assert.Equal(2300m, detail!.ValueHistory.Single(p => p.Date == Today.AddDays(-10)).Value);
        Assert.Equal(0m, detail.ValueHistory.Single(p => p.Date == Today).Value);
    }

    [Fact]
    public async Task The_closing_sum_is_kept_so_the_page_has_something_to_show()
    {
        await using var db = TestDbContext.Create();
        var (_, holding) = Seed(db);
        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today.AddDays(-10), 10m, 230m));
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Sell, Today, 10m, 250m));

        var detail = await SendAsync(db, new GetHoldingByIdQuery(holding.Id));

        Assert.Equal(Today, detail!.ClosedOn);
        Assert.Equal(2500m, detail.ClosedAmount);
        Assert.Equal(0m, detail.CurrentValue);
    }

    [Fact]
    public async Task Deleting_the_sale_re_opens_the_position_and_takes_the_zero_with_it()
    {
        await using var db = TestDbContext.Create();
        var (_, holding) = Seed(db);
        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today.AddDays(-10), 10m, 230m));
        var sale = await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Sell, Today, 10m, 250m));
        Assert.Single(await db.ValuationSnapshots.ToListAsync());

        await SendAsync(db, new DeleteTransactionCommand(sale.Id));

        // Otherwise the chart would keep a dip to zero for a sale that no longer exists.
        Assert.Empty(await db.ValuationSnapshots.ToListAsync());
        Assert.Null((await SendAsync(db, new GetHoldingByIdQuery(holding.Id)))!.ClosedOn);
    }

    [Fact]
    public async Task Buying_back_later_leaves_the_old_closure_where_it_was()
    {
        // On that day it really was worth nothing; a later purchase doesn't change that.
        await using var db = TestDbContext.Create();
        var (_, holding) = Seed(db);
        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today.AddDays(-10), 10m, 230m));
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Sell, Today.AddDays(-5), 10m, 250m));
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today, 4m, 260m));

        var zero = Assert.Single(await db.ValuationSnapshots.ToListAsync());
        Assert.Equal(Today.AddDays(-5), zero.Date);
        Assert.Null((await SendAsync(db, new GetHoldingByIdQuery(holding.Id)))!.ClosedOn);
    }

    [Fact]
    public async Task An_asset_that_was_never_counted_in_units_is_never_closed()
    {
        // An apartment has no unit-bearing transactions, so "position zero" never applies.
        await using var db = TestDbContext.Create();
        var (_, holding) = Seed(db, AccountType.RealEstate);
        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Rent, Today, 1m, 12000m));

        Assert.Empty(await db.ValuationSnapshots.ToListAsync());
        Assert.Null((await SendAsync(db, new GetHoldingByIdQuery(holding.Id)))!.ClosedOn);
    }

    [Fact]
    public async Task The_closing_zero_wins_over_whatever_was_valued_that_day()
    {
        // Normally something is already there: the price job writes a valuation every day a
        // position is open, and creating a holding writes one too. Declining to overwrite
        // would leave the sold asset counting, which is the whole bug.
        await using var db = TestDbContext.Create();
        var (account, holding) = Seed(db);
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(), HoldingId = holding.Id, Date = Today,
            Value = 2500m, Currency = "USD", IsManual = false,
        });
        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today.AddDays(-10), 10m, 230m));
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Sell, Today, 10m, 250m));

        Assert.Equal(0m, Assert.Single(await db.ValuationSnapshots.ToListAsync()).Value);
        Assert.Equal(0m, (await SendAsync(db, new GetAccountByIdQuery(account.Id)))!.TotalValue);
    }

    [Fact]
    public async Task A_sale_entered_late_clears_the_days_it_was_wrongly_valued_for()
    {
        // Selling on the 20th but recording it on the 30th leaves days of valuations
        // describing a holding that was no longer held.
        await using var db = TestDbContext.Create();
        var (account, holding) = Seed(db);
        foreach (var daysAgo in new[] { 10, 5, 0 })
        {
            db.ValuationSnapshots.Add(new ValuationSnapshot
            {
                Id = Guid.NewGuid(), HoldingId = holding.Id, Date = Today.AddDays(-daysAgo),
                Value = 2300m, Currency = "USD", IsManual = false,
            });
        }

        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today.AddDays(-20), 10m, 230m));
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Sell, Today.AddDays(-7), 10m, 250m));

        var detail = await SendAsync(db, new GetAccountByIdQuery(account.Id));

        Assert.Equal(0m, detail!.TotalValue);
        // The day before the sale keeps what it was worth; everything after it is zero.
        Assert.Equal(2300m, detail.ValueHistory.Single(p => p.Date == Today.AddDays(-10)).Value);
        Assert.Equal(0m, detail.ValueHistory.Single(p => p.Date == Today.AddDays(-5)).Value);
    }

    private static (Account Account, Holding Holding) Seed(
        TestDbContext db, AccountType type = AccountType.Brokerage)
    {
        var account = new Account { Id = Guid.NewGuid(), Name = "Брокер", Type = type, Currency = "USD" };
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "Apple", Symbol = "AAPL" };
        db.Accounts.Add(account);
        db.Holdings.Add(holding);
        return (account, holding);
    }

    private static Task<TResponse> SendAsync<TResponse>(TestDbContext db, IRequest<TResponse> request)
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IApplicationDbContext>(db)
            .AddSingleton(Options.Create(new InsightsOptions()))
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AddTransactionCommand).Assembly))
            .BuildServiceProvider();

        return provider.GetRequiredService<ISender>().Send(request);
    }
}
