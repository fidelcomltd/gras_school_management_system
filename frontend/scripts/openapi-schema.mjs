// TASK-0004. Shared by generate-api-schema.mjs and check-api-schema-drift.mjs so the two
// scripts can never disagree about how `src/api/schema.d.ts` is produced.
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import openapiTS, { astToString } from 'openapi-typescript';

const here = path.dirname(fileURLToPath(import.meta.url));

/** The one committed contract. Never a live server — CLAUDE.md §3/§4.4. */
export const CONTRACT_PATH = path.resolve(here, '../../contracts/openapi.json');

/** Where the generated types land. See `src/api/README.md`. */
export const OUTPUT_PATH = path.resolve(here, '../src/api/schema.d.ts');

const HEADER = `/**
 * GENERATED — DO NOT EDIT.
 *
 * Produced from \`contracts/openapi.json\` by \`openapi-typescript\`. Regenerate with
 * \`npm run generate:api\` in frontend/ — never hand-edit this file. See
 * \`src/api/README.md\` for the pipeline and CLAUDE.md §3/§4.4 for why.
 */

`;

/** Reads the committed contract and returns the exact bytes `schema.d.ts` should hold. */
export async function buildSchemaContent() {
  const raw = await readFile(CONTRACT_PATH, 'utf8');
  const document = JSON.parse(raw);
  const ast = await openapiTS(document, { silent: true });
  return HEADER + astToString(ast);
}
