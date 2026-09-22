using System.Text.Json;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Portal;

/// <summary>What a verification token says about the sheet it was printed on.</summary>
public enum ResultVerificationStatus
{
    /// <summary>No sheet carries this token.</summary>
    Unknown,

    /// <summary>The sheet's revision is the published one: the figures are shown.</summary>
    Current,

    /// <summary>A later revision replaced it.</summary>
    Revised,

    /// <summary>Withdrawn for correction and not yet republished.</summary>
    Withdrawn,
}

/// <summary>
/// The public verification page's content (spec 6.9.6). Deliberately little: initials, never the full name; no
/// subject marks; no guardian. The figures are present only for <see cref="ResultVerificationStatus.Current"/>.
/// Position is left out, because neither sheet prints one (§6.7.12 amendment) and the page checks a sheet.
/// </summary>
public sealed record ResultVerificationView(
    ResultVerificationStatus Status,
    string? SchoolName = null,
    string? Initials = null,
    string? RegistrationNumber = null,
    string? ArmName = null,
    string? TermName = null,
    string? SessionName = null,
    int? TotalObtained = null,
    int? TotalObtainable = null,
    decimal? Average = null,
    string? OverallGrade = null,
    DateTimeOffset? IssuedAt = null,
    DateTimeOffset? RevisedAt = null);

/// <summary>Spec 6.9.9 <c>GET /verify/{token}</c>. Public, no pin.</summary>
/// <param name="Token">As typed or scanned; spaces and hyphens are ignored.</param>
public sealed record VerifyResultQuery(string Token) : IQuery<Result<ResultVerificationView>>;

/// <summary>Bounded input.</summary>
internal sealed class VerifyResultQueryValidator : AbstractValidator<VerifyResultQuery>
{
    public VerifyResultQueryValidator() => RuleFor(query => query.Token).NotNull().MaximumLength(64);
}

/// <summary>Handles <see cref="VerifyResultQuery"/>.</summary>
internal sealed class VerifyResultHandler(IResultVerificationReader verifications) : IRequestHandler<VerifyResultQuery, Result<ResultVerificationView>>
{
    /// <inheritdoc />
    public async Task<Result<ResultVerificationView>> HandleAsync(VerifyResultQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = PinValue.Normalize(request.Token);
        if (token.Length != ResultVerification.TokenLength || !token.All(PinValue.Alphabet.Contains)
            || await verifications.FindAsync(token, cancellationToken).ConfigureAwait(false) is not { } record)
        {
            return Result.Success(new ResultVerificationView(ResultVerificationStatus.Unknown));
        }

        var status = record.CurrentState != ResultSetState.Published ? ResultVerificationStatus.Withdrawn
            : record.CurrentRevision != record.TokenRevision ? ResultVerificationStatus.Revised
            : ResultVerificationStatus.Current;
        var current = status == ResultVerificationStatus.Current;

        using var snapshot = JsonDocument.Parse(record.SnapshotJson);
        var root = snapshot.RootElement;
        return Result.Success(new ResultVerificationView(
            status,
            Read(root, "settings", "identity", "schoolName"),
            Initials(record.PupilGivenNames, record.PupilSurname),
            record.RegistrationNumber,
            Read(root, "arm", "displayName"),
            Read(root, "term", "name"),
            Read(root, "session", "name"),
            current ? record.TotalObtained : null,
            current ? record.TotalObtainable : null,
            current ? record.Average : null,
            current ? record.OverallGrade : null,
            record.IssuedAt,
            status == ResultVerificationStatus.Revised ? record.CurrentPublishedAt : null));
    }

    /// <summary>e.g. <c>C. A. O.</c> for Chidera Amaka Okafor.</summary>
    internal static string Initials(string givenNames, string surname) =>
        string.Join(' ', $"{givenNames} {surname}".Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(name => $"{char.ToUpperInvariant(name[0])}."));

    private static string? Read(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
