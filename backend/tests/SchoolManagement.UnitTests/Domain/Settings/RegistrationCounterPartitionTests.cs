using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>
/// Tests <see cref="RegistrationCounterPartition.Resolve"/> — the central mechanism approved delta
/// amendment 1 exists for: <c>continuous</c> must select a partition that never varies with the year,
/// or it would silently behave as <c>per_year</c>.
/// </summary>
public sealed class RegistrationCounterPartitionTests
{
    [Fact]
    public void Resolve_UnderPerYear_ReturnsTheYearAsAString()
    {
        RegistrationCounterPartition.Resolve(RegNumberSerialReset.PerYear, 2026).ShouldBe("2026");
    }

    [Fact]
    public void Resolve_UnderPerYear_DifferentYearsProduceDifferentPartitions()
    {
        // The direct proof that per_year actually resets: two different years never collapse to the
        // same partition key.
        var year2026 = RegistrationCounterPartition.Resolve(RegNumberSerialReset.PerYear, 2026);
        var year2027 = RegistrationCounterPartition.Resolve(RegNumberSerialReset.PerYear, 2027);

        year2026.ShouldNotBe(year2027);
    }

    [Theory]
    [InlineData(2026)]
    [InlineData(2027)]
    [InlineData(2099)]
    public void Resolve_UnderContinuous_AlwaysReturnsTheFixedSentinelRegardlessOfYear(int year)
    {
        // The direct proof of amendment 1's fix: crossing a year boundary under `continuous` must NOT
        // change which partition is read — that is exactly the behaviour a year-keyed-only counter
        // could not express.
        RegistrationCounterPartition.Resolve(RegNumberSerialReset.Continuous, year)
            .ShouldBe(RegistrationCounterPartition.ContinuousKey);
    }
}
