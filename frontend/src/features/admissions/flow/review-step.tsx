import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { useMe } from '@/features/auth/api';
import { useCompleteness } from '@/features/pupils/records/api';
import { CompletenessCard } from '@/features/pupils/records/documents-panel';
import { pupilName, type PupilDto } from '@/features/pupils/types';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { formatDate } from '@/shared/format/date';
import { useAdmissionRecord } from '../api';
import { ApproveAdmissionForm } from '../components/approve-admission-form';
import type { AdmissionRecordDto } from '../types';

const HEALTH_UNANSWERED = 'health.unanswered';

/**
 * Step 9 (spec 6.5.11): what is still missing, each item linking to its step; a read-only summary of sections A, B and
 * I; and approval once nothing but the assessment outcome (asked for here) is left. When the parent declined the health
 * questions, a holder of `pupil.admission.override` may approve with a reason (spec 6.5.16).
 */
export function ReviewStep({ pupil, onGo }: { pupil: PupilDto; onGo: (step: number) => void }) {
  const me = useMe();
  const completeness = useCompleteness(pupil.id, true);
  const record = useAdmissionRecord(pupil.id);
  const can = (privilege: string) => !!me.data && hasPrivilege(me.data, privilege);

  if (completeness.isPending || record.isPending) return <LoadingState label="Checking the admission…" />;
  if (completeness.isError) return <QueryErrorState error={completeness.error} onRetry={() => void completeness.refetch()} />;
  if (record.isError) return <QueryErrorState error={record.error} onRetry={() => void record.refetch()} />;

  const canOverride = can('pupil.admission.override');
  const healthUnanswered = completeness.data.blocking.some((item) => item.code === HEALTH_UNANSWERED);
  // The assessment outcome (step 9) is entered in the approve form itself, so it never hides the form.
  const stillBlocking = completeness.data.blocking.filter(
    (item) => Number(item.step) !== 9 && !(canOverride && item.code === HEALTH_UNANSWERED),
  );

  return (
    <div className="flex flex-col gap-5">
      <CompletenessCard pupilId={pupil.id} onGo={onGo} />
      <Summary pupil={pupil} record={record.data} />
      {!can('pupil.admission.approve') ? (
        <p className="text-sm text-muted-foreground">Approval needs the admission approval privilege.</p>
      ) : stillBlocking.length > 0 ? (
        <p className="text-sm text-muted-foreground">Approval opens here once the steps marked above are complete.</p>
      ) : (
        <section aria-labelledby="approve-heading" className="flex max-w-xl flex-col gap-3 rounded-lg border border-border p-4">
          <h3 id="approve-heading" className="text-base font-semibold text-foreground">
            Approve the admission
          </h3>
          <ApproveAdmissionForm
            key={String(canOverride && healthUnanswered)}
            pupil={pupil}
            record={record.data}
            onClose={() => onGo(9)}
            healthOverride={canOverride && healthUnanswered}
          />
        </section>
      )}
    </div>
  );
}

function Summary({ pupil, record }: { pupil: PupilDto; record: AdmissionRecordDto }) {
  const rows: [string, string][] = [
    ['Name', pupilName(pupil)],
    ['Sex', pupil.sex],
    ['Date of birth', `${formatDate(pupil.dateOfBirth)} (age ${pupil.ageYears})`],
    ['State and LGA', `${pupil.stateOfOrigin}, ${pupil.lga}`],
    ['Home address', pupil.homeAddress],
    ['Class applied for', record.classAdmittedIntoName ?? '—'],
    ['Admission type', record.admissionType],
    ['Date admitted', formatDate(record.dateAdmitted)],
    ['Assessment required', record.assessmentRequired ? 'Yes' : 'No'],
    ['Declaration', record.declarationSigned ? `Signed by ${record.declarationName ?? '—'} on ${formatDate(record.declarationDate)}` : 'Not signed'],
  ];
  return (
    <dl className="grid gap-x-6 gap-y-2 rounded-md border border-border bg-surface p-4 text-sm sm:grid-cols-[max-content_1fr]">
      {rows.map(([label, value]) => (
        <div key={label} className="contents">
          <dt className="text-muted-foreground">{label}</dt>
          <dd className="text-foreground">{value}</dd>
        </div>
      ))}
    </dl>
  );
}
