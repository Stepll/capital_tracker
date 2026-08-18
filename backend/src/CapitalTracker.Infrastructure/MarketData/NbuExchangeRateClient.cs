using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CapitalTracker.Infrastructure.MarketData;

/// <summary>
/// One day's rate for one currency, as UAH per single unit.
/// </summary>
public record NbuDailyRate(DateOnly Date, decimal Rate);

/// <summary>
/// Client for the National Bank of Ukraine's public exchange rate API —
/// free, no API key required. Rates are "UAH per 1 unit of currency", which
/// matches how we store <see cref="Domain.Entities.ExchangeRate"/>.
/// https://bank.gov.ua/ua/open-data/api-dev
/// </summary>
public class NbuExchangeRateClient(HttpClient httpClient)
{
    private const string LatestUrl =
        "https://bank.gov.ua/NBUStatService/v1/statdirectory/exchange?json";

    /// <summary>
    /// A different service from the one above, and deliberately so: the statdirectory
    /// endpoint silently ignores start/end and answers with a single day, so asking it
    /// for a range looks like it worked and quietly backfills nothing. This one honours
    /// the range and fills weekends/holidays by carrying the previous rate forward.
    /// </summary>
    private const string PeriodUrl =
        "https://bank.gov.ua/NBU_Exchange/exchange_site";

    private record NbuRate(
        [property: JsonPropertyName("cc")] string CurrencyCode,
        [property: JsonPropertyName("rate")] decimal Rate);

    private record NbuPeriodRate(
        [property: JsonPropertyName("exchangedate")] string ExchangeDate,
        [property: JsonPropertyName("rate")] decimal Rate,
        [property: JsonPropertyName("units")] int Units);

    public async Task<IReadOnlyDictionary<string, decimal>> GetLatestRatesAsync(
        CancellationToken cancellationToken = default)
    {
        var rates = await httpClient.GetFromJsonAsync<List<NbuRate>>(LatestUrl, cancellationToken) ?? [];

        return rates.ToDictionary(r => r.CurrencyCode, r => r.Rate);
    }

    /// <summary>
    /// Every day's rate for one currency across a closed date range, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<NbuDailyRate>> GetPeriodRatesAsync(
        string currency, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var url = $"{PeriodUrl}?start={start:yyyyMMdd}&end={end:yyyyMMdd}" +
            $"&valcode={currency.ToLowerInvariant()}&sort=exchangedate&order=asc&json";

        var rates = await httpClient.GetFromJsonAsync<List<NbuPeriodRate>>(url, cancellationToken) ?? [];

        return rates
            .Select(r => DateOnly.TryParseExact(r.ExchangeDate, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)
                    // "rate" is per `units` of the currency (1 for USD and EUR, but 100 for
                    // some others) while we store per single unit. Dividing costs nothing
                    // today and stops a tenfold error the day a third currency is added.
                    ? new NbuDailyRate(date, r.Units > 0 ? r.Rate / r.Units : r.Rate)
                    : null)
            .OfType<NbuDailyRate>()
            .OrderBy(r => r.Date)
            .ToList();
    }
}
