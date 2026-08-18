using CapitalTracker.Application.Insights;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Tests;

public class StreamHoldingInsightCommandTests
{
    private const int CooldownHours = 12;

    [Fact]
    public async Task Returns_not_found_for_an_unknown_holding()
    {
        await using var db = TestDbContext.Create();
        var generator = new FakeAnalysisGenerator();

        var events = await RunAsync(db, generator, Guid.NewGuid());

        Assert.Equal(InsightErrorCode.NotFound, SingleFailure(events));
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task Refuses_holdings_opted_out_of_analysis_without_calling_the_model()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedHoldingAsync(db, h => h.ExcludeFromAiAnalysis = true);
        var generator = new FakeAnalysisGenerator();

        var events = await RunAsync(db, generator, holding.Id);

        Assert.Equal(InsightErrorCode.Excluded, SingleFailure(events));
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task Refuses_inside_the_cooldown_window_and_reports_when_to_retry()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedHoldingAsync(db);

        var lastRun = DateTime.UtcNow.AddHours(-1);
        db.AiInsights.Add(new AiInsight { Id = Guid.NewGuid(), HoldingId = holding.Id, Summary = "earlier", GeneratedAt = lastRun });
        await db.SaveChangesAsync(default);

        var generator = new FakeAnalysisGenerator();
        var events = await RunAsync(db, generator, holding.Id);

        var failure = Assert.Single(events, e => e.Type == InsightStreamEventType.Failed);
        Assert.Equal(InsightErrorCode.Cooldown, failure.ErrorCode);
        Assert.Equal(lastRun.AddHours(CooldownHours), failure.RetryAt!.Value, TimeSpan.FromSeconds(1));

        // The guard has to run before anything billable.
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task Allows_a_new_run_once_the_cooldown_has_elapsed()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedHoldingAsync(db);

        db.AiInsights.Add(new AiInsight
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Summary = "earlier",
            GeneratedAt = DateTime.UtcNow.AddHours(-(CooldownHours + 1)),
        });
        await db.SaveChangesAsync(default);

        var events = await RunAsync(db, new FakeAnalysisGenerator(), holding.Id);

