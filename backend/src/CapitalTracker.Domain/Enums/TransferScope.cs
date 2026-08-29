namespace CapitalTracker.Domain.Enums;

/// <summary>
/// How much of the portfolio a file covers. The same three levels serve export and import:
/// the rows are identical, only how much identity comes from the file rather than the URL
/// changes.
/// </summary>
public enum TransferScope
{
    Portfolio,
    Account,
    Holding,
}
