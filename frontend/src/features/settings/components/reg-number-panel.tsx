import { useId, useState } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { useRegNumberPreview, useUpdateAbbreviation, useUpdateRegNumber } from '../api-groups';

type SettingsDto = components['schemas']['SettingsDto'];
type Reset = components['schemas']['RegNumberSerialReset'];

const selectClass = 'h-10 rounded-md border border-input bg-background px-2 text-sm';

/**
 * Registration number format (spec 6.2.10) and the school abbreviation it starts with (6.2.11). Changing the abbreviation
 * only affects numbers issued from now on; it needs the word CHANGE typed and a reason, as the spec asks.
 */
export function RegNumberPanel({ settings, canEditFormat, canEditAbbreviation }: { settings: SettingsDto; canEditFormat: boolean; canEditAbbreviation: boolean }) {
  const format = settings.regNumber;
  const [separator, setSeparator] = useState(format.separator);
  const [width, setWidth] = useState(Number(format.serialWidth));
  const [reset, setReset] = useState<Reset>(format.serialReset);
  const [abbreviation, setAbbreviation] = useState(settings.abbreviation.abbreviation);
  const [confirm, setConfirm] = useState('');
  const [reason, setReason] = useState('');
  const preview = useRegNumberPreview(separator, width);
  const ids = { abbreviation: useId(), confirm: useId(), reason: useId() };
  const saveFormat = useUpdateRegNumber();
  const saveAbbreviation = useUpdateAbbreviation();
  const formatDirty = separator !== format.separator || width !== Number(format.serialWidth) || reset !== format.serialReset;
  const abbreviationDirty = abbreviation.trim().toUpperCase() !== settings.abbreviation.abbreviation;

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <section aria-label="Number format" className="flex flex-col gap-3">
        <h2 className="text-sm font-semibold text-foreground">Number format</h2>
        <div className="flex flex-wrap gap-4 text-sm">
          <label className="flex flex-col gap-1">
            Separator
            <select className={selectClass} value={separator} disabled={!canEditFormat} onChange={(e) => setSeparator(e.target.value)}>
              {['/', '-', '.'].map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1">
            Serial digits
            <select className={selectClass} value={width} disabled={!canEditFormat} onChange={(e) => setWidth(Number(e.target.value))}>
              {[3, 4, 5, 6].map((w) => (
                <option key={w} value={w}>{w}</option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1">
            Serial numbering
            <select className={selectClass} value={reset} disabled={!canEditFormat} onChange={(e) => setReset(e.target.value as Reset)}>
              <option value="PerYear">Restart each admission year</option>
              <option value="Continuous">Run on continuously</option>
            </select>
          </label>
        </div>
        <output className="text-sm text-muted-foreground">Next number will look like: <strong>{preview.data?.preview ?? '…'}</strong></output>
        <FormError message={saveFormat.error instanceof ApiError ? saveFormat.error.message : null} />
        {canEditFormat ? (
          <div>
            <Button
              disabled={!formatDirty || saveFormat.isPending}
              onClick={() => saveFormat.mutate({ separator, serialWidth: width, serialReset: reset, expectedVersion: format.versionNumber })}
            >
              {saveFormat.isPending ? 'Saving…' : 'Save format'}
            </Button>
          </div>
        ) : null}
      </section>

      <section aria-label="School abbreviation" className="flex flex-col gap-3">
        <h2 className="text-sm font-semibold text-foreground">School abbreviation</h2>
        <p className="text-sm text-muted-foreground">Numbers already issued keep their old abbreviation.</p>
        <label htmlFor={ids.abbreviation} className="text-sm">
          Abbreviation (2 to 8 letters)
        </label>
        <Input id={ids.abbreviation} value={abbreviation} disabled={!canEditAbbreviation} onChange={(e) => setAbbreviation(e.target.value)} className="max-w-40 uppercase" />
        {canEditAbbreviation && abbreviationDirty ? (
          <>
            <label htmlFor={ids.confirm} className="text-sm">
              Type CHANGE to confirm
            </label>
            <Input id={ids.confirm} value={confirm} onChange={(e) => setConfirm(e.target.value)} className="max-w-40" />
            <label htmlFor={ids.reason} className="text-sm">
              Reason
            </label>
            <Input id={ids.reason} value={reason} onChange={(e) => setReason(e.target.value)} />
          </>
        ) : null}
        <FormError message={saveAbbreviation.error instanceof ApiError ? saveAbbreviation.error.message : null} />
        {canEditAbbreviation ? (
          <div>
            <Button
              disabled={!abbreviationDirty || confirm !== 'CHANGE' || !reason.trim() || saveAbbreviation.isPending}
              onClick={() =>
                saveAbbreviation.mutate({
                  abbreviation: abbreviation.trim().toUpperCase(),
                  confirmationToken: confirm,
                  reason: reason.trim(),
                  expectedVersion: settings.abbreviation.versionNumber,
                })
              }
            >
              {saveAbbreviation.isPending ? 'Saving…' : 'Change abbreviation'}
            </Button>
          </div>
        ) : null}
      </section>
    </div>
  );
}
