using SchoolManagement.Application.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="CopySubjectMappingsCommandValidator"/> — the source/destination distinctness
/// rule that produces the 422 on a same-term copy request (TASK-0070 dispatch 4, review gap 2).
/// This is a FluentValidation rule, not a <see cref="CopySubjectMappingsHandler"/>-level check: the
/// pipeline's <c>ValidationBehavior</c> short-circuits before the handler ever runs and reports it
/// as the generic <c>request.validation_failed</c> (<c>SchoolManagement.Domain.Common.ValidationError</c>),
/// keyed to <see cref="CopySubjectMappingsCommand.DestinationTermId"/> — there is no bespoke
/// <c>subject_mapping.copy_same_term</c> error code in source. This pins the actual mechanism.
/// </summary>
public sealed class CopySubjectMappingsCommandValidatorTests
{
    private readonly CopySubjectMappingsCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenSourceAndDestinationAreTheSameTerm_IsRejected()
    {
        var termId = Guid.CreateVersion7().ToString();

        var result = await _validator.ValidateAsync(
            new CopySubjectMappingsCommand(termId, termId, DryRun: false), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure =>
            failure.PropertyName == nameof(CopySubjectMappingsCommand.DestinationTermId) &&
            failure.ErrorMessage == "SourceTermId and DestinationTermId must be different.");
    }

    // Positive control for the rule above: two DIFFERENT terms must not trip it, or a validator bug
    // rejecting every copy regardless of the ids would still pass the negative test.
    [Fact]
    public async Task Validate_WhenSourceAndDestinationAreDifferentTerms_AcceptsTheCommand()
    {
        var result = await _validator.ValidateAsync(
            new CopySubjectMappingsCommand(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), DryRun: false),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }
}
