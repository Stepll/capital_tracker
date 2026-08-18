using CapitalTracker.Application.Insights;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Tests;

public class GetInsightsQueryTests
{
    [Fact]
    public async Task Keeps_analyses_of_deleted_holdings_and_still_names_them()
    {
        // A run costs real money, so the archive never drops one for tidiness — and a
        // deleted asset would otherwise leave its analyses labelled with nothing.
        await using var db = TestDbContext.Create();

        var holding = AddHolding(db, "Apple");
        holding.DeletedAt = DateTime.UtcNow;
        AddInsight(db, InsightScope.Holding, holding.Id, "Про Apple");
        await db.SaveChangesAsync(default);

        var archive = await Run(db);

        var entry = Assert.Single(archive);
        Assert.Equal("Apple", entry.HoldingName);
        Assert.True(entry.IsHoldingDeleted);
        Assert.Equal(InsightScope.Holding, entry.Scope);
    }

    [Fact]
    public async Task Lists_portfolio_and_holding_analyses_together_newest_first()
    {
        await using var db = TestDbContext.Create();

        var holding = AddHolding(db, "Apple");
        AddInsight(db, InsightScope.Holding, holding.Id, "старіше", daysAgo: 2);
        AddInsight(db, InsightScope.Portfolio, null, "новіше");
        await db.SaveChangesAsync(default);

        var archive = await Run(db);

        Assert.Equal(["новіше", "старіше"], archive.Select(i => i.Summary));
        Assert.Null(archive[0].HoldingId);
        Assert.False(archive[0].IsHoldingDeleted);
    }

    private static Task<List<AiInsightDto>> Run(TestDbContext db) =>
        new GetInsightsQueryHandler(db).Handle(new GetInsightsQuery(), default);

    private static Holding AddHolding(TestDbContext db, string name)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Брокер",
            Type = AccountType.Brokerage,
            Currency = "USD",
        };
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = name };
        db.Accounts.Add(account);
        db.Holdings.Add(holding);
        return holding;
    }

    private static void AddInsight(
        TestDbContext db, InsightScope scope, Guid? holdingId, string summary, int daysAgo = 0) =>
        db.AiInsights.Add(new AiInsight
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            HoldingId = holdingId,
            Summary = summary,
            GeneratedAt = DateTime.UtcNow.AddDays(-daysAgo),
        });
}
