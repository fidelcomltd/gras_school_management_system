namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Which size a <see cref="SchoolImage"/> row was produced at (TASK-0005b stage B1; spec 9.6).
/// Domain-local: intentionally NOT the same type as
/// <c>SchoolManagement.Application.Abstractions.Settings.SchoolImageSizeVariant</c> — Domain must
/// not depend on Application (<c>DependencyDirectionTests.Domain_DependsOnNothing</c>), so the two
/// layers each carry their own copy of this small, stable concept.
/// </summary>
public enum SchoolImageSizeVariant
{
    /// <summary>The uploaded image at its native (post-orientation-correction) pixel dimensions.</summary>
    Original = 0,

    /// <summary>Logo derivative, 200 pixels on the long edge. Never used for a signature.</summary>
    Size200 = 1,

    /// <summary>Logo derivative, 64 pixels on the long edge. Never used for a signature.</summary>
    Size64 = 2,
}
