import { TriangleAlert } from 'lucide-react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { pupilName } from '../types';
import { usePhoneDuplicates } from './use-phone-duplicates';

/** The candidates, each openable, and the tick that lets the office carry on. */
export function PhoneDuplicateWarning({ pupilId }: { pupilId: string }) {
  const duplicates = usePhoneDuplicates(pupilId);
  if (duplicates.matches.length === 0) return null;

  return (
    <output className="flex gap-3 rounded-md border border-warning/40 bg-warning-subtle p-3 text-sm">
      <TriangleAlert className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden="true" />
      <div className="flex flex-col gap-2">
        <p className="font-medium text-foreground">A record with the same surname shares a contact phone or name and birthday</p>
        <p className="text-muted-foreground">It may be a returning pupil, who is reactivated rather than admitted again.</p>
        <ul className="flex flex-col gap-0.5">
          {duplicates.matches.map((match) => (
            <li key={match.id}>
              <Link to={paths.pupilDetail(match.id)} className="text-primary hover:underline">
                {pupilName(match)}
              </Link>{' '}
              <span className="text-muted-foreground">({match.registrationNumber ?? match.status})</span>
            </li>
          ))}
        </ul>
        <label className="flex items-center gap-2 font-medium text-foreground">
          <input type="checkbox" checked={duplicates.acknowledged} disabled={duplicates.acknowledged} onChange={duplicates.acknowledge} />
          I have checked, this is a different pupil
        </label>
      </div>
    </output>
  );
}
