import { useState } from 'react';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useCompleteness, useDocuments, useSaveDocument, type PupilDocumentDto, type PupilDocumentType } from './api';

const LABELS: Record<PupilDocumentType, string> = {
  BirthCertificate: 'Birth certificate',
  PassportPhotograph: 'Passport photograph',
  PreviousSchoolResult: 'Previous school result',
  TransferLetter: 'Transfer letter',
  Other: 'Other required document',
};

/** Section H (spec 6.5.8): what the office has received. Never blocks approval; tracked until it arrives. */
export function DocumentsPanel({ pupilId, canEdit }: { pupilId: string; canEdit: boolean }) {
  const documents = useDocuments(pupilId);
  if (documents.isPending) return <LoadingState label="Loading documents…" />;
  if (documents.isError) return <QueryErrorState error={documents.error} onRetry={() => void documents.refetch()} />;
  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted-foreground">Ticking a document records that the paper is on file. Nothing here stops an admission being approved.</p>
      <ul className="flex flex-col gap-2">
        {documents.data.items.map((item) => (
          <DocumentRow key={`${item.documentType}-${String(item.received)}-${item.remarks ?? ''}`} pupilId={pupilId} item={item} canEdit={canEdit} />
        ))}
      </ul>
    </div>
  );
}

function DocumentRow({ pupilId, item, canEdit }: { pupilId: string; item: PupilDocumentDto; canEdit: boolean }) {
  const save = useSaveDocument(pupilId);
  const [remarks, setRemarks] = useState(item.remarks ?? '');
  const [otherLabel, setOtherLabel] = useState(item.otherLabel ?? '');
  const isOther = item.documentType === 'Other';
  const label = isOther && item.otherLabel ? item.otherLabel : LABELS[item.documentType];
  const submit = (received: boolean) =>
    save.mutate({ documentType: item.documentType, received, receivedDate: null, remarks: remarks || null, otherLabel: otherLabel || null });

  return (
    <li className="flex flex-col gap-2 rounded-md border border-border p-3">
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2 text-sm font-medium text-foreground">
          <input type="checkbox" checked={item.received} disabled={!canEdit || save.isPending} onChange={(event) => submit(event.target.checked)} />
          {label}
        </label>
        <span className="text-xs text-muted-foreground">
          {item.received && item.receivedDate ? `Received ${item.receivedDate.split('-').reverse().join('/')}` : 'Outstanding'}
        </span>
      </div>
      {canEdit ? (
        <div className="flex flex-wrap items-center gap-2">
          {isOther ? (
            <input
              aria-label="Name of the other document"
              className="h-8 w-56 rounded-md border border-input bg-background px-2 text-sm"
              placeholder="Name of the document"
              value={otherLabel}
              onChange={(event) => setOtherLabel(event.target.value)}
            />
          ) : null}
          <input
            aria-label={`Remarks for ${label}`}
            className="h-8 w-72 rounded-md border border-input bg-background px-2 text-sm"
            placeholder="Remarks"
            value={remarks}
            onChange={(event) => setRemarks(event.target.value)}
          />
          <Button variant="ghost" size="sm" disabled={save.isPending} onClick={() => submit(item.received)}>
            Save remarks
          </Button>
        </div>
      ) : item.remarks ? (
        <span className="text-sm text-muted-foreground">{item.remarks}</span>
      ) : null}
      <FormError message={save.error instanceof ApiError ? save.error.message : null} />
    </li>
  );
}

/**
 * Spec 6.5.12, for a pending admission: what stops approval, by step, and how much of the chased set is present.
 */
export function CompletenessCard({ pupilId, onGo }: { pupilId: string; onGo: (step: number) => void }) {
  const completeness = useCompleteness(pupilId, true);
  if (completeness.isPending) return <LoadingState label="Checking the admission…" />;
  if (completeness.isError) return <QueryErrorState error={completeness.error} onRetry={() => void completeness.refetch()} />;
  const { blocking, chased, chasedPercent } = completeness.data;

  return (
    <section aria-labelledby="completeness-heading" className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4">
      <h2 id="completeness-heading" className="text-base font-semibold text-foreground">
        {blocking.length === 0 ? 'Ready to approve' : 'Before this admission can be approved'}
      </h2>
      {blocking.length > 0 ? (
        <ul className="flex flex-col gap-1 text-sm">
          {blocking.map((item) => (
            <li key={item.code} className="flex items-center gap-2">
              <span className="text-destructive">Step {Number(item.step)}:</span>
              <span className="text-foreground">{item.message}</span>
              <Button variant="link" size="sm" className="h-auto px-0" onClick={() => onGo(Number(item.step))}>
                Go
              </Button>
            </li>
          ))}
        </ul>
      ) : null}
      <p className="text-sm text-muted-foreground" title={chased.map((item) => item.message).join(' ')}>
        Record {Number(chasedPercent)}% complete{chased.length > 0 ? ` — still to chase: ${chased.map((item) => item.message.replace(/\.$/, '')).join(', ')}` : ''}.
      </p>
    </section>
  );
}
