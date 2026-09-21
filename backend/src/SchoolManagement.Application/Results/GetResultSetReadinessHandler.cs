using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="GetResultSetReadinessQuery"/>.</summary>
internal sealed class GetResultSetReadinessHandler(
    IArmRepository arms,
    ITermRepository terms,
    IResultSetRepository resultSets,
    IResultSetReadinessEvaluator readinessEvaluator)
    : IRequestHandler<GetResultSetReadinessQuery, Result<ResultSetReadinessDto>>
{
    /// <inheritdoc />
    public async Task<Result<ResultSetReadinessDto>> HandleAsync(GetResultSetReadinessQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<ResultSetReadinessDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<ResultSetReadinessDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var resultSet = await resultSets.FindReadOnlyByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);

        var dto = await readinessEvaluator.EvaluateAsync(arm, term, resultSet, cancellationToken).ConfigureAwait(false);

        return Result.Success(dto);
    }
}
