import type { AuditEventDto } from './api';

function lagosTime(iso: string): string {
  return new Date(iso).toLocaleString('en-GB', { timeZone: 'Africa/Lagos', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function pretty(json: string | null | undefined): string | null {
  if (!json) return null;
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    return json;
  }
}

/** One audit event; the before and after values open on demand. */
export function AuditRow({ event }: { event: AuditEventDto }) {
  const before = pretty(event.beforeJson);
  const after = pretty(event.afterJson);

  return (
    <li className="rounded-md border border-border bg-surface px-4 py-3 text-sm">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <span className="font-medium text-foreground">
          {event.action}
          {event.outcome === 'Rejected' ? <span className="ml-2 text-destructive">refused</span> : null}
        </span>
        <time dateTime={event.occurredAtUtc} className="text-muted-foreground">
          {lagosTime(event.occurredAtUtc)}
        </time>
      </div>
      <p className="text-muted-foreground">
        {event.actorLabel ?? 'System'}
        {event.entityType ? ` · ${event.entityType}${event.entityId ? ` ${event.entityId}` : ''}` : ''}
        {event.sourceIp ? ` · ${event.sourceIp}` : ''}
      </p>
      {event.reason ? <p className="text-foreground">Reason: {event.reason}</p> : null}
      {before || after ? (
        <details className="mt-2">
          <summary className="cursor-pointer text-primary">Details</summary>
          <div className="mt-2 grid gap-3 sm:grid-cols-2">
            {before ? (
              <div>
                <p className="text-xs font-medium text-muted-foreground">Before</p>
                <pre className="overflow-x-auto rounded bg-muted p-2 text-xs">{before}</pre>
              </div>
            ) : null}
            {after ? (
              <div>
                <p className="text-xs font-medium text-muted-foreground">After</p>
                <pre className="overflow-x-auto rounded bg-muted p-2 text-xs">{after}</pre>
              </div>
            ) : null}
          </div>
        </details>
      ) : null}
    </li>
  );
}
