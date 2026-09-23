using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Pupils.Import;

/// <summary>
/// The active session's open arms, looked up the two ways spec 6.5.13 allows: Class Level plus Arm Label, matched
/// case-insensitively, or a single composed display name such as <c>Primary 2C</c> in the Class Level column.
/// </summary>
internal sealed class ArmDirectory
{
    private readonly AcademicSession _session;
    private readonly Dictionary<string, ClassLevel> _levelsByKey;
    private readonly Dictionary<Guid, List<Arm>> _armsByLevel;
    private readonly Dictionary<Guid, string> _namesByArm;

    // A composed name two arms share (only possible with odd labels) maps to null: ambiguous, so refused.
    private readonly Dictionary<string, Arm?> _armsByComposedKey = new(StringComparer.Ordinal);

    private ArmDirectory(AcademicSession session, IReadOnlyList<ClassLevel> levels, IReadOnlyList<Arm> arms)
    {
        _session = session;
        _levelsByKey = levels.Where(level => level.Status == LevelStatus.Active)
            .GroupBy(level => PupilImportColumns.Key(level.Name))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var levelsById = levels.ToDictionary(level => level.Id);
        var open = arms
            .Where(arm => arm.SessionId == session.Id && arm.Status != ArmStatus.Closed && levelsById.ContainsKey(arm.ClassLevelId))
            .OrderBy(arm => levelsById[arm.ClassLevelId].ProgressionOrder)
            .ThenBy(arm => arm.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _armsByLevel = open.GroupBy(arm => arm.ClassLevelId).ToDictionary(group => group.Key, group => group.ToList());
        _namesByArm = open.ToDictionary(arm => arm.Id, arm => ArmDisplayName.Compose(levelsById[arm.ClassLevelId].Name, arm.Label));
        LevelNames = open.ConvertAll(arm => levelsById[arm.ClassLevelId].Name);
        Arms = open;
        foreach (var arm in open)
        {
            var key = PupilImportColumns.Key(_namesByArm[arm.Id]);
            _armsByComposedKey[key] = _armsByComposedKey.ContainsKey(key) ? null : arm;
        }
    }

    /// <summary>Every open arm of the active session, in level then label order.</summary>
    public IReadOnlyList<Arm> Arms { get; }

    /// <summary>Each of <see cref="Arms"/>' level name, aligned.</summary>
    public IReadOnlyList<string> LevelNames { get; }

    public static async Task<ArmDirectory> LoadAsync(
        AcademicSession session, IClassLevelRepository classLevels, IArmRepository arms, CancellationToken cancellationToken)
    {
        var levels = await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var allArms = await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        return new ArmDirectory(session, levels, allArms);
    }

    public string NameOf(Arm arm) => _namesByArm[arm.Id];

    /// <summary>Resolves a row's arm, recording the reason against its column when it cannot.</summary>
    public Arm? Resolve(string? levelText, string? labelText, PupilImportDraft draft)
    {
        var arm = Find(levelText, labelText, draft);
        draft.ArmName = arm is null ? null : NameOf(arm);
        return arm;
    }

    private Arm? Find(string? levelText, string? labelText, PupilImportDraft draft)
    {
        if (levelText is null)
        {
            draft.Error(PupilImportColumns.ClassLevel, "Enter the class level and arm label, for example Primary 2 and C.");
            return null;
        }

        _levelsByKey.TryGetValue(PupilImportColumns.Key(levelText), out var level);
        var levelArms = level is not null && _armsByLevel.TryGetValue(level.Id, out var found) ? found : [];
        var session = _session.Name;

        if (labelText is not null)
        {
            if (level is null)
            {
                draft.Error(
                    PupilImportColumns.ClassLevel,
                    $"There is no class level named {levelText}. Use a name from the Accepted values sheet.");
                return null;
            }

            var labelKey = PupilImportColumns.Key(labelText);
            var arm = levelArms.FirstOrDefault(candidate => PupilImportColumns.Key(candidate.Label) == labelKey);
            if (arm is null)
            {
                // Spec 6.5.16's own wording.
                draft.Error(
                    PupilImportColumns.ArmLabel,
                    $"{level.Name} has no arm named {labelText} in {session}. Create the arm or correct the row.");
            }

            return arm;
        }

        if (_armsByComposedKey.TryGetValue(PupilImportColumns.Key(levelText), out var composed))
        {
            if (composed is null)
            {
                draft.Error(
                    PupilImportColumns.ClassLevel,
                    $"{levelText} matches more than one arm. Enter the Class Level and Arm Label separately.");
            }

            return composed;
        }

        switch (level, levelArms.Count)
        {
            case (null, _):
                draft.Error(
                    PupilImportColumns.ClassLevel,
                    $"{levelText} is not a class level or an arm in {session}. Use a name from the Accepted values sheet.");
                return null;
            case (_, 1):
                return levelArms[0];
            case (_, 0):
                draft.Error(PupilImportColumns.ClassLevel, $"{level.Name} has no arm in {session}. Create the arm or correct the row.");
                return null;
            default:
                draft.Error(PupilImportColumns.ArmLabel, $"{level.Name} has {levelArms.Count} arms in {session}. Enter the Arm Label.");
                return null;
        }
    }
}
