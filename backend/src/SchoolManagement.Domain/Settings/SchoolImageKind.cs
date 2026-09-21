namespace SchoolManagement.Domain.Settings;

/// <summary>Which identity image a <see cref="SchoolImage"/> row is (TASK-0005b stage B1; spec 9.6).</summary>
public enum SchoolImageKind
{
    /// <summary>The school logo. Produced in three <see cref="SchoolImageSizeVariant"/> renditions.</summary>
    Logo = 0,

    /// <summary>The head teacher's signature. Produced in one rendition only.</summary>
    Signature = 1,
}
