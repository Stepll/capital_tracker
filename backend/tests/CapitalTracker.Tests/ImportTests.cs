using System.Text;
using CapitalTracker.Application.Common;
using CapitalTracker.Application.Transfer;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Tests;

public class ImportTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task A_portfolio_exported_and_imported_elsewhere_comes_back_the_same()
    {
        // The promise the whole format exists to keep.
        await using var source = TestDbContext.Create();
        Seed(source);
        await source.SaveChangesAsync(default);
        var file = await ExportAsync(source);

        await using var target = TestDbContext.Create();
        await CommitAsync(target, file);

        var holding = Assert.Single(await target.Holdings.ToListAsync());
        Assert.Equal("Apple", holding.Name);
        Assert.Equal("AAPL", holding.Symbol);
        Assert.Equal(8m, HoldingPositions.Of(await target.Transactions.ToListAsync()));

        var account = Assert.Single(await target.Accounts.ToListAsync());
        Assert.Equal("Брокер", account.Name);
        Assert.Equal(AccountType.Brokerage, account.Type);

        var latest = (await target.ValuationSnapshots.ToListAsync()).OrderByDescending(v => v.Date).First();
        Assert.Equal(2480m, latest.Value);
    }

    [Fact]
    public async Task Importing_the_same_file_twice_changes_nothing_the_second_time()
    {
        // Quantity is derived from transactions, so a doubled import would silently double
        // every position with nothing on screen to show it. This is the guard.
        await using var db = TestDbContext.Create();
        Seed(db);
        await db.SaveChangesAsync(default);
        var file = await ExportAsync(db);

        await using var target = TestDbContext.Create();
        await CommitAsync(target, file);
        var afterFirst = HoldingPositions.Of(await target.Transactions.ToListAsync());

        var preview = await PreviewAsync(target, file);
        await CommitAsync(target, file);

        Assert.Equal(afterFirst, HoldingPositions.Of(await target.Transactions.ToListAsync()));
        Assert.NotNull(preview.SameFileImportedBefore);
        Assert.All(preview.Holdings, h => Assert.Equal(0, h.NewTransactions));
    }

    [Fact]
    public async Task Undoing_an_import_puts_everything_back()
    {
        await using var db = TestDbContext.Create();
        Seed(db);
        await db.SaveChangesAsync(default);
        var file = await ExportAsync(db);

        await using var target = TestDbContext.Create();
        var result = await CommitAsync(target, file);

        Assert.True(await new UndoImportCommandHandler(target).Handle(new UndoImportCommand(result.BatchId), default));

        Assert.Empty(await target.Transactions.ToListAsync());
        Assert.Empty(await target.ValuationSnapshots.ToListAsync());
        // Holdings and accounts the import created go the way every other one does here.
        Assert.Empty(await target.Holdings.ToListAsync());
        Assert.Empty(await target.Accounts.ToListAsync());
        Assert.NotNull((await target.ImportBatches.SingleAsync()).UndoneAt);
    }

    [Fact]
    public async Task A_statement_that_opens_with_a_sale_is_refused_until_told_what_to_do()
    {
        // Exporting "the last year" from a broker starts with sells whose buys are older
        // than the file. Failing at commit with a rules violation would be useless.
        await using var db = TestDbContext.Create();
        var account = new Account { Id = Guid.NewGuid(), Name = "Брокер", Type = AccountType.Brokerage, Currency = "USD" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(default);

        var file = Csv("Рахунок;Тип рахунку;Валюта рахунку;Актив;Тікер;Подія;Дата;Кількість;Ціна;Валюта\n"
            + "Брокер;Брокерський;USD;Apple;AAPL;Продаж;2026-06-02;2;262;USD\n");

        var blocked = await PreviewAsync(db, file);
        Assert.False(blocked.CanCommit);
        Assert.True(Assert.Single(blocked.Holdings).WouldGoNegative);

        var withOpening = await PreviewAsync(db, file, new ImportOptions(AddMissingOpeningPositions: true));
        Assert.True(withOpening.CanCommit);
        Assert.Equal(0m, Assert.Single(withOpening.Holdings).QuantityAfter);
    }

    [Fact]
    public async Task Real_history_can_replace_the_opening_position_the_migration_left()
    {
        // Every holding carries one, priced off a valuation. Importing the real purchases
        // on top of it would count the same units twice.
        await using var db = TestDbContext.Create();
        var (account, holding) = SeedHolding(db);
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(), HoldingId = holding.Id, Type = TransactionType.Buy,
            Date = Today.AddDays(-100), Quantity = 10m, UnitPrice = 230m, Currency = "USD",
            Notes = Transaction.OpeningPositionNote,
        });
        await db.SaveChangesAsync(default);

        var file = Csv($"Рахунок;Тип рахунку;Валюта рахунку;Актив;Тікер;Подія;Дата;Кількість;Ціна;Валюта\n"
            + $"{account.Name};Брокерський;USD;Apple;AAPL;Купівля;2026-01-10;6;225;USD\n"
            + $"{account.Name};Брокерський;USD;Apple;AAPL;Купівля;2026-02-10;4;240;USD\n");

        var kept = await PreviewAsync(db, file);
        Assert.Equal(20m, Assert.Single(kept.Holdings).QuantityAfter);

        var replaced = await PreviewAsync(db, file, new ImportOptions(ReplaceOpeningPositions: true));
        Assert.Equal(10m, Assert.Single(replaced.Holdings).QuantityAfter);
        Assert.True(Assert.Single(replaced.Holdings).ReplacesOpeningPosition);
    }

    [Fact]
    public async Task At_holding_scope_the_file_needs_no_identity_columns()
    {
        await using var db = TestDbContext.Create();
        var (_, holding) = SeedHolding(db);
        await db.SaveChangesAsync(default);

        var file = Csv("Подія;Дата;Кількість;Ціна;Валюта\nКупівля;2026-01-10;5;100;USD\n");
        await CommitAsync(db, file, TransferScope.Holding, holding.Id);

        Assert.Equal(5m, HoldingPositions.Of(await db.Transactions.ToListAsync()));
        Assert.Single(await db.Holdings.ToListAsync());
    }

    [Fact]
    public async Task A_valuation_on_a_date_that_already_has_one_is_left_alone()
    {
        // Import stays purely additive for values, which is what makes undo exact.
        await using var db = TestDbContext.Create();
        var (account, holding) = SeedHolding(db);
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(), HoldingId = holding.Id, Date = Today, Value = 1000m,
            Currency = "USD", IsManual = true,
        });
        await db.SaveChangesAsync(default);

        var file = Csv($"Рахунок;Актив;Тікер;Подія;Дата;Сума;Валюта\n"
            + $"{account.Name};Apple;AAPL;Оцінка;{Today:yyyy-MM-dd};9999;USD\n");
        await CommitAsync(db, file);

        Assert.Equal(1000m, Assert.Single(await db.ValuationSnapshots.ToListAsync()).Value);
    }

    [Fact]
    public async Task Re_importing_after_an_undo_brings_the_asset_back_rather_than_writing_where_nothing_can_see_it()
    {
        // Undo soft-deletes what the import created. The planner matches past that filter
        // so a second run finds the same holding instead of duplicating it — which would
        // leave the rows invisible unless the holding comes back with them.
        await using var db = TestDbContext.Create();
        Seed(db);
        await db.SaveChangesAsync(default);
        var file = await ExportAsync(db);

        await using var target = TestDbContext.Create();
        var first = await CommitAsync(target, file);
        await new UndoImportCommandHandler(target).Handle(new UndoImportCommand(first.BatchId), default);
        Assert.Empty(await target.Holdings.ToListAsync());

        await CommitAsync(target, file);

        var holding = Assert.Single(await target.Holdings.ToListAsync());
        Assert.Null(holding.DeletedAt);
        Assert.Equal(8m, HoldingPositions.Of(await target.Transactions.ToListAsync()));
    }

    [Fact]
    public async Task A_file_whose_bytes_are_not_utf8_says_so_instead_of_importing_mojibake()
    {
        await using var db = TestDbContext.Create();

        var preview = await PreviewAsync(db, new ImportFile("виписка.csv", [0xFF, 0xFE, 0x41, 0x00]));

        Assert.False(preview.CanCommit);
        Assert.Contains("UTF-8", Assert.Single(preview.Problems).Message);
    }

    private static ImportFile Csv(string content) =>
        new("файл.csv", Encoding.UTF8.GetBytes(content));

    private static async Task<ImportFile> ExportAsync(TestDbContext db)
    {
        var file = await new ExportCsvQueryHandler(db).Handle(new ExportCsvQuery(TransferScope.Portfolio), default);
        return new ImportFile(file!.FileName, Encoding.UTF8.GetBytes(file.Content));
    }

    private static Task<ImportPreviewDto> PreviewAsync(
        TestDbContext db, ImportFile file, ImportOptions? options = null,
        TransferScope scope = TransferScope.Portfolio, Guid? target = null) =>
        new PreviewImportCommandHandler(db)
            .Handle(new PreviewImportCommand(file, scope, target, options ?? new ImportOptions()), default);

    private static Task<ImportResultDto> CommitAsync(
        TestDbContext db, ImportFile file, TransferScope scope = TransferScope.Portfolio, Guid? target = null,
        ImportOptions? options = null) =>
        new CommitImportCommandHandler(db)
            .Handle(new CommitImportCommand(file, scope, target, options ?? new ImportOptions()), default);

    private static (Account Account, Holding Holding) SeedHolding(TestDbContext db)
    {
        var account = new Account { Id = Guid.NewGuid(), Name = "Брокер", Type = AccountType.Brokerage, Currency = "USD" };
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "Apple", Symbol = "AAPL" };
        db.Accounts.Add(account);
        db.Holdings.Add(holding);
        return (account, holding);
    }

    private static void Seed(TestDbContext db)
    {
        var (_, holding) = SeedHolding(db);
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(), HoldingId = holding.Id, Type = TransactionType.Buy,
            Date = Today.AddDays(-30), Quantity = 10m, UnitPrice = 230m, Currency = "USD",
        });
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(), HoldingId = holding.Id, Type = TransactionType.Sell,
            Date = Today.AddDays(-5), Quantity = 2m, UnitPrice = 262m, Currency = "USD",
        });
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(), HoldingId = holding.Id, Date = Today, Value = 2480m,
            Currency = "USD", IsManual = true,
        });
    }
}
