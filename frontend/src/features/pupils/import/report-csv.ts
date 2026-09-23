import type { PupilImportReport } from './api';

/** One cell, quoted when it holds a comma, quote or line break; a leading = + - @ is defused so a spreadsheet never runs it. */
function cell(value: string | number | null | undefined): string {
  const text = value === null || value === undefined ? '' : String(value);
  const safe = /^[=+\-@]/.test(text) ? `'${text}` : text;
  return /[",\r\n]/.test(safe) ? `"${safe.replace(/"/g, '""')}"` : safe;
}

/**
 * The validation report as CSV (spec 6.5.13: "displayed on screen, downloadable as CSV"): one line per problem, one per
 * register match, and one per clean row, so the file accounts for every row of the upload.
 */
export function reportToCsv(report: PupilImportReport): string {
  const lines = [['Sheet row', 'Surname', 'First name', 'Outcome', 'Column', 'Reason'].join(',')];
  for (const row of report.rows) {
    const who = [cell(row.sheetRow), cell(row.surname), cell(row.firstName)];
    if (row.outcome === 'Rejected') {
      for (const issue of row.errors) lines.push([...who, 'Rejected', cell(issue.column), cell(issue.message)].join(','));
    } else if (row.registerMatches.length > 0) {
      for (const match of row.registerMatches) {
        const number = match.registrationNumber ?? 'no number yet';
        lines.push([...who, 'Warning', '', cell(`Already on the register: ${match.surname} ${match.firstName} (${number}, ${match.status})`)].join(','));
      }
    } else {
      lines.push([...who, 'Accepted', '', ''].join(','));
    }
  }
  return `${lines.join('\r\n')}\r\n`;
}
