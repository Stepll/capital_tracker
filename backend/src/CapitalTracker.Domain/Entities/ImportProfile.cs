namespace CapitalTracker.Domain.Entities;

/// <summary>
/// A mapping the owner has already worked out once, kept so the next statement from the
/// same bank or broker does not have to be explained again.
///
/// Recognition is by the file's own header rather than by a name the owner picks from a
/// list: two statements from the same source have the same column names, so the format
/// identifies itself and the profile applies without being chosen. The name exists for the
/// times that guess is wrong and for telling saved profiles apart.
/// </summary>
public class ImportProfile
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>The ImportMapping as JSON — a shape the Application layer owns, not the Domain.</summary>
    public required string Mapping { get; set; }

    /// <summary>
    /// The header row's column names, normalised and joined. Compared against the header
    /// found in an incoming file, which is why it is stored rather than recomputed: the
    /// profile has to outlive the file it was made from.
    /// </summary>
    public required string HeaderSignature { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
