import { execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

/**
 * TASK-0016: `npm run verify` must chain typecheck -> lint -> test -> build
 * with `&&` (so it stops at the first failure), and `check:api-drift` must
 * stay outside that chain (§4.4 check 2 is deliberately not folded into the
 * gate an agent runs by habit). This is enforced here, not by inspection, so
 * a later reorder, an `&&` -> `;`/`&` swap (which would no longer
 * short-circuit), or folding `check:api-drift` in breaks a test instead of
 * shipping silently.
 */

const FRONTEND_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const PACKAGE_JSON_PATH = path.join(FRONTEND_ROOT, 'package.json');

interface PackageJsonShape {
  scripts: Record<string, string>;
}

function readScripts(): Record<string, string> {
  const raw = readFileSync(PACKAGE_JSON_PATH, 'utf8');
  const pkg = JSON.parse(raw) as PackageJsonShape;
  return pkg.scripts;
}

describe('package.json verify ordering', () => {
  // TASK-0056: verify runs through scripts/verify.mjs, which writes the gate summary file; the order lives in its
  // step list, and its stop-at-the-first-failure behaviour is proven by scripts/verify.test.mjs.
  it('runs typecheck -> lint -> test -> script tests -> build in that exact order', () => {
    const scripts = readScripts();
    const runner = readFileSync(path.join(FRONTEND_ROOT, 'scripts', 'verify.mjs'), 'utf8');
    const order = ['npm run typecheck', 'npm run lint', 'npm run test', 'npm run test:scripts', 'npm run build'].map((command) =>
      runner.indexOf(`'${command}'`),
    );

    expect(scripts['verify']).toBe('node scripts/verify.mjs');
    expect(order.every((index) => index > 0)).toBe(true);
    expect([...order].sort((a, b) => a - b)).toEqual(order);
  });

  it('keeps check:api-drift defined but out of the verify chain', () => {
    const scripts = readScripts();
    const runner = readFileSync(path.join(FRONTEND_ROOT, 'scripts', 'verify.mjs'), 'utf8');

    expect(scripts['check:api-drift']).toBeTruthy();
    expect(runner).not.toContain('check:api-drift');
  });
});

describe('&& fail-fast semantics (synthetic chain)', () => {
  it('stops before a later step once an earlier one exits non-zero', () => {
    // Mirrors verify's own shape: three steps chained with &&, the middle
    // one failing. A chain rewritten with `;` or `&` would not short-circuit
    // and the third step would run anyway.
    const command = [
      'node -e "console.log(\'first-ran\')"',
      'node -e "console.log(\'second-ran\'); process.exit(3)"',
      'node -e "console.log(\'third-ran\')"',
    ].join(' && ');

    let stdout = '';
    let exitCode = 0;
    try {
      stdout = execSync(command, { encoding: 'utf8' });
    } catch (error) {
      const execError = error as { status?: number; stdout?: string };
      exitCode = execError.status ?? 1;
      stdout = execError.stdout ?? '';
    }

    expect(exitCode).toBe(3);
    expect(stdout).toContain('first-ran');
    expect(stdout).toContain('second-ran');
    expect(stdout).not.toContain('third-ran');
  });
});
