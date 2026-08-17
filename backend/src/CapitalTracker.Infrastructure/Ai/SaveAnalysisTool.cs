using System.Text.Json;

namespace CapitalTracker.Infrastructure.Ai;

/// <summary>
/// Schema for the tool the model calls with its findings.
///
/// Structured output goes through a tool rather than output_config.format because that
/// format is documented as incompatible with citations, and web search results carry
/// them. A tool call also gives the stream an unambiguous "analysis is done" signal.
///
/// Written as literal JSON so it reads as the schema it is. Strict mode requires
/// additionalProperties:false and every property listed in required — which is why the
/// optional source fields are typed ["string","null"] rather than simply omitted.
/// </summary>
public static class SaveAnalysisTool
{
    public const string Name = "save_analysis";

    public const string Description =
        "Record the completed analysis of the asset. Call exactly once, after research.";

    private const string SchemaJson = """
        {
          "type": "object",
          "properties": {
            "summary": {
              "type": "string",
              "description": "Two or three sentences in Ukrainian, plain text, no markdown."
            },
            "facts": {
              "type": "array",
              "description": "Concrete findings. Fewer well-sourced facts beat many vague ones.",
              "items": {
                "type": "object",
                "properties": {
                  "claim": {
                    "type": "string",
                    "description": "One checkable statement in Ukrainian, plain text."
                  },
                  "category": {
                    "type": "string",
                    "enum": ["risk", "opportunity", "market-news", "legal", "financial", "reputation", "liquidity"]
                  },
                  "polarity": {
                    "type": "string",
                    "enum": ["positive", "negative", "neutral"]
                  },
                  "confidence": {
                    "type": "string",
                    "enum": ["high", "medium", "low"]
                  },
                  "isNew": {
                    "type": "boolean",
                    "description": "False if the previous analysis already reported this."
                  },
                  "sourceName": { "type": ["string", "null"] },
                  "sourceUrl": { "type": ["string", "null"] },
                  "sourceDate": {
                    "type": ["string", "null"],
                    "description": "Publication date as YYYY-MM-DD."
                  }
                },
                "required": ["claim", "category", "polarity", "confidence", "isNew", "sourceName", "sourceUrl", "sourceDate"],
                "additionalProperties": false
              }
            }
          },
          "required": ["summary", "facts"],
          "additionalProperties": false
        }
        """;

    public static IReadOnlyDictionary<string, JsonElement> Schema { get; } =
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(SchemaJson)!;
}
