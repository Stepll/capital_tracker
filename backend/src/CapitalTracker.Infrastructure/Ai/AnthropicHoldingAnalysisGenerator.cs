using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Models.Messages;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;
using CapitalTracker.Domain.Enums;
using CapitalTracker.Infrastructure.MarketData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Infrastructure.Ai;

public class AnthropicHoldingAnalysisGenerator(
    AnthropicClient client,
    FinnhubClient finnhub,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicHoldingAnalysisGenerator> logger)
    : IHoldingAnalysisGenerator
{
    private const int DetailMaxLength = 140;

    public async IAsyncEnumerable<AnalysisGenerationEvent> GenerateAsync(
        HoldingAnalysisRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        MarketDataBlock? marketData = null;

        // Finnhub free tier identifies crypto by exchange-prefixed symbols (BINANCE:BTCUSDT)
        // that we cannot derive from a bare ticker, so only listed equities go through it.
        // Everything else — property, deposits, crypto — relies on web search alone.
        if (!string.IsNullOrWhiteSpace(request.Symbol) && request.AccountType == AccountType.Brokerage)
        {
            yield return AnalysisGenerationEvent.AtPhase(InsightPhase.MarketData);
            marketData = await finnhub.GetAsync(request.Symbol!, cancellationToken);
        }

        var parameters = BuildParameters(request, marketData);

        var blocks = new Dictionary<int, StreamedBlock>();
        string? stopReason = null;
        HoldingAnalysisResult? result = null;

        await foreach (var evt in client.Messages.CreateStreaming(parameters, cancellationToken))
        {
            foreach (var progress in Consume(evt, blocks, ref stopReason, ref result))
            {
                yield return progress;
            }

            if (result is not null)
            {
                break;
            }
        }

        // Safety classifiers decline with a normal 200; in a stream the verdict arrives on
        // message_delta rather than on any final object, so this is the only place to catch it.
        if (stopReason == "refusal")
        {
            logger.LogWarning("Anthropic declined to analyse holding {Name}.", request.Name);
            yield return AnalysisGenerationEvent.Failed(InsightErrorCode.Refusal);
            yield break;
        }

        if (result is null)
        {
            logger.LogWarning("Analysis of {Name} ended without a save_analysis call (stop reason: {StopReason}).",
                request.Name, stopReason ?? "none");
            yield return AnalysisGenerationEvent.Failed(InsightErrorCode.Upstream);
            yield break;
        }

        yield return AnalysisGenerationEvent.Completed(result);
    }

    private sealed class StreamedBlock
    {
        public string? ToolName { get; init; }
        public StringBuilder Json { get; } = new();
    }

    /// <summary>
    /// Turns one raw stream event into zero or more progress events, accumulating tool
    /// input as it goes. Streaming never hands over a materialised tool input: the JSON
    /// arrives as a run of partial fragments that only become parseable at block stop.
    /// </summary>
    private IEnumerable<AnalysisGenerationEvent> Consume(
        RawMessageStreamEvent evt,
        Dictionary<int, StreamedBlock> blocks,
        ref string? stopReason,
        ref HoldingAnalysisResult? result)
    {
        var events = new List<AnalysisGenerationEvent>();

        if (evt.TryPickContentBlockStart(out var start))
        {
            var block = start.ContentBlock;

            if (block.TryPickToolUse(out var toolUse))
            {
                blocks[(int)start.Index] = new StreamedBlock { ToolName = toolUse.Name };
                if (toolUse.Name == SaveAnalysisTool.Name)
                {
                    events.Add(AnalysisGenerationEvent.AtPhase(InsightPhase.Writing));
                }
            }
            else if (block.TryPickServerToolUse(out _))
            {
                blocks[(int)start.Index] = new StreamedBlock { ToolName = "server" };
                events.Add(AnalysisGenerationEvent.AtPhase(InsightPhase.Searching));
            }
            else if (block.TryPickThinking(out _))
            {
                events.Add(AnalysisGenerationEvent.AtPhase(InsightPhase.Thinking));
            }
        }
        else if (evt.TryPickContentBlockDelta(out var delta))
        {
            var index = (int)delta.Index;

            if (delta.Delta.TryPickInputJson(out var inputJson) && blocks.TryGetValue(index, out var target))
            {
                target.Json.Append(inputJson.PartialJson);
            }
            else if (delta.Delta.TryPickThinking(out var thinking))
            {
                events.Add(AnalysisGenerationEvent.AtPhase(InsightPhase.Thinking, Truncate(thinking.Thinking)));
            }
        }
        else if (evt.TryPickContentBlockStop(out var stop))
        {
            var index = (int)stop.Index;

            if (blocks.TryGetValue(index, out var finished))
            {
                var json = finished.Json.ToString();
                logger.LogDebug("Stream block {Index} ({Tool}) closed with {Length} chars of input: {Json}",
                    index, finished.ToolName, json.Length, Truncate(json, 300) ?? "<empty>");

                if (finished.ToolName == SaveAnalysisTool.Name)
                {
                    result = ParseResult(json);
                }
                else if (finished.ToolName == "server")
                {
                    // The server tool's accumulated input holds the query the model chose —
                    // the most informative thing we can show during a long search phase.
                    var query = ReadSearchQuery(json);
                    if (query is not null)
                    {
                        events.Add(AnalysisGenerationEvent.AtPhase(InsightPhase.Searching, query));
                    }
                }

                blocks.Remove(index);
            }
        }
        else if (evt.TryPickDelta(out var messageDelta))
        {
            // Raw(), not ToString(): StopReason is an ApiEnum wrapper, and ToString gives
            // the C# member name ("Refusal") where the wire value is "refusal". Comparing
            // against the wrong one compiles fine and silently never matches. Raw() also
            // survives stop reasons this SDK version predates, which Value() maps to -1.
            stopReason = messageDelta.Delta.StopReason?.Raw();
        }

        return events;
    }

    private HoldingAnalysisResult? ParseResult(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : null;
            if (string.IsNullOrWhiteSpace(summary))
            {
                return null;
            }

            // Occasionally the model serializes the tool call as its text-mode pseudo-XML
            // *inside* the summary argument — "…</summary><parameter name="facts">[…]" — so
            // facts never becomes a real key and the whole call collapses into one string.
            // Observed intermittently on the same code path that works fine most runs.
            // Failing here means nothing is persisted and the cooldown stays open, which is
            // far better than showing a wall of markup as the analysis.
            if (LooksLikeLeakedToolCall(summary!))
            {
                logger.LogWarning("save_analysis returned a text-mode tool call inside its summary argument; discarding.");
                return null;
            }

            var facts = new List<AnalysisFactDto>();
            if (root.TryGetProperty("facts", out var factsElement) && factsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in factsElement.EnumerateArray())
                {
                    var fact = ParseFact(element);
                    if (fact is not null)
                    {
                        facts.Add(fact);
                    }
                }
            }

            return new HoldingAnalysisResult(summary!, facts);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "save_analysis returned unparseable JSON.");
            return null;
        }
    }

    private static bool LooksLikeLeakedToolCall(string summary) =>
        summary.Contains("<parameter name=", StringComparison.OrdinalIgnoreCase)
        || summary.Contains("</summary>", StringComparison.OrdinalIgnoreCase)
        || summary.Contains("<invoke", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Drops a fact rather than throwing when a field is missing or unrecognised — one
    /// malformed entry should cost that entry, not the whole analysis the user paid for.
    /// </summary>
    private static AnalysisFactDto? ParseFact(JsonElement element)
    {
        var claim = element.TryGetProperty("claim", out var c) ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(claim))
        {
            return null;
        }

        if (!TryMapCategory(GetString(element, "category"), out var category))
        {
            return null;
        }

        return new AnalysisFactDto(
            claim!,
            category,
            GetString(element, "polarity") switch
            {
                "positive" => FactPolarity.Positive,
                "negative" => FactPolarity.Negative,
                _ => FactPolarity.Neutral,
            },
            GetString(element, "confidence") switch
            {
                "high" => FactConfidence.High,
                "low" => FactConfidence.Low,
                _ => FactConfidence.Medium,
            },
            element.TryGetProperty("isNew", out var isNew) && isNew.ValueKind == JsonValueKind.True,
            GetString(element, "sourceName"),
            GetString(element, "sourceUrl"),
            DateOnly.TryParse(GetString(element, "sourceDate"), out var date) ? date : null);
    }

    // The model-facing schema uses kebab-case; the API and the frontend use the enum
    // names. This is the one place the two vocabularies meet — keep it that way.
    private static bool TryMapCategory(string? value, out FactCategory category)
    {
        (category, var ok) = value switch
        {
            "risk" => (FactCategory.Risk, true),
            "opportunity" => (FactCategory.Opportunity, true),
            "market-news" => (FactCategory.MarketNews, true),
            "legal" => (FactCategory.Legal, true),
            "financial" => (FactCategory.Financial, true),
            "reputation" => (FactCategory.Reputation, true),
            "liquidity" => (FactCategory.Liquidity, true),
            _ => (default(FactCategory), false),
        };
        return ok;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// Pulls the human-readable search terms out of a server-tool call.
    ///
    /// Not as simple as reading a "query" field: web_search_20260209 does its dynamic
    /// filtering through code execution, so the tool input is a snippet of Python that
    /// calls web_search itself — the queries live inside string literals in that code.
    /// Anything that doesn't look like natural language (glue, separators, identifiers)
    /// is dropped, and a miss just means the phase shows without a detail line.
    /// </summary>
    private static string? ReadSearchQuery(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var code = GetString(document.RootElement, "code");
            if (code is null)
            {
                // Older/basic web search does hand over a plain query.
                return Truncate(GetString(document.RootElement, "query"));
            }

            var queries = Regex
                .Matches(code, "\"((?:[^\"\\\\]|\\\\.)*)\"")
                .Select(m => Regex.Unescape(m.Groups[1].Value))
                .Where(LooksLikeSearchTerms)
                .Distinct()
                .ToList();

            return queries.Count == 0 ? null : Truncate(string.Join(" · ", queries));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (RegexParseException)
        {
            return null;
        }
    }

    private static bool LooksLikeSearchTerms(string candidate) =>
        candidate.Length >= 8
        && candidate.Contains(' ')
        && candidate.Any(char.IsLetter)
        && !candidate.Contains('_')
        && !candidate.Contains("://", StringComparison.Ordinal);

    private static string? Truncate(string? text, int max = DetailMaxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }

    private MessageCreateParams BuildParameters(HoldingAnalysisRequest request, MarketDataBlock? marketData) => new()
    {
        Model = options.Value.Model,
        // Caps thinking and output together, so this needs headroom well beyond the
        // visible answer or the analysis truncates mid-JSON and parses to nothing.
        MaxTokens = options.Value.MaxTokens,
        System = new List<TextBlockParam>
        {
            new()
            {
                Text = InsightPrompts.System,
                // Worth little in production (5-minute cache TTL against a 12-hour
                // cooldown), but it makes prompt iteration in development much cheaper.
                CacheControl = new CacheControlEphemeral(),
            },
        },
        Thinking = new ThinkingConfigAdaptive { Display = Display.Summarized },
        // 'high' rather than 'xhigh': this is research and judgement, not agentic coding.
        OutputConfig = new OutputConfig { Effort = Effort.High },
        Tools =
        [
            new ToolUnion(new WebSearchTool20260209 { MaxUses = options.Value.MaxWebSearches }),
            new Tool
            {
                Name = SaveAnalysisTool.Name,
                Description = SaveAnalysisTool.Description,
                InputSchema = InputSchema.FromRawUnchecked(SaveAnalysisTool.Schema),
                Strict = true,
            },
        ],
        // No ToolChoice: forcing save_analysis would stop the model searching first.
        // No Temperature/TopP/TopK either — Opus 5 rejects them outright.
        Messages = [new() { Role = Role.User, Content = BuildUserMessage(request, marketData) }],
    };

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
