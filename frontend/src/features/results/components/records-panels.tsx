import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { useAttendance, useDevelopmentRatings, useRemarks, useTraitRatings } from '../api-records';
import { AttendanceEditor } from './attendance-editor';
import { DevelopmentRatingsEditor } from './development-ratings-editor';
import { RemarksEditor } from './remarks-editor';
import { TraitRatingsEditor } from './trait-ratings-editor';

/** Each panel loads its own sheet and keys its editor by version, so a save starts from the server's values. */

export function RatingsPanel({ armId, termId, ratesTraits, canEdit }: { armId: string; termId: string; ratesTraits: boolean; canEdit: boolean }) {
  const traits = useTraitRatings(armId, termId, ratesTraits);
  const development = useDevelopmentRatings(armId, termId, !ratesTraits);
  const query = ratesTraits ? traits : development;

  if (query.isPending) return <LoadingState label="Loading ratings…" />;
  if (query.isError) return <QueryErrorState error={query.error} onRetry={() => void query.refetch()} />;
  if (ratesTraits && traits.data) {
    return <TraitRatingsEditor key={traits.data.version ?? `${armId}:${termId}`} sheet={traits.data} canEdit={canEdit} />;
  }
  return development.data ? (
    <DevelopmentRatingsEditor key={development.data.version ?? `${armId}:${termId}`} sheet={development.data} canEdit={canEdit} />
  ) : null;
}

export function AttendancePanel({ armId, termId, canEdit }: { armId: string; termId: string; canEdit: boolean }) {
  const sheet = useAttendance(armId, termId);
  if (sheet.isPending) return <LoadingState label="Loading attendance…" />;
  if (sheet.isError) return <QueryErrorState error={sheet.error} onRetry={() => void sheet.refetch()} />;
  return <AttendanceEditor key={sheet.data.version ?? `${armId}:${termId}`} sheet={sheet.data} canEdit={canEdit} />;
}

export function RemarksPanel({ kind, armId, termId, canEdit }: { kind: 'ClassTeacher' | 'HeadTeacher'; armId: string; termId: string; canEdit: boolean }) {
  const sheet = useRemarks(kind === 'ClassTeacher' ? 'class-teacher-remarks' : 'head-teacher-remarks', armId, termId);
  if (sheet.isPending) return <LoadingState label="Loading remarks…" />;
  if (sheet.isError) return <QueryErrorState error={sheet.error} onRetry={() => void sheet.refetch()} />;
  return <RemarksEditor key={sheet.data.version ?? `${kind}:${armId}:${termId}`} kind={kind} sheet={sheet.data} canEdit={canEdit} />;
}
