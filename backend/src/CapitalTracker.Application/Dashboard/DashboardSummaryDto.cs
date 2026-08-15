namespace CapitalTracker.Application.Dashboard;

public record AllocationItemDto(string Type, decimal Value);

public record NetWorthPointDto(DateOnly Date, decimal Value);

public record DashboardSummaryDto(
    decimal TotalNetWorth,
    string Currency,
    List<AllocationItemDto> AllocationByType,
    List<NetWorthPointDto> NetWorthHistory);
