using System.Globalization;
using System.Text;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Transfer;

/// <summary>
/// The one file format the app both writes and reads. Import and export share it on
/// purpose: a file we produce ourselves needs no column mapping to come back in, which
/// makes backup, moving between machines and bulk-editing in a spreadsheet the same
/// feature — and gives the broker-file mapper a fixed shape to map onto.
///
/// Every row is a dated event about one holding. Identity (account, asset) repeats on
/// each row rather than living in a header, so the file stays editable in a spreadsheet
/// without lookups, and so the narrower scopes can simply ignore the columns they already
/// know from the URL.
///
/// SecretAttributes have no column here, and never will: an export that carried logins
/// out of the database in plaintext would be the worst regression this project could
/// ship. Full backups are pg_dump's job.
/// </summary>
public static class PortfolioCsv
{
    /// <summary>
    /// Semicolons, decimal commas and a BOM: this is opened in a Ukrainian-locale Excel,
    /// where a comma-separated file lands in a single column. The reader accepts both
    /// separators either way, so nothing is lost by writing the friendlier one.
    /// </summary>
    public const char Delimiter = ';';

    public static readonly string[] Headers =
    [
        "Рахунок", "Тип рахунку", "Валюта рахунку", "Актив", "Тікер",
        "Подія", "Дата", "Кількість", "Ціна", "Сума", "Валюта", "Нотатка",
    ];

    /// <summary>A valuation is an event like any other here — see the note on ValuationEvent.</summary>
    public static readonly IReadOnlyDictionary<TransactionType, string> EventLabels =
        new Dictionary<TransactionType, string>
        {
            [TransactionType.Buy] = "Купівля",
            [TransactionType.Sell] = "Продаж",
            [TransactionType.Dividend] = "Дивіденди",
            [TransactionType.Rent] = "Оренда",
            [TransactionType.Expense] = "Витрата",
            [TransactionType.Deposit] = "Внесення",
            [TransactionType.Withdrawal] = "Виведення",
        };

    /// <summary>
    /// Carried as an event rather than a second file because a portfolio restored from
    /// transactions alone has no history: the net worth chart is built from valuation
    /// snapshots, not from what things cost. They still land in separate tables — cost and
    /// value stay as distinct as they have always been; only the transport is shared.
    /// </summary>
    public const string ValuationEvent = "Оцінка";

    /// <summary>
    /// Closes out a soft-deleted holding. Without it an export would quietly drop every
    /// asset the owner has ever sold, and with them the part of the capital history those
    /// assets account for — which is exactly what soft deletion exists to preserve.
    /// </summary>
    public const string DeletionEvent = "Видалення";

    /// <summary>
    /// Written in Ukrainian because the file is meant to be read and edited by hand. The
    /// reader also accepts the raw enum names, so a file produced by anything else still
    /// loads.
    /// </summary>
    public static readonly IReadOnlyDictionary<AccountType, string> AccountTypeLabels =
        new Dictionary<AccountType, string>
        {
            [AccountType.Brokerage] = "Брокерський",
            [AccountType.Bank] = "Банківський",
            [AccountType.RealEstate] = "Нерухомість",
            [AccountType.Cash] = "Готівка",
            [AccountType.Crypto] = "Криптовалюта",
            [AccountType.Other] = "Інше",
        };

    public static string Write(IEnumerable<PortfolioCsvRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(Delimiter, Headers));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(Delimiter, new[]
            {
                Field(row.AccountName),
                Field(row.AccountType),
                Field(row.AccountCurrency),
                Field(row.HoldingName),
                Field(row.Symbol),
                Field(row.Event),
                row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Number(row.Quantity, "0.##########"),
                Number(row.UnitPrice, "0.00"),
                Number(row.Amount, "0.00"),
                Field(row.Currency),
                Field(row.Notes),
            }));
        }

        return builder.ToString();
    }

    /// <summary>Decimal comma, to match the separator choice above.</summary>
    private static string Number(decimal? value, string format) =>
        value is null ? "" : value.Value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // Quote only when the value would otherwise break the row apart — a file full of
        // unnecessary quotes is harder to read in the spreadsheet it exists for.
        var needsQuotes = value.Contains(Delimiter) || value.Contains('"')
            || value.Contains('\n') || value.Contains('\r');

        return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}

public record PortfolioCsvRow(
    string AccountName,
    string AccountType,
    string AccountCurrency,
    string HoldingName,
    string? Symbol,
    string Event,
    DateOnly Date,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? Amount,
    string? Currency,
    string? Notes);
