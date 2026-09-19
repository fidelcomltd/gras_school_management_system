using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Infrastructure.Persistence.Configurations;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0072 stage 3b, proven directly against the persistence mechanism (the same technique
/// <c>DevelopmentDomainRepositoryTests</c> uses): <c>TraitRepository</c>'s id-stable diff, the
/// fresh-database seed (19 traits from Appendix F.3, two trait_block rows), and
/// <see cref="IRatingScaleUsageGate"/> counting <c>trait_block.rating_scale_id</c> — proving the
/// Primary trait scale reports in-use straight out of a fresh database, before any save.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class TraitRepositoryTests(ApiTestFixture fixture)
{
    private void RequireDatabase()
    {
        if (!fixture.IsDatabaseAvailable)
        {
            Assert.Skip(fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    [Fact]
    public async Task FreshDatabase_SeedsNineteenTraitsAndTwoBlocksPointingAtThePrimaryTraitScale()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITraitRepository>();

        var traits = await repository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);
        var blocks = await repository.ListBlocksReadOnlyAsync(TestContext.Current.CancellationToken);

        traits.Count.ShouldBe(19);
        traits.Count(trait => trait.Domain == TraitDomain.Affective).ShouldBe(11);
        traits.Count(trait => trait.Domain == TraitDomain.Psychomotor).ShouldBe(8);
        traits.Select(trait => trait.Id).ShouldBe(TraitConfiguration.AllSeededIds, ignoreOrder: true);

        var primaryTraitScaleId = RatingScaleConfiguration.SeededIds[1];
        blocks.Count.ShouldBe(2);
        blocks.ShouldAllBe(block => block.RatingScaleId == primaryTraitScaleId);
    }

    [Fact]
    public async Task RatingScaleUsageGate_IsInUse_ReportsThePrimaryTraitScaleInUseFromTheSeededTraitBlocks()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var usageGate = scope.ServiceProvider.GetRequiredService<IRatingScaleUsageGate>();

        var primaryTraitScaleId = RatingScaleConfiguration.SeededIds[1];

        // No save has happened yet — the seeded trait_block rows alone make this true.
        (await usageGate.IsInUseAsync(primaryTraitScaleId, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task ReplaceAllAsync_ResavingWithNoChanges_KeepsEveryTraitIdAndUpdatesBothBlocks()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITraitRepository>();
        var db = scope.ServiceProvider.GetRequiredService<SchoolManagement.Infrastructure.Persistence.ApplicationDbContext>();

        var before = await repository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);
        var blocksBefore = await repository.ListBlocksReadOnlyAsync(TestContext.Current.CancellationToken);
        var fivePointNumericId = RatingScaleConfiguration.SeededIds[2];

        await repository.ReplaceAllAsync(before, fivePointNumericId, fivePointNumericId, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readScope = fixture.CreateScope();
        var readRepository = readScope.ServiceProvider.GetRequiredService<ITraitRepository>();
        var after = await readRepository.ListReadOnlyOrderedAsync(TestContext.Current.CancellationToken);
        var blocksAfter = await readRepository.ListBlocksReadOnlyAsync(TestContext.Current.CancellationToken);

        after.Select(trait => trait.Id).ShouldBe(before.Select(trait => trait.Id), ignoreOrder: true);
        blocksAfter.ShouldAllBe(block => block.RatingScaleId == fivePointNumericId);
        blocksBefore.Count.ShouldBe(blocksAfter.Count);
    }
}
