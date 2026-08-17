namespace CapitalTracker.Domain.Enums;

/// <summary>
/// What an analysis fact is *about*. Orthogonal to <see cref="FactPolarity"/>,
/// which says whether it's good or bad news — a "legal" fact can be either.
/// Serialized by name into the Facts jsonb column, so renaming a member is a
/// data migration, not a refactor.
/// </summary>
public enum FactCategory
{
    Risk,
    Opportunity,
    MarketNews,
    Legal,
    Financial,
    Reputation,
    Liquidity
}
