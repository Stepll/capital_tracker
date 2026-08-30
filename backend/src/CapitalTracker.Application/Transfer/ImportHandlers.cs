using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transfer;

internal static class ImportGrid
{
    /// <summary>
    /// A foreign file is rewritten into our columns first; our own export is already there.
    /// Either way the parser downstream sees exactly one shape.
    /// </summary>
    public static List<string[]> Canonicalise(List<string[]> grid, ImportMapping? mapping) =>
        mapping is null ? grid : GridMapper.ToCanonical(grid, mapping);
}

public class PreviewImportCommandHandler(IApplicationDbContext db)
    : IRequestHandler<PreviewImportCommand, ImportPreviewDto>
{
    public async Task<ImportPreviewDto> Handle(PreviewImportCommand request, CancellationToken cancellationToken)
    {
        if (!SourceFile.TryReadGrid(request.File.Content, out var grid, out var problem))
            return new ImportPreviewDto(request.File.FileName, [new ImportProblem(0, problem!)], [], [], null, false);

        var plan = await ImportPlanner.PlanAsync(
            db, PortfolioCsvParser.ParseGrid(ImportGrid.Canonicalise(grid, request.Mapping)),
            request.Scope, request.TargetId, request.Options, cancellationToken);

        var hash = ImportDtoMapping.Sha256(request.File.Content);
        var sameFile = (await db.ImportBatches.Where(b => b.FileHash == hash && b.UndoneAt == null)
                .ToListAsync(cancellationToken))
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefault();

        return plan.ToPreview(request.File.FileName, sameFile);
    }
}

/// <summary>
/// Re-parses and re-plans the same file rather than trusting rows sent back from the
/// browser: the commit then does exactly what the preview computed, and nothing the client
/// could have edited in between.
/// </summary>
public class CommitImportCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CommitImportCommand, ImportResultDto>
{
    public async Task<ImportResultDto> Handle(CommitImportCommand request, CancellationToken cancellationToken)
    {
        if (!SourceFile.TryReadGrid(request.File.Content, out var grid, out var problem))
            throw new Common.DomainValidationException(problem!);

        var plan = await ImportPlanner.PlanAsync(
            db, PortfolioCsvParser.ParseGrid(ImportGrid.Canonicalise(grid, request.Mapping)),
            request.Scope, request.TargetId, request.Options, cancellationToken);

        var preview = plan.ToPreview(request.File.FileName, null);
        if (!preview.CanCommit)
            throw new Common.DomainValidationException("Цей файл не можна імпортувати — подивіться зауваження в прев'ю.");

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Scope = request.Scope,
            TargetId = request.TargetId,
            FileName = request.File.FileName,
            FileHash = ImportDtoMapping.Sha256(request.File.Content),
        };
        db.ImportBatches.Add(batch);

        var createdAccounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

        foreach (var planned in plan.Holdings)
        {
            var account = planned.ExistingAccount;
            if (account is null && !createdAccounts.TryGetValue(planned.AccountName, out account))
            {
                account = new Account
                {
                    Id = Guid.NewGuid(),
                    Name = planned.AccountName,
                    Type = planned.AccountType,
                    Currency = planned.Currency,
                    ImportBatchId = batch.Id,
                };
                db.Accounts.Add(account);
                createdAccounts[planned.AccountName] = account;
                batch.AccountsCreated++;
            }

            var holding = planned.Existing;
            if (holding is null)
            {
                holding = new Holding
                {
                    // The id the planner already wrote onto every row it built for this
                    // holding, so nothing has to be rewired here.
                    Id = planned.HoldingId,
                    AccountId = account.Id,
                    Name = planned.HoldingName,
                    Symbol = planned.Symbol,
                    ImportBatchId = batch.Id,
                };
                db.Holdings.Add(holding);
                batch.HoldingsCreated++;
            }

            // Rows written to a soft-deleted holding would land where nothing can see them.
            if (planned.RevivesHolding)
                holding.DeletedAt = null;

            if (planned.OpeningPositionToRemove is not null)
                db.Transactions.Remove(planned.OpeningPositionToRemove);

            foreach (var transaction in planned.NewTransactions)
            {
                transaction.ImportBatchId = batch.Id;
                db.Transactions.Add(transaction);
                batch.TransactionsCreated++;
            }

            foreach (var valuation in planned.Valuations)
            {
                valuation.ImportBatchId = batch.Id;
                db.ValuationSnapshots.Add(valuation);
                batch.ValuationsWritten++;
            }

            // A Видалення row closes the holding out, the same soft delete the UI performs.
            if (planned.DeletedOn is not null)
                holding.DeletedAt = planned.DeletedOn.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportResultDto(batch.Id, preview);
    }
}

