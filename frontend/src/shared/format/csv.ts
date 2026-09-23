/** One cell, quoted when it holds a comma, quote or line break; a leading = + - @ is defused so a spreadsheet never runs it. */
function cell(value: string | number | boolean | null | undefined): string {
  const text = value === null || value === undefined ? '' : String(value);
  const safe = /^[=+\-@]/.test(text) ? `'${text}` : text;
  return /[",\r\n]/.test(safe) ? `"${safe.replace(/"/g, '""')}"` : safe;
}

/** One CSV line from its cells. */
export function csvLine(values: readonly (string | number | boolean | null | undefined)[]): string {
  return values.map(cell).join(',');
}

/** Lines joined with CRLF and a trailing line break, which is what spreadsheets expect. */
export function csvDocument(lines: readonly string[]): string {
  return `${lines.join('\r\n')}\r\n`;
}
