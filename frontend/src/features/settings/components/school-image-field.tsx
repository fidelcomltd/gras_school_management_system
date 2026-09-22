import { useId } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { ApiError } from '@/lib/http';
import { useSchoolImageUrl, useUploadSchoolImage } from '../api-groups';

type SchoolImageDto = components['schemas']['SchoolImageDto'];

/**
 * The logo or the head teacher's signature (spec 6.2.3, 14): shows the current image and replaces it on upload. PNG or
 * JPEG only (SVG is refused server-side); the server strips metadata and makes the printed sizes.
 */
export function SchoolImageField({ kind, image, canEdit }: { kind: 'logo' | 'signature'; image: SchoolImageDto | null; canEdit: boolean }) {
  const inputId = useId();
  const upload = useUploadSchoolImage(kind);
  const url = useSchoolImageUrl(kind, image?.uploadedAt ?? null);
  const label = kind === 'logo' ? 'School logo' : "Head teacher's signature";

  return (
    <section aria-label={label} className="flex flex-col gap-2 rounded-md border border-border p-3">
      <h2 className="text-sm font-semibold text-foreground">{label}</h2>
      <div className="flex min-h-20 items-center gap-4">
        {url.data ? (
          <img src={url.data} alt={`Current ${label.toLowerCase()}`} className={kind === 'logo' ? 'size-20 object-contain' : 'h-12 max-w-48 object-contain'} />
        ) : (
          <span className="text-sm text-muted-foreground">{image ? 'Loading…' : 'None uploaded yet.'}</span>
        )}
        {image ? (
          <span className="text-xs text-muted-foreground">
            {image.width}×{image.height}px, uploaded by {image.uploadedByName}
          </span>
        ) : null}
      </div>
      <FormError message={upload.error instanceof ApiError ? upload.error.message : null} />
      {canEdit ? (
        <div className="flex items-center gap-2">
          <label htmlFor={inputId} className="text-sm text-foreground">
            {image ? 'Replace' : 'Upload'} {kind === 'logo' ? 'logo' : 'signature'} (PNG or JPEG{kind === 'logo' ? ', up to 2 MB' : ', up to 1 MB'})
          </label>
          <input
            id={inputId}
            type="file"
            accept="image/png,image/jpeg"
            disabled={upload.isPending}
            className="text-sm"
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) upload.mutate(file);
              event.target.value = '';
            }}
          />
          {upload.isPending ? <output className="text-sm text-muted-foreground">Uploading…</output> : null}
        </div>
      ) : null}
    </section>
  );
}
