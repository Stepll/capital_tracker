using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transfer;

public class PreviewImportCommandHandler(IApplicationDbContext db)
    : IRequestHandler<PreviewImportCommand, ImportPreviewDto>
{
    public async Task<ImportPreviewDto> Handle(PreviewImportCommand request, CancellationToken cancellationToken)
    {
        if (!ImportMapping.TryDecode(request.File.Content, out var text))
        {
            return new ImportPreviewDto(
                request.File.FileName,
                [new ImportProblem(0, "Файл не у кодуванні UTF-8. Збережіть його як CSV UTF-8 і спробуйте ще раз.")],
                [], [], null, false);
        }

        var plan = await ImportPlanner.PlanAsync(
            db, PortfolioCsvParser.Parse(text), request.Scope, request.TargetId, request.Options, cancellationToken);

        var hash = ImportMapping.Sha256(request.File.Content);
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
        if (!ImportMapping.TryDecode(request.File.Content, out var text))
            throw new Common.DomainValidationException("Файл не у кодуванні UTF-8.");

        var plan = await ImportPlanner.PlanAsync(
            db, PortfolioCsvParser.Parse(text), request.Scope, request.TargetId, request.Options, cancellationToken);

        var preview = plan.ToPreview(request.File.FileName, null);
        if (!preview.CanCommit)
            throw new Common.DomainValidationException("Цей файл не можна імпортувати — подивіться зауваження в прев'ю.");

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Scope = request.Scope,
            TargetId = request.TargetId,
            FileName = request.File.FileName,
            FileHash = ImportMapping.Sha256(request.File.Content),
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
