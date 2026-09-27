import { UserRound } from 'lucide-react';
import { cn } from '@/lib/utils/cn';
import { usePupilPhotoUrl } from '../records/files-api';

/** Spec 6.5.15's photograph column: the 96 pixel thumbnail, or a placeholder; fetched only for a pupil who has one. */
export function PupilThumbnail({ pupilId, photoUpdatedAtUtc }: { pupilId: string; photoUpdatedAtUtc: string | null | undefined }) {
  const photo = usePupilPhotoUrl(pupilId, photoUpdatedAtUtc, true);
  return (
    <span className="grid size-9 shrink-0 place-items-center overflow-hidden rounded-full bg-muted">
      {photo.data ? <img src={photo.data} alt="" className="size-full object-cover" /> : <UserRound className="size-4 text-muted-foreground" aria-hidden="true" />}
    </span>
  );
}

/** Spec 6.5.15's completeness column: the chased-set percentage, coloured so a gap is seen at a glance. */
export function CompletenessBadge({ percent }: { percent: number }) {
  return (
    <span
      title="Record completeness"
      className={cn(
        'rounded-full px-2 py-0.5 text-xs font-medium',
        percent >= 100 ? 'bg-success-subtle text-success' : percent >= 60 ? 'bg-warning-subtle text-warning' : 'bg-destructive-subtle text-destructive',
      )}
    >
      {percent}% complete
    </span>
  );
}
