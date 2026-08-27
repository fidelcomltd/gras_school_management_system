#!/usr/bin/env node
// TASK-0004. Regenerates `src/api/schema.d.ts` from the committed `contracts/openapi.json`.
// Run via `npm run generate:api`. Never edit the output by hand — see src/api/README.md.
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { buildSchemaContent, OUTPUT_PATH } from './openapi-schema.mjs';

async function main() {
  const content = await buildSchemaContent();
  await mkdir(path.dirname(OUTPUT_PATH), { recursive: true });
  await writeFile(OUTPUT_PATH, content, 'utf8');
  console.log(`Generated ${path.relative(process.cwd(), OUTPUT_PATH)} from contracts/openapi.json`);
}

main().catch((error) => {
  console.error('Failed to generate the API schema:', error);
  process.exitCode = 1;
});
