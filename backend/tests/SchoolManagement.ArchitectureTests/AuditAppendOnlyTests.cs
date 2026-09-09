using SchoolManagement.Application.Abstractions.Audit;

namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// Spec 9.4: "audit_event | Never [hard-deleted] | Nothing." and spec 6.1.12: "Entries cannot be
/// edited or deleted through any interface." Held here, not only by review, so an update or delete
/// path cannot be added back to <see cref="IAuditEventRepository"/> quietly later (TASK-0048).
/// </summary>
public sealed class AuditAppendOnlyTests
{
    /// <summary>
    /// The only method names this interface may ever declare. Anything named <c>Update</c>,
    /// <c>Delete</c>, <c>Remove</c> or similar fails this test rather than the review that missed
    /// it.
    /// </summary>
    private static readonly string[] AllowedMethodNames = ["AddAsync"];

    [Fact]
    public void IAuditEventRepository_HasNoUpdateOrDeleteMethod()
    {
        var methodNames = typeof(IAuditEventRepository)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        methodNames.ShouldBe(AllowedMethodNames, ignoreOrder: true);
    }
}
