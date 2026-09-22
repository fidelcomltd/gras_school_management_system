using System.Globalization;
using System.Text.Json;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Results.Computation;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Results.Annual;

/// <summary><c>POST /api/v1/arms/{armId}/annual-results</c> (spec 6.7.10, 6.7.13). Rewrites the arm's annual rows.</summary>
/// <param name="ArmId">The arm the pupils ended the session in, from the route.</param>
public sealed record ComputeAnnualResultsCommand(Guid ArmId) : ICommand<Result<ComputeAnnualResultsResponse>>;

/// <summary>Route-only input.</summary>
internal sealed class ComputeAnnualResultsCommandValidator : AbstractValidator<ComputeAnnualResultsCommand>;

/// <summary>The 200 response.</summary>
/// <param name="ArmId">The arm computed.</param>
/// <param name="SessionId">Its session.</param>
/// <param name="ComputedAt">When this run happened.</param>
/// <param name="PupilCount">Annual rows written.</param>
/// <param name="RankedCount">Of those, ranked (two or more terms).</param>
/// <param name="ProposedPromoted">Rows proposing Promoted.</param>
/// <param name="ProposedRepeat">Rows proposing Repeat.</param>
public sealed record ComputeAnnualResultsResponse(
    string ArmId, string SessionId, DateTimeOffset ComputedAt, int PupilCount, int RankedCount, int ProposedPromoted, int ProposedRepeat);

/// <summary>Handles <see cref="ComputeAnnualResultsCommand"/>.</summary>
internal sealed class ComputeAnnualResultsHandler(
    IAnnualResultRepository annualResults,
    IResultRulesRepository resultRules,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<ComputeAnnualResultsCommand, Result<ComputeAnnualResultsResponse>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<Result<ComputeAnnualResultsResponse>> HandleAsync(ComputeAnnualResultsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await annualResults.LoadArmAsync(request.ArmId, cancellationToken).ConfigureAwait(false) is not { } arm)
        {
            return Result.Failure<ComputeAnnualResultsResponse>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        // 6.7.12: every term the arm was scored in must be published; Third Term always (it is what the annual reads).
        var armName = ArmDisplayName.Compose(arm.LevelName, arm.ArmLabel);
        foreach (var term in arm.Terms.OrderBy(term => term.Ordinal))
        {
            var unscoredEarlierTerm = term.ArmResultSetState is null && term.Ordinal < AnnualComputation.TermsInSession;
            if (!unscoredEarlierTerm && term.ArmResultSetState != ResultSetState.Published)
            {
                return Result.Failure<ComputeAnnualResultsResponse>(Error.Conflict(
                    "annual_result.term_not_published",
                    $"{term.TermName} results for {armName} are not published. Publish all three terms before computing annual results."));
            }
        }

        var pupils = await annualResults.LoadPupilsAsync(arm.ArmId, arm.SessionId, cancellationToken).ConfigureAwait(false);
        if (pupils.Count == 0)
        {
            return Result.Failure<ComputeAnnualResultsResponse>(Error.Conflict(
                "annual_result.no_pupils", "This class has no Third Term results. Nothing to compute."));
        }

        var rules = await resultRules.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var computed = AnnualComputation.Compute(new AnnualInput(
            pupils,
            Bands(arm.ThirdTermSnapshotJson),
            new AnnualRules(rules.AnnualMethod, rules.WeightFirst, rules.WeightSecond, rules.WeightThird, rules.PassMark, rules.PromotionThreshold, rules.RequireCorePass, rules.CoreSubjectIds)));
        if (computed.IsFailure)
        {
            return Result.Failure<ComputeAnnualResultsResponse>(computed.Error);
        }

        var now = timeProvider.GetUtcNow();
        var outputs = computed.Value;
        var ranked = outputs.Count(output => output.Position is not null);
        var rows = outputs.Select(output => AnnualResult.Create(
                arm.SessionId,
                arm.ArmId,
                output.PupilId,
                output.TermsCounted,
                output.TermAverages,
                output.TermTotals,
                output.GrandTotal,
                output.CumulativeAverage,
                output.CumulativeGrade,
                output.CumulativeRemark,
                output.Position,
                output.PositionTied,
                ranked,
                JsonSerializer.Serialize(output.Subjects, Json),
                output.ProposedOutcome,
                now))
            .ToList();
        await annualResults.ReplaceAsync(arm.SessionId, pupils.Select(pupil => pupil.PupilId).ToList(), rows, cancellationToken).ConfigureAwait(false);

        var response = new ComputeAnnualResultsResponse(
            arm.ArmId.ToString("D", CultureInfo.InvariantCulture),
            arm.SessionId.ToString("D", CultureInfo.InvariantCulture),
            now,
            rows.Count,
            ranked,
            rows.Count(row => row.ProposedOutcome == PromotionOutcome.Promoted),
            rows.Count(row => row.ProposedOutcome == PromotionOutcome.Repeat));
        await auditSink.RecordAsync(
            Privileges.Results.AnnualCompute,
            "arm",
            response.ArmId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sessionId"] = response.SessionId,
                ["pupilCount"] = response.PupilCount,
                ["rankedCount"] = response.RankedCount,
                ["annualMethod"] = rules.AnnualMethod.ToString(),
            },
            currentUser.UserId,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(response);
    }

    // The bands frozen on the Third Term publication (6.7.10: cumulative_grade resolves against that snapshot).
    private static List<ComputationGradingBand> Bands(string? snapshotJson)
    {
        if (snapshotJson is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(snapshotJson);
        return document.RootElement.TryGetProperty("settings", out var settings)
            && settings.TryGetProperty("grading", out var grading)
            && grading.TryGetProperty("bands", out var bands)
            && bands.ValueKind == JsonValueKind.Array
            ? bands.EnumerateArray()
                .Select(band => new ComputationGradingBand(
                    (int)band.GetProperty("lowerBound").GetDecimal(),
                    (int)band.GetProperty("upperBound").GetDecimal(),
                    band.GetProperty("gradeLetter").GetString() ?? string.Empty,
                    band.TryGetProperty("remark", out var remark) ? remark.GetString() ?? string.Empty : string.Empty))
                .ToList()
            : [];
    }
}
