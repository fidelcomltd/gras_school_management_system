using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One publication's configuration snapshot (spec 6.7.3, 6.7.9). Append-only: a republication after a withdrawal
/// writes a new row beside the first, so what each revision looked like survives. <see cref="ResultSet.ConfigSnapshotJson"/>
/// mirrors the current one.
/// </summary>
public sealed class ResultSetSnapshot : Entity<Guid>
{
    private ResultSetSnapshot(Guid id, Guid resultSetId, int revisionNumber, string snapshotJson, Guid? configVersionId, DateTimeOffset publishedAtUtc)
        : base(id)
    {
        ResultSetId = resultSetId;
        RevisionNumber = revisionNumber;
        SnapshotJson = snapshotJson;
        ConfigVersionId = configVersionId;
        PublishedAtUtc = publishedAtUtc;
    }

    // EF Core materialisation constructor.
    private ResultSetSnapshot()
        : base()
    {
        SnapshotJson = string.Empty;
    }

    /// <summary>The published result set.</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>1 for the first publication, 2 after one withdrawal, and so on. Unique per result set.</summary>
    public int RevisionNumber { get; private set; }

    /// <summary>Raw JSON (jsonb), exactly as written at publication. Never rewritten.</summary>
    public string SnapshotJson { get; private set; }

    /// <summary>The latest config version at publication.</summary>
    public Guid? ConfigVersionId { get; private set; }

    /// <summary>When this revision was published; the revision notice prints it.</summary>
    public DateTimeOffset PublishedAtUtc { get; private set; }

    /// <summary>Records the snapshot a <see cref="ResultSet.Publish"/> call just wrote.</summary>
    public static ResultSetSnapshot For(ResultSet resultSet)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        return new ResultSetSnapshot(
            Guid.CreateVersion7(),
            resultSet.Id,
            resultSet.RevisionNumber,
            resultSet.ConfigSnapshotJson ?? throw new InvalidOperationException("The result set has not been published."),
            resultSet.ConfigVersionId,
            resultSet.PublishedAtUtc ?? throw new InvalidOperationException("The result set has not been published."));
    }
}
