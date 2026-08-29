using CapitalTracker.Application.Transfer;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Tests;

public class PortfolioCsvParserTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Reads_back_exactly_what_the_export_wrote()
    {
        // The test that makes the format worth having: if the two halves ever disagree,
        // every promise made about backup and round-tripping through a spreadsheet is void.
        await using var db = TestDbContext.Create();
        var account = new Account
        {
            Id = Guid.NewGuid(), Name = "Брокер; головний", Type = AccountType.Brokerage, Currency = "USD",
        };
        var holding = new Holding { Id = Guid.NewGuid(), AccountId = account.Id, Name = "Apple", Symbol = "AAPL" };
        db.Accounts.Add(account);
        db.Holdings.Add(holding);
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(), HoldingId = holding.Id, Type = TransactionType.Buy,
            Date = Today.AddDays(-10), Quantity = 10m, UnitPrice = 230m, Currency = "USD",
            Notes = "перша; купівля",
        });
        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(), HoldingId = holding.Id, Date = Today, Value = 2480.55m,
            Currency = "USD", IsManual = true,
        });
        await db.SaveChangesAsync(default);

        var file = await new ExportCsvQueryHandler(db).Handle(new ExportCsvQuery(TransferScope.Portfolio), default);
        var parsed = PortfolioCsvParser.Parse(file!.Content);

        Assert.Empty(parsed.Problems);
        var buy = parsed.Events[0];
        Assert.Equal("Брокер; головний", buy.AccountName);
        Assert.Equal(AccountType.Brokerage, buy.AccountType);
        Assert.Equal("Apple", buy.HoldingName);
        Assert.Equal(TransactionType.Buy, buy.TransactionType);
        Assert.Equal(10m, buy.Quantity);
        Assert.Equal(230m, buy.UnitPrice);
        Assert.Equal("перша; купівля", buy.Notes);

        var valuation = parsed.Events[1];
        Assert.Equal(ImportedEventKind.Valuation, valuation.Kind);
        Assert.Equal(2480.55m, valuation.Amount);
        Assert.Equal(Today, valuation.Date);
    }

    [Fact]
    public void Takes_a_comma_separated_file_too()
    {
        // Half the world writes commas; the file itself says which, so nothing is configured.
        var parsed = PortfolioCsvParser.Parse(
            "Актив,Подія,Дата,Кількість,Ціна,Валюта\nApple,Купівля,2026-01-10,10,230.00,USD\n");

        Assert.Empty(parsed.Problems);
        Assert.Equal(2300m, Assert.Single(parsed.Events).Amount);
    }

    [Fact]
    public void Reads_numbers_however_the_spreadsheet_wrote_them()
    {
        var parsed = PortfolioCsvParser.Parse(
            "Актив;Подія;Дата;Сума\n"
            + "A;Оцінка;2026-01-10;1 234,56\n"
            + "B;Оцінка;2026-01-10;1234.56\n"
            + "C;Оцінка;2026-01-10;1,234.56\n");

        Assert.Empty(parsed.Problems);
        Assert.All(parsed.Events, e => Assert.Equal(1234.56m, e.Amount));
    }

    [Fact]
    public void Reads_dates_however_the_broker_wrote_them()
    {
        var parsed = PortfolioCsvParser.Parse(
            "Актив;Подія;Дата;Сума\n"
            + "A;Оцінка;2026-01-10;1\n"
            + "B;Оцінка;10.01.2026;1\n"
            + "C;Оцінка;2026-01-10 14:33:02;1\n");

        Assert.Empty(parsed.Problems);
        Assert.All(parsed.Events, e => Assert.Equal(new DateOnly(2026, 1, 10), e.Date));
    }

    [Fact]
    public void Derives_the_third_number_from_the_other_two()
    {
        var parsed = PortfolioCsvParser.Parse(
            "Актив;Подія;Дата;Кількість;Ціна;Сума\n"
            + "A;Купівля;2026-01-10;10;230;\n"
            + "B;Купівля;2026-01-10;10;;2300\n");

        Assert.Equal(2300m, parsed.Events[0].Amount);
        Assert.Equal(230m, parsed.Events[1].UnitPrice);
    }

    [Fact]
    public void A_cash_flow_with_only_a_sum_becomes_one_unit_at_that_price()
    {
        // Same shape the manual form stores, so Quantity x UnitPrice still equals the sum.
        var parsed = PortfolioCsvParser.Parse("Актив;Подія;Дата;Сума\nA;Дивіденди;2026-04-15;24,50\n");

        var dividend = Assert.Single(parsed.Events);
        Assert.Equal(1m, dividend.Quantity);
        Assert.Equal(24.50m, dividend.UnitPrice);
    }

    [Fact]
    public void A_note_holding_the_separator_does_not_shift_the_columns()
    {
        var parsed = PortfolioCsvParser.Parse(
            "Актив;Подія;Дата;Сума;Нотатка\nA;Оцінка;2026-01-10;100;\"продано; частинами\"\n");

        Assert.Equal("продано; частинами", Assert.Single(parsed.Events).Notes);
    }

    [Fact]
    public void Refuses_a_negative_quantity_and_says_which_line()
    {
        // Direction lives in the event, never in the sign — the same rule the manual form
        // enforces, so an import can't smuggle in what typing cannot.
        var parsed = PortfolioCsvParser.Parse(
            "Актив;Подія;Дата;Кількість;Ціна\nA;Продаж;2026-01-10;-2;262\n");

        Assert.Empty(parsed.Events);
        Assert.Equal(2, Assert.Single(parsed.Problems).Line);
        Assert.Contains("напрямок", Assert.Single(parsed.Problems).Message);
    }

    [Fact]
    public void An_unreadable_row_is_reported_and_the_rest_of_the_file_still_loads()
    {
        var parsed = PortfolioCsvParser.Parse(
            "Актив;Подія;Дата;Сума\n"
            + "A;Купівля;2026-01-10;100\n"
            + "B;Невідомо;2026-01-11;100\n"
            + "C;Оцінка;2026-01-12;100\n");

        Assert.Equal(2, parsed.Events.Count);
        Assert.Equal(3, Assert.Single(parsed.Problems).Line);
    }

    [Fact]
    public void A_file_without_the_required_headers_says_so_rather_than_importing_nothing()
    {
        var parsed = PortfolioCsvParser.Parse("Актив;Сума\nA;100\n");

        Assert.Empty(parsed.Events);
        Assert.Contains("Подія", Assert.Single(parsed.Problems).Message);
    }

    [Fact]
    public void Survives_a_byte_order_mark_and_crlf_line_endings()
    {
        var parsed = PortfolioCsvParser.Parse("﻿Актив;Подія;Дата;Сума\r\nA;Оцінка;2026-01-10;100\r\n");

        Assert.Empty(parsed.Problems);
        Assert.Equal("A", Assert.Single(parsed.Events).HoldingName);
    }
}
