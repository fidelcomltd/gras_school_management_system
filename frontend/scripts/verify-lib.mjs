// The pieces of `npm run verify` (TASK-0056) that can be tested without running it: the step list and how a step's
// output is read. verify.mjs runs them; verify.test.mjs and src/test/gate-scripts.test.ts check them.

/** The verify steps, in order: cheapest first, stop at the first failure. */
export const DEFAULT_STEPS = [
  { name: 'Typecheck', command: 'npm run typecheck' },
  { name: 'Lint', command: 'npm run lint' },
  { name: 'Tests', command: 'npm run test' },
  { name: 'Script tests', command: 'npm run test:scripts' },
  { name: 'Build', command: 'npm run build' },
];

/** Error lines: type errors, lint errors, failed tests, build errors. Each once, in order. */
export function failingLines(output) {
  const seen = new Set();
  return output
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => /(error TS\d+)|(\berror\b\s*[\w-]+\()|(^FAIL\s)|(^×\s)|(\berror\b:)/i.test(line) && !seen.has(line) && seen.add(line));
}

/** The vitest totals, e.g. "Test Files  86 passed (86)" and "Tests  499 passed (499)". */
export function testTotals(output) {
  return output
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => /^(Test Files|Tests)\s{2,}/.test(line));
}

/** The summary file's text: PASS or FAIL first, then outcomes, totals and the failing lines. */
export function summaryText({ failed, abortReason, outcomes = [], totals = [], failing = [] }) {
  const lines = [failed || abortReason ? 'FAIL' : 'PASS'];
  if (abortReason) lines.push(`ABORTED: ${abortReason}`);
  lines.push(...outcomes, ...totals);
  if (failing.length > 0) lines.push('FAILING LINES:', ...failing.map((line) => `  ${line}`));
  return `${lines.join('\n')}\n`;
}
