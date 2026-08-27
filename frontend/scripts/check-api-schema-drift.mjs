#!/usr/bin/env node
// TASK-0004. CLAUDE.md §4.4 check 2: regenerate the client into a temp path and diff it
// against `src/api/schema.d.ts`. A non-empty diff means the committed file is stale or was
// hand-edited — exit non-zero so this is a blocker locally and in CI.
// Run via `npm run check:api-drift`.
import { execFileSync } from 'node:child_process';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { buildSchemaContent, OUTPUT_PATH } from './openapi-schema.mjs';

async function readCommitted() {
  try {
    return await readFile(OUTPUT_PATH, 'utf8');
  } catch (error) {
    if (error.code === 'ENOENT') return null;
    throw error;
  }
}

async function main() {
  const [freshContent, committedContent] = await Promise.all([buildSchemaContent(), readCommitted()]);

  if (committedContent === null) {
    console.error(
      `${path.relative(process.cwd(), OUTPUT_PATH)} does not exist. Run \`npm run generate:api\` first.`,
    );
    process.exitCode = 1;
    return;
  }

  if (freshContent === committedContent) {
    console.log('src/api/schema.d.ts matches contracts/openapi.json. No drift.');
    return;
  }

  const tempDir = await mkdtemp(path.join(tmpdir(), 'api-schema-drift-'));
  const tempPath = path.join(tempDir, 'schema.d.ts');
  await writeFile(tempPath, freshContent, 'utf8');

  console.error('src/api/schema.d.ts is stale or was hand-edited. Diff (committed -> regenerated):');
  try {
    execFileSync('git', ['diff', '--no-color', '--no-index', '--', OUTPUT_PATH, tempPath], {
      stdio: 'inherit',
    });
  } catch (diffError) {
    // `git diff --no-index` exits 1 when the files differ — that is expected here, not a
    // failure of the diff itself. Anything other than a plain non-zero exit is worth showing.
    if (diffError.status === undefined) console.error(diffError);
  } finally {
    await rm(tempDir, { recursive: true, force: true });
  }

  console.error(
    '\nRun `npm run generate:api` and commit the result. Never hand-edit src/api/schema.d.ts.',
  );
  process.exitCode = 1;
}

main().catch((error) => {
  console.error('Failed to check API schema drift:', error);
  process.exitCode = 1;
});
