import { cellKey, type SubjectMappingGridDto } from '../types';

/**
 * The subjects-by-classes grid (spec 6.6.5): one checkbox per (subject, class) for the chosen term. `edits` overrides
 * the server's state for the cells the admin has toggled; nothing is sent until the admin reviews and saves.
 */
export function MappingGrid({
  grid,
  edits,
  readOnly,
  onToggle,
}: {
  grid: SubjectMappingGridDto;
  edits: ReadonlyMap<string, boolean>;
  readOnly: boolean;
  onToggle: (key: string, mapped: boolean) => void;
}) {
  return (
    <div className="overflow-x-auto rounded-md border border-border">
      <table className="w-full text-sm">
        <thead className="bg-muted text-muted-foreground">
          <tr>
            <th scope="col" className="sticky left-0 bg-muted px-3 py-2 text-left font-medium">
              Subject
            </th>
            {grid.levels.map((level) => (
              <th key={level.classLevelId} scope="col" className="px-2 py-2 text-center font-medium whitespace-nowrap">
                {level.classLevelName}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {grid.subjects.map((subject) => (
            <tr key={subject.subjectId} className="border-t border-border">
              <th scope="row" className="sticky left-0 bg-surface px-3 py-2 text-left font-medium text-foreground">
                {subject.subjectName}
              </th>
              {grid.levels.map((level) => {
                const key = cellKey(subject.subjectId, level.classLevelId);
                const server = subject.cells.find((cell) => cell.classLevelId === level.classLevelId)?.mapped ?? false;
                const mapped = edits.get(key) ?? server;
                const changed = edits.has(key) && edits.get(key) !== server;
                return (
                  <td key={level.classLevelId} className={changed ? 'bg-warning/15 text-center' : 'text-center'}>
                    <input
                      type="checkbox"
                      aria-label={`${subject.subjectName} in ${level.classLevelName}`}
                      checked={mapped}
                      disabled={readOnly}
                      onChange={(event) => onToggle(key, event.target.checked)}
                    />
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
