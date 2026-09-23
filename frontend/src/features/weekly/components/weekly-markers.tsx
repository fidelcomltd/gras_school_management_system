import { useState } from 'react';
import type { WeeklyDayDto } from '../types';

/** Spec 6.10.6: a quiet marker beside a pupil with symptoms noted on two or more days of the week. */
export function IllnessMarker({ days }: { days: number }) {
  return days >= 2 ? (
    <span className="mt-0.5 inline-block rounded bg-accent/30 px-1.5 text-xs text-foreground">Unwell {days} days</span>
  ) : null;
}

/** Spec 6.10.10: another account's edit within the last hour shows its name and time beneath the cell. */
export function EditedByOther({ day, accountId }: { day: WeeklyDayDto; accountId: string | undefined }) {
  const [now] = useState(() => Date.now());
  if (!day.lastEditedAt || !day.lastEditedBy || day.lastEditedById === accountId) return null;
  const at = new Date(day.lastEditedAt);
  if (now - at.getTime() > 60 * 60 * 1000) return null;
  const time = at.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', timeZone: 'Africa/Lagos' });
  return <span className="block text-xs text-muted-foreground">Edited by {day.lastEditedBy} at {time}</span>;
}
