using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Accounts;

public record AccountDetailDto(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    DateTime CreatedAt,
    List<HoldingDto> Holdings);
