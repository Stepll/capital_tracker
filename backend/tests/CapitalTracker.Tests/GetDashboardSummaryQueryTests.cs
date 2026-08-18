using CapitalTracker.Application.Dashboard;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Tests;

public class GetDashboardSummaryQueryTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Prices_every_history_point_at_the_rate_of_its_own_date()
    {
        // The production bug this guards: the whole series was converted at today's rate,
        // so a $100 holding that never moved was drawn as if it had grown 10% — that was
        // the hryvnia falling, not the asset rising.
        await using var db = TestDbContext.Create();

        var holding = AddHolding(db, AddAccount(db, "UAH"));
        AddSnapshot(db, holding, 100m, "USD", daysAgo: 5);
        AddSnapshot(db, holding, 100m, "USD");
        AddRate(db, "USD", 40m, daysAgo: 5);
        AddRate(db, "USD", 44m);
        await db.SaveChangesAsync(default);

        var result = await Run(db, displayCurrency: "UAH");

        Assert.Equal(4000m, result.NetWorthHistory.Single(p => p.Date == Today.AddDays(-5)).Value);
        Assert.Equal(4400m, result.NetWorthHistory.Single(p => p.Date == Today).Value);
    }

    [Fact]
    public async Task Carries_a_stale_valuation_forward_at_the_current_rate()
    {
        await using var db = TestDbContext.Create();

        var holding = AddHolding(db, AddAccount(db, "UAH"));
        AddSnapshot(db, holding, 100m, "USD", daysAgo: 5);
        AddRate(db, "USD", 40m, daysAgo: 5);
        AddRate(db, "USD", 44m);
        await db.SaveChangesAsync(default);

        var result = await Run(db, displayCurrency: "UAH");

        // Today has no snapshot of its own, but the holding is still held — and it is
        // worth what $100 is worth today, not what it was worth five days ago.
        Assert.Equal(4400m, result.NetWorthHistory.Last().Value);
    }

    [Fact]
    public async Task Ends_the_history_on_today_so_it_agrees_with_the_headline_total()
    {
        await using var db = TestDbContext.Create();

        var holding = AddHolding(db, AddAccount(db, "UAH"));
        AddSnapshot(db, holding, 100m, "USD", daysAgo: 5);
        AddRate(db, "USD", 40m, daysAgo: 5);
        AddRate(db, "USD", 44m);
        await db.SaveChangesAsync(default);

        var result = await Run(db, displayCurrency: "UAH");

        var last = result.NetWorthHistory.Last();
        Assert.Equal(Today, last.Date);
        Assert.Equal(result.TotalNetWorth, last.Value);
    }

    [Fact]
    public async Task Leaves_the_headline_total_and_allocation_on_the_current_rate()
    {
        await using var db = TestDbContext.Create();

        var holding = AddHolding(db, AddAccount(db, "UAH"));
        AddSnapshot(db, holding, 100m, "USD", daysAgo: 5);
        AddRate(db, "USD", 40m, daysAgo: 5);
        AddRate(db, "USD", 44m);
        await db.SaveChangesAsync(default);

        var result = await Run(db, displayCurrency: "UAH");

        Assert.Equal(4400m, result.TotalNetWorth);
        Assert.Equal(4400m, Assert.Single(result.AllocationByType).Value);
    }

    private static async Task<DashboardSummaryDto> Run(TestDbContext db, string displayCurrency)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "hash",
            DisplayCurrency = displayCurrency,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(default);

        return await new GetDashboardSummaryQueryHandler(db)
            .Handle(new GetDashboardSummaryQuery(user.Id), default);
    }

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

    private static Holding AddHolding(TestDbContext db, Account account)
    {
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "акції" };
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

    [Fact]
    public async Task Keeps_a_deleted_holding_in_the_history_for_the_dates_it_was_held()
    {
        // The bug soft deletion exists to prevent: a hard delete took the holding's
        // valuation snapshots with it, so an asset sold today vanished from March too
        // and the whole past net worth shrank.
        await using var db = TestDbContext.Create();

        var holding = AddHolding(db, AddAccount(db, "UAH"));
        AddSnapshot(db, holding, 100m, "UAH", daysAgo: 5);
        holding.DeletedAt = DateTime.UtcNow.AddDays(-2);
        await db.SaveChangesAsync(default);

        var result = await Run(db, displayCurrency: "UAH");

        Assert.Equal(100m, result.NetWorthHistory.Single(p => p.Date == Today.AddDays(-5)).Value);
        Assert.Equal(0m, result.NetWorthHistory.Single(p => p.Date == Today).Value);
        Assert.Equal(0m, result.TotalNetWorth);
        Assert.Empty(result.AllocationByType);
    }

    [Fact]
    public async Task Stops_counting_a_deleted_holding_from_the_day_it_was_deleted()
    {
        await using var db = TestDbContext.Create();

        var account = AddAccount(db, "UAH");
        var kept = AddHolding(db, account);
        AddSnapshot(db, kept, 10m, "UAH", daysAgo: 5);
        AddSnapshot(db, kept, 10m, "UAH", daysAgo: 1);

        var sold = AddHolding(db, account);
        AddSnapshot(db, sold, 100m, "UAH", daysAgo: 5);
        sold.DeletedAt = DateTime.UtcNow.AddDays(-3);
        await db.SaveChangesAsync(default);

        var result = await Run(db, displayCurrency: "UAH");

        // Held five days ago, gone by yesterday. (The series only has points on dates
        // that carry a snapshot, plus today — it is sparse by design.)
        Assert.Equal(110m, result.NetWorthHistory.Single(p => p.Date == Today.AddDays(-5)).Value);
        Assert.Equal(10m, result.NetWorthHistory.Single(p => p.Date == Today.AddDays(-1)).Value);
        Assert.Equal(10m, result.TotalNetWorth);
    }
}
