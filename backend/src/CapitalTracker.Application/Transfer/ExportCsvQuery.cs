using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transfer;

public enum ExportScope
{
    Portfolio,
    Account,
    Holding,
}

public record CsvFileDto(string FileName, string Content);

/// <summary>
/// The whole portfolio, one account, or one holding — the same rows either way, only the
/// set of holdings differs. <c>TargetId</c> is null for the portfolio scope.
/// </summary>
public record ExportCsvQuery(ExportScope Scope, Guid? TargetId = null) : IRequest<CsvFileDto?>;

public class ExportCsvQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ExportCsvQuery, CsvFileDto?>
{
    public async Task<CsvFileDto?> Handle(ExportCsvQuery request, CancellationToken cancellationToken)
    {
        // Past the soft-delete filters throughout: a sold asset is part of what happened,
        // and an export that dropped it would restore a portfolio whose capital history no
        // longer matches the original. Its DeletedAt comes back as a Видалення row.
        var holdingsQuery = db.Holdings.IgnoreQueryFilters();

        holdingsQuery = request.Scope switch
        {
            ExportScope.Account => holdingsQuery.Where(h => h.AccountId == request.TargetId),
            ExportScope.Holding => holdingsQuery.Where(h => h.Id == request.TargetId),
            _ => holdingsQuery,
        };

        var holdings = await holdingsQuery.ToListAsync(cancellationToken);

        // A target that doesn't exist is a 404, not an empty file — an empty CSV would look
        // like a successful backup of nothing.
        if (request.Scope != ExportScope.Portfolio && holdings.Count == 0)
            return null;

        var accountIds = holdings.Select(h => h.AccountId).Distinct().ToList();
        var accounts = await db.Accounts
            .IgnoreQueryFilters()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var holdingIds = holdings.Select(h => h.Id).ToList();
        var transactions = await db.Transactions
            .Where(t => holdingIds.Contains(t.HoldingId))
            .ToListAsync(cancellationToken);
        var snapshots = await db.ValuationSnapshots
            .Where(v => holdingIds.Contains(v.HoldingId))
            .ToListAsync(cancellationToken);

        var transactionsByHolding = transactions.ToLookup(t => t.HoldingId);
        var snapshotsByHolding = snapshots.ToLookup(v => v.HoldingId);

        var rows = holdings
            .Where(h => accounts.ContainsKey(h.AccountId))
            .OrderBy(h => accounts[h.AccountId].Name)
            .ThenBy(h => h.Name)
            .SelectMany(h => EventsOf(h, accounts[h.AccountId], transactionsByHolding[h.Id], snapshotsByHolding[h.Id]))
            .ToList();

        var name = request.Scope switch
        {
            ExportScope.Account => accounts.Values.First().Name,
            ExportScope.Holding => holdings[0].Name,
            _ => "портфель",
        };

        return new CsvFileDto(
            // Hyphens rather than spaced dashes: the ASCII fallback in Content-Disposition
            // turns everything non-Latin into underscores, and this keeps that readable too.
            $"capital-tracker-{name}-{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}.csv",
            PortfolioCsv.Write(rows));
    }

    /// <summary>
    /// One holding's events in the order they happened, so the file reads as a history
    /// rather than as two tables stapled together. Same-day ties put what the owner did
    /// before what the asset was then worth, and the deletion last.
    /// </summary>
    private static IEnumerable<PortfolioCsvRow> EventsOf(
        Holding holding,
        Account account,
        IEnumerable<Transaction> transactions,
        IEnumerable<ValuationSnapshot> snapshots)
    {
        PortfolioCsvRow Row(string @event, DateOnly date, decimal? quantity, decimal? unitPrice, decimal? amount, string? currency, string? notes) =>
            new(account.Name,
                PortfolioCsv.AccountTypeLabels[account.Type],
                account.Currency,
                holding.Name,
                holding.Symbol,
                @event,
                date,
                quantity,
                unitPrice,
                amount,
                currency,
                notes);

        var events = transactions
            .Select(t => (t.Date, Order: 0, Row: Row(
                PortfolioCsv.EventLabels[t.Type], t.Date, t.Quantity, t.UnitPrice,
                Math.Round(t.Quantity * t.UnitPrice, 2), t.Currency, t.Notes)))
            .Concat(snapshots.Select(v => (v.Date, Order: 1, Row: Row(
                PortfolioCsv.ValuationEvent, v.Date, null, null, v.Value, v.Currency, null))))
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Order)
            .Select(e => e.Row);

        foreach (var row in events)
        {
            yield return row;
        }

        if (holding.DeletedAt is not null)
        {
            yield return Row(
                PortfolioCsv.DeletionEvent,
                DateOnly.FromDateTime(holding.DeletedAt.Value),
                null, null, null, null, null);
        }
    }
}
