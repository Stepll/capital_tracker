using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CapitalTracker.Infrastructure.MarketData;

/// <summary>
/// Client for the National Bank of Ukraine's public exchange rate API —
/// free, no API key required. Rates are "UAH per 1 unit of currency", which
/// matches how we store <see cref="Domain.Entities.ExchangeRate"/>.
/// https://bank.gov.ua/ua/open-data/api-dev
/// </summary>
public class NbuExchangeRateClient(HttpClient httpClient)
{
    private record NbuRate(
        [property: JsonPropertyName("cc")] string CurrencyCode,
        [property: JsonPropertyName("rate")] decimal Rate);

    public async Task<IReadOnlyDictionary<string, decimal>> GetLatestRatesAsync(
        CancellationToken cancellationToken = default)
    {
        var rates = await httpClient.GetFromJsonAsync<List<NbuRate>>(
            "https://bank.gov.ua/NBUStatService/v1/statdirectory/exchange?json",
            cancellationToken) ?? [];

        return rates.ToDictionary(r => r.CurrencyCode, r => r.Rate);
    }
}
