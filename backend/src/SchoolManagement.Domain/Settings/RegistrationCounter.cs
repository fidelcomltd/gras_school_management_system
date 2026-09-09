using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One row of the registration-number counter (spec 6.5.10): a partition key plus its highest issued
/// serial. Under <see cref="RegNumberSerialReset.PerYear"/> the key is the admission year as a
/// string; under <see cref="RegNumberSerialReset.Continuous"/> it is the fixed
/// <see cref="RegistrationCounterPartition.ContinuousKey"/>.
/// </summary>
/// <remarks>
/// <para>
/// THIS CARD (TASK-0005c) OWNS THE TABLE AND ITS READ PATHS ONLY. There is no factory method that
/// creates or mutates a row — the Application-layer read port exposes only a read — because
/// incrementing this counter is admission approval's job (TASK-0051),
/// via the atomic <c>INSERT ... ON CONFLICT (counter_key) DO UPDATE ... RETURNING last_serial</c> spec
/// 6.5.10 specifies. That statement takes a row lock and serialises concurrent approvals on the same
/// key; reading the value back into application code and writing it again here would reintroduce
/// exactly the race it exists to prevent. The counter is never derived from the pupil table either
/// (spec 6.5.10: "must not be used, including in the import path") — this card has no pupil table to
/// derive it from yet, and never will.
/// </para>
/// </remarks>
public sealed class RegistrationCounter : Entity<string>
{
    // EF Core materialisation constructor. Every field is set from a database row immediately
    // afterward — there is no code path that leaves this placeholder live, and no application code
    // ever constructs an instance any other way (see the remarks above).
    private RegistrationCounter()
        : base()
    {
    }

    /// <summary>The partition's current highest issued serial. Never read as 0 to mean "issued serial zero" — 0 means nothing has been issued under this partition yet.</summary>
    public int LastSerial { get; private set; }
}
