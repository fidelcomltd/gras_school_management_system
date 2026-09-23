using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Pupils.Import;

/// <summary>Bulk import's size limits (spec 6.5.13; human ruling 2026-09-23).</summary>
public static class PupilImportLimits
{
    /// <summary>The synchronous limit; the background job for larger files is deferred.</summary>
    public const int MaxRows = 1000;

    /// <summary>Far above a 1000-row register, which is well under 1 MB.</summary>
    public const int MaxFileBytes = 5 * 1024 * 1024;
}

/// <summary>An XLSX file to download.</summary>
/// <param name="FileName">The suggested name.</param>
/// <param name="Content">The workbook.</param>
public sealed record SpreadsheetFile(string FileName, ReadOnlyMemory<byte> Content);

/// <summary><c>GET /api/v1/pupils/import/template</c> (spec 6.5.13): the header row plus the accepted-values sheets.</summary>
public sealed record GetPupilImportTemplateQuery : IQuery<Result<SpreadsheetFile>>;

/// <summary>Nothing to check.</summary>
internal sealed class GetPupilImportTemplateQueryValidator : AbstractValidator<GetPupilImportTemplateQuery>;

/// <summary><c>POST /api/v1/pupils/import/validate</c> (spec 6.5.13): the report, writing nothing.</summary>
/// <param name="File">The uploaded workbook's bytes.</param>
public sealed record ValidatePupilImportQuery(ReadOnlyMemory<byte> File) : IQuery<Result<PupilImportReportDto>>;

/// <summary>An empty body never reaches the reader; the reader owns every real rule.</summary>
internal sealed class ValidatePupilImportQueryValidator : AbstractValidator<ValidatePupilImportQuery>
{
    public ValidatePupilImportQueryValidator() =>
        RuleFor(query => query.File.Length).GreaterThan(0).LessThanOrEqualTo(PupilImportLimits.MaxFileBytes);
}

/// <summary>
/// <c>POST /api/v1/pupils/import/commit</c> (spec 6.5.13): the SAME file again, re-validated statelessly, then imported
/// all or nothing.
/// </summary>
/// <param name="File">The workbook's bytes.</param>
/// <param name="FileSha256">The report's <c>fileSha256</c>, proving the decisions were made against this file.</param>
/// <param name="SkipRows">Sheet rows matching the register to leave out.</param>
/// <param name="CreateRows">Sheet rows matching the register to import anyway.</param>
/// <param name="OverrideCapacity">Confirms importing past an arm's capacity; needs <c>arm.capacity.override</c>.</param>
public sealed record CommitPupilImportCommand(
    ReadOnlyMemory<byte> File, string FileSha256, IReadOnlyList<int> SkipRows, IReadOnlyList<int> CreateRows, bool OverrideCapacity)
    : ICommand<Result<PupilImportResultDto>>;

/// <summary>Structural checks; the processor and handler own the rest.</summary>
internal sealed class CommitPupilImportCommandValidator : AbstractValidator<CommitPupilImportCommand>
{
    public CommitPupilImportCommandValidator()
    {
        RuleFor(command => command.File.Length).GreaterThan(0).LessThanOrEqualTo(PupilImportLimits.MaxFileBytes);
        RuleFor(command => command.FileSha256).NotEmpty().Length(64).Matches("^[0-9a-fA-F]{64}$");
        RuleForEach(command => command.SkipRows).GreaterThan(1);
        RuleForEach(command => command.CreateRows).GreaterThan(1);
    }
}
