import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

/**
 * CLAUDE.md §4.4 check 3: no raw `fetch(`/`axios` call to the API host
 * outside `src/lib/http/`. `src/lib/http/` is the only place allowed to
 * touch Axios or the network directly (CONVENTIONS.md §6); everything else,
 * including `src/api/`, calls through its verb helpers.
 *
 * Also enforces that nothing outside `src/lib/http/` imports `httpClient` —
 * the raw axios instance is not exported from the barrel (`src/lib/http/index.ts`)
 * precisely so a feature can't reach past `ApiError` normalisation and 401
 * handling; this test is what makes that rule mechanical instead of just tidy.
 */

const SRC_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const ALLOWED_DIR = path.join(SRC_ROOT, 'lib', 'http');
const AXIOS_IMPORT = /from\s+['"]axios['"]|require\(\s*['"]axios['"]\s*\)/;
// Word-bounded so `refetch(`/`queryFn` etc. (TanStack Query's own vocabulary) don't match.
const RAW_FETCH_CALL = /\bfetch\s*\(/;
// Matches `import { httpClient } from ...` / `import { foo, httpClient } from ...` in any order.
const HTTP_CLIENT_IMPORT = /import\s*\{[^}]*\bhttpClient\b[^}]*\}\s*from/;

function listSourceFiles(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = path.join(dir, entry);
    const stats = statSync(full);
    if (stats.isDirectory()) {
      listSourceFiles(full, out);
      continue;
    }
    if (/\.(ts|tsx)$/.test(entry) && !/\.(test|spec)\.tsx?$/.test(entry)) {
      out.push(full);
    }
  }
  return out;
}

describe('HTTP boundary', () => {
  it('confines axios imports and raw fetch() calls to src/lib/http', () => {
    const offenders: string[] = [];

    for (const file of listSourceFiles(SRC_ROOT)) {
      if (file.startsWith(ALLOWED_DIR + path.sep)) continue;

      const content = readFileSync(file, 'utf8');
      if (AXIOS_IMPORT.test(content) || RAW_FETCH_CALL.test(content)) {
        offenders.push(path.relative(SRC_ROOT, file));
      }
    }

    expect(offenders).toEqual([]);
  });

  it('confines httpClient imports to src/lib/http', () => {
    const offenders: string[] = [];

    for (const file of listSourceFiles(SRC_ROOT)) {
      if (file.startsWith(ALLOWED_DIR + path.sep)) continue;

      const content = readFileSync(file, 'utf8');
      if (HTTP_CLIENT_IMPORT.test(content)) {
        offenders.push(path.relative(SRC_ROOT, file));
      }
    }

    expect(offenders).toEqual([]);
  });
});
