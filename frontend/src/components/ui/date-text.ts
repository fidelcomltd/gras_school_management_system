/** Converting between the ISO dates the API speaks and the DD/MM/YYYY the school writes. */

export const ISO = /^(\d{4})-(\d{2})-(\d{2})$/;
const SCHOOL = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;

/** `YYYY-MM-DD` to `DD/MM/YYYY`; anything else to `''`. */
export function isoToSchool(iso: string | null | undefined): string {
  const match = ISO.exec(iso ?? '');
  return match ? `${match[3]}/${match[2]}/${match[1]}` : '';
}

/** `DD/MM/YYYY` (or a pasted `YYYY-MM-DD`) to ISO when it names a real calendar day; otherwise `null`. */
export function schoolToIso(text: string): string | null {
  const trimmed = text.trim();
  const iso = ISO.exec(trimmed);
  const school = SCHOOL.exec(trimmed);
  const [year, month, day] = iso ? [iso[1], iso[2], iso[3]] : school ? [school[3], school[2], school[1]] : [];
  if (!year || !month || !day) return null;
  const y = Number(year);
  const m = Number(month);
  const d = Number(day);
  const probe = new Date(Date.UTC(y, m - 1, d));
  if (probe.getUTCFullYear() !== y || probe.getUTCMonth() !== m - 1 || probe.getUTCDate() !== d) return null;
  return `${year}-${month.padStart(2, '0')}-${day.padStart(2, '0')}`;
}
