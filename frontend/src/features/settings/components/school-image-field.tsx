import { Upload } from 'lucide-react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { FileButton } from '@/components/ui/file-button';
import { Spinner } from '@/components/ui/spinner';
import { ApiError } from '@/lib/http';
import { useSchoolImageUrl, useUploadSchoolImage } from '../api-groups';

type SchoolImageDto = components['schemas']['SchoolImageDto'];

/**
 * The logo or the head teacher's signature (spec 6.2.3, 14): shows the current image and replaces it on upload. PNG or
 * JPEG only (SVG is refused server-side); the server strips metadata and makes the printed sizes.
 */
export function SchoolImageField({ kind, image, canEdit }: { kind: 'logo' | 'signature'; image: SchoolImageDto | null; canEdit: boolean }) {
  const upload = useUploadSchoolImage(kind);
  const url = useSchoolImageUrl(kind, image?.uploadedAt ?? null);
  const label = kind === 'logo' ? 'School logo' : "Head teacher's signature";
  const action = `${image ? 'Replace' : 'Upload'} ${kind === 'logo' ? 'logo' : 'signature'}`;

  return (
    <section aria-label={label} className="flex flex-col gap-2 rounded-md border border-border p-3">
      <h2 className="text-sm font-semibold text-foreground">{label}</h2>
      <div className="flex min-h-20 items-center gap-4">
        {url.data ? (
          <img src={url.data} alt={`Current ${label.toLowerCase()}`} className={kind === 'logo' ? 'size-20 object-contain' : 'h-12 max-w-48 object-contain'} />
        ) : (
          url.isError ? (
            <button type="button" className="text-sm text-destructive underline" onClick={() => void url.refetch()}>
              The {kind === 'logo' ? 'logo' : 'signature'} could not be loaded. Retry
            </button>
          ) : image ? (
            <output className="flex items-center gap-2 text-sm text-muted-foreground">
              <Spinner />
              <span className="sr-only">Loading the {kind === 'logo' ? 'logo' : 'signature'}…</span>
            </output>
          ) : (
            <span className="text-sm text-muted-foreground">None uploaded yet.</span>
          )
        )}
        {image ? (
          <span className="text-xs text-muted-foreground">
            {image.width}×{image.height}px, uploaded by {image.uploadedByName}
          </span>
        ) : null}
      </div>
      <FormError message={upload.error instanceof ApiError ? upload.error.message : null} />
      {canEdit ? (
        <div className="flex flex-wrap items-center gap-3">
          <FileButton inputLabel={action} accept="image/png,image/jpeg" onFile={(file) => upload.mutate(file)} disabled={upload.isPending}>
            {upload.isPending ? <Spinner className="size-4" /> : <Upload aria-hidden="true" />}
            {upload.isPending ? 'Uploading…' : action}
          </FileButton>
          <span className="text-xs text-muted-foreground">PNG or JPEG{kind === 'logo' ? ', up to 2 MB' : ', up to 1 MB'}</span>
        </div>
      ) : null}
    </section>
  );
}
