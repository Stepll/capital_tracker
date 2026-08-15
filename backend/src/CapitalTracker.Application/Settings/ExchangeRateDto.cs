namespace CapitalTracker.Application.Settings;

public record ExchangeRateDto(string Currency, decimal RateToUah, DateOnly Date);
