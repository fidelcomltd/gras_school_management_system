// TASK-0056: `npm run verify` writes artifacts/gate-summary.txt on a FAILING run too, and still exits non-zero.
// Runs verify.mjs in a child process with three stand-in steps (pass, fail, never reached).
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import assert from 'node:assert/strict';

const verify = new URL('./verify.mjs', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');

test('a failing verify run writes FAIL, the failing lines, and exits with the failing code', () => {
  const dir = mkdtempSync(join(tmpdir(), 'verify-'));
  const summaryPath = join(dir, 'gate-summary.txt');
  const steps = [
    { name: 'Typecheck', command: 'node -e "console.log(\'fine\')"' },
    { name: 'Lint', command: 'node -e "console.log(\'src/a.ts(1,1): error TS2322: bad\'); process.exit(3)"' },
    { name: 'Build', command: 'node -e "console.log(\'never\')"' },
  ];

  const result = spawnSync(process.execPath, [verify], {
    env: { ...process.env, VERIFY_STEPS: JSON.stringify(steps), VERIFY_SUMMARY_PATH: summaryPath },
    encoding: 'utf8',
  });
  const lines = readFileSync(summaryPath, 'utf8').trim().split('\n');
  rmSync(dir, { recursive: true, force: true });

  assert.equal(result.status, 3, 'the failing step\'s exit code is kept');
  assert.equal(lines[0], 'FAIL');
  assert.ok(lines.includes('PASS: Typecheck'));
  assert.ok(lines.includes('FAIL: Lint -- exit code 3'));
  assert.ok(lines.includes('SKIPPED-NOT-RUN: Build'));
  assert.ok(lines.includes('  [Lint] src/a.ts(1,1): error TS2322: bad'));
});

test('a passing verify run writes PASS and exits 0', () => {
  const dir = mkdtempSync(join(tmpdir(), 'verify-'));
  const summaryPath = join(dir, 'gate-summary.txt');
  const result = spawnSync(process.execPath, [verify], {
    env: { ...process.env, VERIFY_STEPS: JSON.stringify([{ name: 'Only', command: 'node -e "0"' }]), VERIFY_SUMMARY_PATH: summaryPath },
    encoding: 'utf8',
  });
  const first = readFileSync(summaryPath, 'utf8').split('\n')[0];
  rmSync(dir, { recursive: true, force: true });

  assert.equal(result.status, 0);
  assert.equal(first, 'PASS');
});
