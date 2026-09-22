import type { components } from '@/api/schema';
import { cn } from '@/lib/utils/cn';

type ResultSetReadinessDto = components['schemas']['ResultSetReadinessDto'];
type MarkCompletionStatus = components['schemas']['MarkCompletionStatus'];

const MARK: Record<MarkCompletionStatus, { text: string; className: string }> = {
  Complete: { text: '✓', className: 'text-success' },
  Partial: { text: 'part', className: 'text-warning' },
  Empty: { text: '—', className: 'text-destructive' },
};

const tick = (done: boolean) => (
  <span className={done ? 'text-success' : 'text-destructive'}>{done ? '✓' : '—'}</span>
);

/** The completeness gate (spec 6.7.5): subjects across, pupils down, then ratings, attendance and the two remarks. */
export function ReadinessGrid({ readiness }: { readiness: ResultSetReadinessDto }) {
  const counters = readiness.counters;
  const summary = [
    ['Marks', counters.marks],
    ['Ratings', counters.ratings],
    ['Attendance', counters.attendance],
    ["Teacher's remarks", counters.classTeacherRemarks],
    ["Head teacher's remarks", counters.headTeacherRemarks],
  ] as const;

  return (
    <div className="flex flex-col gap-3">
      <dl className="flex flex-wrap gap-3 text-sm">
        {summary.map(([label, counter]) => (
          <div key={label} className="rounded-md border border-border bg-surface px-3 py-2">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="font-medium text-foreground">
              {counter.complete} of {counter.total}
            </dd>
          </div>
        ))}
      </dl>

      <div className="overflow-x-auto rounded-md border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted text-muted-foreground">
            <tr>
              <th scope="col" className="px-3 py-2 text-left font-medium">Pupil</th>
              {readiness.subjects.map((subject) => (
                <th key={subject.subjectId} scope="col" className="px-2 py-2 font-medium">{subject.name}</th>
              ))}
              <th scope="col" className="px-2 py-2 font-medium">Ratings</th>
              <th scope="col" className="px-2 py-2 font-medium">Attendance</th>
              <th scope="col" className="px-2 py-2 font-medium">Teacher</th>
              <th scope="col" className="px-2 py-2 font-medium">Head</th>
            </tr>
          </thead>
          <tbody>
            {readiness.pupils.map((pupil) => (
              <tr key={pupil.pupilId} className="border-t border-border">
                <th scope="row" className="px-3 py-1.5 text-left font-normal text-foreground">{pupil.displayName}</th>
                {readiness.subjects.map((subject) => {
                  const cell = pupil.marks.find((mark) => mark.subjectId === subject.subjectId);
                  const shown = MARK[cell?.status ?? 'Empty'];
                  return (
                    <td key={subject.subjectId} className={cn('px-2 py-1.5 text-center', shown.className)}>
                      {shown.text}
                    </td>
                  );
                })}
                <td className="px-2 py-1.5 text-center">{tick(pupil.ratingsComplete)}</td>
                <td className="px-2 py-1.5 text-center">{tick(pupil.attendanceComplete)}</td>
                <td className="px-2 py-1.5 text-center">{tick(pupil.classTeacherRemarkPresent)}</td>
                <td className="px-2 py-1.5 text-center">{tick(pupil.headTeacherRemarkPresent)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {readiness.leftDuringTerm.length > 0 ? (
        <p className="text-sm text-muted-foreground">
          Left the class this term: {readiness.leftDuringTerm.map((pupil) => pupil.displayName).join(', ')}.
        </p>
      ) : null}
    </div>
  );
}
