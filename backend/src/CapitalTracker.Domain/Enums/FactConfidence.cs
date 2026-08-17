namespace CapitalTracker.Domain.Enums;

/// <summary>
/// How much weight to put on a fact. Facts without a source are capped at
/// <see cref="Medium"/> by the prompt — assets with few attributes (real estate,
/// cash) legitimately produce low-confidence output rather than invented detail.
/// </summary>
public enum FactConfidence
{
    High,
    Medium,
    Low
}
