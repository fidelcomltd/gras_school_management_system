using System.Globalization;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Resolves which <see cref="RegistrationCounter"/> partition (spec 6.5.10) a given
/// <see cref="RegNumberSerialReset"/> selects, for a given year.
/// </summary>
/// <remarks>
/// TASK-0005c's own reads (the preview, and the width-reduction scan — approved delta amendment 1)
/// both call this with <c>TimeProvider</c>'s current year, standing in for "the counter that is
/// actually active right now." TASK-0051's real issuance calls the same method with the admission
/// year from <c>admission_record.date_admitted</c> instead (spec 6.5.10: "the admission year... not
/// the current year") — the resolution rule itself does not change, only which year is supplied.
/// </remarks>
public static class RegistrationCounterPartition
{
    /// <summary>The fixed sentinel key spec 6.5.10 assigns to <see cref="RegNumberSerialReset.Continuous"/>.</summary>
    public const string ContinuousKey = "ALL";

    /// <summary>
    /// Under <see cref="RegNumberSerialReset.PerYear"/> the key is <paramref name="year"/> as a
    /// string; under <see cref="RegNumberSerialReset.Continuous"/> it is always
    /// <see cref="ContinuousKey"/>, regardless of <paramref name="year"/>.
    /// </summary>
    public static string Resolve(RegNumberSerialReset serialReset, int year) =>
        serialReset == RegNumberSerialReset.Continuous
            ? ContinuousKey
            : year.ToString(CultureInfo.InvariantCulture);
}
