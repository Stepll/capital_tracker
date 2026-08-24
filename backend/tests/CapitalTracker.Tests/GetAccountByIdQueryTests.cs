using CapitalTracker.Application.Accounts;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Tests;

/// <summary>
/// The account page draws its own donut and its own line, so it repeats every currency
/// trap the dashboard already had to survive — one figure at the current rate, the series
/// at the rate of each date, and a deleted holding present in the past but not in the now.
/// </summary>
public class GetAccountByIdQueryTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Converts_every_slice_into_the_accounts_currency()
    {
        await using var db = TestDbContext.Create();

        var account = AddAccount(db, "UAH");
        AddSnapshot(db, AddHolding(db, account, "акції"), 300m, "USD");
        AddSnapshot(db, AddHolding(db, account, "депозит"), 5000m, "UAH");
        AddRate(db, "USD", 41m);
        await db.SaveChangesAsync(default);

        var result = await Run(db, account);

        // Biggest first, and the dollar slice arrives as hryvnia rather than as 300.
        Assert.Equal(["акції", "депозит"], result!.AllocationByHolding.Select(a => a.Name));
        Assert.Equal(12300m, result.AllocationByHolding[0].Value);
        Assert.Equal(5000m, result.AllocationByHolding[1].Value);
    }

    [Fact]
    public async Task Leaves_a_holding_with_no_valuation_out_of_the_donut()
    {
        // It has no currency to convert either — HoldingDto falls back to an empty one.
        await using var db = TestDbContext.Create();

        var account = AddAccount(db, "UAH");
        AddHolding(db, account, "без оцінки");
        await db.SaveChangesAsync(default);

        var result = await Run(db, account);

        Assert.Empty(result!.AllocationByHolding);
        Assert.Single(result.Holdings);
    }

    [Fact]
    public async Task Prices_every_history_point_at_the_rate_of_its_own_date()
    {
        // Same guard as the dashboard's series: converting the whole line at today's rate
        // draws a hryvnia slide as if the account had grown.
        await using var db = TestDbContext.Create();

        var account = AddAccount(db, "UAH");
        var holding = AddHolding(db, account, "акції");
        AddSnapshot(db, holding, 100m, "USD", daysAgo: 5);
        AddSnapshot(db, holding, 100m, "USD");
        AddRate(db, "USD", 40m, daysAgo: 5);
        AddRate(db, "USD", 44m);
        await db.SaveChangesAsync(default);

        var result = await Run(db, account);

        Assert.Equal(4000m, result!.ValueHistory.Single(p => p.Date == Today.AddDays(-5)).Value);
        Assert.Equal(4400m, result.ValueHistory.Single(p => p.Date == Today).Value);
    }

    [Fact]
    public async Task Keeps_a_deleted_holding_in_the_history_and_out_of_everything_else()
    {
        await using var db = TestDbContext.Create();

        var account = AddAccount(db, "USD");
        var sold = AddHolding(db, account, "продане");
        sold.DeletedAt = DateTime.UtcNow;
        AddSnapshot(db, sold, 1000m, "USD", daysAgo: 5);
        await db.SaveChangesAsync(default);

        var result = await Run(db, account);

        Assert.Empty(result!.AllocationByHolding);
        Assert.Empty(result.Holdings);
        Assert.Equal(0m, result.TotalValue);
        Assert.Equal(1000m, result.ValueHistory.Single(p => p.Date == Today.AddDays(-5)).Value);
        Assert.Equal(0m, result.ValueHistory.Single(p => p.Date == Today).Value);
    }

    private static Task<AccountDetailDto?> Run(TestDbContext db, Account account) =>
        new GetAccountByIdQueryHandler(db).Handle(new GetAccountByIdQuery(account.Id), default);

    private static Account AddAccount(TestDbContext db, string currency)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Брокер",
            Type = AccountType.Brokerage,
            Currency = currency,
        };
        db.Accounts.Add(account);
        return account;
    }

    private static Holding AddHolding(TestDbContext db, Account account, string name)
    {
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = name };
        db.Holdings.Add(holding);
        return holding;
    }

    private static void AddSnapshot(
        TestDbContext db, Holding holding, decimal value, string currency, int daysAgo = 0) =>
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Value = value,
            Currency = currency,
            Date = Today.AddDays(-daysAgo),
            IsManual = true,
        });

    private static void AddRate(TestDbContext db, string currency, decimal toUah, int daysAgo = 0) =>
        db.ExchangeRates.Add(new ExchangeRate
        {
            Id = Guid.NewGuid(),
            Currency = currency,
            RateToUah = toUah,
            Date = Today.AddDays(-daysAgo),
        });
}
