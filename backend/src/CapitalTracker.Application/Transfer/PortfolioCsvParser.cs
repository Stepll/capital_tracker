using System.Globalization;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Transfer;

public enum ImportedEventKind
{
    Transaction,
    Valuation,
    Deletion,
}

public record ImportedEvent(
    int Line,
    string? AccountName,
    AccountType? AccountType,
    string? AccountCurrency,
    string? HoldingName,
    string? Symbol,
    ImportedEventKind Kind,
    TransactionType? TransactionType,
    DateOnly Date,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? Amount,
    string? Currency,
    string? Notes);

/// <summary>A row the file could not offer, with the line number so it can be found and fixed.</summary>
public record ImportProblem(int Line, string Message);

public record ParsedCsv(List<ImportedEvent> Events, List<ImportProblem> Problems);

/// <summary>
/// Turns the raw grid from <see cref="CsvReader"/> into typed events. Deliberately
/// forgiving about how the file was written and strict about what it means: separators,
/// decimal commas, date formats and label casing all vary between the spreadsheet that
/// produced the file and the one that will edit it next, while a row whose numbers don't
/// add up is reported rather than guessed at.
/// </summary>
public static class PortfolioCsvParser
{
    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy", "yyyy/MM/dd", "MM/dd/yyyy"];

    public static ParsedCsv Parse(string text)
    {
        var stripped = CsvReader.StripBom(text);
        return ParseGrid(CsvReader.Read(stripped, CsvReader.DetectDelimiter(stripped)));
    }

    /// <summary>
    /// The entry point everything shares. A foreign statement becomes a grid with our
    /// headers first (see GridMapper) and lands here, so every rule below — validation,
    /// derived numbers, cash-flow shaping — applies to it unchanged.
    /// </summary>
    public static ParsedCsv ParseGrid(List<string[]> grid)
    {
        var events = new List<ImportedEvent>();
        var problems = new List<ImportProblem>();

        if (grid.Count == 0)
        {
            problems.Add(new ImportProblem(0, "Файл порожній."));
            return new ParsedCsv(events, problems);
        }

        // Columns are matched by name, not position, so a file with them reordered or with
        // extra columns of its own still loads.
        var columns = grid[0]
            .Select((name, index) => (Name: name.Trim().ToLowerInvariant(), Index: index))
            .GroupBy(c => c.Name)
            .ToDictionary(g => g.Key, g => g.First().Index);

        int? Column(string header) =>
            columns.TryGetValue(header.ToLowerInvariant(), out var index) ? index : null;

        var eventColumn = Column("Подія");
        var dateColumn = Column("Дата");
        if (eventColumn is null || dateColumn is null)
        {
            problems.Add(new ImportProblem(1, "У шапці бракує обов'язкових колонок «Подія» та «Дата»."));
            return new ParsedCsv(events, problems);
        }

        for (var row = 1; row < grid.Count; row++)
        {
            // Line numbers are what the person will see in their spreadsheet: 1-based, and
            // the header is line 1.
            var line = row + 1;
            var cells = grid[row];

            string? Cell(int? index) =>
                index is null || index >= cells.Length || string.IsNullOrWhiteSpace(cells[index.Value])
                    ? null
                    : cells[index.Value].Trim();

            if (cells.All(string.IsNullOrWhiteSpace))
                continue;

            var eventLabel = Cell(eventColumn);
            if (eventLabel is null)
            {
                problems.Add(new ImportProblem(line, "Не вказано подію."));
                continue;
            }

            if (!TryReadEvent(eventLabel, out var kind, out var transactionType))
            {
                problems.Add(new ImportProblem(line, $"Невідома подія «{eventLabel}»."));
                continue;
            }

            var rawDate = Cell(dateColumn);
            if (!TryReadDate(rawDate, out var date))
            {
                problems.Add(new ImportProblem(line, $"Не вдалося прочитати дату «{rawDate}»."));
                continue;
            }

            var quantity = TryReadNumber(Cell(Column("Кількість")), line, "кількість", problems);
            var unitPrice = TryReadNumber(Cell(Column("Ціна")), line, "ціну", problems);
            var amount = TryReadNumber(Cell(Column("Сума")), line, "суму", problems);

            // Any two of the three imply the third — which is precisely where real
            // statements differ: some give a unit price, others only the deal's total.
            if (amount is null && quantity is not null && unitPrice is not null)
                amount = Math.Round(quantity.Value * unitPrice.Value, 2);
            else if (unitPrice is null && amount is not null && quantity is > 0m)
                unitPrice = Math.Round(amount.Value / quantity.Value, 2);
            else if (quantity is null && amount is not null && unitPrice is > 0m)
                quantity = amount.Value / unitPrice.Value;

            var accountType = ReadAccountType(Cell(Column("Тип рахунку")));

            var parsed = new ImportedEvent(
                line,
                Cell(Column("Рахунок")),
                accountType,
                Cell(Column("Валюта рахунку"))?.ToUpperInvariant(),
                Cell(Column("Актив")),
                Cell(Column("Тікер")),
                kind,
                transactionType,
                date,
                quantity,
                unitPrice,
                amount,
                Cell(Column("Валюта"))?.ToUpperInvariant(),
                Cell(Column("Нотатка")));

            var problem = Validate(parsed);
            if (problem is not null)
            {
                problems.Add(new ImportProblem(line, problem));
                continue;
            }

            events.Add(Normalise(parsed));
        }

        return new ParsedCsv(events, problems);
    }

