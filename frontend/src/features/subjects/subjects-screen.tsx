import { useState } from 'react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useDeleteSubject, useSubjects } from './api';
import { SubjectDialog } from './components/subject-dialog';
import type { SubjectDto } from './types';

/** `/subjects` — the subject catalogue (spec 6.6.3); which levels take which subject is on the mapping screen. */
export function SubjectsScreen() {
  const subjects = useSubjects();
  const remove = useDeleteSubject();
  const me = useMe();
  const [editing, setEditing] = useState<SubjectDto | 'new' | null>(null);
  const can = (privilege: string) => !!me.data && hasPrivilege(me.data, privilege);

  if (subjects.isPending) return <LoadingState label="Loading subjects…" />;
  if (subjects.isError) return <QueryErrorState error={subjects.error} onRetry={() => void subjects.refetch()} />;

  const items = subjects.data.pages.flatMap((page) => page.items);

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">Subjects</h1>
          <p className="text-sm text-muted-foreground">
            The subjects the school teaches. <Link className="text-primary hover:underline" to={paths.subjectMapping}>Map them to classes</Link> for each term.
          </p>
        </div>
        {can('subject.create') ? <Button onClick={() => setEditing('new')}>New subject</Button> : null}
      </header>

      <FormError message={remove.error instanceof ApiError ? remove.error.message : null} />

      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No subjects yet.</p>
      ) : (
        <table className="w-full text-left text-sm">
          <thead className="text-muted-foreground">
            <tr>
              <th className="py-2 font-medium">Subject</th>
              <th className="py-2 font-medium">Code</th>
              <th className="py-2 font-medium">Status</th>
              <th className="py-2 font-medium">Classes</th>
              <th className="py-2">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((subject) => (
              <tr key={subject.id} className="border-t border-border">
                <td className="py-2 font-medium text-foreground">{subject.name}</td>
                <td className="py-2 text-muted-foreground">{subject.code ?? '—'}</td>
                <td className="py-2 text-muted-foreground">{subject.status}</td>
                <td className="py-2 text-muted-foreground">{subject.mappedLevelCount ?? 0}</td>
                <td className="flex justify-end gap-2 py-2">
                  {can('subject.update') ? (
                    <Button size="sm" variant="outline" onClick={() => setEditing(subject)}>
                      Edit <span className="sr-only">{subject.name}</span>
                    </Button>
                  ) : null}
                  {can('subject.delete') ? (
                    <Button size="sm" variant="ghost" disabled={remove.isPending} onClick={() => remove.mutate(subject.id)}>
                      Delete <span className="sr-only">{subject.name}</span>
                    </Button>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {editing ? (
        <SubjectDialog
          subject={editing === 'new' ? undefined : editing}
          canDeactivate={can('subject.deactivate')}
          onClose={() => setEditing(null)}
        />
      ) : null}
    </div>
  );
}
