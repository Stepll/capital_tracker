using System.IO.Compression;
using System.Text;
using CapitalTracker.Application.Transfer;

namespace CapitalTracker.Tests;

public class XlsxAndMappingTests
{
    [Fact]
    public void A_gap_in_a_row_does_not_shift_the_columns_after_it()
    {
        // Excel omits empty cells rather than emitting them, so appending in document order
        // slides every later column one to the left. This is the bug that would quietly put
        // the amount under "currency" for a whole file.
        var xlsx = Workbook([
            [("A1", "Дата"), ("B1", "Опис"), ("C1", "Сума")],
            [("A2", "2026-01-10"), ("C2", "100")],
        ]);

        var grid = XlsxReader.Read(xlsx);

        Assert.Equal(["2026-01-10", "", "100"], grid[1]);
    }

    [Fact]
    public void Finds_the_table_under_a_bank_letterhead()
    {
        // The real card statement puts twenty-three rows of address and account details
        // above its header.
        List<(string, string)[]> rows =
        [
            [("A1", "Банк, місто Київ")],
            [("A2", "КОБРІЙ СТЕПАН")],
            [("A3", "Номер рахунку"), ("B3", "UA89")],
            [("A4", "Дата операції"), ("B4", "Опис операції"), ("C4", "Сума"), ("D4", "Валюта")],
            [("A5", "2026-01-10"), ("B5", "Оплата"), ("C5", "-100"), ("D5", "UAH")],
        ];

        var suggestion = GridMapper.Suggest(XlsxReader.Read(Workbook(rows)));

        Assert.Equal(3, suggestion.HeaderRow);
        Assert.Equal(0, suggestion.Columns["Дата"]);
        Assert.Equal(2, suggestion.Columns["Сума"]);
        Assert.Equal(3, suggestion.Columns["Валюта"]);
    }

    [Fact]
    public void Maps_a_debit_credit_column_onto_events()
    {
        // The business statement's shape: direction is a word in its own column, which is
        // why the mapper has to map values and not only columns.
        List<string[]> grid =
        [
            ["Дата операції", "Вид операції", "Деталі", "Сума в валюті рахунку", "Валюта"],
            ["2026-07-01", "кредит", "Оплата за послуги", "12000", "UAH"],
            ["2026-07-02", "дебет", "Комісія", "150", "UAH"],
        ];

        var mapping = new ImportMapping(
            0,
            new Dictionary<string, int> { ["Дата"] = 0, ["Нотатка"] = 2, ["Сума"] = 3, ["Валюта"] = 4 },
            new EventMapping(
                Column: 1,
                Values: new Dictionary<string, string> { ["кредит"] = "Внесення", ["дебет"] = "Виведення" }));

        var parsed = PortfolioCsvParser.ParseGrid(GridMapper.ToCanonical(grid, mapping));

        Assert.Empty(parsed.Problems);
        Assert.Equal([Domain.Enums.TransactionType.Deposit, Domain.Enums.TransactionType.Withdrawal],
            parsed.Events.Select(e => e.TransactionType));
        Assert.Equal(12000m, parsed.Events[0].Amount);
        Assert.Equal("Оплата за послуги", parsed.Events[0].Notes);
    }

    [Fact]
    public void Takes_the_direction_from_the_sign_when_there_is_no_column_for_it()
    {
        // The card statement's shape: nothing says "debit" anywhere, the amount is just
        // negative. The sign is consumed here, since a negative sum would fail the rule that
        // direction lives in the event.
        List<string[]> grid =
        [
            ["Дата", "Сума"],
            ["2026-01-10", "-100.50"],
            ["2026-01-11", "2500"],
        ];

        var mapping = new ImportMapping(
            0,
            new Dictionary<string, int> { ["Дата"] = 0, ["Сума"] = 1 },
            new EventMapping(WhenPositive: "Внесення", WhenNegative: "Виведення"));

        var parsed = PortfolioCsvParser.ParseGrid(GridMapper.ToCanonical(grid, mapping));

        Assert.Empty(parsed.Problems);
        Assert.Equal(Domain.Enums.TransactionType.Withdrawal, parsed.Events[0].TransactionType);
        Assert.Equal(100.50m, parsed.Events[0].Amount);
        Assert.Equal(Domain.Enums.TransactionType.Deposit, parsed.Events[1].TransactionType);
    }

    [Fact]
    public void The_footer_a_bank_prints_under_its_table_is_not_read_as_a_deposit()
    {
        // The card statement ends with notes about deposit guarantees, spread across the
        // same columns. With direction taken from a sign, a row holding no number at all
        // would otherwise come out as a deposit of zero.
        List<string[]> grid =
        [
            ["Дата", "Сума"],
            ["2026-01-10", "-100"],
            ["Вклади гарантуються відповідно до Закону України", ""],
        ];

        var mapping = new ImportMapping(
            0,
            new Dictionary<string, int> { ["Дата"] = 0, ["Сума"] = 1 },
            new EventMapping(WhenPositive: "Внесення", WhenNegative: "Виведення"));

        var parsed = PortfolioCsvParser.ParseGrid(GridMapper.ToCanonical(grid, mapping));

        Assert.Single(parsed.Events);
        Assert.Empty(parsed.Problems);
    }

