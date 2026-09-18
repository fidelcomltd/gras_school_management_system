using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// Tests <see cref="ResetGradingCommandHandler"/>: it is an ordinary save whose input happens to be
/// <see cref="GradingScaleSeed.SeededBands"/> (spec 6.2.11: "treated as an ordinary edit under 6.2.9").
/// </summary>
public sealed class ResetGradingCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly IGradingBandRepository _gradingBandRepository = Substitute.For<IGradingBandRepository>();

    private readonly IAssessmentComponentRepository _assessmentComponentRepository =
        Substitute.For<IAssessmentComponentRepository>();

    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly IAcademicSessionRepository _academicSessionRepository = Substitute.For<IAcademicSessionRepository>();
    private readonly IPublishedResultsGate _publishedResultsGate = Substitute.For<IPublishedResultsGate>();
    private readonly IResultRulesRepository _resultRulesRepository = Substitute.For<IResultRulesRepository>();
    private readonly IRatingScaleRepository _ratingScaleRepository = Substitute.For<IRatingScaleRepository>();
    private readonly IDevelopmentDomainRepository _developmentDomainRepository = Substitute.For<IDevelopmentDomainRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    // Defaults set in the CONSTRUCTOR, not CreateHandler() — see UpdateGradingCommandHandlerTests'
    // own constructor comment for why.
    public ResetGradingCommandHandlerTests()
    {
        _assessmentComponentRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<AssessmentComponent>());
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns((AcademicSession?)null);
        _resultRulesRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(ResultRules.CreateSeed(Guid.CreateVersion7()));
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RatingScale>());
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<DevelopmentDomain>());
    }

    private ResetGradingCommandHandler CreateHandler()
    {
        return new(
            _schoolProfileRepository,
            _gradingBandRepository,
            _assessmentComponentRepository,
            _configVersionRepository,
            _academicSessionRepository,
            _publishedResultsGate,
            _resultRulesRepository,
            _ratingScaleRepository,
            _developmentDomainRepository,
            _currentUser,
            _auditSink,
            _timeProvider);
    }

    private SchoolProfile CreateTrackedProfile(int gradingVersionNumber = 0)
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), gradingVersionNumber: gradingVersionNumber);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    [Fact]
    public async Task HandleAsync_WithAStaleExpectedVersion_Returns409()
    {
        CreateTrackedProfile(gradingVersionNumber: 2);

        var result = await CreateHandler().HandleAsync(
            new ResetGradingCommand(ExpectedVersion: 1, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ResetGradingCommandHandler.StaleVersionErrorCode);
        await _gradingBandRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<GradingBand>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RestoresExactlyTheNineSeededBands()
    {
        var profile = CreateTrackedProfile(gradingVersionNumber: 6);

        var result = await CreateHandler().HandleAsync(
            new ResetGradingCommand(ExpectedVersion: 6, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Bands.Count.ShouldBe(9);
        result.Value.Bands.Select(band => band.GradeLetter).ShouldBe(
            ["A+", "A", "B", "B-", "C+", "C", "D", "E", "F"]);
        profile.GradingVersionNumber.ShouldBe(7);

        await _gradingBandRepository.Received(1).ReplaceAllAsync(
            Arg.Is<IReadOnlyList<GradingBand>>(bands => bands != null && bands.Count == 9),
            Arg.Any<CancellationToken>());
    }
}
