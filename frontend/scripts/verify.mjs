// `npm run verify` (TASK-0056): the same steps as before, in the same order, stopping at the first failure, and a
// machine-readable summary in artifacts/gate-summary.txt on every exit, so a report quotes one small file instead of
// the log. First line PASS or FAIL; each step's outcome (SKIPPED-NOT-RUN after a failure); the test totals; the
// failing lines in full. The exit code is the failing step's, so writing the file never hides a failure.
//
// VERIFY_STEPS (a JSON array of { name, command }) and VERIFY_SUMMARY_PATH exist for scripts/verify.test.mjs only.
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const summaryPath = process.env.VERIFY_SUMMARY_PATH ?? resolve(root, 'artifacts/gate-summary.txt');
const steps = process.env.VERIFY_STEPS
  ? JSON.parse(process.env.VERIFY_STEPS)
  : [
      { name: 'Typecheck', command: 'npm run typecheck' },
      { name: 'Lint', command: 'npm run lint' },
      { name: 'Tests', command: 'npm run test' },
      { name: 'Script tests', command: 'npm run test:scripts' },
      { name: 'Build', command: 'npm run build' },
    ];

const ansi = /\u001b\[[0-9;]*m/g;

/** Runs one step, streaming its output as it arrives and keeping a copy. */
function run(command) {
  return new Promise((resolveRun) => {
    const child = spawn(command, { cwd: root, shell: true, env: { ...process.env, FORCE_COLOR: process.stdout.isTTY ? '1' : '0' } });
    let output = '';
    child.stdout.on('data', (chunk) => {
      process.stdout.write(chunk);
      output += chunk;
    });
    child.stderr.on('data', (chunk) => {
      process.stderr.write(chunk);
      output += chunk;
    });
    child.on('close', (code) => resolveRun({ code: code ?? 1, output: output.replace(ansi, '') }));
  });
}

/** Error lines: type errors, lint errors, failed tests, build errors. Each once, in order. */
export function failingLines(output) {
  const seen = new Set();
  return output
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => /(error TS\d+)|(\berror\b\s*[\w-]+\()|(^FAIL\s)|(^×\s)|(\berror\b:)/i.test(line) && !seen.has(line) && seen.add(line));
}

/** The vitest totals, e.g. "Test Files  86 passed (86)" and "Tests  499 passed (499)". */
function testTotals(output) {
  return output
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => /^(Test Files|Tests)\s{2,}/.test(line));
}

const outcomes = [];
const totals = [];
const failing = [];
let exitCode = 0;

for (const step of steps) {
  if (exitCode !== 0) {
    outcomes.push(`SKIPPED-NOT-RUN: ${step.name}`);
    continue;
  }

  const { code, output } = await run(step.command);
  totals.push(...testTotals(output));
  if (code === 0) {
    outcomes.push(`PASS: ${step.name}`);
  } else {
    exitCode = code;
    outcomes.push(`FAIL: ${step.name} -- exit code ${code}`);
    failing.push(...failingLines(output).map((line) => `[${step.name}] ${line}`));
  }
}

const lines = [exitCode === 0 ? 'PASS' : 'FAIL', ...outcomes, ...totals];
if (failing.length > 0) lines.push('FAILING LINES:', ...failing.map((line) => `  ${line}`));
mkdirSync(dirname(summaryPath), { recursive: true });
writeFileSync(summaryPath, `${lines.join('\n')}\n`, 'utf8');
console.log(`\nSummary written to ${summaryPath}`);
process.exit(exitCode);
