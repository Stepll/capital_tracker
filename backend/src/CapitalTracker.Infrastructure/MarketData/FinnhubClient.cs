using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Infrastructure.MarketData;

/// <summary>
/// Quote and recent-news lookup for exchange-traded tickers, used to give the
/// analysis model hard numbers instead of making it infer prices from search results.
/// Free-tier endpoints only.
/// </summary>
public class FinnhubClient(
    HttpClient httpClient,
    IOptions<FinnhubOptions> options,
    ILogger<FinnhubClient> logger)
{
    private const int NewsWindowDays = 14;
    private const int MaxNewsItems = 15;

    private record Quote(
        [property: JsonPropertyName("c")] decimal Current,
        [property: JsonPropertyName("dp")] decimal? PercentChange);

    private record NewsItem(
        [property: JsonPropertyName("datetime")] long UnixTime,
        [property: JsonPropertyName("headline")] string? Headline,
        [property: JsonPropertyName("source")] string? Source,
        [property: JsonPropertyName("url")] string? Url);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    /// <summary>
    /// Returns null whenever market data is unavailable for any reason — no key,
    /// unknown symbol, API error, timeout. Callers treat that as "analyse without it",
    /// never as a failure: a Finnhub outage must not take down the whole analysis.
    /// </summary>
    public async Task<MarketDataBlock?> GetAsync(string symbol, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var key = options.Value.ApiKey;

        try
        {
            var quote = await httpClient.GetFromJsonAsync<Quote>(
                $"api/v1/quote?symbol={Uri.EscapeDataString(symbol)}&token={key}", cancellationToken);

            // Finnhub answers an unrecognised ticker with an all-zero quote rather than
            // a 404. Reporting that as a real price would tell the model the asset is
            // worthless — a false premise it would then reason from, confidently.
            if (quote is null || quote.Current == 0m)
            {
                logger.LogInformation("Finnhub returned no usable quote for {Symbol}; analysing without market data.", symbol);
                return null;
            }

            return new MarketDataBlock(quote.Current, quote.PercentChange, await GetNewsAsync(symbol, key, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Finnhub lookup failed for {Symbol}; analysing without market data.", symbol);
            return null;
        }
    }

    private async Task<IReadOnlyList<MarketNewsItem>> GetNewsAsync(
        string symbol, string? key, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-NewsWindowDays);

        try
        {
            var items = await httpClient.GetFromJsonAsync<List<NewsItem>>(
                $"api/v1/company-news?symbol={Uri.EscapeDataString(symbol)}" +
                $"&from={from:yyyy-MM-dd}&to={today:yyyy-MM-dd}&token={key}", cancellationToken) ?? [];

            return items
                .Where(i => !string.IsNullOrWhiteSpace(i.Headline) && !string.IsNullOrWhiteSpace(i.Url))
                .OrderByDescending(i => i.UnixTime)
                .Take(MaxNewsItems)
                .Select(i => new MarketNewsItem(
                    DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(i.UnixTime).UtcDateTime),
                    i.Source ?? "?",
                    i.Headline!,
                    i.Url!))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // company-news is US-equities-only on the free tier, so a miss here is
            // routine for non-US tickers. The quote alone is still worth having.
            logger.LogInformation(ex, "Finnhub news lookup failed for {Symbol}; continuing with the quote only.", symbol);
            return [];
        }
    }
}
