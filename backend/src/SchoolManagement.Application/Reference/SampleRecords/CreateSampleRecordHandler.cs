using System.Globalization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Reference;

namespace SchoolManagement.Application.Reference.SampleRecords;

/// <summary>
/// Handles <see cref="CreateSampleRecordCommand"/>.
/// </summary>
/// <remarks>
/// <para>
/// NOTE WHAT IS ABSENT: there is no <c>SaveChangesAsync</c> call and no transaction management. The
/// unit-of-work behaviour opens a transaction, and commits only if this handler returns a SUCCESS.
/// Returning a failure rolls everything back, so the early return below cannot leave a partial write
/// behind — and no handler has to remember that.
/// </para>
/// <para>
/// Also absent: any check that Label is non-empty or short enough. The pipeline already guaranteed
/// that. Re-checking here would be dead code implying the guarantee is unreliable.
/// </para>
/// </remarks>
internal sealed class CreateSampleRecordHandler(ISampleRecordRepository repository)
    : IRequestHandler<CreateSampleRecordCommand, Result<CreateSampleRecordResponse>>
{
    /// <inheritdoc />
    public async Task<Result<CreateSampleRecordResponse>> HandleAsync(
        CreateSampleRecordCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await repository.LabelExistsAsync(request.Label, cancellationToken))
        {
            // An expected outcome, so a Result — not an exception. Becomes HTTP 409 through the
            // central ErrorType mapping, with no status code named anywhere in this layer.
            return Result.Failure<CreateSampleRecordResponse>(Error.Conflict(
                "sample_record.label_taken",
                "A record with that label already exists."));
        }

        // Version 7 GUIDs are time-ordered, so inserts stay at the right-hand edge of the primary
        // key index instead of scattering random writes across it as v4 does.
        var creation = SampleRecord.Create(Guid.CreateVersion7(), request.Label, request.Note);

        if (creation.IsFailure)
        {
            return Result.Failure<CreateSampleRecordResponse>(creation.Error);
        }

        var record = creation.Value;
        await repository.AddAsync(record, cancellationToken);

        return Result.Success(new CreateSampleRecordResponse(
            record.Id.ToString("D", CultureInfo.InvariantCulture)));
    }
}
