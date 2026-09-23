import { csvDocument, csvLine } from '@/shared/format/csv';
import type { PupilImportReport } from './api';

/**
 * The validation report as CSV (spec 6.5.13: "displayed on screen, downloadable as CSV"): one line per problem, one per
 * register match, and one per clean row, so the file accounts for every row of the upload.
 */
export function reportToCsv(report: PupilImportReport): string {
  const lines = [csvLine(['Sheet row', 'Surname', 'First name', 'Outcome', 'Column', 'Reason'])];
  for (const row of report.rows) {
    const who = [row.sheetRow, row.surname, row.firstName];
    if (row.outcome === 'Rejected') {
      for (const issue of row.errors) lines.push(csvLine([...who, 'Rejected', issue.column, issue.message]));
    } else if (row.registerMatches.length > 0) {
      for (const match of row.registerMatches) {
        const number = match.registrationNumber ?? 'no number yet';
        lines.push(csvLine([...who, 'Warning', '', `Already on the register: ${match.surname} ${match.firstName} (${number}, ${match.status})`]));
      }
    } else {
      lines.push(csvLine([...who, 'Accepted', '', '']));
    }
  }
  return csvDocument(lines);
}
