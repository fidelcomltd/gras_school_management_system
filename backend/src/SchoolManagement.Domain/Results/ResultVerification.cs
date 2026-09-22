using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pins;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// The verification token printed on one pupil's sheet for one publication (spec 6.9.6). A fresh token per revision,
/// so a sheet from a withdrawn revision resolves to the revision notice, never to the new figures. Append-only.
/// Spec 6.9.6 (22 characters, groups of five) wins over Appendix C.7 (12, groups of four): newer and more specific.
/// </summary>
public sealed class ResultVerification : Entity<Guid>
{
    /// <summary>Characters in a token: about 109 bits from the 31-character pin alphabet, not guessable.</summary>
    public const int TokenLength = 22;

    private ResultVerification(Guid id, string token, Guid resultSetId, Guid pupilId, int revisionNumber, DateTimeOffset issuedAtUtc)
        : base(id)
    {
        Token = token;
        ResultSetId = resultSetId;
        PupilId = pupilId;
        RevisionNumber = revisionNumber;
        IssuedAtUtc = issuedAtUtc;
    }

    // EF Core materialisation constructor.
    private ResultVerification()
        : base()
    {
        Token = string.Empty;
    }

    /// <summary>Random, opaque, not derivable from the pupil. Unique.</summary>
    public string Token { get; private set; }

    /// <summary>The published result set.</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>The pupil the sheet belongs to.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The revision the sheet was printed from.</summary>
    public int RevisionNumber { get; private set; }

    /// <summary>The publication date: the sheet's date issued.</summary>
    public DateTimeOffset IssuedAtUtc { get; private set; }

    /// <summary>A new token for <paramref name="pupilId"/>'s sheet in the revision just published.</summary>
    public static ResultVerification Issue(Guid resultSetId, Guid pupilId, int revisionNumber, DateTimeOffset issuedAtUtc) =>
        new(Guid.CreateVersion7(), PinValue.Generate(TokenLength), resultSetId, pupilId, revisionNumber, issuedAtUtc);

    /// <summary>The token as printed: groups of five, e.g. <c>ABCDE FGHJK MNPQR STUVW XY</c>.</summary>
    public static string Format(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return string.Join(' ', token.Chunk(5).Select(chunk => new string(chunk)));
    }
}