    [Fact]
    public void A_row_whose_value_was_not_mapped_is_left_out_rather_than_guessed_at()
    {
        List<string[]> grid =
        [
            ["Дата", "Тип", "Сума"],
            ["2026-01-10", "кредит", "100"],
            ["2026-01-11", "комісія банку", "5"],
        ];

        var mapping = new ImportMapping(
            0,
            new Dictionary<string, int> { ["Дата"] = 0, ["Сума"] = 2 },
            new EventMapping(Column: 1, Values: new Dictionary<string, string> { ["кредит"] = "Внесення" }));

        var parsed = PortfolioCsvParser.ParseGrid(GridMapper.ToCanonical(grid, mapping));

        Assert.Single(parsed.Events);
        Assert.Empty(parsed.Problems);
    }

    [Fact]
    public void Reads_a_date_that_arrived_as_an_excel_serial()
    {
        // The reader deliberately leaves serials alone — recognising them needs the style
        // table — so the date parser has to know one when it sees it.
        List<string[]> grid = [["Дата", "Подія", "Сума"], ["46032", "Оцінка", "100"]];

        var parsed = PortfolioCsvParser.ParseGrid(grid);

        Assert.Empty(parsed.Problems);
        Assert.Equal(new DateOnly(2026, 1, 10), Assert.Single(parsed.Events).Date);
    }

    [Fact]
    public void Our_own_export_needs_no_mapping_at_all()
    {
        List<string[]> grid =
        [
            PortfolioCsv.Headers,
            ["Б", "Брокерський", "USD", "A", "AAPL", "Купівля", "2026-01-10", "10", "230", "2300", "USD", ""],
        ];

        Assert.True(SourceFile.LooksCanonical(grid));
    }

    [Fact]
    public void A_bank_statement_read_as_balances_gives_one_valuation_per_day_and_no_transactions()
    {
        // What a card statement is actually worth to a capital tracker: not a hundred café
        // payments, but what the account was worth on each of those days.
        List<string[]> grid =
        [
            ["Дата", "Опис", "Залишок"],
            ["2026-01-10", "Оплата", "5000"],
            ["2026-01-11", "Оплата", "4800"],
        ];

        var mapping = new ImportMapping(
            0,
            new Dictionary<string, int> { ["Дата"] = 0, ["Нотатка"] = 1, ["Сума"] = 2 },
            new EventMapping(Fixed: "Оцінка"));

        var parsed = PortfolioCsvParser.ParseGrid(GridMapper.ToCanonical(grid, mapping));

        Assert.Empty(parsed.Problems);
        Assert.All(parsed.Events, e => Assert.Equal(ImportedEventKind.Valuation, e.Kind));
        Assert.Equal([5000m, 4800m], parsed.Events.Select(e => e.Amount));
    }

    [Fact]
    public void In_a_newest_first_statement_the_days_closing_balance_wins()
    {
        // Banks print newest first, and only the last row for a date becomes that day's
        // valuation. Read in the file's own order that would hand over the day's opening
        // balance instead of the one it closed on.
        List<string[]> grid =
        [
            ["Дата", "Залишок"],
            ["2026-01-11", "4800"],
            ["2026-01-10", "5000"],
            ["2026-01-10", "5300"],
        ];

        var mapping = new ImportMapping(
            0,
            new Dictionary<string, int> { ["Дата"] = 0, ["Сума"] = 1 },
            new EventMapping(Fixed: "Оцінка"));

        var parsed = PortfolioCsvParser.ParseGrid(GridMapper.ToCanonical(grid, mapping));

        // 5300 came first in the file, so chronologically it is the earlier of the two.
        var tenth = parsed.Events.Where(e => e.Date == new DateOnly(2026, 1, 10)).ToList();
        Assert.Equal([5300m, 5000m], tenth.Select(e => e.Amount));
    }

    /// <summary>
    /// A minimal xlsx: the parts this reader actually consults, written by hand so the tests
    /// exercise the real path — shared strings, the workbook relationship, sparse cells —
    /// without a library and without shipping anyone's statement into the repository.
    /// </summary>
    private static byte[] Workbook(List<(string Reference, string Value)[]> rows)
    {
        var shared = rows.SelectMany(r => r).Select(c => c.Value).Distinct().ToList();

        var sheet = new StringBuilder(
            """<?xml version="1.0"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");

        for (var i = 0; i < rows.Count; i++)
        {
            sheet.Append($"<row r=\"{i + 1}\">");
            foreach (var (reference, value) in rows[i])
            {
                sheet.Append($"<c r=\"{reference}\" t=\"s\"><v>{shared.IndexOf(value)}</v></c>");
            }

            sheet.Append("</row>");
        }

        sheet.Append("</sheetData></worksheet>");

        var strings = new StringBuilder(
            """<?xml version="1.0"?><sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        foreach (var value in shared)
        {
            strings.Append($"<si><t>{System.Security.SecurityElement.Escape(value)}</t></si>");
        }

        strings.Append("</sst>");

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            Write(archive, "xl/workbook.xml",
                """<?xml version="1.0"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Write(archive, "xl/_rels/workbook.xml.rels",
                """<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            Write(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
            Write(archive, "xl/sharedStrings.xml", strings.ToString());
        }

        return buffer.ToArray();
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        using var stream = archive.CreateEntry(path).Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }
}
