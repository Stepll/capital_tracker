using System.Runtime.CompilerServices;
using System.Text;
using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;
using CapitalTracker.Infrastructure.MarketData;

namespace CapitalTracker.Infrastructure.Ai;

/// <summary>
/// Portfolio-level analysis. Same runner, same tool, same result shape as the per-asset
/// generator — what differs is the prompt and that market data is a quote per ticker
/// rather than quote-plus-news for one: the portfolio view reasons about proportions,
/// and per-symbol news would multiply the request for context the model rarely uses.
/// </summary>
public class AnthropicPortfolioAnalysisGenerator(AnthropicAnalysisRunner runner, FinnhubClient finnhub)
    : IPortfolioAnalysisGenerator
{
    public async IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        PortfolioAnalysisRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var quotable = request.Holdings
            .Where(h => MarketPricing.CanQuote(h.Symbol, h.AccountType))
            .ToList();

        var quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase);

        if (quotable.Count > 0)
        {
            yield return AnalysisGenerationEvent.AtPhase(InsightPhase.MarketData);

            // Grouped so the same ticker held in two accounts costs one request, and a
            // missing quote is simply left out — the client swallows failures into null.
            foreach (var symbol in quotable.Select(h => h.Symbol!).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var quote = await finnhub.GetQuoteAsync(symbol, cancellationToken);
                if (quote is not null)
                {
                    quotes[symbol] = quote;
                }
            }
        }

        var parameters = runner.BuildParameters(
            InsightPrompts.PortfolioSystem, BuildUserMessage(request, quotes));

        await foreach (var e in runner.RunAsync(parameters, "the portfolio", cancellationToken))
        {
            yield return e;
        }
    }

    private static string BuildUserMessage(
        PortfolioAnalysisRequest request, IReadOnlyDictionary<string, MarketQuote> quotes)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<portfolio>");
        sb.AppendLine($"Валюта відображення: {request.DisplayCurrency}");
        sb.AppendLine($"Загальна вартість: {request.TotalValue:N2} {request.DisplayCurrency}");
        sb.AppendLine($"Активів: {request.Holdings.Count}");
        if (request.ExcludedHoldingCount > 0)
        {
            // A number and nothing else: the model should know the picture is partial
            // without learning anything about what was withheld.
            sb.AppendLine($"Не аналізуються (виключені власником): {request.ExcludedHoldingCount}");
        }
        // Today's date belongs in the user turn, never in the system prompt — it would
        // invalidate the cached prefix on every call.
        sb.AppendLine($"Сьогодні: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine("</portfolio>");

        sb.AppendLine();
        sb.AppendLine("<holdings>");
        foreach (var holding in request.Holdings)
        {
            // The share is computed here rather than passed in: it is a presentation
            // detail of the prompt, and a zero total would otherwise need guarding twice.
            var share = request.TotalValue == 0m
                ? 0m
                : holding.ValueInDisplayCurrency / request.TotalValue * 100m;

            sb.AppendLine($"- {holding.Name}");
            sb.AppendLine($"  Тікер: {holding.Symbol ?? "—"}");
            sb.AppendLine($"  Рахунок: {holding.AccountName} ({holding.AccountType})");
            sb.AppendLine($"  Кількість: {holding.Quantity?.ToString() ?? "—"}");
            sb.AppendLine($"  Вартість: {holding.Value:N2} {holding.Currency}"
                + (holding.Currency == request.DisplayCurrency
                    ? string.Empty
                    : $" ({holding.ValueInDisplayCurrency:N2} {request.DisplayCurrency})"));
            sb.AppendLine($"  Частка портфеля: {share:N1}%");

            if (holding.Symbol is not null && quotes.TryGetValue(holding.Symbol, out var quote))
            {
                sb.AppendLine($"  Ринкова ціна: {quote.Price:N2}"
                    + (quote.PercentChange is null
                        ? string.Empty
                        : $" ({quote.PercentChange:+0.00;-0.00}% до попереднього закриття)"));
            }

            foreach (var fact in holding.LatestFacts)
            {
                sb.AppendLine($"  Раніше знайдено: [{fact.Category}] {fact.Claim}");
            }
        }
        sb.AppendLine("</holdings>");

        if (request.Previous is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"<previous_analysis generated_at=\"{request.Previous.GeneratedAt:yyyy-MM-dd}\">");
            foreach (var fact in request.Previous.Facts)
            {
                sb.AppendLine($"- [{fact.Category}] {fact.Claim}{(fact.SourceUrl is null ? "" : $" · {fact.SourceUrl}")}");
            }
            sb.AppendLine("</previous_analysis>");
        }

        sb.AppendLine();
        sb.AppendLine($"<task>{InsightPrompts.PortfolioTask}</task>");

        return sb.ToString();
    }
}
