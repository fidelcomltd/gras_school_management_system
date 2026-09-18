import { AdmissionQueueList } from './components/admission-queue-list';

/**
 * `/admissions` (spec 6.5.15), gated `pupil.view` at the route
 * (`admissions-routes.tsx`). The office's entry point to finish a pending
 * application: approve into a named arm, or decline with a reason.
 */
export function AdmissionsQueueScreen() {
  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Admissions queue</h1>
        <p className="text-sm text-muted-foreground">
          Every pending applicant, with what is still missing before a decision can be made.
        </p>
      </header>

      <AdmissionQueueList />
    </div>
  );
}