    private static string? Validate(ImportedEvent e) => e.Kind switch
    {
        ImportedEventKind.Valuation when e.Amount is null => "Оцінка без суми.",
        ImportedEventKind.Deletion => null,
        ImportedEventKind.Transaction when e.Amount is null && e.Quantity is null =>
            "Транзакція без суми й без кількості.",
        ImportedEventKind.Transaction when e.Quantity is < 0m =>
            "Від'ємна кількість — напрямок задається типом події (Продаж, Виведення), а не знаком.",
        _ => null,
    };

    /// <summary>
    /// Fills in what the row left implicit. A cash flow — a dividend, rent — has an amount
    /// and no units, and is stored the same way the manual form stores it: one unit at the
    /// full price, so Quantity × UnitPrice still equals the sum.
    /// </summary>
    private static ImportedEvent Normalise(ImportedEvent e)
    {
        if (e.Kind != ImportedEventKind.Transaction)
            return e;

        if (e.Quantity is null or 0m)
            return e with { Quantity = 1m, UnitPrice = e.Amount ?? 0m };

        return e with { UnitPrice = e.UnitPrice ?? 0m };
    }

    private static bool TryReadEvent(string label, out ImportedEventKind kind, out TransactionType? type)
    {
        kind = ImportedEventKind.Transaction;
        type = null;

        if (string.Equals(label, PortfolioCsv.ValuationEvent, StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Valuation", StringComparison.OrdinalIgnoreCase))
        {
            kind = ImportedEventKind.Valuation;
            return true;
        }

        if (string.Equals(label, PortfolioCsv.DeletionEvent, StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "Deletion", StringComparison.OrdinalIgnoreCase))
        {
            kind = ImportedEventKind.Deletion;
            return true;
        }

        foreach (var (value, ukrainian) in PortfolioCsv.EventLabels)
        {
            // The enum name is accepted too, so a file produced by something other than
            // this app — or by a future English UI — still loads.
            if (string.Equals(label, ukrainian, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, value.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                type = value;
                return true;
            }
        }

        return false;
    }

    private static AccountType? ReadAccountType(string? label)
    {
        if (label is null)
            return null;

        foreach (var (value, ukrainian) in PortfolioCsv.AccountTypeLabels)
        {
            if (string.Equals(label, ukrainian, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, value.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryReadDate(string? raw, out DateOnly date)
    {
        date = default;
        if (raw is null)
            return false;

        // Statements often carry a time alongside the date; only the day matters here.
        var datePart = raw.Split(' ', 'T')[0];

        if (DateOnly.TryParseExact(datePart, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return TryReadExcelSerial(datePart, out date);
    }

    /// <summary>
    /// Excel stores a date as days since 1899-12-30 and only the cell's format says so, which
    /// the reader deliberately doesn't chase. Recognising it here instead keeps the guess in
    /// one place and bounded: it applies only where a date was expected and nothing else
    /// parsed. The range keeps it away from both ordinary numbers and yyyyMMdd, which is far
    /// above it.
    /// </summary>
    private static bool TryReadExcelSerial(string raw, out DateOnly date)
    {
        date = default;

        if (raw.Length == 8 && DateOnly.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial))
            return false;

        if (serial is < 1000m or > 80000m)
            return false;

        date = DateOnly.FromDateTime(new DateTime(1899, 12, 30).AddDays((double)serial));
        return true;
    }

    private static decimal? TryReadNumber(string? raw, int line, string what, List<ImportProblem> problems)
    {
        if (raw is null)
            return null;

        // Thousands separators as spaces (including the non-breaking kind Excel likes) and
        // currency symbols pasted along with the number.
        var cleaned = new string(raw.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());

        var lastDot = cleaned.LastIndexOf('.');
        var lastComma = cleaned.LastIndexOf(',');

        // Whichever separator comes last is the decimal one; anything before it groups digits.
        if (lastDot >= 0 && lastComma >= 0)
        {
            cleaned = lastComma > lastDot
                ? cleaned.Replace(".", "").Replace(',', '.')
                : cleaned.Replace(",", "");
        }
        else
        {
            cleaned = cleaned.Replace(',', '.');
        }

        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return value;

        problems.Add(new ImportProblem(line, $"Не вдалося прочитати {what} «{raw}»."));
        return null;
    }
}
