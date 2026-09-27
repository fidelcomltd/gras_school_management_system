import { Printer } from 'lucide-react';
import { FormError } from '@/components/feedback/query-states';
import { Button, type ButtonProps } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useDownloadAdmissionSlip } from '../api';
import { errorText } from '../records/format';

/** Spec 6.5.11's "Print admission slip": the half-A4 slip with the issued number, as a PDF to print and file. */
export function PrintAdmissionSlip({ pupilId, variant = 'outline' }: { pupilId: string; variant?: ButtonProps['variant'] }) {
  const download = useDownloadAdmissionSlip();
  return (
    <div className="flex flex-col items-start gap-2">
      <Button type="button" variant={variant} size="sm" disabled={download.isPending} onClick={() => download.mutate(pupilId)}>
        {download.isPending ? <Spinner className="size-4" /> : <Printer aria-hidden="true" />}
        {download.isPending ? 'Preparing slip…' : 'Print admission slip'}
      </Button>
      <FormError message={errorText(download.error)} />
    </div>
  );
}
