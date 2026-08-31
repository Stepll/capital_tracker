using CapitalTracker.Application.Common;

namespace CapitalTracker.Application.Dashboard;

public record AllocationItemDto(string Type, decimal Value);

public record NetWorthPointDto(DateOnly Date, decimal Value);

/// <summary>
/// A holding whose value has gone out of date, with what it is currently counted as worth
/// — the point of the list is that this much of the total is no longer trustworthy.
/// </summary>
public record StaleValuationDto(
    Guid HoldingId,
    string Name,
    string AccountName,
    ValuationAgeDto ValuationAge,
    decimal ValueInDisplayCurrency);

public record DashboardSummaryDto(
    decimal TotalNetWorth,
    string Currency,
    List<AllocationItemDto> AllocationByType,
    List<NetWorthPointDto> NetWorthHistory,
    // The same breakdown a single asset shows, summed across the portfolio and answered in
    // the display currency — so a dollar purchase counts what it cost in hryvnia that day.
    InvestmentReturnDto Return,
    // Biggest first: the one distorting the total most is the one worth fixing first.
    List<StaleValuationDto> StaleValuations);
