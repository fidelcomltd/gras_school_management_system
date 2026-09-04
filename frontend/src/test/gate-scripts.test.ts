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
  it('chains typecheck -> lint -> test -> build with && in that exact order', () => {
    const scripts = readScripts();

    expect(scripts['verify']).toBe(
      'npm run typecheck && npm run lint && npm run test && npm run build',
    );
  });

  it('keeps check:api-drift defined but out of the verify chain', () => {
    const scripts = readScripts();

    expect(scripts['check:api-drift']).toBeTruthy();
    expect(scripts['verify']).not.toContain('check:api-drift');
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
