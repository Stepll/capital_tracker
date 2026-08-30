using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Transfer;

/// <summary>
/// How the owner said a foreign statement lines up with our columns. Only ever produced by
/// a person looking at their own file — nothing here is guessed at commit time.
/// </summary>
public record ImportMapping(
    // Real statements open with a letterhead: the Credit Agricole one puts 23 rows of bank
    // address and account details above the table.
    int HeaderRow,
    // Canonical column name -> index in the source grid.
    Dictionary<string, int> Columns,
    EventMapping Event);

/// <summary>
/// Where the direction of a row comes from — and it is a different place in every file.
/// A business statement puts it in its own column as "кредит"/"дебет"; a card statement
/// has no such column and leans on the sign of the amount; a Binance export names it in a
/// type column. All three exist among the files this was built against.
/// </summary>
public record EventMapping(
    int? Column = null,
    /// <summary>Raw cell value (lower-cased) -> one of PortfolioCsv's event labels.</summary>
    Dictionary<string, string>? Values = null,
    /// <summary>Every row is this event — a file that is nothing but purchases, say.</summary>
    string? Fixed = null,
    /// <summary>Used when the sign of the amount is the only thing saying which way money went.</summary>
    string? WhenPositive = null,
    string? WhenNegative = null);

public static class GridMapper
{
    /// <summary>
    /// Rewrites a foreign grid into one with our headers, and then hands it to the parser
    /// that already knows what to do with it. The mapper's whole job is this translation —
    /// validation, derived numbers and the planner stay untouched and stay tested.
    /// </summary>
    public static List<string[]> ToCanonical(List<string[]> source, ImportMapping mapping)
    {
        var canonical = new List<string[]> { PortfolioCsv.Headers };
        var amountColumn = mapping.Columns.TryGetValue("Сума", out var amount) ? amount : (int?)null;

        foreach (var row in source.Skip(mapping.HeaderRow + 1))
        {
            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            string Cell(string header) =>
                mapping.Columns.TryGetValue(header, out var index) && index < row.Length
                    ? row[index].Trim()
                    : "";

            var line = PortfolioCsv.Headers.Select(Cell).ToArray();

            var eventLabel = ResolveEvent(mapping.Event, row, amountColumn);
            if (eventLabel is null)
                continue;

            line[Array.IndexOf(PortfolioCsv.Headers, "Подія")] = eventLabel;

            // The sign carried the direction, so it has done its job; leaving it in would
            // trip the rule that quantities and sums are never negative.
            var sumIndex = Array.IndexOf(PortfolioCsv.Headers, "Сума");
            if (mapping.Event.WhenNegative is not null)
                line[sumIndex] = line[sumIndex].TrimStart('-').Replace("−", "");

            canonical.Add(line);
        }

        return canonical;
    }

    private static string? ResolveEvent(EventMapping mapping, string[] row, int? amountColumn)
    {
        if (mapping.Fixed is not null)
            return mapping.Fixed;

        if (mapping.Column is int column && column < row.Length)
        {
            var raw = row[column].Trim().ToLowerInvariant();
            // A value the owner didn't map is a row they chose not to import — a commission
            // line among trades, say — so it is dropped rather than guessed at.
            return mapping.Values is not null && mapping.Values.TryGetValue(raw, out var label) ? label : null;
        }

        if (mapping.WhenPositive is not null && amountColumn is int index && index < row.Length)
        {
            var raw = row[index].Trim();

            // A row with no amount is not a zero transaction — it is the footer the bank
            // prints under its table ("Вклади гарантуються…"), and reading the sign of
            // nothing would turn every one of those lines into a deposit.
            if (!raw.Any(char.IsDigit))
                return null;

            var negative = raw.StartsWith('-') || raw.StartsWith('−');
            return negative ? mapping.WhenNegative : mapping.WhenPositive;
        }

        return null;
    }

    /// <summary>
    /// A first guess at which row is the header and which column is which, so the owner
    /// starts from something close rather than from an empty form. Every part of it is
    /// theirs to override.
    /// </summary>
    public static ImportMapping Suggest(List<string[]> grid)
    {
        var headerRow = GuessHeaderRow(grid);
        var headers = headerRow < grid.Count ? grid[headerRow] : [];
        var columns = new Dictionary<string, int>();

        for (var i = 0; i < headers.Length; i++)
        {
            var name = Normalise(headers[i]);
            if (name.Length == 0)
                continue;

            foreach (var (canonical, hints) in Hints)
            {
                if (!columns.ContainsKey(canonical) && hints.Any(h => name.Contains(h)))
                {
                    columns[canonical] = i;
                    break;
                }
            }
        }

        return new ImportMapping(headerRow, columns, new EventMapping());
    }

    /// <summary>
    /// Substrings, not exact names: real headers read "Дата здійснення операції" and
    /// "Сума у валюті рахунку", and they arrive with line breaks inside them.
    /// </summary>
    private static readonly (string Canonical, string[] Hints)[] Hints =
    [
        ("Дата", ["дата"]),
        ("Сума", ["сума", "total", "amount"]),
        ("Кількість", ["кількість", "quantity", "qty"]),
        ("Ціна", ["ціна", "price"]),
        ("Валюта", ["валюта операції", "валюта", "currency"]),
        ("Актив", ["актив", "тікер", "symbol", "asset", "base-asset"]),
        ("Нотатка", ["деталі", "опис", "призначення", "note"]),
    ];

    /// <summary>
    /// The header is the first row that reads like one: several non-empty cells, and text
    /// rather than numbers. A letterhead line has one or two filled cells; the table's
    /// header has most of them.
    /// </summary>
    private static int GuessHeaderRow(List<string[]> grid)
    {
        var best = 0;
        var bestScore = 0;

        for (var i = 0; i < Math.Min(grid.Count, 40); i++)
        {
            var filled = grid[i].Count(c => !string.IsNullOrWhiteSpace(c));
            var textual = grid[i].Count(c => !string.IsNullOrWhiteSpace(c) && !decimal.TryParse(c, out _));
            var score = filled >= 3 ? textual : 0;

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private static string Normalise(string header) =>
        header.Replace('\n', ' ').Replace('\r', ' ').Trim().ToLowerInvariant();
}
