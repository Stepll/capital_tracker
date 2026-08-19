using System.Runtime.CompilerServices;
using System.Text;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;

namespace CapitalTracker.Infrastructure.Ai;

/// <summary>
/// Market research. No Finnhub pre-fetch here, unlike the other two generators: the
/// subject is rates, yields and policy rather than any ticker the app tracks, and all of
/// that comes from web search.
/// </summary>
public class AnthropicMarketAnalysisGenerator(AnthropicAnalysisRunner runner) : IMarketAnalysisGenerator
{
    public async IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        MarketAnalysisRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var parameters = runner.BuildParameters(InsightPrompts.MarketSystem, BuildUserMessage(request));
        var subject = request.Focus == MarketFocus.Ukraine ? "the Ukrainian market" : "global markets";

        await foreach (var e in runner.RunAsync(parameters, subject, cancellationToken))
        {
            yield return e;
        }
    }

    private static string BuildUserMessage(MarketAnalysisRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<focus>");
        sb.AppendLine(request.Focus == MarketFocus.Ukraine
            ? InsightPrompts.UkraineFocus
            : InsightPrompts.GlobalFocus);
        sb.AppendLine("</focus>");

        sb.AppendLine();
        sb.AppendLine("<context>");
        sb.AppendLine($"Валюта відображення: {request.DisplayCurrency}");
        // Today's date lives in the user turn — in the system prompt it would invalidate
        // the cached prefix on every call, and this scope needs it badly: a rate means
        // nothing without the date it was read on.
        sb.AppendLine($"Сьогодні: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine("</context>");

        sb.AppendLine();
        if (request.Holdings.Count == 0)
        {
            // Said out loud rather than left as an empty block, so the model reports the
            // market plainly instead of guessing at a portfolio it was never shown.
            sb.AppendLine("<current_holdings>Портфель порожній або недоступний для аналізу.</current_holdings>");
        }
        else
        {
            sb.AppendLine("<current_holdings>");
            sb.AppendLine($"Загальна вартість: {request.TotalValue:N2} {request.DisplayCurrency}");
            foreach (var holding in request.Holdings)
            {
                var share = request.TotalValue == 0m
                    ? 0m
                    : holding.ValueInDisplayCurrency / request.TotalValue * 100m;

                sb.AppendLine($"- {holding.Name} ({holding.AccountType}): "
                    + $"{holding.ValueInDisplayCurrency:N2} {request.DisplayCurrency}, {share:N1}%"
                    + (holding.Currency == request.DisplayCurrency
                        ? string.Empty
                        : $", номінал у {holding.Currency}"));
            }
            if (request.ExcludedHoldingCount > 0)
            {
                sb.AppendLine($"Не показані (виключені власником): {request.ExcludedHoldingCount}");
            }
            sb.AppendLine("</current_holdings>");
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
        sb.AppendLine($"<task>{InsightPrompts.MarketTask}</task>");

        return sb.ToString();
    }
}
