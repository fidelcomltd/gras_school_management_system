const lagos = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Africa/Lagos',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

/** Spec 6.1.8's DD/MM/YYYY HH:MM, in Lagos time. */
export function formatLagosDateTime(utc: string): string {
  return lagos.format(new Date(utc)).replace(',', '');
}
