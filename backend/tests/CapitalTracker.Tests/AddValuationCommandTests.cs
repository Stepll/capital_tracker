using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Application.Insights;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Tests;

public class AddValuationCommandTests
{
    [Fact]
    public async Task Keeps_the_currency_a_row_is_already_in_when_correcting_it()
    {
        // The regression that matters: re-stamping the account's currency on save is
        // what turned $300 of Apple in a UAH account back into ₴300.
        await using var db = TestDbContext.Create();
        var (holding, date) = await SeedAsync(db, accountCurrency: "UAH", snapshotCurrency: "USD");

        await SendAsync(db, new AddValuationCommand(holding.Id, 305m, date));

        var snapshot = Assert.Single(await db.ValuationSnapshots.ToListAsync());
        Assert.Equal("USD", snapshot.Currency);
        Assert.Equal(305m, snapshot.Value);
    }

    [Fact]
    public async Task An_explicit_currency_restamps_the_row()
    {
        // This is how the mis-stamped production row gets repaired from the UI.
        await using var db = TestDbContext.Create();
        var (holding, date) = await SeedAsync(db, accountCurrency: "UAH", snapshotCurrency: "UAH");

        await SendAsync(db, new AddValuationCommand(holding.Id, 300m, date, "USD"));

        Assert.Equal("USD", Assert.Single(await db.ValuationSnapshots.ToListAsync()).Currency);
    }

    [Fact]
    public async Task Editing_a_row_by_hand_marks_it_manual()
    {
        // Without this the price job would silently discard the correction on its next run.
        await using var db = TestDbContext.Create();
        var (holding, date) = await SeedAsync(db, accountCurrency: "USD", snapshotCurrency: "USD", isManual: false);

        await SendAsync(db, new AddValuationCommand(holding.Id, 250m, date));

        Assert.True(Assert.Single(await db.ValuationSnapshots.ToListAsync()).IsManual);
    }

    [Fact]
    public async Task A_new_date_inherits_the_holding_denomination_not_the_account()
    {
        await using var db = TestDbContext.Create();
        var (holding, date) = await SeedAsync(db, accountCurrency: "UAH", snapshotCurrency: "USD");

        await SendAsync(db, new AddValuationCommand(holding.Id, 310m, date.AddDays(1)));

        var added = (await db.ValuationSnapshots.ToListAsync()).Single(v => v.Date == date.AddDays(1));
        Assert.Equal("USD", added.Currency);
    }

    [Fact]
    public async Task Falls_back_to_the_account_currency_when_there_is_no_history()
    {
        await using var db = TestDbContext.Create();
        var account = new Account { Id = Guid.NewGuid(), Name = "Готівка", Type = AccountType.Cash, Currency = "EUR" };
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "конверт" };
        db.Accounts.Add(account);
        db.Holdings.Add(holding);
        await db.SaveChangesAsync(default);

        await SendAsync(db, new AddValuationCommand(holding.Id, 500m));

        Assert.Equal("EUR", Assert.Single(await db.ValuationSnapshots.ToListAsync()).Currency);
    }

    [Fact]
    public async Task Rejects_a_currency_the_app_cannot_convert()
    {
        await using var db = TestDbContext.Create();
        var (holding, date) = await SeedAsync(db, accountCurrency: "UAH", snapshotCurrency: "UAH");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendAsync(db, new AddValuationCommand(holding.Id, 100m, date, "GBP")));
    }

    private static async Task<(Holding Holding, DateOnly Date)> SeedAsync(
        TestDbContext db, string accountCurrency, string snapshotCurrency, bool isManual = true)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Брокер",
            Type = AccountType.Brokerage,
            Currency = accountCurrency,
        };
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "акції" };
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        db.Accounts.Add(account);
        db.Holdings.Add(holding);
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Date = date,
            Value = 300m,
            Currency = snapshotCurrency,
            IsManual = isManual,
        });
        await db.SaveChangesAsync(default);

        return (holding, date);
    }

    /// <summary>
    /// The handler re-reads its result through GetHoldingByIdQuery via ISender, so a real
    /// mediator over the Application assembly is less work — and less of a lie — than a
    /// hand-written ISender stub.
    /// </summary>
    private static Task SendAsync(TestDbContext db, AddValuationCommand command)
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IApplicationDbContext>(db)
            .AddSingleton(Options.Create(new InsightsOptions()))
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AddValuationCommand).Assembly))
            .BuildServiceProvider();

        return provider.GetRequiredService<ISender>().Send(command);
    }
}
