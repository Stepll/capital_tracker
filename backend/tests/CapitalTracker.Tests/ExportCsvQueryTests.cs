using CapitalTracker.Application.Transfer;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Tests;

public class ExportCsvQueryTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Writes_a_holdings_events_in_the_order_they_happened()
    {
        await using var db = TestDbContext.Create();
        var account = AddAccount(db, "Брокер", AccountType.Brokerage, "USD");
        var holding = AddHolding(db, account, "Apple", "AAPL");
        AddTransaction(db, holding, TransactionType.Buy, Today.AddDays(-10), 10m, 230m);
        AddSnapshot(db, holding, Today.AddDays(-10), 2300m);
        AddTransaction(db, holding, TransactionType.Sell, Today.AddDays(-2), 2m, 262m);
        await db.SaveChangesAsync(default);

        var lines = await ExportLinesAsync(db, ExportScope.Portfolio);

        // Same day: what the owner did, then what it was worth.
        Assert.Equal("Купівля", Column(lines[1], "Подія"));
        Assert.Equal("Оцінка", Column(lines[2], "Подія"));
        Assert.Equal("Продаж", Column(lines[3], "Подія"));
    }

    [Fact]
    public async Task A_transaction_carries_quantity_price_and_their_product()
    {
        await using var db = TestDbContext.Create();
        var account = AddAccount(db, "Брокер", AccountType.Brokerage, "USD");
        AddTransaction(db, AddHolding(db, account, "Apple", "AAPL"), TransactionType.Buy, Today, 10m, 230m);
        await db.SaveChangesAsync(default);

        var row = (await ExportLinesAsync(db, ExportScope.Portfolio))[1];

        // Decimal commas, to match the semicolon separator — see PortfolioCsv.
        Assert.Equal("10", Column(row, "Кількість"));
        Assert.Equal("230,00", Column(row, "Ціна"));
        Assert.Equal("2300,00", Column(row, "Сума"));
        Assert.Equal("Брокерський", Column(row, "Тип рахунку"));
    }

    [Fact]
    public async Task A_valuation_carries_only_a_sum()
    {
        await using var db = TestDbContext.Create();
        var account = AddAccount(db, "Нерухомість", AccountType.RealEstate, "USD");
        AddSnapshot(db, AddHolding(db, account, "Квартира", null), Today, 81_400m);
        await db.SaveChangesAsync(default);

        var row = (await ExportLinesAsync(db, ExportScope.Portfolio))[1];

        Assert.Equal("Оцінка", Column(row, "Подія"));
        Assert.Equal("", Column(row, "Кількість"));
        Assert.Equal("", Column(row, "Ціна"));
        Assert.Equal("81400,00", Column(row, "Сума"));
    }

    [Fact]
    public async Task A_sold_asset_is_exported_and_closed_with_a_deletion_row()
    {
        // Dropping deleted holdings would restore a portfolio whose capital history no
        // longer matches the original — the very rewriting soft deletion exists to stop.
        await using var db = TestDbContext.Create();
        var account = AddAccount(db, "Брокер", AccountType.Brokerage, "USD");
        var holding = AddHolding(db, account, "Продане", "OLD");
        holding.DeletedAt = DateTime.UtcNow;
        AddSnapshot(db, holding, Today.AddDays(-5), 1000m);
        await db.SaveChangesAsync(default);

        var lines = await ExportLinesAsync(db, ExportScope.Portfolio);

        Assert.Equal("Оцінка", Column(lines[1], "Подія"));
        Assert.Equal("Видалення", Column(lines[2], "Подія"));
    }

    [Fact]
    public async Task Secrets_never_reach_the_file()
    {
        // Structural, like the AI request: there is no column that could carry them. This
        // proves the ciphertext isn't leaking through some other field either.
        await using var db = TestDbContext.Create();
        var account = AddAccount(db, "Брокер", AccountType.Brokerage, "USD");
        var holding = AddHolding(db, account, "Apple", "AAPL");
        holding.SecretAttributes = new Dictionary<string, string> { ["Логін"] = "ciphertext-that-must-not-appear" };
        AddSnapshot(db, holding, Today, 100m);
        await db.SaveChangesAsync(default);

        var file = await RunAsync(db, ExportScope.Portfolio);

        Assert.DoesNotContain("ciphertext-that-must-not-appear", file!.Content);
        Assert.DoesNotContain("Логін", file.Content);
    }

    [Fact]
    public async Task A_note_containing_the_separator_is_quoted()
    {
        await using var db = TestDbContext.Create();
        var account = AddAccount(db, "Брокер", AccountType.Brokerage, "USD");
        var holding = AddHolding(db, account, "Apple", "AAPL");
        AddTransaction(db, holding, TransactionType.Buy, Today, 1m, 10m, "куплено; частинами");
        await db.SaveChangesAsync(default);

        var file = await RunAsync(db, ExportScope.Portfolio);

        Assert.Contains("\"куплено; частинами\"", file!.Content);
    }

    [Fact]
    public async Task An_account_export_leaves_the_other_accounts_out()
    {
        await using var db = TestDbContext.Create();
        var broker = AddAccount(db, "Брокер", AccountType.Brokerage, "USD");
        var bank = AddAccount(db, "Банк", AccountType.Bank, "UAH");
        AddSnapshot(db, AddHolding(db, broker, "Apple", "AAPL"), Today, 100m);
        AddSnapshot(db, AddHolding(db, bank, "Депозит", null), Today, 5000m);
        await db.SaveChangesAsync(default);

        var lines = await ExportLinesAsync(db, ExportScope.Account, broker.Id);

        Assert.Equal("Apple", Column(Assert.Single(lines.Skip(1)), "Актив"));
    }

    [Fact]
    public async Task A_target_that_does_not_exist_is_not_an_empty_file()
    {
        await using var db = TestDbContext.Create();

        Assert.Null(await RunAsync(db, ExportScope.Holding, Guid.NewGuid()));
    }

    private static Task<CsvFileDto?> RunAsync(TestDbContext db, ExportScope scope, Guid? target = null) =>
        new ExportCsvQueryHandler(db).Handle(new ExportCsvQuery(scope, target), default);

    private static async Task<string[]> ExportLinesAsync(TestDbContext db, ExportScope scope, Guid? target = null)
    {
        var file = await RunAsync(db, scope, target);
        return file!.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string Column(string line, string header)
    {
        var index = Array.IndexOf(PortfolioCsv.Headers, header);
        return line.TrimEnd('\r').Split(PortfolioCsv.Delimiter)[index];
    }

    private static Account AddAccount(TestDbContext db, string name, AccountType type, string currency)
    {
        var account = new Account { Id = Guid.NewGuid(), Name = name, Type = type, Currency = currency };
        db.Accounts.Add(account);
        return account;
    }

    private static Holding AddHolding(TestDbContext db, Account account, string name, string? symbol)
    {
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = name, Symbol = symbol };
        db.Holdings.Add(holding);
        return holding;
    }

    private static void AddTransaction(
        TestDbContext db, Holding holding, TransactionType type, DateOnly date,
        decimal quantity, decimal unitPrice, string? notes = null) =>
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Type = type,
            Date = date,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = "USD",
            Notes = notes,
        });

    private static void AddSnapshot(TestDbContext db, Holding holding, DateOnly date, decimal value) =>
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Date = date,
            Value = value,
            Currency = "USD",
            IsManual = true,
        });
}
