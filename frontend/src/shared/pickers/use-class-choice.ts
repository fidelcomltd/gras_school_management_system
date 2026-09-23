import { useState } from 'react';
import { useArms } from '@/features/arms/api';
import type { ArmDto } from '@/features/arms/types';
import { useMe } from '@/features/auth/api';
import { hasPrivilegeInArm } from '@/lib/auth/auth-session';

export interface ClassChoice {
  armId: string;
  arm: ArmDto | undefined;
  arms: ArmDto[];
  isPending: boolean;
  setArmId: (id: string) => void;
}

/**
 * The arm a results screen works on, from the chosen session's arms, narrowed to those `privilege` covers for the
 * signed-in admin (a class teacher sees only their own). Defaults to the first; the choice is client state.
 */
export function useClassChoice(sessionId: string, privilege: string): ClassChoice {
  const [choice, setChoice] = useState<string | null>(null);
  const me = useMe();
  const query = useArms({ sessionId, status: 'Active' });
  const arms = (sessionId ? (query.data?.pages.flatMap((page) => page.items) ?? []) : []).filter(
    (arm) => !!me.data && hasPrivilegeInArm(me.data, privilege, arm.id),
  );
  const armId = choice !== null && arms.some((arm) => arm.id === choice) ? choice : (arms[0]?.id ?? '');

  return {
    armId,
    arm: arms.find((arm) => arm.id === armId),
    arms,
    isPending: sessionId !== '' && (query.isPending || me.isPending),
    setArmId: setChoice,
  };
}
