import { useId, useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import type { PupilDocumentDto } from './api';
import { downloadDocumentFile, useRemoveDocumentFile, useUploadDocumentFile } from './files-api';
import { errorText } from './format';

const KIND: Record<string, string> = { 'application/pdf': 'PDF', 'image/jpeg': 'JPEG', 'image/png': 'PNG' };

const size = (bytes: number) => (bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

/** Spec 6.5.8: one optional scan per row (PDF, JPEG or PNG, up to 5 MB). Attaching ticks the row; removing leaves the tick. */
export function DocumentScan({ pupilId, item, label, canEdit }: { pupilId: string; item: PupilDocumentDto; label: string; canEdit: boolean }) {
  const inputId = useId();
  const upload = useUploadDocumentFile(pupilId);
  const remove = useRemoveDocumentFile(pupilId);
  const [downloadError, setDownloadError] = useState<unknown>(null);
  const busy = upload.isPending || remove.isPending;
  const file = item.file;

  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      {file ? (
        <>
          <span className="text-muted-foreground">
            Scan attached ({KIND[file.contentType] ?? file.contentType}, {size(Number(file.sizeBytes))})
          </span>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setDownloadError(null);
              downloadDocumentFile(pupilId, item.documentType).catch(setDownloadError);
            }}
          >
            Download
          </Button>
        </>
      ) : null}
      {canEdit ? (
        <>
          <label htmlFor={inputId} className="text-foreground">
            {file ? 'Replace scan' : 'Attach a scan'}
          </label>
          <input
            id={inputId}
            type="file"
            aria-label={`Scan of ${label}`}
            accept="application/pdf,image/png,image/jpeg"
            disabled={busy}
            className="text-sm"
            onChange={(event) => {
              const chosen = event.target.files?.[0];
              if (chosen) upload.mutate({ documentType: item.documentType, file: chosen });
              event.target.value = '';
            }}
          />
          {upload.isPending ? <output className="text-muted-foreground">Uploading…</output> : null}
          {file ? (
            <Button variant="ghost" size="sm" disabled={busy} onClick={() => remove.mutate(item.documentType)}>
              Remove scan
            </Button>
          ) : null}
        </>
      ) : null}
      <FormError message={errorText(upload.error) ?? errorText(remove.error) ?? errorText(downloadError)} />
    </div>
  );
}
