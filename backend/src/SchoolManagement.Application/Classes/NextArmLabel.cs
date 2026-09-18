using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>GET /api/v1/arms/next-label</c> (spec 6.4.3, 6.4.9): the suggested next unused label for a level
/// and session — A if the level has no arm this session, B if it has A, and so on.
/// </summary>
/// <param name="LevelId">The level to suggest for.</param>
/// <param name="SessionId">The session to suggest for.</param>
public sealed record NextArmLabelQuery(string LevelId, string SessionId) : IQuery<Result<NextArmLabelResponse>>;

/// <summary>The response to <see cref="NextArmLabelQuery"/>.</summary>
/// <param name="Label">The suggested next label. Pre-filled and editable — not reserved by asking.</param>
public sealed record NextArmLabelResponse(string Label);

/// <summary>Validates <see cref="NextArmLabelQuery"/>.</summary>
internal sealed class NextArmLabelQueryValidator : AbstractValidator<NextArmLabelQuery>
{
    public NextArmLabelQueryValidator()
    {
        RuleFor(query => query.LevelId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("LevelId must be a valid identifier.");

        RuleFor(query => query.SessionId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SessionId must be a valid identifier.");
    }
}
