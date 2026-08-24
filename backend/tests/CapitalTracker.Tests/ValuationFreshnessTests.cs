using CapitalTracker.Application.Common;
using CapitalTracker.Application.Dashboard;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Tests;

public class ValuationFreshnessTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void What_counts_as_out_of_date_depends_on_the_kind_of_asset()
    {
        // An apartment revalued three months ago is normal. A brokerage position that has
        // not moved in ten days is not — that one is priced daily, or should be.
        var flat = ValuationFreshness.Age(
            Today.AddDays(-100), AccountType.RealEstate, PricingMode.Manual, Today);
        var shares = ValuationFreshness.Age(
            Today.AddDays(-10), AccountType.Brokerage, PricingMode.Automatic, Today);

        Assert.Equal(ValuationStatus.Fresh, flat.Status);
        Assert.NotEqual(ValuationStatus.Fresh, shares.Status);
    }

    [Fact]
    public void An_auto_priced_holding_going_stale_is_reported_as_a_stalled_job()
    {
        // This is the case nobody would otherwise notice: FinnhubClient swallows failures
        // into "no data", so an expired key looks exactly like "ran fine, nothing to do".
        // Typing a value here fixes today and nothing else.
        var age = ValuationFreshness.Age(
            Today.AddDays(-30), AccountType.Brokerage, PricingMode.Automatic, Today);

        Assert.Equal(ValuationStatus.AutoPricingStalled, age.Status);
        Assert.Equal(30, age.Days);
    }

    [Fact]
    public void A_holding_only_the_owner_can_value_asks_the_owner()
    {
        var age = ValuationFreshness.Age(
            Today.AddDays(-200), AccountType.RealEstate, PricingMode.Manual, Today);

        Assert.Equal(ValuationStatus.NeedsManualUpdate, age.Status);
    }

    [Fact]
    public void A_quotable_holding_with_no_position_is_the_owners_problem_not_the_jobs()
    {
        // Nothing is stalled — the price job is right to skip it. The units are missing.
        var age = ValuationFreshness.Age(
            Today.AddDays(-30), AccountType.Brokerage, PricingMode.NeedsQuantity, Today);

        Assert.Equal(ValuationStatus.NeedsManualUpdate, age.Status);
    }

    [Fact]
    public void Never_valued_at_all_needs_attention_rather_than_counting_as_fresh()
    {
        var age = ValuationFreshness.Age(null, AccountType.Bank, PricingMode.Manual, Today);

        Assert.Equal(ValuationStatus.NeedsManualUpdate, age.Status);
        Assert.Null(age.Days);
    }

    [Fact]
    public async Task The_dashboard_lists_stale_holdings_biggest_first_and_leaves_fresh_ones_out()
    {
        await using var db = TestDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b", PasswordHash = "x", DisplayCurrency = "UAH" };
        db.Users.Add(user);

        var flats = new Account { Id = Guid.NewGuid(), Name = "Нерухомість", Type = AccountType.RealEstate, Currency = "USD" };
        var broker = new Account { Id = Guid.NewGuid(), Name = "Брокер", Type = AccountType.Brokerage, Currency = "USD" };
        db.Accounts.AddRange(flats, broker);

        // Stale and large, stale and small, and one valued today.
        var flat = AddHolding(db, flats, "Квартира", null, 80_000m, daysAgo: 300);
        AddHolding(db, broker, "Apple", "AAPL", 2_000m, daysAgo: 40);
        AddHolding(db, broker, "Nvidia", "NVDA", 5_000m, daysAgo: 0);
        db.ExchangeRates.Add(new ExchangeRate
        {
            Id = Guid.NewGuid(), Currency = "USD", RateToUah = 40m, Date = Today,
        });
        await db.SaveChangesAsync(default);

        var result = await new GetDashboardSummaryQueryHandler(db)
            .Handle(new GetDashboardSummaryQuery(user.Id), default);

        Assert.Equal(["Квартира", "Apple"], result.StaleValuations.Select(s => s.Name));
        // Converted like every other figure on this page, not left in the holding's currency.
        Assert.Equal(3_200_000m, result.StaleValuations[0].ValueInDisplayCurrency);
        Assert.Equal(flat.Id, result.StaleValuations[0].HoldingId);
        Assert.Equal("Нерухомість", result.StaleValuations[0].AccountName);
    }

    private static Holding AddHolding(
        TestDbContext db, Account account, string name, string? symbol, decimal value, int daysAgo)
    {
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = name, Symbol = symbol };
        db.Holdings.Add(holding);
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Date = Today.AddDays(-daysAgo),
            Value = value,
            Currency = "USD",
            IsManual = true,
        });
        return holding;
    }
}
