using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Accounts;

public record AccountDetailDto(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    DateTime CreatedAt,
    // Computed server-side because holdings can be denominated differently from the
    // account; summing HoldingDto.CurrentValue on the client would add USD to UAH.
    decimal TotalValue,
    List<HoldingDto> Holdings);
