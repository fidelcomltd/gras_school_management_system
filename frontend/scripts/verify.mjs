// `npm run verify` (TASK-0056): the same steps as before, in the same order, stopping at the first failure, and a
// machine-readable summary in artifacts/gate-summary.txt, so a report quotes one small file instead of the log. First
// line PASS or FAIL; each step's outcome (SKIPPED-NOT-RUN after a failure); the test totals; the failing lines in full.
// The exit code is the failing step's, so writing the file never hides a failure. The file says FAIL (in progress)
// from the moment the run starts, so an interrupted run never leaves the last run's PASS behind.
//
// VERIFY_STEPS (a JSON array of { name, command }) and VERIFY_SUMMARY_PATH exist for scripts/verify.test.mjs only.
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { DEFAULT_STEPS, failingLines, summaryText, testTotals } from './verify-lib.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const summaryPath = process.env.VERIFY_SUMMARY_PATH ?? resolve(root, 'artifacts/gate-summary.txt');
const write = (summary) => {
  mkdirSync(dirname(summaryPath), { recursive: true });
  writeFileSync(summaryPath, summaryText(summary), 'utf8');
};
write({ abortReason: 'a verify run started and has not finished (in progress, or interrupted).' });

const steps = process.env.VERIFY_STEPS ? JSON.parse(process.env.VERIFY_STEPS) : DEFAULT_STEPS;
const ansi = /\u001b\[[0-9;]*m/g;

/** Runs one step, streaming its output as it arrives and keeping a copy. */
function run(command) {
  return new Promise((resolveRun) => {
    const child = spawn(command, { cwd: root, shell: true, env: { ...process.env, FORCE_COLOR: process.stdout.isTTY ? '1' : '0' } });
    let output = '';
    // Decoded as a stream, so a character split across two chunks (vitest's ×) arrives whole.
    child.stdout.setEncoding('utf8');
    child.stderr.setEncoding('utf8');
    child.stdout.on('data', (text) => {
      process.stdout.write(text);
      output += text;
    });
    child.stderr.on('data', (text) => {
      process.stderr.write(text);
      output += text;
    });
    child.on('error', (error) => {
      output += `\nerror: ${error.message}`;
    });
    child.on('close', (code) => resolveRun({ code: code ?? 1, output: output.replace(ansi, '') }));
  });
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

write({ failed: exitCode !== 0, outcomes, totals, failing });
console.log(`\nSummary written to ${summaryPath}`);
process.exit(exitCode);
