import { useState } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { useCreateRemarkTemplate, useDeleteRemarkTemplate } from '../api-records';

type RemarkTemplateDto = components['schemas']['RemarkTemplateDto'];

/** The saved phrases for one remark kind (ruling H): add a new one, remove an old one. */
export function RemarkPhrases({ kind, phrases }: { kind: 'ClassTeacher' | 'HeadTeacher'; phrases: RemarkTemplateDto[] }) {
  const create = useCreateRemarkTemplate();
  const remove = useDeleteRemarkTemplate(kind);
  const [text, setText] = useState('');
  const error = create.error ?? remove.error;

  return (
    <details className="rounded-md border border-border p-3 text-sm">
      <summary className="cursor-pointer font-medium text-foreground">Saved phrases ({phrases.length})</summary>
      <div className="mt-3 flex flex-col gap-3">
        <FormError message={error instanceof ApiError ? error.message : null} />
        <ul className="flex flex-col gap-1">
          {phrases.map((phrase) => (
            <li key={phrase.id} className="flex items-center justify-between gap-2">
              <span className="text-foreground">{phrase.text}</span>
              <Button size="sm" variant="ghost" disabled={remove.isPending} onClick={() => remove.mutate(phrase.id)}>
                Remove <span className="sr-only">{phrase.text}</span>
              </Button>
            </li>
          ))}
        </ul>
        <form
          className="flex gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            if (text.trim()) create.mutate({ kind, text: text.trim() }, { onSuccess: () => setText('') });
          }}
        >
          <Input aria-label="New phrase" placeholder="A new phrase" value={text} onChange={(event) => setText(event.target.value)} />
          <Button type="submit" variant="outline" disabled={!text.trim() || create.isPending}>
            Add
          </Button>
        </form>
      </div>
    </details>
  );
}
