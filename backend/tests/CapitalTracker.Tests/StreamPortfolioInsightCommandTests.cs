using CapitalTracker.Application.Insights;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Tests;

public class StreamPortfolioInsightCommandTests
{
    private const int CooldownHours = 12;

    [Fact]
    public async Task Saves_a_portfolio_scoped_insight_with_no_holding()
    {
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, ("Apple", "AAPL", 300m, "USD"));

        var events = await RunAsync(db, new FakePortfolioAnalysisGenerator(), user.Id);

        Assert.Contains(events, e => e.Type == InsightStreamEventType.Completed);
        var saved = Assert.Single(await db.AiInsights.ToListAsync());
        Assert.Equal(InsightScope.Portfolio, saved.Scope);
        Assert.Null(saved.HoldingId);
    }

    [Fact]
    public async Task Values_every_holding_in_the_display_currency()
    {
        // Holdings are denominated by their snapshot, so the shares the model reasons
        // about are only comparable once everything is converted.
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, ("Apple", "AAPL", 100m, "USD"), ("Квартира", null, 4000m, "UAH"));
        db.ExchangeRates.Add(new ExchangeRate
        {
            Id = Guid.NewGuid(),
            Currency = "USD",
            RateToUah = 40m,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await db.SaveChangesAsync(default);

        var generator = new FakePortfolioAnalysisGenerator();
        await RunAsync(db, generator, user.Id);

        var sent = generator.ReceivedRequest!;
        Assert.Equal("UAH", sent.DisplayCurrency);
        Assert.Equal(8000m, sent.TotalValue);
        // Sorted by weight, so the biggest position leads the prompt.
        Assert.Equal(["Apple", "Квартира"], sent.Holdings.Select(h => h.Name));
        Assert.Equal(4000m, sent.Holdings[0].ValueInDisplayCurrency);
        Assert.Equal(100m, sent.Holdings[0].Value);
        Assert.Equal("USD", sent.Holdings[0].Currency);
    }

    [Fact]
    public async Task Leaves_out_opted_out_holdings_but_says_how_many()
    {
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, ("Apple", "AAPL", 100m, "USD"), ("Квартира", null, 200m, "USD"));

        var opted = await db.Holdings.SingleAsync(h => h.Name == "Квартира");
        opted.ExcludeFromAiAnalysis = true;
        await db.SaveChangesAsync(default);

        var generator = new FakePortfolioAnalysisGenerator();
        await RunAsync(db, generator, user.Id);

        var sent = generator.ReceivedRequest!;
        Assert.Equal("Apple", Assert.Single(sent.Holdings).Name);
        Assert.Equal(1, sent.ExcludedHoldingCount);
    }

    [Fact]
    public async Task Carries_each_holdings_own_latest_facts_as_context()
    {
        // Those analyses are already paid for — re-searching them would be paying twice.
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, ("Apple", "AAPL", 100m, "USD"));
        var holding = await db.Holdings.SingleAsync();

        db.AiInsights.AddRange(
            Insight(InsightScope.Holding, holding.Id, "старий", daysAgo: 5, claim: "старий факт"),
            Insight(InsightScope.Holding, holding.Id, "новий", daysAgo: 1, claim: "свіжий факт"));
        await db.SaveChangesAsync(default);

        var generator = new FakePortfolioAnalysisGenerator();
        await RunAsync(db, generator, user.Id);

        var fact = Assert.Single(generator.ReceivedRequest!.Holdings[0].LatestFacts);
        Assert.Equal("свіжий факт", fact.Claim);
    }

    [Fact]
    public async Task Refuses_an_empty_portfolio_before_paying_for_a_model_call()
    {
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db);

        var generator = new FakePortfolioAnalysisGenerator();
        var events = await RunAsync(db, generator, user.Id);

        Assert.Equal(InsightErrorCode.Empty, SingleFailure(events));
        Assert.Equal(0, generator.CallCount);
        Assert.Empty(await db.AiInsights.ToListAsync());
    }

    [Fact]
    public async Task Cooldown_counts_portfolio_runs_only()
    {
        // A per-asset analysis an hour ago says nothing about the portfolio view, and
        // blocking on it would make the two scopes fight over one window.
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, ("Apple", "AAPL", 100m, "USD"));
        var holding = await db.Holdings.SingleAsync();

        db.AiInsights.Add(Insight(InsightScope.Holding, holding.Id, "щойно", daysAgo: 0));
        await db.SaveChangesAsync(default);

        var events = await RunAsync(db, new FakePortfolioAnalysisGenerator(), user.Id);
        Assert.Contains(events, e => e.Type == InsightStreamEventType.Completed);

        // The portfolio run it just saved does close the window.
        var blocked = await RunAsync(db, new FakePortfolioAnalysisGenerator(), user.Id);
        Assert.Equal(InsightErrorCode.Cooldown, SingleFailure(blocked));
    }

    [Fact]
    public void Has_nowhere_to_put_secrets_or_free_text()
    {
        // Structural, like the per-asset request: the portfolio view reasons about
        // composition, so it carries no attributes and no notes at all.
        var properties = typeof(PortfolioHoldingSummary).GetProperties()
            .Concat(typeof(PortfolioAnalysisRequest).GetProperties())
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(properties, n => n.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, n => n.Contains("Attribute", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, n => n.Contains("Note", StringComparison.OrdinalIgnoreCase));
    }

    private static AiInsight Insight(
        InsightScope scope, Guid? holdingId, string summary, int daysAgo, string claim = "факт") => new()
    {
        Id = Guid.NewGuid(),
        Scope = scope,
        HoldingId = holdingId,
        Summary = summary,
        GeneratedAt = DateTime.UtcNow.AddDays(-daysAgo),
        Facts =
        [
            new AnalysisFact
            {
                Claim = claim,
                Category = FactCategory.MarketNews,
                Polarity = FactPolarity.Neutral,
                Confidence = FactConfidence.Medium,
            },
        ],
    };

    private static async Task<User> SeedAsync(
        TestDbContext db, params (string Name, string? Symbol, decimal Value, string Currency)[] holdings)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "hash",
            DisplayCurrency = "UAH",
        };
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Брокер",
            Type = AccountType.Brokerage,
            Currency = "UAH",
        };

        db.Users.Add(user);
        db.Accounts.Add(account);

        foreach (var (name, symbol, value, currency) in holdings)
        {
            var holding = new Holding
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Name = name,
                Symbol = symbol,
            };
            db.Holdings.Add(holding);
            db.ValuationSnapshots.Add(new ValuationSnapshot
            {
                Id = Guid.NewGuid(),
                HoldingId = holding.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Value = value,
                Currency = currency,
                IsManual = true,
            });
        }

        await db.SaveChangesAsync(default);
        return user;
    }

    private static async Task<List<InsightStreamEvent>> RunAsync(
        TestDbContext db, FakePortfolioAnalysisGenerator generator, Guid userId)
    {
        var handler = new StreamPortfolioInsightCommandHandler(
            db,
            generator,
            Options.Create(new InsightsOptions { CooldownHours = CooldownHours }),
            NullLogger<StreamPortfolioInsightCommandHandler>.Instance);

        var events = new List<InsightStreamEvent>();
        await foreach (var e in handler.Handle(new StreamPortfolioInsightCommand(userId), default))
        {
            events.Add(e);
        }

        return events;
    }

    private static InsightErrorCode? SingleFailure(List<InsightStreamEvent> events) =>
        Assert.Single(events, e => e.Type == InsightStreamEventType.Failed).ErrorCode;
}