        Assert.Contains(events, e => e.Type == InsightStreamEventType.Completed);
    }

    [Fact]
    public async Task A_failed_run_saves_nothing_and_leaves_the_cooldown_open()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedHoldingAsync(db);

        var failed = await RunAsync(db, FakeAnalysisGenerator.Failing(), holding.Id);
        Assert.Equal(InsightErrorCode.Upstream, SingleFailure(failed));
        Assert.Empty(await db.AiInsights.ToListAsync());

        // The user paid no waiting time for a run that produced nothing, so an immediate
        // retry has to be allowed — the cooldown is derived from stored insights alone.
        var retry = await RunAsync(db, new FakeAnalysisGenerator(), holding.Id);
        Assert.Contains(retry, e => e.Type == InsightStreamEventType.Completed);
    }

    [Fact]
    public async Task Saved_insight_is_scoped_to_the_holding_it_analysed()
    {
        // Regression, in its second form: the sector stub this replaced set both FKs, so
        // per-asset analyses leaked into the sector feed. The scope is explicit now, and
        // it is what the archive groups on.
        await using var db = TestDbContext.Create();

        var holding = await SeedHoldingAsync(db);

        await RunAsync(db, new FakeAnalysisGenerator(), holding.Id);

        var saved = Assert.Single(await db.AiInsights.ToListAsync());
        Assert.Equal(InsightScope.Holding, saved.Scope);
        Assert.Equal(holding.Id, saved.HoldingId);
    }

    [Fact]
    public async Task Facts_survive_the_round_trip_with_their_enums_and_is_new_flag()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedHoldingAsync(db);

        var fact = new AnalysisFactDto(
            "Забудовник отримав дозвіл на третю чергу",
            FactCategory.MarketNews,
            FactPolarity.Positive,
            FactConfidence.High,
            IsNew: true,
            "Економічна правда",
            "https://example.com/news",
            new DateOnly(2026, 8, 14));

        var generator = new FakeAnalysisGenerator(() =>
            [AnalysisGenerationEvent.Completed(new HoldingAnalysisResult("Підсумок", [fact]))]);

        var events = await RunAsync(db, generator, holding.Id);

        var completed = Assert.Single(events, e => e.Type == InsightStreamEventType.Completed);
        var returned = Assert.Single(completed.Insight!.Facts);
        Assert.Equal(fact, returned);

        // And again after a real load, so the JSON conversion is genuinely exercised.
        var reloaded = Assert.Single(await db.AiInsights.AsNoTracking().ToListAsync());
        var persisted = Assert.Single(reloaded.Facts);
        Assert.Equal(FactCategory.MarketNews, persisted.Category);
        Assert.Equal(FactConfidence.High, persisted.Confidence);
        Assert.True(persisted.IsNew);
        Assert.Equal(new DateOnly(2026, 8, 14), persisted.SourceDate);

        // SourceUrls is the legacy field the sector feed still reads — keep it coherent.
        Assert.Equal(["https://example.com/news"], reloaded.SourceUrls);
    }

    [Fact]
    public async Task Sends_notes_and_public_attributes_to_the_model_but_has_nowhere_to_put_secrets()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedHoldingAsync(db, h =>
        {
            h.Notes = "Планую продати після введення в експлуатацію";
            h.Attributes = new Dictionary<string, string> { ["Забудовник"] = "Кевал Груп" };
            h.SecretAttributes = new Dictionary<string, string> { ["password"] = "ciphertext" };
        });

        var generator = new FakeAnalysisGenerator();
        await RunAsync(db, generator, holding.Id);

        var sent = generator.ReceivedRequest!;
        Assert.Equal("Планую продати після введення в експлуатацію", sent.Notes);
        Assert.Equal("Кевал Груп", sent.Attributes["Забудовник"]);

        // The guarantee is structural rather than a filter that could be edited away:
        // HoldingAnalysisRequest has no member capable of carrying secret attributes.
        // If someone adds one, this fails and they have to justify it.
        Assert.DoesNotContain(
            typeof(HoldingAnalysisRequest).GetProperties(),
            p => p.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Passes_only_the_most_recent_previous_analysis_as_context()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedHoldingAsync(db);

        db.AiInsights.AddRange(
            new AiInsight { Id = Guid.NewGuid(), HoldingId = holding.Id, Summary = "oldest", GeneratedAt = DateTime.UtcNow.AddDays(-30) },
            new AiInsight { Id = Guid.NewGuid(), HoldingId = holding.Id, Summary = "newest", GeneratedAt = DateTime.UtcNow.AddDays(-20) });
        await db.SaveChangesAsync(default);

        var generator = new FakeAnalysisGenerator();
        await RunAsync(db, generator, holding.Id);

        Assert.Equal(
            DateTime.UtcNow.AddDays(-20),
            generator.ReceivedRequest!.Previous!.GeneratedAt,
            TimeSpan.FromSeconds(5));
    }

    private static async Task<Holding> SeedHoldingAsync(TestDbContext db, Action<Holding>? configure = null)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Інвестиції",
            Type = AccountType.RealEstate,
            Currency = "UAH",
        };

        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "ЖК Файна Таун" };
        configure?.Invoke(holding);

        db.Accounts.Add(account);
        db.Holdings.Add(holding);
        await db.SaveChangesAsync(default);

        return holding;
    }

    private static async Task<List<InsightStreamEvent>> RunAsync(
        TestDbContext db, FakeAnalysisGenerator generator, Guid holdingId)
    {
        var handler = new StreamHoldingInsightCommandHandler(
            db,
            generator,
            Options.Create(new InsightsOptions { CooldownHours = CooldownHours }),
            NullLogger<StreamHoldingInsightCommandHandler>.Instance);

        var events = new List<InsightStreamEvent>();
        await foreach (var e in handler.Handle(new StreamHoldingInsightCommand(holdingId), default))
        {
            events.Add(e);
        }

        return events;
    }

    private static InsightErrorCode? SingleFailure(List<InsightStreamEvent> events) =>
        Assert.Single(events, e => e.Type == InsightStreamEventType.Failed).ErrorCode;
}
