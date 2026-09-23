import { Link, useParams, useSearchParams } from 'react-router';
import { paths } from '@/app/router/paths';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { usePupil } from '@/features/pupils/api';
import { PupilInfoForm } from '@/features/pupils/components/edit-pupil-dialog';
import { useCompleteness } from '@/features/pupils/records/api';
import { CollectionPanel } from '@/features/pupils/records/collection-panel';
import { ContactsPanel } from '@/features/pupils/records/contacts-panel';
import { DocumentsPanel } from '@/features/pupils/records/documents-panel';
import { HealthPanel } from '@/features/pupils/records/health-panel';
import { pupilName } from '@/features/pupils/types';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { cn } from '@/lib/utils/cn';
import { AdmittedNotice, DeclarationStep, OtherInformationStep } from './flow-steps';
import { ReviewStep } from './review-step';

/** Spec 6.5.11's nine steps. Step 1 is the create dialog; this screen resumes from step 2. */
const STEPS = [
  'Start',
  'Pupil information',
  'Parents and contacts',
  'Collection',
  'Health and safety',
  'Other information',
  'Documents',
  'Declaration',
  'Review and approve',
] as const;

/**
 * `/admissions/:id?step=n` (spec 6.5.11): a pending admission, one step at a time. The step lives in the URL, so a
 * half-finished admission resumes where the office left it; with no step given it opens at the first step still
 * blocking approval. Each section saves on its own button, so nothing typed is lost to a missing birth certificate.
 */
export function AdmissionFlowScreen() {
  const { id = '' } = useParams();
  const [params, setParams] = useSearchParams();
  const pupil = usePupil(id);
  const pending = pupil.data?.status === 'Pending';
  const completeness = useCompleteness(id, pending);
  const me = useMe();
  const can = (privilege: string) => !!me.data && hasPrivilege(me.data, privilege);

  if (pupil.isPending) return <LoadingState label="Loading the admission…" />;
  if (pupil.isError) return <QueryErrorState error={pupil.error} onRetry={() => void pupil.refetch()} />;
  const record = pupil.data;
  if (!pending) return <AdmittedNotice pupil={record} />;

  const requested = Number(params.get('step'));
  const valid = requested >= 2 && requested <= 9;
  if (!valid && completeness.isPending) return <LoadingState label="Finding where this admission stopped…" />;
  const blockingSteps = new Set((completeness.data?.blocking ?? []).map((item) => Number(item.step)));
  const firstBlocking = [...blockingSteps].sort((a, b) => a - b)[0];
  const step = valid ? requested : Math.max(2, firstBlocking ?? 9);
  const go = (next: number) => setParams({ step: String(next) });

  return (
    <div className="flex flex-col gap-6">
      <Link to={paths.admissions} className="text-sm text-primary hover:underline">
        ← Admissions queue
      </Link>
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Admission: {pupilName(record)}</h1>
        <p className="text-sm text-muted-foreground">Pending. Each section saves on its own button; you can leave and come back.</p>
      </header>

      <div className="grid gap-6 lg:grid-cols-[14rem_1fr]">
        <nav aria-label="Admission steps">
          <ol className="flex flex-col gap-1">
            {STEPS.map((title, index) => {
              const number = index + 1;
              return (
                <li key={title}>
                  <button
                    type="button"
                    disabled={number === 1}
                    aria-current={number === step ? 'step' : undefined}
                    onClick={() => go(number)}
                    className={cn(
                      'flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm',
                      number === step ? 'bg-primary/10 font-medium text-primary' : 'text-foreground hover:bg-muted disabled:text-muted-foreground',
                    )}
                  >
                    <span className="w-5 text-muted-foreground">{number}.</span>
                    <span className="flex-1">{title}</span>
                    {blockingSteps.has(number) ? <span className="text-xs text-destructive">needed</span> : null}
                    {number === 1 ? <span className="text-xs text-muted-foreground">done</span> : null}
                  </button>
                </li>
              );
            })}
          </ol>
        </nav>

        <section aria-labelledby="step-heading" className="flex min-w-0 flex-col gap-4">
          <h2 id="step-heading" className="text-lg font-semibold text-foreground">
            Step {step}: {STEPS[step - 1]}
          </h2>
          {step === 2 ? <PupilInfoForm pupil={record} onSaved={() => go(3)} submitLabel="Save and continue" /> : null}
          {step === 3 ? <ContactsPanel pupilId={id} canEdit={can('contact.update')} /> : null}
          {step === 4 ? (
            <CollectionPanel
              pupilId={id}
              canEdit={can('contact.update')}
              canSeeBarred={can('pupil.safeguarding.view')}
              canEditBarred={can('pupil.safeguarding.update')}
            />
          ) : null}
          {step === 5 && can('pupil.safeguarding.view') ? <HealthPanel pupilId={id} canEdit={can('pupil.safeguarding.update')} /> : null}
          {step === 5 && !can('pupil.safeguarding.view') ? (
            <p className="text-sm text-muted-foreground">Health and safety needs the safeguarding privilege. Ask the head teacher to complete this step.</p>
          ) : null}
          {step === 6 ? <OtherInformationStep pupil={record} onSaved={() => go(7)} /> : null}
          {step === 7 ? <DocumentsPanel pupilId={id} canEdit={can('pupil.document.manage')} /> : null}
          {step === 8 ? <DeclarationStep pupilId={id} onSaved={() => go(9)} /> : null}
          {step === 9 ? <ReviewStep pupil={record} onGo={go} /> : null}

          <div className="flex justify-between border-t border-border pt-4">
            <Button variant="ghost" disabled={step <= 2} onClick={() => go(step - 1)}>
              Back
            </Button>
            {step < 9 ? (
              <Button variant="outline" onClick={() => go(step + 1)}>
                Next: {STEPS[step]}
              </Button>
            ) : null}
          </div>
        </section>
      </div>
    </div>
  );
}
