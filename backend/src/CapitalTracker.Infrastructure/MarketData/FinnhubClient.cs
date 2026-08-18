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
    /// <summary>
    /// Every registration must set this as the client's BaseAddress — the requests below
    /// use relative paths, and because failures are swallowed into "no data", forgetting
    /// it turns the whole client into a permanently silent no-op rather than an error.
    /// </summary>
    public const string BaseUrl = "https://finnhub.io/";

    /// <summary>
    /// Finnhub's /quote endpoint returns no currency field, so this is an assumption
    /// rather than a reading: on the free tier, and behind the "bare ticker on a
    /// brokerage account" filter callers apply, quotes are US listings priced in USD.
    /// /stock/profile2 would report the real currency at the cost of a second request
    /// per symbol to learn something that is USD in every case we can price today.
    /// </summary>
    public const string QuoteCurrency = "USD";

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
    /// Current price only. Returns null whenever it is unavailable for any reason — no
    /// key, unknown symbol, API error, timeout — so callers decide the consequence
    /// (skip a holding, or analyse without market data) rather than handling exceptions.
    /// </summary>
    public async Task<MarketQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            var quote = await httpClient.GetFromJsonAsync<Quote>(
                $"api/v1/quote?symbol={Uri.EscapeDataString(symbol)}&token={options.Value.ApiKey}",
                cancellationToken);

            // Finnhub answers an unrecognised ticker with an all-zero quote rather than
            // a 404. Treating that as a real price would report the asset as worthless.
            if (quote is null || quote.Current == 0m)
            {
                logger.LogInformation("Finnhub returned no usable quote for {Symbol}.", symbol);
                return null;
            }

            return new MarketQuote(quote.Current, quote.PercentChange);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Finnhub quote lookup failed for {Symbol}.", symbol);
            return null;
        }
    }

    /// <summary>
    /// Quote plus recent headlines, for the analysis pipeline. The price job wants
    /// <see cref="GetQuoteAsync"/> instead — fetching news per symbol in a bulk refresh
    /// doubles the requests against a 60/min free tier to no purpose.
    /// </summary>
    public async Task<MarketDataBlock?> GetAsync(string symbol, CancellationToken cancellationToken)
    {
        var quote = await GetQuoteAsync(symbol, cancellationToken);
        if (quote is null)
        {
            return null;
        }

        var news = await GetNewsAsync(symbol, options.Value.ApiKey, cancellationToken);
        return new MarketDataBlock(quote.Price, quote.PercentChange, news);
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
