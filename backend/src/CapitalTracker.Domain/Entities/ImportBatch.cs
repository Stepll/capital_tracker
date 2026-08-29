using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Domain.Entities;

/// <summary>
/// One run of an import, and everything it brought in.
///
/// This exists because quantity is derived from transactions: importing the same statement
/// twice would silently double every position, with nothing on screen to show it happened.
/// A batch makes the whole run reversible, tells the same file apart on its second arrival,
/// and keeps the provenance of every row it created.
///
/// Undone batches are kept rather than deleted — "imported on the 27th, undone the same
/// day" is a more honest history than a run that vanishes, and it frees the file's hash so
/// a corrected version can come back in.
/// </summary>
public class ImportBatch
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TransferScope Scope { get; set; }

    /// <summary>The account or holding the import was aimed at; null for a whole portfolio.</summary>
    public Guid? TargetId { get; set; }

    public required string FileName { get; set; }

    /// <summary>SHA-256 of the uploaded bytes, so the same file is recognised on arrival.</summary>
    public required string FileHash { get; set; }

    public int AccountsCreated { get; set; }
    public int HoldingsCreated { get; set; }
    public int TransactionsCreated { get; set; }
    public int ValuationsWritten { get; set; }

    public DateTime? UndoneAt { get; set; }
}
