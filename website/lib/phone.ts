/**
 * "+2348136475557" → "+234 813 647 5557". Nigerian numbers group as +234 XXX XXX XXXX;
 * anything else is returned unchanged rather than guessed at.
 */
export function formatPhone(e164: string): string {
  const ng = /^\+234(\d{3})(\d{3})(\d{4})$/.exec(e164)
  return ng ? `+234 ${ng[1]} ${ng[2]} ${ng[3]}` : e164
}
