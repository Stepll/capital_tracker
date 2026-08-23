using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Application.Insights;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Tests;

/// <summary>
/// Quantity is derived from transactions and stored nowhere. These pin the two halves of
/// that: what the fold counts, and that creating a holding opens a position for it.
/// </summary>
public class HoldingPositionTests
{
    [Fact]
    public void Nets_buys_against_sells()
    {
        var position = HoldingPositions.Of([
            Row(TransactionType.Buy, 10m),
            Row(TransactionType.Buy, 5m),
            Row(TransactionType.Sell, 4m),
            Row(TransactionType.Deposit, 2m),
            Row(TransactionType.Withdrawal, 1m),
        ]);

        Assert.Equal(12m, position);
    }

    [Fact]
    public void Cash_flows_leave_the_position_alone()
    {
        // A dividend is money arriving, not units. Counting it would inflate the share
        // count the price job multiplies by.
        var position = HoldingPositions.Of([
            Row(TransactionType.Buy, 10m),
            Row(TransactionType.Dividend, 3m),
            Row(TransactionType.Rent, 1m),
            Row(TransactionType.Expense, 1m),
        ]);

        Assert.Equal(10m, position);
    }

    [Fact]
    public void Nothing_unit_bearing_means_unknown_rather_than_zero()
    {
        // Null is what keeps an apartment from claiming it holds zero units — and what
        // stops the price job from pricing a ticker nobody gave a quantity for.
        Assert.Null(HoldingPositions.Of([]));
        Assert.Null(HoldingPositions.Of([Row(TransactionType.Rent, 12000m)]));
        Assert.Equal(0m, HoldingPositions.Of([Row(TransactionType.Buy, 3m), Row(TransactionType.Sell, 3m)]));
    }

    [Fact]
    public async Task Creating_a_holding_opens_its_position()
    {
        await using var db = TestDbContext.Create();
        var account = Seed(db, AccountType.Brokerage, "USD");
        await db.SaveChangesAsync(default);

        await SendAsync(db, new CreateHoldingCommand(account.Id, "Apple", "AAPL", 4m, 400m));

        var transaction = Assert.Single(await db.Transactions.ToListAsync());
        Assert.Equal(TransactionType.Buy, transaction.Type);
        Assert.Equal(4m, transaction.Quantity);
        Assert.Equal(100m, transaction.UnitPrice);
        Assert.Equal("USD", transaction.Currency);
        Assert.Equal(Transaction.OpeningPositionNote, transaction.Notes);
    }

    [Fact]
    public async Task A_quotable_holding_without_a_quantity_opens_no_position()
    {
        // The regression this guards: assuming one share would make the holding look
        // auto-priceable, and the next price job would overwrite EUR 1200 of Tesla with
        // the price of a single share.
        await using var db = TestDbContext.Create();
        var account = Seed(db, AccountType.Brokerage, "USD");
        await db.SaveChangesAsync(default);

        var created = await SendAsync(db, new CreateHoldingCommand(account.Id, "Tesla", "TSLA", null, 1200m));

        Assert.Empty(await db.Transactions.ToListAsync());
        var detail = await SendAsync(db, new GetHoldingByIdQuery(created.Id));
        Assert.Null(detail!.Quantity);
        Assert.Equal(PricingMode.NeedsQuantity, detail.PricingMode);
    }

    [Fact]
    public async Task An_asset_that_cannot_be_quoted_opens_at_a_single_unit()
    {
        // An apartment has no unit count, but it does have a purchase — and the account's
        // history is poorer if that never happened.
        await using var db = TestDbContext.Create();
        var account = Seed(db, AccountType.RealEstate, "UAH");
        await db.SaveChangesAsync(default);

        await SendAsync(db, new CreateHoldingCommand(account.Id, "Квартира", null, null, 3_000_000m));

        var transaction = Assert.Single(await db.Transactions.ToListAsync());
        Assert.Equal(1m, transaction.Quantity);
        Assert.Equal(3_000_000m, transaction.UnitPrice);
    }

    [Fact]
    public async Task The_holding_page_reports_the_position_its_transactions_add_up_to()
    {
        await using var db = TestDbContext.Create();
        var account = Seed(db, AccountType.Brokerage, "USD");
        await db.SaveChangesAsync(default);

        var created = await SendAsync(db, new CreateHoldingCommand(account.Id, "Apple", "AAPL", 10m, 2300m));
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            HoldingId = created.Id,
            Type = TransactionType.Sell,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Quantity = 3m,
            UnitPrice = 240m,
            Currency = "USD",
        });
        await db.SaveChangesAsync(default);

        var detail = await SendAsync(db, new GetHoldingByIdQuery(created.Id));

        Assert.Equal(7m, detail!.Quantity);
        Assert.Equal(PricingMode.Automatic, detail.PricingMode);
    }

    private static Transaction Row(TransactionType type, decimal quantity) =>
        new() { Id = Guid.NewGuid(), Type = type, Quantity = quantity };

    private static Account Seed(TestDbContext db, AccountType type, string currency)
    {
        var account = new Account { Id = Guid.NewGuid(), Name = "Рахунок", Type = type, Currency = currency };
        db.Accounts.Add(account);
        return account;
    }

    private static Task<TResponse> SendAsync<TResponse>(TestDbContext db, IRequest<TResponse> request)
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IApplicationDbContext>(db)
            .AddSingleton(Options.Create(new InsightsOptions()))
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateHoldingCommand).Assembly))
            .BuildServiceProvider();

        return provider.GetRequiredService<ISender>().Send(request);
    }
}