public class UndoImportCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UndoImportCommand, bool>
{
    public async Task<bool> Handle(UndoImportCommand request, CancellationToken cancellationToken)
    {
        var batch = await db.ImportBatches.SingleOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken);
        if (batch is null || batch.UndoneAt is not null)
            return false;

        // Exact by construction: the import only ever added rows — it never overwrote a
        // valuation that was already there — so removing what it added restores the state
        // before it ran.
        db.Transactions.RemoveRange(
            await db.Transactions.Where(t => t.ImportBatchId == batch.Id).ToListAsync(cancellationToken));
        db.ValuationSnapshots.RemoveRange(
            await db.ValuationSnapshots.Where(v => v.ImportBatchId == batch.Id).ToListAsync(cancellationToken));

        // Soft-deleted rather than removed, like every other holding in this app — by now
        // they hold nothing, and the global filter takes them out of every view.
        var deletedAt = DateTime.UtcNow;
        foreach (var holding in await db.Holdings.Where(h => h.ImportBatchId == batch.Id).ToListAsync(cancellationToken))
        {
            holding.DeletedAt = deletedAt;
        }

        foreach (var account in await db.Accounts.Where(a => a.ImportBatchId == batch.Id).ToListAsync(cancellationToken))
        {
            account.DeletedAt = deletedAt;
        }

        // Kept rather than deleted: "imported, then undone" is the honest history, and it
        // frees the file's hash so a corrected version can come back in.
        batch.UndoneAt = deletedAt;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class GetImportBatchesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetImportBatchesQuery, List<ImportBatchDto>>
{
    public async Task<List<ImportBatchDto>> Handle(GetImportBatchesQuery request, CancellationToken cancellationToken) =>
        (await db.ImportBatches.ToListAsync(cancellationToken))
        .OrderByDescending(b => b.CreatedAt)
        .Select(b => b.ToDto())
        .ToList();
}

public class InspectImportQueryHandler : IRequestHandler<InspectImportQuery, FileInspectionDto>
{
    /// <summary>Enough of the file to recognise it by eye, and no more.</summary>
    private const int PreviewRows = 25;

    /// <summary>
    /// Above this a column is data, not a category — there is nothing to map value by value.
    /// </summary>
    private const int CategoryLimit = 12;

    public Task<FileInspectionDto> Handle(InspectImportQuery request, CancellationToken cancellationToken)
    {
        if (!SourceFile.TryReadGrid(request.File.Content, out var grid, out var problem))
            return Task.FromResult(new FileInspectionDto(request.File.FileName, [], 0, [], [], false, problem));

        if (SourceFile.LooksCanonical(grid))
        {
            return Task.FromResult(new FileInspectionDto(
                request.File.FileName, [.. grid.Take(PreviewRows)], 0, [], [], true, null));
        }

        var suggestion = GridMapper.Suggest(grid);
        var width = grid.Count == 0 ? 0 : grid.Max(r => r.Length);

        // Computed over the whole file, not just the rows shown: the value that decides the
        // direction of a row may not appear until line 300.
        var distinct = new Dictionary<int, List<string>>();
        for (var column = 0; column < width; column++)
        {
            var values = grid
                .Skip(suggestion.HeaderRow + 1)
                .Where(r => column < r.Length && !string.IsNullOrWhiteSpace(r[column]))
                .Select(r => r[column].Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(CategoryLimit + 1)
                .ToList();

            if (values.Count is > 0 and <= CategoryLimit)
                distinct[column] = values;
        }

        return Task.FromResult(new FileInspectionDto(
            request.File.FileName,
            [.. grid.Take(PreviewRows)],
            suggestion.HeaderRow,
            suggestion.Columns,
            distinct,
            false,
            null));
    }
}
