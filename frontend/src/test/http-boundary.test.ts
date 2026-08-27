import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

/**
 * CLAUDE.md §4.4 check 3: no raw `fetch(`/`axios` call to the API host
 * outside `src/lib/http/`. `src/lib/http/` is the only place allowed to
 * touch Axios or the network directly (CONVENTIONS.md §6); everything else,
 * including `src/api/`, calls through its verb helpers.
 */

const SRC_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const ALLOWED_DIR = path.join(SRC_ROOT, 'lib', 'http');
const AXIOS_IMPORT = /from\s+['"]axios['"]|require\(\s*['"]axios['"]\s*\)/;
// Word-bounded so `refetch(`/`queryFn` etc. (TanStack Query's own vocabulary) don't match.
const RAW_FETCH_CALL = /\bfetch\s*\(/;

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
});
