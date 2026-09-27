import { useQueries } from '@tanstack/react-query';
import { create } from 'zustand';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { fetchDuplicates, usePupil } from '../api';
import { PupilsKeys, type PupilDto } from '../types';
import { useContacts } from './api';

/** The saved phones checked at most: a form has five contact slots, each with a phone and a WhatsApp number. */
const MAX_PHONES = 10;

/**
 * Which pupils the office has confirmed are different from the matches shown, keyed by the exact set of matches seen:
 * a new match clears the confirmation. Client state for the sitting, as the spec's tick is (it records no decision).
 */
const useAcknowledged = create<{ signatures: Record<string, string>; acknowledge: (pupilId: string, signature: string) => void }>((set) => ({
  signatures: {},
  acknowledge: (pupilId, signature) => set((state) => ({ signatures: { ...state.signatures, [pupilId]: signature } })),
}));

export interface PhoneDuplicates {
  matches: PupilDto[];
  /** Matches exist and the office has not ticked "I have checked, this is a different pupil" for them. */
  needsAcknowledgement: boolean;
  acknowledged: boolean;
  acknowledge: () => void;
}

/**
 * Spec 6.5.11 step 1's second half: once a contact phone is on a pending admission, any existing record with the same
 * surname and that phone (or the same name and birthday) is a candidate duplicate. Detection never blocks outright, but the
 * office must tick that it has checked before continuing. Runs only for staff who may use the duplicate search
 * (`pupil.create`, the privilege that starts an admission).
 */
export function usePhoneDuplicates(pupilId: string): PhoneDuplicates {
  const me = useMe();
  const pupil = usePupil(pupilId);
  const contacts = useContacts(pupilId);
  const record = pupil.data;
  const enabled = record?.status === 'Pending' && !!me.data && hasPrivilege(me.data, 'pupil.create');
  const phones = [
    ...new Set((contacts.data?.items ?? []).flatMap((item) => [item.phone, item.whatsappNumber]).filter((phone): phone is string => !!phone)),
  ].slice(0, MAX_PHONES);

  const results = useQueries({
    queries: phones.map((phone) => ({
      queryKey: [PupilsKeys.Duplicates, pupilId, phone],
      queryFn: () => fetchDuplicates(record?.surname ?? '', record?.firstName ?? '', record?.dateOfBirth ?? '', phone),
      enabled,
      staleTime: 30_000,
    })),
  });

  const found = new Map<string, PupilDto>();
  for (const result of results) for (const match of result.data ?? []) if (match.id !== pupilId) found.set(match.id, match);
  const matches = enabled ? [...found.values()] : [];
  const signature = matches.map((match) => match.id).sort().join(',');
  const acknowledgedSignature = useAcknowledged((state) => state.signatures[pupilId]);
  const acknowledgeFor = useAcknowledged((state) => state.acknowledge);
  const acknowledged = matches.length > 0 && acknowledgedSignature === signature;

  return {
    matches,
    needsAcknowledgement: matches.length > 0 && !acknowledged,
    acknowledged,
    acknowledge: () => acknowledgeFor(pupilId, signature),
  };
}
