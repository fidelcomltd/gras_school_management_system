namespace SchoolManagement.Domain.Weekly;

/// <summary>A weekly report's visibility to parents (spec 6.10.5). Draft is invisible on the portal.</summary>
public enum WeeklyReportState
{
    /// <summary>Not visible to parents.</summary>
    Draft,

    /// <summary>Visible on the portal.</summary>
    Published,
}

/// <summary>The five school days of a weekly report (spec 6.10.6), Monday first.</summary>
public enum WeeklyDay
{
    /// <summary>Monday.</summary>
    Monday,

    /// <summary>Tuesday.</summary>
    Tuesday,

    /// <summary>Wednesday.</summary>
    Wednesday,

    /// <summary>Thursday.</summary>
    Thursday,

    /// <summary>Friday.</summary>
    Friday,
}

/// <summary>The eight labelled lines of a day panel (spec 6.10.2), in the paper form's fixed order.</summary>
public enum WeeklyField
{
    /// <summary>How the child conducted themselves.</summary>
    Behaviour,

    /// <summary>How they engaged with work that day.</summary>
    Performance,

    /// <summary>Uniform and personal presentation.</summary>
    Dressing,

    /// <summary>Whether homework was returned and its state.</summary>
    HomeWork,

    /// <summary>Whether the child ate.</summary>
    Eating,

    /// <summary>Anything the school observed. Not a diagnosis.</summary>
    SymptomsOfIllness,

    /// <summary>The class teacher's free note for the day.</summary>
    TeacherComment,

    /// <summary>What the parent wrote back, transcribed by staff. Optional (conflict item 4).</summary>
    ParentComment,
}
