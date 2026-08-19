using System.Runtime.CompilerServices;
using System.Text;
using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;
using CapitalTracker.Infrastructure.MarketData;

namespace CapitalTracker.Infrastructure.Ai;

/// <summary>
/// Per-asset analysis: pre-fetch what Finnhub knows about the ticker, then hand the
/// prompt to <see cref="AnthropicAnalysisRunner"/>, which owns everything about the
/// model call itself.
/// </summary>
public class AnthropicHoldingAnalysisGenerator(AnthropicAnalysisRunner runner, FinnhubClient finnhub)
    : IHoldingAnalysisGenerator
{
    public async IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        HoldingAnalysisRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        MarketDataBlock? marketData = null;

        // Shared with the price job so the two can't drift on what counts as quotable.
        // Everything else — property, deposits, crypto — relies on web search alone.
        if (MarketPricing.CanQuote(request.Symbol, request.AccountType))
        {
            yield return AnalysisGenerationEvent.AtPhase(InsightPhase.MarketData);
            marketData = await finnhub.GetAsync(request.Symbol!, cancellationToken);
        }

        var parameters = runner.BuildParameters(InsightPrompts.System, BuildUserMessage(request, marketData));

        await foreach (var e in runner.RunAsync(parameters, request.Name, cancellationToken))
        {
            yield return e;
        }
    }

    private static string BuildUserMessage(HoldingAnalysisRequest request, MarketDataBlock? marketData)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<holding>");
        sb.AppendLine($"Назва: {request.Name}");
        sb.AppendLine($"Тип рахунку: {request.AccountType}");
        sb.AppendLine($"Рахунок: {request.AccountName}");
        sb.AppendLine($"Сектор: {request.SectorName ?? "—"}");
        sb.AppendLine($"Тікер: {request.Symbol ?? "—"}");
        sb.AppendLine($"Кількість: {request.Quantity?.ToString() ?? "—"}");
        sb.AppendLine($"Поточна оцінка: {request.CurrentValue:N2} {request.Currency}");
        // Today's date belongs here, never in the system prompt — it would invalidate the
        // cached prefix on every call, and the model needs it to judge what counts as recent.
        sb.AppendLine($"Сьогодні: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine("</holding>");

        if (request.Attributes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<attributes>");
            foreach (var (key, value) in request.Attributes)
            {
                sb.AppendLine($"{key}: {value}");
            }
            sb.AppendLine("</attributes>");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            sb.AppendLine();
            sb.AppendLine($"<notes>{request.Notes}</notes>");
        }

        if (marketData is not null)
        {
            sb.AppendLine();
            sb.AppendLine("<market_data>");
            sb.Append($"Ціна: {marketData.Price:N2} {request.Currency}");
            sb.AppendLine(marketData.PercentChange is null
                ? string.Empty
                : $" ({marketData.PercentChange:+0.00;-0.00}% до попереднього закриття)");

            if (marketData.News.Count > 0)
            {
                sb.AppendLine("Новини:");
                foreach (var item in marketData.News)
                {
                    sb.AppendLine($"- {item.Date:yyyy-MM-dd} · {item.Source} · {item.Headline} · {item.Url}");
                }
            }
            sb.AppendLine("</market_data>");
        }

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
        sb.AppendLine($"<task>{InsightPrompts.Task}</task>");

        return sb.ToString();
    }
}
