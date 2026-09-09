using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>POST /api/v1/arms/bulk</c> (spec 6.4.3, 6.4.9): "Create arms for session" — an administrator
/// opening a new session creates every level's rooms in one action instead of one form per level.
/// Labels continue from each level's highest existing label (spec 6.4.8, same rule as
/// <c>GET /arms/next-label</c>). Levels given zero arms are skipped. The whole run is one transaction;
/// <see cref="DryRun"/> returns the preview without writing anything.
/// </summary>
/// <param name="SessionId">Must reference an upcoming or active session.</param>
/// <param name="Levels">One entry per level to size. A level omitted from this list gets no arms.</param>
/// <param name="DryRun">When <see langword="true"/>, computes and returns the preview but adds nothing.</param>
public sealed record BulkCreateArmsCommand(
    string SessionId,
    IReadOnlyList<BulkCreateArmsLevelEntry> Levels,
    bool DryRun)
    : ICommand<Result<BulkCreateArmsResponse>>;

/// <summary>One level's row in a <see cref="BulkCreateArmsCommand"/> request.</summary>
/// <param name="LevelId">Must reference an active level.</param>
/// <param name="ArmCount">0 skips the level entirely (spec 6.4.3).</param>
/// <param name="Capacity"><see langword="null"/> defaults to <see cref="Arm.DefaultCapacity"/> for every new arm at this level.</param>
public sealed record BulkCreateArmsLevelEntry(string LevelId, int ArmCount, int? Capacity);

/// <summary>The response to <see cref="BulkCreateArmsCommand"/>.</summary>
/// <param name="Created">The arms created (or, for a <see cref="BulkCreateArmsCommand.DryRun"/>, previewed).</param>
public sealed record BulkCreateArmsResponse(IReadOnlyList<ArmDto> Created);

/// <summary>Structural checks only — level/session existence and state live in the handler.</summary>
internal sealed class BulkCreateArmsCommandValidator : AbstractValidator<BulkCreateArmsCommand>
{
    private const int MaxArmsPerLevel = 26;

    public BulkCreateArmsCommandValidator()
    {
        RuleFor(command => command.SessionId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SessionId must be a valid identifier.");

        RuleFor(command => command.Levels).NotEmpty();

        RuleForEach(command => command.Levels).ChildRules(level =>
        {
            level.RuleFor(entry => entry.LevelId)
                .NotEmpty()
                .Must(value => Guid.TryParse(value, out _))
                .WithMessage("LevelId must be a valid identifier.");

            level.RuleFor(entry => entry.ArmCount)
                .InclusiveBetween(0, MaxArmsPerLevel);

            level.RuleFor(entry => entry.Capacity)
                .InclusiveBetween(Arm.MinCapacity, Arm.MaxCapacity)
                .When(entry => entry.Capacity is not null);
        });
    }
}
