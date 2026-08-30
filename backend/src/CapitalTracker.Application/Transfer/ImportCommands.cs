using System.Security.Cryptography;
using System.Text;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transfer;

public record ImportPreviewHoldingDto(
    string Name,
    string? Symbol,
    string AccountName,
    bool IsNewHolding,
    bool IsNewAccount,
    string Currency,
    decimal? QuantityBefore,
    decimal? QuantityAfter,
    decimal ValueBefore,
    decimal ValueAfter,
    int NewTransactions,
    int NewValuations,
    // Rows already present, which the import leaves alone — the normal outcome of
    // re-importing a statement that overlaps one already loaded.
    int SkippedRows,
    bool ReplacesOpeningPosition,
    bool HasOpeningPosition,
    bool RevivesHolding,
    bool WouldGoNegative,
    DateOnly? DeletedOn);

public record ImportBatchDto(
    Guid Id,
    DateTime CreatedAt,
    TransferScope Scope,
    string FileName,
    int AccountsCreated,
    int HoldingsCreated,
    int TransactionsCreated,
    int ValuationsWritten,
    DateTime? UndoneAt);

public record ImportPreviewDto(
    string FileName,
    List<ImportProblem> Problems,
    List<ImportPreviewHoldingDto> Holdings,
    List<string> AccountsToCreate,
    // Set when a file with these exact bytes has already been imported and not undone.
    ImportBatchDto? SameFileImportedBefore,
    bool CanCommit);

public record ImportFile(string FileName, byte[] Content);

/// <summary>
/// A first look at an unknown file: the top of the grid as it actually is, plus a guess at
/// which row is the header and which column is which. Nothing is imported from this — it
/// exists so the owner has something to correct rather than a blank form to fill in.
/// </summary>
public record FileInspectionDto(
    string FileName,
    List<string[]> Rows,
    int HeaderRow,
    Dictionary<string, int> Columns,
    // Columns with few enough distinct values to be a category — where the direction of a
    // row usually hides ("кредит"/"дебет", BUY/SELL).
    Dictionary<int, List<string>> DistinctValues,
    // The running balance column, when the file has one — a bank statement is usually worth
    // reading as "what the account was worth each day" rather than as a hundred payments.
    int? BalanceColumn,
    bool LooksCanonical,
    string? Problem);

public record InspectImportQuery(ImportFile File) : IRequest<FileInspectionDto>;

public record PreviewImportCommand(
    ImportFile File,
    TransferScope Scope,
    Guid? TargetId,
    ImportOptions Options,
    // Null when the file is already in our format — see SourceFile.LooksCanonical.
    ImportMapping? Mapping = null) : IRequest<ImportPreviewDto>;

public record ImportResultDto(Guid BatchId, ImportPreviewDto Preview);

public record CommitImportCommand(
    ImportFile File,
    TransferScope Scope,
    Guid? TargetId,
    ImportOptions Options,
    ImportMapping? Mapping = null) : IRequest<ImportResultDto>;

public record UndoImportCommand(Guid BatchId) : IRequest<bool>;

public record GetImportBatchesQuery : IRequest<List<ImportBatchDto>>;

internal static class ImportDtoMapping
{
    public static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    /// <summary>
    /// UTF-8 only for now, which is what this app's own exports are. Anything else is
    /// reported as an encoding problem rather than silently read as mojibake — Ukrainian
    /// bank exports in windows-1251 arrive with the column mapper, not before it.
    /// </summary>
    public static bool TryDecode(byte[] content, out string text)
    {
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(content);
            return true;
        }
        catch (ArgumentException)
        {
            text = "";
            return false;
        }
    }

    public static ImportBatchDto ToDto(this ImportBatch batch) =>
        new(batch.Id, batch.CreatedAt, batch.Scope, batch.FileName,
            batch.AccountsCreated, batch.HoldingsCreated, batch.TransactionsCreated,
            batch.ValuationsWritten, batch.UndoneAt);

    public static ImportPreviewDto ToPreview(this ImportPlan plan, string fileName, ImportBatch? sameFile)
    {
        var holdings = plan.Holdings
            .Select(h => new ImportPreviewHoldingDto(
                h.HoldingName, h.Symbol, h.AccountName,
                h.Existing is null, h.ExistingAccount is null, h.Currency,
                h.QuantityBefore, h.QuantityAfter, h.ValueBefore, h.ValueAfter,
                h.NewTransactions.Count, h.Valuations.Count, h.DuplicateRows,
                h.OpeningPositionToRemove is not null, h.HasOpeningPosition, h.RevivesHolding,
                h.WouldGoNegative, h.DeletedOn))
            .OrderBy(h => h.AccountName)
            .ThenBy(h => h.Name)
            .ToList();

        return new ImportPreviewDto(
            fileName,
            plan.Problems.OrderBy(p => p.Line).ToList(),
            holdings,
            [.. plan.AccountsToCreate],
            sameFile?.ToDto(),
            // Unreadable rows don't block — they are simply left out, and listed. A position
            // that would go negative does, because the commit would refuse it anyway.
            holdings.Count > 0 && !holdings.Any(h => h.WouldGoNegative));
    }
}
