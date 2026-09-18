using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Infrastructure.Persistence.Configurations;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0072 stage 2a, proven directly against the persistence mechanism (the same technique
/// <c>SubjectPersistenceTests</c>/<c>EnrolmentPersistenceTests</c> use): <c>DevelopmentDomainRepository</c>'s
/// id-stable diff, applied from the start rather than fixed after the fact as
/// <c>RatingScaleRepository</c>'s was in stage 1's review; <see cref="IRatingScaleUsageGate"/> becoming
/// a real query against <c>development_domain</c>; and the fresh-database seed (4 domains, 45
/// indicators from Appendix E.3).
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class DevelopmentDomainRepositoryTests(ApiTestFixture fixture)
{
    private void RequireDatabase()
    {
        if (!fixture.IsDatabaseAvailable)
        {
            Assert.Skip(fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    [Fact]
    public async Task FreshDatabase_SeedsFourNurseryDomainsAndFortyFiveIndicators()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();

        var domains = await repository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);

        domains.Count.ShouldBe(4);
        domains.Sum(domain => domain.Indicators.Count).ShouldBe(45);
        domains.ShouldAllBe(domain => domain.SectionId == SchoolManagement.Domain.Classes.SeededClassLevels.NurserySectionId);
        domains.ShouldAllBe(domain => domain.RatingScaleId == RatingScaleConfiguration.SeededIds[0]);
        domains.Select(domain => domain.Id).ShouldBe(DevelopmentDomainConfiguration.SeededIds, ignoreOrder: true);
    }

    [Fact]
    public async Task ReplaceAllAsync_ResavingWithNoChanges_KeepsEveryDomainAndIndicatorId()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var firstScope = fixture.CreateScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();
        var before = await firstRepository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);

        await firstRepository.ReplaceAllAsync(before, TestContext.Current.CancellationToken);
        var db = firstScope.ServiceProvider.GetRequiredService<SchoolManagement.Infrastructure.Persistence.ApplicationDbContext>();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var secondScope = fixture.CreateScope();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();
        var after = await secondRepository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);

        after.Select(domain => domain.Id).ShouldBe(before.Select(domain => domain.Id), ignoreOrder: true);
        after.SelectMany(domain => domain.Indicators).Select(indicator => indicator.Id)
            .ShouldBe(before.SelectMany(domain => domain.Indicators).Select(indicator => indicator.Id), ignoreOrder: true);
    }

    [Fact]
    public async Task ReplaceAllAsync_RenameById_KeepsTheDomainIdAndIndicatorIds()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();
        var before = await repository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);
        var target = before[0];

        var renamed = before.Select(domain => domain.Id != target.Id
                ? domain
                : DevelopmentDomain.Create(
                    domain.Id,
                    domain.SectionId,
                    "Renamed Domain",
                    domain.DisplayOrder,
                    domain.RatingScaleId,
                    domain.AllowsIndicatorComment,
                    domain.Status,
                    domain.Indicators))
            .ToList();

        await repository.ReplaceAllAsync(renamed, TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<SchoolManagement.Infrastructure.Persistence.ApplicationDbContext>();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readScope = fixture.CreateScope();
        var readRepository = readScope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();
        var after = await readRepository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);

        var renamedDomain = after.Single(domain => domain.Id == target.Id);
        renamedDomain.Name.ShouldBe("Renamed Domain");
        renamedDomain.Indicators.Select(indicator => indicator.Id)
            .ShouldBe(target.Indicators.Select(indicator => indicator.Id), ignoreOrder: true);
    }

    [Fact]
    public async Task ReplaceAllAsync_OmittingADomain_RemovesItAndItsIndicators()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();
        var before = await repository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);
        var removedId = before[0].Id;

        var remaining = before.Where(domain => domain.Id != removedId).ToList();

        await repository.ReplaceAllAsync(remaining, TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<SchoolManagement.Infrastructure.Persistence.ApplicationDbContext>();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readScope = fixture.CreateScope();
        var readRepository = readScope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();
        var after = await readRepository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);

        after.Count.ShouldBe(3);
        after.ShouldNotContain(domain => domain.Id == removedId);
    }

    [Fact]
    public async Task ReplaceAllAsync_OmittingOneIndicator_RemovesOnlyThatIndicator()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();
        var before = await repository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);
        var target = before.First(domain => domain.Indicators.Count > 1);
        var removedIndicatorId = target.Indicators[0].Id;

        var edited = before.Select(domain => domain.Id != target.Id
                ? domain
                : DevelopmentDomain.Create(
                    domain.Id,
                    domain.SectionId,
                    domain.Name,
                    domain.DisplayOrder,
                    domain.RatingScaleId,
                    domain.AllowsIndicatorComment,
                    domain.Status,
                    domain.Indicators.Where(indicator => indicator.Id != removedIndicatorId).ToList()))
            .ToList();

        await repository.ReplaceAllAsync(edited, TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<SchoolManagement.Infrastructure.Persistence.ApplicationDbContext>();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readScope = fixture.CreateScope();
        var readRepository = readScope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();
        var after = await readRepository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);

        var afterTarget = after.Single(domain => domain.Id == target.Id);
        afterTarget.Indicators.Count.ShouldBe(target.Indicators.Count - 1);
        afterTarget.Indicators.ShouldNotContain(indicator => indicator.Id == removedIndicatorId);
    }

    [Fact]
    public async Task RatingScaleUsageGate_IsInUse_FlipsTrueThenFalseAsADomainReferenceIsAddedThenRemoved()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var domainRepository = scope.ServiceProvider.GetRequiredService<IDevelopmentDomainRepository>();
        var ratingScaleRepository = scope.ServiceProvider.GetRequiredService<IRatingScaleRepository>();
        var usageGate = scope.ServiceProvider.GetRequiredService<IRatingScaleUsageGate>();
        var db = scope.ServiceProvider.GetRequiredService<SchoolManagement.Infrastructure.Persistence.ApplicationDbContext>();

        // The "Five-point numeric" scale is referenced by nothing per the seed (spec 6.2.13) — a clean starting point.
        var scales = await ratingScaleRepository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);
        var unusedScale = scales.Single(scale => scale.Name == RatingScaleSeed.FivePointNumericName);

        (await usageGate.IsInUseAsync(unusedScale.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();

        var domains = await domainRepository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);
        var target = domains[0];
        var pointedAtUnusedScale = domains.Select(domain => domain.Id != target.Id
                ? domain
                : DevelopmentDomain.Create(
                    domain.Id,
                    domain.SectionId,
                    domain.Name,
                    domain.DisplayOrder,
                    unusedScale.Id,
                    domain.AllowsIndicatorComment,
                    domain.Status,
                    domain.Indicators))
            .ToList();

        await domainRepository.ReplaceAllAsync(pointedAtUnusedScale, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await usageGate.IsInUseAsync(unusedScale.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();

        // Point it back at its original scale — the gate must flip back to false.
        var restored = pointedAtUnusedScale.Select(domain => domain.Id != target.Id
                ? domain
                : DevelopmentDomain.Create(
                    domain.Id,
                    domain.SectionId,
                    domain.Name,
                    domain.DisplayOrder,
                    target.RatingScaleId,
                    domain.AllowsIndicatorComment,
                    domain.Status,
                    domain.Indicators))
            .ToList();

        await domainRepository.ReplaceAllAsync(restored, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await usageGate.IsInUseAsync(unusedScale.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
}
