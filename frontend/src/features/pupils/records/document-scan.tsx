import { Download, FileText, Paperclip, RefreshCw, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { FileButton } from '@/components/ui/file-button';
import { Spinner } from '@/components/ui/spinner';
import type { PupilDocumentDto } from './api';
import { downloadDocumentFile, useRemoveDocumentFile, useUploadDocumentFile } from './files-api';
import { errorText } from './format';

const KIND: Record<string, string> = { 'application/pdf': 'PDF', 'image/jpeg': 'JPEG', 'image/png': 'PNG' };

const ACCEPT = 'application/pdf,image/png,image/jpeg';

const size = (bytes: number) => (bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

/**
 * Spec 6.5.8: one optional scan per row (PDF, JPEG or PNG, up to 5 MB). A primary Attach scan button when there is none;
 * once attached, the file's own name (human ruling 2026-09-26) with download, replace and remove. Attaching ticks the row;
 * removing leaves the tick.
 */
export function DocumentScan({ pupilId, item, label, canEdit }: { pupilId: string; item: PupilDocumentDto; label: string; canEdit: boolean }) {
  const upload = useUploadDocumentFile(pupilId);
  const remove = useRemoveDocumentFile(pupilId);
  const [downloadError, setDownloadError] = useState<unknown>(null);
  const busy = upload.isPending || remove.isPending;
  const file = item.file;
  const attach = (chosen: File) => upload.mutate({ documentType: item.documentType, file: chosen });

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        {file ? (
          <>
            <span className="flex min-w-0 items-center gap-2 rounded-md bg-muted px-2 py-1">
              <FileText className="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
              <span className="max-w-64 truncate font-medium text-foreground" title={file.fileName ?? undefined}>
                {file.fileName ?? `${label} scan`}
              </span>
              <span className="shrink-0 text-xs text-muted-foreground">
                {KIND[file.contentType] ?? file.contentType} · {size(Number(file.sizeBytes))}
              </span>
            </span>
            <Button
              variant="ghost"
              size="sm"
              aria-label={`Download scan: ${label}`}
              disabled={busy}
              onClick={() => {
                setDownloadError(null);
                downloadDocumentFile(pupilId, item.documentType).catch(setDownloadError);
              }}
            >
              <Download aria-hidden="true" />
              Download
            </Button>
            {canEdit ? (
              <>
                <FileButton inputLabel={`Replace scan: ${label}`} buttonLabel={`Replace scan: ${label}`} accept={ACCEPT} onFile={attach} disabled={busy} variant="ghost">
                  <RefreshCw aria-hidden="true" />
                  Replace
                </FileButton>
                <Button variant="ghost" size="sm" aria-label={`Remove scan: ${label}`} disabled={busy} onClick={() => remove.mutate(item.documentType)}>
                  <Trash2 aria-hidden="true" />
                  Remove
                </Button>
              </>
            ) : null}
          </>
        ) : canEdit ? (
          <FileButton inputLabel={`Attach scan: ${label}`} buttonLabel={`Attach scan: ${label}`} accept={ACCEPT} onFile={attach} disabled={busy} variant="primary">
            <Paperclip aria-hidden="true" />
            Attach scan
          </FileButton>
        ) : (
          <span className="text-xs text-muted-foreground">No scan attached.</span>
        )}
        {busy ? (
          <output className="flex items-center gap-2 text-xs text-muted-foreground">
            <Spinner className="size-4" />
            {upload.isPending ? 'Uploading…' : 'Removing…'}
          </output>
        ) : null}
      </div>
      <FormError message={errorText(upload.error) ?? errorText(remove.error) ?? errorText(downloadError)} />
    </div>
  );
}
