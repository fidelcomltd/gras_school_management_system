// TASK-0056: `npm run verify` writes artifacts/gate-summary.txt on a FAILING run too, and still exits non-zero.
// Runs verify.mjs in a child process with stand-in steps (pass, fail, never reached).
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
import { failingLines } from './verify-lib.mjs';

const verify = fileURLToPath(new URL('./verify.mjs', import.meta.url));

function runVerify(steps) {
  const dir = mkdtempSync(join(tmpdir(), 'verify-'));
  const summaryPath = join(dir, 'gate-summary.txt');
  const result = spawnSync(process.execPath, [verify], {
    env: { ...process.env, VERIFY_STEPS: JSON.stringify(steps), VERIFY_SUMMARY_PATH: summaryPath },
    encoding: 'utf8',
  });
  const lines = readFileSync(summaryPath, 'utf8').trim().split('\n');
  rmSync(dir, { recursive: true, force: true });
  return { status: result.status, lines };
}

test('a failing verify run writes FAIL, the failing lines, and exits with the failing code', () => {
  const { status, lines } = runVerify([
    { name: 'Typecheck', command: 'node -e "console.log(1)"' },
    { name: 'Lint', command: 'node -e "console.log(\'src/a.ts(1,1): error TS2322: bad\'); process.exit(3)"' },
    { name: 'Build', command: 'node -e "console.log(2)"' },
  ]);

  assert.equal(status, 3, "the failing step's exit code is kept");
  assert.equal(lines[0], 'FAIL');
  assert.ok(lines.includes('PASS: Typecheck'));
  assert.ok(lines.includes('FAIL: Lint -- exit code 3'));
  assert.ok(lines.includes('SKIPPED-NOT-RUN: Build'));
  assert.ok(lines.includes('  [Lint] src/a.ts(1,1): error TS2322: bad'));
});

test('a passing verify run writes PASS and exits 0', () => {
  const { status, lines } = runVerify([{ name: 'Only', command: 'node -e "0"' }]);

  assert.equal(status, 0);
  assert.equal(lines[0], 'PASS');
});

test('failing lines are the error lines, each once, and not the passing chatter', () => {
  const output = [
    '✓ src/a.test.ts (3 tests)',
    '× shows the pupil 12ms',
    '× shows the pupil 12ms',
    'FAIL  src/b.test.ts > b',
    'src/c.ts(1,1): error TS2322: bad',
    'Test Files  1 failed | 2 passed (3)',
  ].join('\n');

  assert.deepEqual(failingLines(output), ['× shows the pupil 12ms', 'FAIL  src/b.test.ts > b', 'src/c.ts(1,1): error TS2322: bad']);
});
