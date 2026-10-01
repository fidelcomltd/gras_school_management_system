/** An ISO date (or date-time) as DD/MM/YYYY, the way the school writes dates; a dash when there is none. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const [year, month, day] = iso.slice(0, 10).split('-');
  return `${day ?? ''}/${month ?? ''}/${year ?? ''}`;
}

/** Today in Lagos (fixed UTC+1), as a date input wants it: YYYY-MM-DD. */
export function lagosToday(): string {
  return lagosDateOf(new Date().toISOString());
}

/** The Lagos calendar date (fixed UTC+1) of an ISO timestamp, as YYYY-MM-DD. */
export function lagosDateOf(iso: string): string {
  return new Date(Date.parse(iso) + 60 * 60 * 1000).toISOString().slice(0, 10);
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'June', 'July', 'Aug', 'Sept', 'Oct', 'Nov', 'Dec'];

/** An ISO timestamp on the school's clock (WAT, fixed UTC+1), as the time alone: "9:58pm WAT". */
export function schoolClock(iso: string): string {
  const at = new Date(Date.parse(iso) + 60 * 60 * 1000);
  const hours = at.getUTCHours();
  const minutes = String(at.getUTCMinutes()).padStart(2, '0');
  return `${hours % 12 === 0 ? 12 : hours % 12}:${minutes}${hours < 12 ? 'am' : 'pm'} WAT`;
}

/** An ISO timestamp as the school reads it, the same form as on its prints: "Sept 30, 2026, 9:58pm WAT". */
export function schoolDateTime(iso: string): string {
  const at = new Date(Date.parse(iso) + 60 * 60 * 1000);
  return `${MONTHS[at.getUTCMonth()]} ${at.getUTCDate()}, ${at.getUTCFullYear()}, ${schoolClock(iso)}`;
}
