/**
 * Helpers for the frozen interface verification.
 *
 * The frozen contract files (`contracts/openapi.v1.yaml` and
 * `contracts/schemas/chart.v1.schema.json`) are the single source of truth.
 * They are read from disk here — never copied into the test code — so a drift
 * between the contract and the API fails the test instead of hiding.
 *
 * The helper is deliberately independent of `helpers/contract.ts` (the read
 * API's own helper): the verification must work against the contract file
 * itself, and it must not break when the API helpers change.
 */
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import { parse } from "yaml";

const openapiPath = fileURLToPath(new URL("../../../contracts/openapi.v1.yaml", import.meta.url));
const chartSchemaPath = fileURLToPath(
  new URL("../../../contracts/schemas/chart.v1.schema.json", import.meta.url),
);
const chartFixturePath = fileURLToPath(
  new URL("../../db/fixtures/who.chart.json", import.meta.url),
);

/**
 * The frozen OpenAPI document, read from the repository. The YAML parser
 * returns an arbitrary document, so the value is treated as untyped data and
 * narrowed at every access.
 */
export const openapi: Record<string, unknown> = parse(readFileSync(openapiPath, "utf-8"));

/** The frozen chart schema, read from the repository. */
export const chartSchema: Record<string, unknown> = JSON.parse(
  readFileSync(chartSchemaPath, "utf-8"),
);

const ajv = new Ajv2020({ strict: false, allErrors: true });

// The OpenAPI document is registered as one schema so that the internal
// `#/components/schemas/...` references of the response schemas resolve. The
// chart schema is registered under the URL the contract's relative reference
// `./schemas/chart.v1.schema.json` points to.
const openapiId = "https://flashbeat.local/contracts/openapi.v1.yaml";
ajv.addSchema({ ...openapi, $id: openapiId }, openapiId);
ajv.addSchema(chartSchema, "https://flashbeat.local/contracts/schemas/chart.v1.schema.json");

/** JSON pointer of a path inside the registered OpenAPI document. */
function pointer(...segments: string[]): string {
  return segments.map((segment) => segment.replace(/~/g, "~0").replace(/\//g, "~1")).join("/");
}

/**
 * Find an operation of the frozen document by its `operationId`.
 *
 * The `operationId` is the stable name of an operation in the contract
 * (`getCatalog`, `getChart`, `publishSong`), so the test never hardcodes a
 * path that could drift from the contract.
 */
function findOperation(operationId: string): {
  path: string;
  method: string;
  responses: [string, unknown][];
} {
  const paths = openapi.paths;
  if (typeof paths !== "object" || paths === null) {
    throw new Error("contracts/openapi.v1.yaml has no paths object");
  }
  for (const [path, methods] of Object.entries(paths)) {
    if (typeof methods !== "object" || methods === null) {
      continue;
    }
    for (const [method, operation] of Object.entries(methods)) {
      if (typeof operation !== "object" || operation === null || !("operationId" in operation)) {
        continue;
      }
      if (operation.operationId !== operationId) {
        continue;
      }
      const responses = "responses" in operation ? operation.responses : undefined;
      if (typeof responses !== "object" || responses === null) {
        throw new Error(`contracts/openapi.v1.yaml has no responses for ${method} ${path}`);
      }
      return { path, method, responses: Object.entries(responses) };
    }
  }
  throw new Error(`contracts/openapi.v1.yaml has no operation with operationId "${operationId}"`);
}

/**
 * Validator for a response of an operation, taken straight from the frozen
 * OpenAPI document.
 *
 * @param operationId `operationId` of the contract operation
 * @param status HTTP status code of the response, e.g. `"200"`
 */
export function operationResponseValidator(
  operationId: string,
  status: string,
): (data: unknown) => boolean {
  const { path, method, responses } = findOperation(operationId);
  const response = responses.find(([code]) => code === status)?.[1];
  if (typeof response !== "object" || response === null || !("content" in response)) {
    throw new Error(`contracts/openapi.v1.yaml has no ${status} response for ${method} ${path}`);
  }
  const content = response.content;
  if (typeof content !== "object" || content === null || !("application/json" in content)) {
    throw new Error(
      `contracts/openapi.v1.yaml has no application/json content for ${status} ${method} ${path}`,
    );
  }
  const jsonContent = content["application/json"];
  if (typeof jsonContent !== "object" || jsonContent === null || !("schema" in jsonContent)) {
    throw new Error(
      `contracts/openapi.v1.yaml has no application/json schema for ${status} ${method} ${path}`,
    );
  }
  const validate = ajv.compile({
    $ref: `${openapiId}#/${pointer("paths", path, method, "responses", status, "content", "application/json", "schema")}`,
  });
  return (data: unknown) => {
    const valid = validate(data);
    if (!valid) {
      throw new Error(
        `${method} ${path} response does not match the contract: ${ajv.errorsText(validate.errors)}`,
      );
    }
    return true;
  };
}

/**
 * The chart of the seeded song, exactly as `db/seed.ts` stores it: the file
 * text is the byte identical copy of `Assets/Resources/Who.json`.
 */
export function chartFixtureText(): string {
  return readFileSync(chartFixturePath, "utf-8");
}

/** The parsed chart fixture. */
export function chartFixture(): Record<string, unknown> {
  return JSON.parse(chartFixtureText()) as Record<string, unknown>;
}

/** Number of notes of the chart fixture. */
export function chartFixtureNoteCount(): number {
  return (chartFixture().notes as unknown[]).length;
}

/**
 * A valid publish body for the fixture song.
 *
 * The metadata is the metadata of the fixture song, so the published chart is
 * the chart the Unity player ships with.
 */
export function publishRequestFixture(): Record<string, unknown> {
  return {
    title: "Who",
    artist: "Azari",
    youtubeId: "8JXiXt0D6tw",
    level: 5,
    durationSec: 68,
    chart: chartFixture(),
  };
}
