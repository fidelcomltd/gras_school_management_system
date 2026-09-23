using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Weekly;

/// <summary>
/// An arm's weekly-report settings (spec 6.10.8): the auto-publish option, off by default, which publishes each week at
/// 17:00 on its Friday. No row means off.
/// </summary>
public sealed class ArmWeeklySetting : IAuditableEntity
{
    private ArmWeeklySetting(Guid armId, bool autoPublish)
    {
        ArmId = armId;
        AutoPublish = autoPublish;
    }

    // EF Core materialisation constructor.
    private ArmWeeklySetting()
    {
    }

    /// <summary>The arm; also the key.</summary>
    public Guid ArmId { get; private set; }

    /// <summary>Publish each week automatically at 17:00 on its Friday.</summary>
    public bool AutoPublish { get; private set; }

    /// <summary>
    /// The Monday of the last week the job considered, so a week a teacher unpublishes after the automatic publish is
    /// not published again on the next run.
    /// </summary>
    public DateOnly? LastAutoPublishedWeekStart { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>A new setting row.</summary>
    public static ArmWeeklySetting Create(Guid armId, bool autoPublish) => new(armId, autoPublish);

    /// <summary>Turns the option on or off.</summary>
    public void SetAutoPublish(bool autoPublish) => AutoPublish = autoPublish;

    /// <summary>Records that the job has handled the week starting <paramref name="weekStart"/>.</summary>
    public void MarkAutoPublished(DateOnly weekStart) => LastAutoPublishedWeekStart = weekStart;
}
