using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;
using CapitalTracker.Application.Transactions;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Tests;

public class TransactionCommandTests
{
    [Fact]
    public async Task Inherits_the_currency_the_holding_is_already_in()
    {
        // Same trap as valuations: a USD asset in a UAH account must not be re-stamped UAH
        // just because that is what the account says.
        await using var db = TestDbContext.Create();
        var holding = await SeedAsync(db, accountCurrency: "UAH", snapshotCurrency: "USD");

        var added = await SendAsync(db, new AddTransactionCommand(
            holding.Id, TransactionType.Buy, Today, 2m, 150m));

        Assert.Equal("USD", added.Currency);
        Assert.Equal(300m, added.Amount);
    }

    [Fact]
    public async Task Refuses_to_sell_more_units_than_are_held()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedAsync(db, accountCurrency: "USD", snapshotCurrency: "USD");
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today, 5m, 100m));

        var error = await Assert.ThrowsAsync<DomainValidationException>(() => SendAsync(db,
            new AddTransactionCommand(holding.Id, TransactionType.Sell, Today, 6m, 120m)));

        Assert.Contains("від'ємною", error.Message);
        Assert.Single(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Refuses_an_edit_that_would_leave_the_position_negative()
    {
        // Not just sells: editing the buy that a later sell drew on lands in the same place.
        await using var db = TestDbContext.Create();
        var holding = await SeedAsync(db, accountCurrency: "USD", snapshotCurrency: "USD");
        var buy = await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today, 10m, 100m));
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Sell, Today, 8m, 120m));

        await Assert.ThrowsAsync<DomainValidationException>(() => SendAsync(db,
            new UpdateTransactionCommand(buy.Id, TransactionType.Buy, Today, 5m, 100m)));

        var unchanged = await db.Transactions.SingleAsync(t => t.Id == buy.Id);
        Assert.Equal(10m, unchanged.Quantity);
    }

    [Fact]
    public async Task An_edit_that_keeps_the_position_valid_goes_through()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedAsync(db, accountCurrency: "USD", snapshotCurrency: "USD");
        var buy = await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today, 10m, 100m));

        var edited = await SendAsync(db, new UpdateTransactionCommand(
            buy.Id, TransactionType.Buy, Today.AddDays(-3), 12m, 95m, Notes: "  уточнив  "));

        Assert.Equal(12m, edited.Quantity);
        Assert.Equal(Today.AddDays(-3), edited.Date);
        Assert.Equal("уточнив", edited.Notes);
    }

    [Fact]
    public async Task Deleting_a_transaction_takes_its_units_back_out()
    {
        // Deletion is the escape hatch for a row that should never have existed, so it is
        // deliberately not held to the position check the writes are.
        await using var db = TestDbContext.Create();
        var holding = await SeedAsync(db, accountCurrency: "USD", snapshotCurrency: "USD");
        var buy = await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today, 4m, 100m));

        Assert.True(await SendAsync(db, new DeleteTransactionCommand(buy.Id)));

        Assert.Null(HoldingPositions.Of(await db.Transactions.ToListAsync()));
    }

    [Fact]
    public async Task Rejects_a_quantity_that_is_not_positive()
    {
        // Direction belongs to the type; a negative quantity on a Sell would subtract twice.
        await using var db = TestDbContext.Create();
        var holding = await SeedAsync(db, accountCurrency: "USD", snapshotCurrency: "USD");

        await Assert.ThrowsAsync<DomainValidationException>(() => SendAsync(db,
            new AddTransactionCommand(holding.Id, TransactionType.Buy, Today, -3m, 100m)));
    }

    [Fact]
    public async Task Rejects_a_currency_the_app_cannot_convert()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedAsync(db, accountCurrency: "USD", snapshotCurrency: "USD");

        await Assert.ThrowsAsync<DomainValidationException>(() => SendAsync(db,
            new AddTransactionCommand(holding.Id, TransactionType.Buy, Today, 1m, 100m, "GBP")));
    }

    [Fact]
    public async Task An_accounts_list_gathers_every_holdings_transactions_newest_first()
    {
        await using var db = TestDbContext.Create();
        var first = await SeedAsync(db, accountCurrency: "USD", snapshotCurrency: "USD");
        var second = new Holding { Id = Guid.NewGuid(), AccountId = first.AccountId, Name = "Друга позиція" };
        db.Holdings.Add(second);
        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddTransactionCommand(first.Id, TransactionType.Buy, Today.AddDays(-5), 1m, 10m));
        await SendAsync(db, new AddTransactionCommand(second.Id, TransactionType.Buy, Today, 2m, 20m));

        var rows = await SendAsync(db, new GetAccountTransactionsQuery(first.AccountId));

        Assert.Equal(["Друга позиція", "акції"], rows.Select(r => r.HoldingName));
    }

    [Fact]
    public async Task A_deleted_holding_keeps_its_history_and_takes_no_more()
    {
        await using var db = TestDbContext.Create();
        var holding = await SeedAsync(db, accountCurrency: "USD", snapshotCurrency: "USD");
        await SendAsync(db, new AddTransactionCommand(holding.Id, TransactionType.Buy, Today, 3m, 100m));

        holding.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(default);

        // Its own page still lists what happened...
        Assert.Single(await SendAsync(db, new GetHoldingTransactionsQuery(holding.Id)));
        // ...the account's stream no longer does, and nothing new can be written to it.
        Assert.Empty(await SendAsync(db, new GetAccountTransactionsQuery(holding.AccountId)));
        await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(db,
            new AddTransactionCommand(holding.Id, TransactionType.Buy, Today, 1m, 100m)));
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<Holding> SeedAsync(TestDbContext db, string accountCurrency, string snapshotCurrency)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Брокер",
            Type = AccountType.Brokerage,
            Currency = accountCurrency,
        };
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "акції" };

        db.Accounts.Add(account);
        db.Holdings.Add(holding);
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Date = Today,
            Value = 300m,
            Currency = snapshotCurrency,
            IsManual = true,
        });
        await db.SaveChangesAsync(default);

        return holding;
    }

    private static Task<TResponse> SendAsync<TResponse>(TestDbContext db, IRequest<TResponse> request)
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IApplicationDbContext>(db)
            .AddSingleton(Options.Create(new InsightsOptions()))
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AddTransactionCommand).Assembly))
            .BuildServiceProvider();

        return provider.GetRequiredService<ISender>().Send(request);
    }
}
