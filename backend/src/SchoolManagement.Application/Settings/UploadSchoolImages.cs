using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>POST /api/v1/settings/identity/logo</c> (spec 6.2.12, 9.6). <see cref="FileBytes"/> is the
/// multipart file's raw bytes, already read out of the request by the endpoint —
/// <c>ISchoolImageProcessor.ProcessLogo</c> does the real validation (magic bytes, size, minimum
/// dimensions) inside the handler. <see cref="ReadOnlyMemory{T}"/> rather than <c>byte[]</c> so this
/// public record does not hand every caller a directly mutable array (CA1819), matching
/// <c>SchoolImageRendition.Bytes</c>'s own reason.
/// </summary>
public sealed record UploadSchoolLogoCommand(ReadOnlyMemory<byte> FileBytes) : ICommand<Result<SchoolImageDto>>;

/// <summary>
/// Validates <see cref="UploadSchoolLogoCommand"/>. The processor owns every real rule; this only
/// guards against an empty body reaching it.
/// </summary>
internal sealed class UploadSchoolLogoCommandValidator : AbstractValidator<UploadSchoolLogoCommand>
{
    public UploadSchoolLogoCommandValidator() => RuleFor(command => command.FileBytes.Length).GreaterThan(0);
}

/// <summary>
/// <c>POST /api/v1/settings/identity/signature</c> (spec 6.2.12, 9.6). See
/// <see cref="UploadSchoolLogoCommand"/>'s remarks.
/// </summary>
public sealed record UploadSchoolSignatureCommand(ReadOnlyMemory<byte> FileBytes) : ICommand<Result<SchoolImageDto>>;

/// <summary>Validates <see cref="UploadSchoolSignatureCommand"/>. See <see cref="UploadSchoolLogoCommandValidator"/>'s remarks.</summary>
internal sealed class UploadSchoolSignatureCommandValidator : AbstractValidator<UploadSchoolSignatureCommand>
{
    public UploadSchoolSignatureCommandValidator() => RuleFor(command => command.FileBytes.Length).GreaterThan(0);
}
