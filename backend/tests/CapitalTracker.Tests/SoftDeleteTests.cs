using CapitalTracker.Application.Accounts;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Application.Insights;
using CapitalTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Tests;

public class SoftDeleteTests
{
    [Fact]
    public async Task Deleting_a_holding_keeps_its_valuation_history()
    {
        // This is the whole point of soft deletion: the snapshots are what the net worth
        // chart is computed from, so removing them rewrites the past.
        await using var db = TestDbContext.Create();
        var (account, holding) = Seed(db);
        await db.SaveChangesAsync(default);

        Assert.True(await new DeleteHoldingCommandHandler(db).Handle(new DeleteHoldingCommand(holding.Id), default));

        Assert.NotEmpty(await db.ValuationSnapshots.ToListAsync());
        var stored = await db.Holdings.IgnoreQueryFilters().SingleAsync(h => h.Id == holding.Id);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(account.Id, stored.AccountId);
    }

    [Fact]
    public async Task A_deleted_holding_is_gone_from_ordinary_reads()
    {
        await using var db = TestDbContext.Create();
        var (_, holding) = Seed(db);
        await db.SaveChangesAsync(default);

        await new DeleteHoldingCommandHandler(db).Handle(new DeleteHoldingCommand(holding.Id), default);

        Assert.Empty(await db.Holdings.ToListAsync());
        Assert.Equal(0m, Assert.Single(await new GetAccountsQueryHandler(db).Handle(new GetAccountsQuery(), default)).TotalValue);
    }

    [Fact]
    public async Task Deleting_an_account_marks_its_holdings_rather_than_erasing_them()
    {
        // Left to the database cascade this would hard-delete live holdings and their
        // history — the one path that could still destroy data after soft deletion.
        await using var db = TestDbContext.Create();
        var (account, holding) = Seed(db);
        await db.SaveChangesAsync(default);

        Assert.True(await new DeleteAccountCommandHandler(db).Handle(new DeleteAccountCommand(account.Id), default));

        var stored = await db.Holdings.IgnoreQueryFilters().SingleAsync(h => h.Id == holding.Id);
        Assert.NotNull(stored.DeletedAt);
        Assert.NotEmpty(await db.ValuationSnapshots.ToListAsync());
        Assert.Empty(await db.Accounts.ToListAsync());
        Assert.Empty(await db.Holdings.ToListAsync());
    }

    [Fact]
    public async Task A_deleted_holding_still_opens_read_only()
    {
        // Links to it have to keep working — the analysis archive is full of them.
        await using var db = TestDbContext.Create();
        var (_, holding) = Seed(db);
        await db.SaveChangesAsync(default);

        await new DeleteHoldingCommandHandler(db).Handle(new DeleteHoldingCommand(holding.Id), default);

        var detail = await new GetHoldingByIdQueryHandler(db, Options.Create(new InsightsOptions()))
            .Handle(new GetHoldingByIdQuery(holding.Id), default);

        Assert.NotNull(detail);
        Assert.NotNull(detail!.DeletedAt);
        Assert.NotEmpty(detail.ValuationHistory);
    }

    private static (Account Account, Holding Holding) Seed(TestDbContext db)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Брокер",
            Type = AccountType.Brokerage,
            Currency = "UAH",
        };
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "акції" };

        db.Accounts.Add(account);
        db.Holdings.Add(holding);
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
            Value = 100m,
            Currency = "UAH",
            IsManual = true,
        });

        return (account, holding);
    }
}
