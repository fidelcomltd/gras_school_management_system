/** An ISO date (or date-time) as DD/MM/YYYY, the way the school writes dates; a dash when there is none. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const [year, month, day] = iso.slice(0, 10).split('-');
  return `${day ?? ''}/${month ?? ''}/${year ?? ''}`;
}
