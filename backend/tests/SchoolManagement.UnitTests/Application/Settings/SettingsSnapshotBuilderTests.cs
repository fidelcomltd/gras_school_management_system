using System.Text.Json.Nodes;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// Pins <see cref="SettingsSnapshotBuilder.Build"/>'s serialised SHAPE across the TASK-0072 stage 3a
/// refactor — the method now takes one bundled <see cref="SettingsSnapshotState"/> instead of one
/// parameter per group, and this test is the proof that moving how the input is ASSEMBLED never
/// touched what gets written into <c>config_version.snapshot_json</c> (spec 6.2.9: every historical
/// snapshot must remain parseable the same way it always was).
/// </summary>
public sealed class SettingsSnapshotBuilderTests
{
    [Fact]
    public void Build_ProducesTheSameShapeAsBeforeTheStage3ARefactor()
    {
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            schoolName: "Golden Royal Ark School",
            identityVersionNumber: 1,
            abbreviationVersionNumber: 2,
            regNumberVersionNumber: 3,
            gradingVersionNumber: 4,
            assessmentVersionNumber: 5,
            resultRulesVersionNumber: 6,
            ratingScalesVersionNumber: 7,
            developmentDomainsVersionNumber: 8,
            traitsVersionNumber: 9);

        var band = GradingBand.Create(Guid.CreateVersion7(), 50, 100, "P", "Pass", displayOrder: 1);
        var component = AssessmentComponent.Create(Guid.CreateVersion7(), "Exam", "EXAM", 100, isExamination: true, displayOrder: 1);
        var resultRules = ResultRules.CreateSeed(Guid.CreateVersion7());

        var scaleId = Guid.CreateVersion7();
        var scale = RatingScale.Create(
            scaleId,
            "Primary trait",
            [RatingScalePoint.Create(Guid.CreateVersion7(), scaleId, "E", "Excellent", 1)]);

        var domainId = Guid.CreateVersion7();
        var domain = DevelopmentDomain.Create(
            domainId,
            Guid.CreateVersion7(),
            "Maths Readiness",
            1,
            scaleId,
            allowsIndicatorComment: true,
            DevelopmentDomainStatus.Active,
            [DevelopmentIndicator.Create(Guid.CreateVersion7(), domainId, "Ability to Count", 1, DevelopmentIndicatorStatus.Active)]);

        var trait = Trait.Create(Guid.CreateVersion7(), TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active);
        var traitBlock = TraitBlock.Create(TraitDomain.Affective, scaleId);

        var state = new SettingsSnapshotState(
            [band],
            [component],
            resultRules,
            [scale],
            [domain],
            [trait],
            [traitBlock]);

        var json = SettingsSnapshotBuilder.Build(profile, state);
        var node = JsonNode.Parse(json)!.AsObject();

        // The exact top-level key set, camelCase — this is the SHAPE contract. Any addition, removal
        // or rename here is a real snapshot-shape change and must not slip through as a "refactor".
        var topLevelKeys = node.Select(property => property.Key).Order(StringComparer.Ordinal).ToList();
        var expectedKeys = new[]
        {
            "assessmentComponents",
            "assessmentVersionNumber",
            "developmentDomains",
            "developmentDomainsVersionNumber",
            "gradingBands",
            "gradingVersionNumber",
            "ratingScales",
            "ratingScalesVersionNumber",
            "resultRules",
            "resultRulesVersionNumber",
            "schoolProfile",
            "traits",
            "traitsVersionNumber",
        }.Order(StringComparer.Ordinal).ToList();

        topLevelKeys.ShouldBe(expectedKeys);

        node["schoolProfile"]!["schoolName"]!.GetValue<string>().ShouldBe("Golden Royal Ark School");
        node["gradingBands"]!.AsArray().Count.ShouldBe(1);
        node["gradingBands"]![0]!["gradeLetter"]!.GetValue<string>().ShouldBe("P");
        node["assessmentComponents"]!.AsArray().Count.ShouldBe(1);
        node["assessmentComponents"]![0]!["name"]!.GetValue<string>().ShouldBe("Exam");
        node["resultRules"]!["passMark"]!.GetValue<int>().ShouldBe(resultRules.PassMark);
        node["ratingScales"]!.AsArray().Count.ShouldBe(1);
        node["ratingScales"]![0]!["name"]!.GetValue<string>().ShouldBe("Primary trait");
        node["ratingScales"]![0]!["points"]!.AsArray().Count.ShouldBe(1);
        node["developmentDomains"]!.AsArray().Count.ShouldBe(1);
        node["developmentDomains"]![0]!["name"]!.GetValue<string>().ShouldBe("Maths Readiness");
        node["developmentDomains"]![0]!["indicators"]!.AsArray().Count.ShouldBe(1);
        node["gradingVersionNumber"]!.GetValue<int>().ShouldBe(4);
        node["assessmentVersionNumber"]!.GetValue<int>().ShouldBe(5);
        node["resultRulesVersionNumber"]!.GetValue<int>().ShouldBe(6);
        node["ratingScalesVersionNumber"]!.GetValue<int>().ShouldBe(7);
        node["developmentDomainsVersionNumber"]!.GetValue<int>().ShouldBe(8);
        node["traits"]!["affectiveRatingScaleId"]!.GetValue<string>().ShouldBe(scaleId.ToString());
        node["traits"]!["items"]!.AsArray().Count.ShouldBe(1);
        node["traits"]!["items"]![0]!["name"]!.GetValue<string>().ShouldBe("Punctuality");
        node["traitsVersionNumber"]!.GetValue<int>().ShouldBe(9);
    }
}
