using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transfer;

/// <summary>
/// What the owner gets to decide once they have seen the preview. Defaults are the safe
/// reading of an overlapping statement: bring in what is new, touch nothing that already
/// matches, and change no position without being told to.
/// </summary>
public record ImportOptions(
    bool SkipDuplicateRows = true,
    bool ReplaceOpeningPositions = false,
    bool AddMissingOpeningPositions = false);

public record PlannedHolding(
    Guid HoldingId,
    string HoldingName,
    string? Symbol,
    string AccountName,
    Holding? Existing,
    Account? ExistingAccount,
    AccountType AccountType,
    string Currency,
    decimal? QuantityBefore,
    decimal? QuantityAfter,
    decimal ValueBefore,
    decimal ValueAfter,
    List<Transaction> NewTransactions,
    List<ValuationSnapshot> Valuations,
    Transaction? OpeningPositionToRemove,
    // Whether one exists at all, regardless of the toggle — the option to replace it can
    // only be offered on the strength of this, not on the result of having it switched on.
    bool HasOpeningPosition,
    // The planner matches past the soft-delete filter, because an export carries deleted
    // holdings and re-importing it must find them rather than build duplicates alongside.
    // The cost of that is this: rows written to a deleted holding would be invisible, so
    // anything the import adds brings it back — and a Видалення row in the same file puts
    // it away again afterwards.
    bool RevivesHolding,
    DateOnly? DeletedOn,
    int DuplicateRows,
    bool WouldGoNegative);

public record ImportPlan(
    List<ImportProblem> Problems,
    List<PlannedHolding> Holdings,
    IReadOnlyList<string> AccountsToCreate);

