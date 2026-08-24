using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Accounts;

public record AccountAllocationItemDto(Guid HoldingId, string Name, decimal Value);

public record AccountDetailDto(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    DateTime CreatedAt,
    // Computed server-side because holdings can be denominated differently from the
    // account; summing HoldingDto.CurrentValue on the client would add USD to UAH.
    decimal TotalValue,
    List<HoldingDto> Holdings,
    // What the account is made of, biggest first, every slice already converted into the
    // account's currency — the same reason the total is: a donut of raw values would be
    // drawing USD and UAH as if they were the same unit.
    List<AccountAllocationItemDto> AllocationByHolding,
    // This account's own value over time, on the same builder as the dashboard's line:
    // each point at the rate of its own date, deleted holdings counted up to their
    // deletion, today always the last point.
    List<ValuationPointDto> ValueHistory);
