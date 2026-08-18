using CapitalTracker.Application.Accounts;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Tests;

public class GetAccountsQueryTests
{
    [Fact]
    public async Task Converts_a_holding_denominated_differently_from_its_account()
    {
        // The production bug this guards: $300 of Apple recorded in a UAH brokerage
        // account was summed raw and shown as ₴300 in the net worth.
        await using var db = TestDbContext.Create();

        var account = AddAccount(db, "Брокер", "UAH");
        AddSnapshot(db, AddHolding(db, account, "акції"), 300m, "USD");
        AddRate(db, "USD", 41m);
        await db.SaveChangesAsync(default);

        var result = await Run(db);

        Assert.Equal(12300m, Assert.Single(result).TotalValue);
    }

    [Fact]
    public async Task Sums_without_conversion_when_currencies_already_match()
    {
        await using var db = TestDbContext.Create();

        var account = AddAccount(db, "Інвестиції", "USD");
        AddSnapshot(db, AddHolding(db, account, "Квартира"), 80000m, "USD");
        AddSnapshot(db, AddHolding(db, account, "Solana"), 300m, "USD");
        await db.SaveChangesAsync(default);

        var result = await Run(db);

        Assert.Equal(80300m, Assert.Single(result).TotalValue);
    }

    [Fact]
    public async Task A_holding_with_no_valuation_contributes_nothing()
    {
        await using var db = TestDbContext.Create();

        AddHolding(db, AddAccount(db, "Готівка", "UAH"), "без оцінки");
        await db.SaveChangesAsync(default);

        var result = await Run(db);

        Assert.Equal(0m, Assert.Single(result).TotalValue);
    }

    [Fact]
    public async Task Uses_the_most_recent_snapshot_per_holding()
    {
        await using var db = TestDbContext.Create();

        var holding = AddHolding(db, AddAccount(db, "Інвестиції", "USD"), "Квартира");
        AddSnapshot(db, holding, 80000m, "USD", daysAgo: 10);
        AddSnapshot(db, holding, 81400m, "USD");
        await db.SaveChangesAsync(default);

        var result = await Run(db);

        Assert.Equal(81400m, Assert.Single(result).TotalValue);
    }

    private static Task<List<AccountDto>> Run(TestDbContext db) =>
        new GetAccountsQueryHandler(db).Handle(new GetAccountsQuery(), default);

    private static Account AddAccount(TestDbContext db, string name, string currency)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
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
            Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysAgo),
            IsManual = true,
        });

    private static void AddRate(TestDbContext db, string currency, decimal toUah) =>
        db.ExchangeRates.Add(new ExchangeRate
        {
            Id = Guid.NewGuid(),
            Currency = currency,
            RateToUah = toUah,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
        });
}