/// <summary>
/// Turns a parsed file into exactly what would happen to the database, without touching it.
/// Preview renders this; commit executes it. One implementation on purpose — a preview that
/// predicts something other than what the commit does is worse than no preview at all.
/// </summary>
public static class ImportPlanner
{
    public static async Task<ImportPlan> PlanAsync(
        IApplicationDbContext db,
        ParsedCsv parsed,
        TransferScope scope,
        Guid? targetId,
        ImportOptions options,
        CancellationToken cancellationToken)
    {
        var problems = new List<ImportProblem>(parsed.Problems);

        var accounts = await db.Accounts.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var holdings = await db.Holdings.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var transactions = await db.Transactions.ToListAsync(cancellationToken);
        var snapshots = await db.ValuationSnapshots.ToListAsync(cancellationToken);

        var transactionsByHolding = transactions.ToLookup(t => t.HoldingId);
        var snapshotsByHolding = snapshots.ToLookup(v => v.HoldingId);

        var scopeHolding = scope == TransferScope.Holding
            ? holdings.SingleOrDefault(h => h.Id == targetId)
            : null;
        var scopeAccount = scope switch
        {
            TransferScope.Account => accounts.SingleOrDefault(a => a.Id == targetId),
            TransferScope.Holding => accounts.SingleOrDefault(a => a.Id == scopeHolding?.AccountId),
            _ => null,
        };

        if (scope != TransferScope.Portfolio && scopeAccount is null)
        {
            problems.Add(new ImportProblem(0, "Ціль імпорту не знайдено."));
            return new ImportPlan(problems, [], []);
        }

        // Identity the file doesn't have to carry: at holding scope every row is about the
        // asset in the URL, at account scope every row is inside that account.
        var groups = parsed.Events
            .GroupBy(e => scope == TransferScope.Holding
                ? (Account: scopeAccount!.Name, Holding: scopeHolding!.Name, Symbol: scopeHolding.Symbol)
                : (Account: scope == TransferScope.Account ? scopeAccount!.Name : e.AccountName ?? "",
                   Holding: e.HoldingName ?? "",
                   Symbol: e.Symbol))
            .ToList();

        var planned = new List<PlannedHolding>();
        var accountsToCreate = new List<string>();

        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.Key.Account) || string.IsNullOrWhiteSpace(group.Key.Holding))
            {
                problems.Add(new ImportProblem(
                    group.First().Line,
                    "Не вказано рахунок або актив — на цьому рівні імпорту вони обов'язкові."));
                continue;
            }

            var account = scopeAccount
                ?? accounts.FirstOrDefault(a => a.Name.Equals(group.Key.Account, StringComparison.OrdinalIgnoreCase));

            if (account is null && !accountsToCreate.Contains(group.Key.Account))
                accountsToCreate.Add(group.Key.Account);

            var accountType = account?.Type
                ?? group.Select(e => e.AccountType).FirstOrDefault(t => t is not null)
                ?? AccountType.Other;
            var accountCurrency = account?.Currency
                ?? group.Select(e => e.AccountCurrency).FirstOrDefault(c => c is not null)
                ?? SupportedCurrencies.Base;

            // Matched on the ticker first — it is the identity a broker file actually
            // carries — and only then on the name.
            var existing = scopeHolding ?? holdings.FirstOrDefault(h =>
                h.AccountId == account?.Id
                && (!string.IsNullOrWhiteSpace(group.Key.Symbol)
                    ? string.Equals(h.Symbol, group.Key.Symbol, StringComparison.OrdinalIgnoreCase)
                    : h.Name.Equals(group.Key.Holding, StringComparison.OrdinalIgnoreCase)));

            planned.Add(Plan(
                group.Key.Holding,
                group.Key.Symbol,
                group.Key.Account,
                existing,
                account,
                accountType,
                accountCurrency,
                [.. group],
                existing is null ? [] : [.. transactionsByHolding[existing.Id]],
                existing is null ? [] : [.. snapshotsByHolding[existing.Id]],
                options,
                problems));
        }

        return new ImportPlan(problems, planned, accountsToCreate);
    }

    private static PlannedHolding Plan(
        string name,
        string? symbol,
        string accountName,
        Holding? existing,
        Account? account,
        AccountType accountType,
        string accountCurrency,
        List<ImportedEvent> events,
        List<Transaction> existingTransactions,
        List<ValuationSnapshot> existingSnapshots,
        ImportOptions options,
        List<ImportProblem> problems)
    {
        var denomination = existingSnapshots.Count > 0
            ? existingSnapshots.OrderByDescending(s => s.Date).First().Currency
            : accountCurrency;

        var holdingId = existing?.Id ?? Guid.NewGuid();
        var duplicates = 0;

        var incoming = new List<Transaction>();
        foreach (var e in events.Where(e => e.Kind == ImportedEventKind.Transaction))
        {
            var candidate = new Transaction
            {
                Id = Guid.NewGuid(),
                HoldingId = holdingId,
                Type = e.TransactionType!.Value,
                Date = e.Date,
                Quantity = e.Quantity ?? 1m,
                UnitPrice = e.UnitPrice ?? 0m,
                Currency = e.Currency ?? denomination,
                Notes = e.Notes,
            };

            // The same statement exported twice is the normal case, not the exception, so
            // a row already present is skipped rather than doubled.
            var alreadyThere = existingTransactions.Any(t =>
                t.Type == candidate.Type && t.Date == candidate.Date
                && t.Quantity == candidate.Quantity && t.UnitPrice == candidate.UnitPrice
                && t.Currency == candidate.Currency);

            if (alreadyThere && options.SkipDuplicateRows)
            {
                duplicates++;
                continue;
            }

            incoming.Add(candidate);
        }

        var valuedDates = existingSnapshots.Select(s => s.Date).ToHashSet();
        var incomingValuations = events
            .Where(e => e.Kind == ImportedEventKind.Valuation)
            .GroupBy(e => e.Date)
            // The unique index on (HoldingId, Date) has bitten production before: the last
            // row for a date wins here rather than blowing up at commit.
            .Select(g => g.Last())
            .ToList();

        // A date that already has a valuation is left exactly as it is. Import stays purely
        // additive for values, which is what makes undo exact: nothing it removes was
        // sitting on top of an older number. Correcting a value is the holding page's job.
        duplicates += incomingValuations.Count(e => valuedDates.Contains(e.Date));

        var valuations = incomingValuations
            .Where(e => !valuedDates.Contains(e.Date))
            .Select(e => new ValuationSnapshot
            {
                Id = Guid.NewGuid(),
                HoldingId = holdingId,
                Date = e.Date,
                Value = e.Amount!.Value,
                Currency = e.Currency ?? denomination,
                IsManual = true,
            })
            .ToList();

        // A holding created by the migration carries an opening position priced off a
        // valuation. Real history imported on top of it would count the same units twice.
        var openingPosition = existing is null
            ? null
            : existingTransactions.FirstOrDefault(t => t.Notes == Transaction.OpeningPositionNote);
        var replaceOpening = openingPosition is not null
            && options.ReplaceOpeningPositions
            && incoming.Any(t => HoldingPositions.Direction(t.Type) != 0);

        var quantityBefore = HoldingPositions.Of(existingTransactions);
        var after = existingTransactions
            .Where(t => !replaceOpening || t.Id != openingPosition!.Id)
            .Concat(incoming)
            .ToList();
        var quantityAfter = HoldingPositions.Of(after);

        // A statement covering only the last year opens with sells whose buys are older
        // than the file. Rather than failing at commit, the missing units can be opened.
        var wouldGoNegative = quantityAfter is < 0m;
        if (wouldGoNegative && options.AddMissingOpeningPositions)
        {
            var missing = -quantityAfter!.Value;
            var opening = new Transaction
            {
                Id = Guid.NewGuid(),
                HoldingId = holdingId,
                Type = TransactionType.Buy,
                Date = events.Min(e => e.Date),
                Quantity = missing,
                UnitPrice = 0m,
                Currency = denomination,
                Notes = Transaction.OpeningPositionNote,
            };
            incoming.Insert(0, opening);
            quantityAfter = 0m;
        }
        else if (wouldGoNegative)
        {
            problems.Add(new ImportProblem(
                events.Min(e => e.Line),
                $"«{name}»: після імпорту позиція стала б від'ємною ({quantityAfter:0.####} од.). "
                + "Увімкніть «добудувати початкову позицію» або долийте ранішу історію."));
        }

        var valueBefore = existingSnapshots.OrderByDescending(s => s.Date).FirstOrDefault()?.Value ?? 0m;
        var latestAfter = valuations
            .Select(v => (v.Date, v.Value))
            .Concat(existingSnapshots.Select(s => (s.Date, s.Value)))
            .OrderByDescending(x => x.Date)
            .FirstOrDefault();

        return new PlannedHolding(
            holdingId, name, symbol, accountName, existing, account, accountType, denomination,
            quantityBefore, quantityAfter, valueBefore, latestAfter.Value,
            incoming, valuations,
            replaceOpening ? openingPosition : null,
            openingPosition is not null,
            existing?.DeletedAt is not null && (incoming.Count > 0 || valuations.Count > 0),
            events.Where(e => e.Kind == ImportedEventKind.Deletion).Select(e => (DateOnly?)e.Date).LastOrDefault(),
            duplicates,
            wouldGoNegative && !options.AddMissingOpeningPositions);
    }
}
