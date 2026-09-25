/** An ISO date (or date-time) as DD/MM/YYYY, the way the school writes dates; a dash when there is none. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const [year, month, day] = iso.slice(0, 10).split('-');
  return `${day ?? ''}/${month ?? ''}/${year ?? ''}`;
}

/** Today in Lagos (fixed UTC+1), as a date input wants it: YYYY-MM-DD. */
export function lagosToday(): string {
  return new Date(Date.now() + 60 * 60 * 1000).toISOString().slice(0, 10);
}
