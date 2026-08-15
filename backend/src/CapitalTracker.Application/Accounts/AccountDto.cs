using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Accounts;

public record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    DateTime CreatedAt);
