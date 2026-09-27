import { ChevronRight } from 'lucide-react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { REPORTS, type ReportDefinition } from './definitions';

const hrefOf = (report: ReportDefinition) => (report.kind === 'table' ? paths.report(report.key) : report.to);

/** `/reports` — every report the caller may open, grouped (spec 15 section 10). A report they cannot open is absent. */
export function ReportsScreen() {
  const me = useMe();
  const visible = REPORTS.filter((report) => !!me.data && hasPrivilege(me.data, report.privilege));
  const groups = [...new Set(visible.map((report) => report.group))];

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Reports</h1>
        <p className="text-sm text-muted-foreground">Read-only views of the school’s records, each printable and exportable.</p>
      </header>
      {groups.map((group) => (
        <section key={group} aria-labelledby={`reports-${group}`} className="flex flex-col gap-2">
          <h2 id={`reports-${group}`} className="text-sm font-semibold text-muted-foreground">
            {group}
          </h2>
          <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {visible
              .filter((report) => report.group === group)
              .map((report) => (
                <li key={report.title}>
                  <Link
                    to={hrefOf(report)}
                    className="flex h-full items-start justify-between gap-3 rounded-md border border-border bg-surface p-4 hover:bg-muted"
                  >
                    <span className="flex flex-col gap-1">
                      <span className="font-medium text-foreground">{report.title}</span>
                      <span className="text-sm text-muted-foreground">{report.description}</span>
                    </span>
                    <ChevronRight aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                  </Link>
                </li>
              ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
