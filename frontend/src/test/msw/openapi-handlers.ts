/// <reference types="node" />
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { http, HttpResponse, type HttpHandler } from 'msw';

/**
 * Builds default MSW handlers straight from `contracts/openapi.json`, so the
 * mocked network cannot describe a shape the real API does not (CLAUDE.md §7
 * Testing). Each declared operation gets a handler that answers its
 * documented success status with the schema's own `example` payload.
 *
 * This only covers the happy path. Error scenarios (`422`, `401`, `409`, …)
 * are added per-test with `server.use(...)` and `problemResponse` from
 * `./handlers.ts`, which override these defaults for that one test.
 */

const HTTP_METHODS = ['get', 'post', 'put', 'patch', 'delete'] as const;
type HttpMethod = (typeof HTTP_METHODS)[number];

interface OpenApiSchema {
  $ref?: string;
  example?: unknown;
  [key: string]: unknown;
}

interface OpenApiResponse {
  content?: Record<string, { schema?: OpenApiSchema; example?: unknown }>;
}

interface OpenApiOperation {
  responses?: Record<string, OpenApiResponse>;
}

type OpenApiPathItem = Partial<Record<HttpMethod, OpenApiOperation>>;

interface OpenApiDocument {
  paths: Record<string, OpenApiPathItem>;
  components?: { schemas?: Record<string, OpenApiSchema> };
}

function readContract(): OpenApiDocument {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const contractPath = path.resolve(here, '../../../../contracts/openapi.json');
  return JSON.parse(readFileSync(contractPath, 'utf8')) as OpenApiDocument;
}

function resolveSchema(document: OpenApiDocument, schema: OpenApiSchema | undefined): OpenApiSchema | undefined {
  if (!schema?.$ref) return schema;
  const name = schema.$ref.split('/').pop();
  return name ? document.components?.schemas?.[name] : undefined;
}

/** The lowest declared 2xx status code, i.e. the operation's primary success response. */
function successStatus(responses: Record<string, OpenApiResponse> | undefined): string | undefined {
  return Object.keys(responses ?? {})
    .filter((code) => /^2\d\d$/.test(code))
    .sort()[0];
}

function exampleFor(document: OpenApiDocument, response: OpenApiResponse | undefined): unknown {
  const jsonContent = response?.content?.['application/json'];
  if (!jsonContent) return undefined;
  if (jsonContent.example !== undefined) return jsonContent.example;
  return resolveSchema(document, jsonContent.schema)?.example;
}

/** Converts an OpenAPI `{id}` path template to MSW's `:id` matcher syntax. */
function toMswRoute(openApiRoute: string): string {
  return openApiRoute.replace(/\{([^}]+)\}/g, ':$1');
}

export function buildHandlersFromContract(baseUrl: string): HttpHandler[] {
  const document = readContract();
  const handlers: HttpHandler[] = [];

  for (const [route, pathItem] of Object.entries(document.paths)) {
    for (const method of HTTP_METHODS) {
      const operation = pathItem[method];
      if (!operation) continue;

      const status = successStatus(operation.responses);
      if (!status) continue;

      const example = exampleFor(document, operation.responses?.[status]);
      const url = `${baseUrl}${toMswRoute(route)}`;

      handlers.push(
        http[method](url, () =>
          example === undefined
            ? new HttpResponse(null, { status: Number(status) })
            : HttpResponse.json(example, { status: Number(status) }),
        ),
      );
    }
  }

  return handlers;
}
