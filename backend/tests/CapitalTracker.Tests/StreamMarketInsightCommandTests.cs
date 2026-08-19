using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Tests;

public class StreamMarketInsightCommandTests
{
    [Theory]
    [InlineData(MarketFocus.Ukraine, InsightScope.MarketUkraine)]
    [InlineData(MarketFocus.Global, InsightScope.MarketGlobal)]
    public async Task Saves_the_analysis_under_the_scope_that_was_asked_for(
        MarketFocus focus, InsightScope expected)
    {
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, withHolding: true);

        var events = await RunAsync(db, new FakeMarketAnalysisGenerator(), user.Id, focus);

        Assert.Contains(events, e => e.Type == InsightStreamEventType.Completed);
        var saved = Assert.Single(await db.AiInsights.ToListAsync());
        Assert.Equal(expected, saved.Scope);
        Assert.Null(saved.HoldingId);
    }

    [Fact]
    public async Task Runs_on_an_empty_portfolio_because_the_subject_is_the_market()
    {
        // Unlike the portfolio scope, which has nothing to say without holdings.
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, withHolding: false);

        var generator = new FakeMarketAnalysisGenerator();
        var events = await RunAsync(db, generator, user.Id, MarketFocus.Ukraine);

        Assert.Contains(events, e => e.Type == InsightStreamEventType.Completed);
        Assert.Empty(generator.ReceivedRequest!.Holdings);
    }

    [Fact]
    public async Task Passes_the_current_holdings_as_context()
    {
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, withHolding: true);

        var generator = new FakeMarketAnalysisGenerator();
        await RunAsync(db, generator, user.Id, MarketFocus.Global);

        var sent = generator.ReceivedRequest!;
        Assert.Equal("UAH", sent.DisplayCurrency);
        Assert.Equal("Квартира", Assert.Single(sent.Holdings).Name);
        Assert.Equal(MarketFocus.Global, sent.Focus);
    }

    [Fact]
    public async Task Each_focus_keeps_its_own_cooldown()
    {
        // Two separate questions: asking about Ukraine should not lock out the world.
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, withHolding: true);

        await RunAsync(db, new FakeMarketAnalysisGenerator(), user.Id, MarketFocus.Ukraine);

        var blocked = await RunAsync(db, new FakeMarketAnalysisGenerator(), user.Id, MarketFocus.Ukraine);
        Assert.Equal(
            InsightErrorCode.Cooldown,
            Assert.Single(blocked, e => e.Type == InsightStreamEventType.Failed).ErrorCode);

        var other = await RunAsync(db, new FakeMarketAnalysisGenerator(), user.Id, MarketFocus.Global);
        Assert.Contains(other, e => e.Type == InsightStreamEventType.Completed);
    }

    [Fact]
    public async Task Feeds_back_only_the_previous_analysis_of_the_same_market()
    {
        await using var db = TestDbContext.Create();
        var user = await SeedAsync(db, withHolding: true);

        db.AiInsights.AddRange(
            new AiInsight
            {
                Id = Guid.NewGuid(),
                Scope = InsightScope.MarketGlobal,
                Summary = "світ",
                GeneratedAt = DateTime.UtcNow.AddDays(-30),
            },
            new AiInsight
            {
                Id = Guid.NewGuid(),
                Scope = InsightScope.MarketUkraine,
                Summary = "україна",
                GeneratedAt = DateTime.UtcNow.AddDays(-20),
            });
        await db.SaveChangesAsync(default);

        var generator = new FakeMarketAnalysisGenerator();
        await RunAsync(db, generator, user.Id, MarketFocus.Global);

        Assert.Equal(
            DateTime.UtcNow.AddDays(-30),
            generator.ReceivedRequest!.Previous!.GeneratedAt,
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Has_nowhere_to_put_secrets()
    {
        Assert.DoesNotContain(
            typeof(MarketAnalysisRequest).GetProperties(),
            p => p.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<User> SeedAsync(TestDbContext db, bool withHolding)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "hash",
            DisplayCurrency = "UAH",
        };
        db.Users.Add(user);

        if (withHolding)
        {
            var account = new Account
            {
                Id = Guid.NewGuid(),
                Name = "Інвестиції",
                Type = AccountType.RealEstate,
                Currency = "UAH",
            };
            var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "Квартира" };
            db.Accounts.Add(account);
            db.Holdings.Add(holding);
            db.ValuationSnapshots.Add(new ValuationSnapshot
            {
                Id = Guid.NewGuid(),
                HoldingId = holding.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Value = 2_000_000m,
                Currency = "UAH",
                IsManual = true,
            });
        }

        await db.SaveChangesAsync(default);
        return user;
    }

    private static async Task<List<InsightStreamEvent>> RunAsync(
        TestDbContext db, FakeMarketAnalysisGenerator generator, Guid userId, MarketFocus focus)
    {
        var handler = new StreamMarketInsightCommandHandler(
            db,
            generator,
            Options.Create(new InsightsOptions()),
            NullLogger<StreamMarketInsightCommandHandler>.Instance);

        var events = new List<InsightStreamEvent>();
        await foreach (var e in handler.Handle(new StreamMarketInsightCommand(userId, focus), default))
        {
            events.Add(e);
        }

        return events;
    }
}

/// <summary>Records what reached the model and replays a scripted outcome.</summary>
public class FakeMarketAnalysisGenerator : IMarketAnalysisGenerator
{
    public MarketAnalysisRequest? ReceivedRequest { get; private set; }

    public async IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        MarketAnalysisRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ReceivedRequest = request;
        yield return AnalysisGenerationEvent.Completed(new AnalysisResult("ok", []));
        await Task.CompletedTask;
    }
}
