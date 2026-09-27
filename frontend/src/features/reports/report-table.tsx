import { cn } from '@/lib/utils/cn';
import type { ReportColumnDto, ReportDto } from './definitions';

const ALIGN: Record<ReportColumnDto['align'], string> = {
  Left: 'text-left',
  Center: 'text-center',
  Right: 'text-right tabular-nums',
};

/** Consecutive columns sharing a group, for the upper header row. */
function groupsOf(columns: ReportColumnDto[]) {
  const spans: { group: string | null; span: number; index: number }[] = [];
  columns.forEach((column, index) => {
    const last = spans.at(-1);
    if (column.group && last?.group === column.group) last.span += 1;
    else spans.push({ group: column.group ?? null, span: 1, index });
  });
  return spans;
}

/**
 * Any report's table (spec 15 section 10): grouped headers where columns share a group (the broadsheet's subjects), heading
 * rows across the full width, bold subtotals and totals. The same cells the CSV and PDF print.
 */
export function ReportTable({ report }: { report: ReportDto }) {
  const grouped = report.columns.some((column) => column.group);
  const spans = groupsOf(report.columns);
  // Rows and columns carry no ids and never reorder: their position is their identity.
  const columns = report.columns.map((column, position) => ({ ...column, id: `c${position}` }));
  const rows = report.rows.map((row, position) => ({ ...row, id: `r${position}` }));

  if (report.rows.length === 0) {
    return <p className="text-sm text-muted-foreground">Nothing matches these filters.</p>;
  }

  return (
    <div className="overflow-x-auto rounded-md border border-border">
      <table className="w-full text-sm">
        <caption className="sr-only">{report.title}</caption>
        <thead className="bg-muted text-muted-foreground">
          {grouped ? (
            <tr>
              {spans.map((span) =>
                span.group ? (
                  <th key={span.index} scope="colgroup" colSpan={span.span} className="border-b border-border px-2 py-1.5 text-center font-medium">
                    {span.group}
                  </th>
                ) : (
                  <th
                    key={span.index}
                    scope="col"
                    rowSpan={2}
                    className={cn('px-2 py-1.5 font-medium', ALIGN[report.columns[span.index]?.align ?? 'Left'])}
                  >
                    {report.columns[span.index]?.label}
                  </th>
                ),
              )}
            </tr>
          ) : null}
          <tr>
            {columns.map((column) =>
              grouped && !column.group ? null : (
                <th key={column.id} scope="col" className={cn('px-2 py-1.5 font-medium', ALIGN[column.align])}>
                  {column.label}
                </th>
              ),
            )}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) =>
            row.kind === 'Heading' ? (
              <tr key={row.id} className="border-t border-border bg-muted/50">
                <th scope="colgroup" colSpan={report.columns.length} className="px-2 py-1.5 text-left font-semibold text-foreground">
                  {row.cells[0]}
                </th>
              </tr>
            ) : (
              <tr
                key={row.id}
                className={cn('border-t border-border', row.kind !== 'Data' && 'font-semibold text-foreground')}
              >
                {columns.map((column, index) => (
                  <td key={column.id} className={cn('px-2 py-1.5', ALIGN[column.align])}>
                    {row.cells[index] ?? ''}
                  </td>
                ))}
              </tr>
            ),
          )}
        </tbody>
      </table>
    </div>
  );
}
