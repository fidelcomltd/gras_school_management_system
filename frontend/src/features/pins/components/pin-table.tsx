import { Button } from '@/components/ui/button';
import type { PinSummaryDto } from '../types';

/** Each pin by its first four characters (the value itself is never shown after printing), with its state and uses. */
export function PinTable({
  pins,
  canRevoke,
  onRevoke,
  onReinstate,
}: {
  pins: PinSummaryDto[];
  canRevoke: boolean;
  onRevoke: (pin: PinSummaryDto) => void;
  onReinstate: (pin: PinSummaryDto) => void;
}) {
  return (
    <div className="overflow-x-auto rounded-md border border-border">
      <table className="w-full text-sm">
        <thead className="bg-muted text-muted-foreground">
          <tr>
            <th scope="col" className="px-3 py-2 text-left font-medium">Pin</th>
            <th scope="col" className="px-3 py-2 text-left font-medium">State</th>
            <th scope="col" className="px-3 py-2 font-medium">Uses</th>
            <th scope="col" className="px-3 py-2 font-medium">Pupils opened</th>
            <th scope="col" className="px-3 py-2">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {pins.map((pin) => (
            <tr key={pin.id} className="border-t border-border">
              <th scope="row" className="px-3 py-1.5 text-left font-mono font-normal text-foreground">{pin.prefix}…</th>
              <td className="px-3 py-1.5 text-foreground">
                {pin.state}
                {pin.stateReason ? <span className="block text-xs text-muted-foreground">{pin.stateReason}</span> : null}
              </td>
              <td className="px-3 py-1.5 text-center text-muted-foreground">
                {pin.useCount} of {pin.maxUses}
              </td>
              <td className="px-3 py-1.5 text-center text-muted-foreground">{pin.distinctPupilCount}</td>
              <td className="px-3 py-1.5 text-right">
                {canRevoke && (pin.state === 'Unused' || pin.state === 'Active') ? (
                  <Button size="sm" variant="ghost" onClick={() => onRevoke(pin)}>
                    Revoke <span className="sr-only">pin {pin.prefix}</span>
                  </Button>
                ) : null}
                {canRevoke && (pin.state === 'Suspended' || pin.state === 'Revoked') ? (
                  <Button size="sm" variant="ghost" onClick={() => onReinstate(pin)}>
                    Reinstate <span className="sr-only">pin {pin.prefix}</span>
                  </Button>
                ) : null}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
