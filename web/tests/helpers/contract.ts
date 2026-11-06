/**
 * Contract validation helpers for the festival read API (issue #15).
 *
 * The responses are validated against the *real* contract files
 * (`contracts/openapi.v1.yaml` and `contracts/schemas/chart.v1.schema.json`),
 * not against a copy of the schema, so a drift between the API and the frozen
 * contract fails the test suite.
 */
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import { parse } from "yaml";

const openapiPath = fileURLToPath(new URL("../../../contracts/openapi.v1.yaml", import.meta.url));
const chartSchemaPath = fileURLToPath(
  new URL("../../../contracts/schemas/chart.v1.schema.json", import.meta.url),
);

/** The frozen OpenAPI document. */
export const openapi = parse(readFileSync(openapiPath, "utf-8")) as Record<string, unknown>;

/** The frozen chart schema. */
export const chartSchema = JSON.parse(readFileSync(chartSchemaPath, "utf-8")) as Record<
  string,
  unknown
>;

const ajv = new Ajv2020({ strict: false, allErrors: true });

// The OpenAPI document is registered as one schema so that the internal
// `#/components/schemas/...` references of the response schemas resolve.
// The chart schema is registered under the URL the contract's relative
// reference `./schemas/chart.v1.schema.json` points to.
const openapiId = "https://flashbeat.local/contracts/openapi.v1.yaml";
ajv.addSchema({ ...openapi, $id: openapiId }, openapiId);
ajv.addSchema(chartSchema, "https://flashbeat.local/contracts/schemas/chart.v1.schema.json");

/** JSON pointer of a path inside the registered OpenAPI document. */
function pointer(...segments: string[]): string {
  return segments.map((segment) => segment.replace(/~/g, "~0").replace(/\//g, "~1")).join("/");
}

/**
 * Validator for the `200` response of an operation, taken straight from the
 * OpenAPI document.
 */
export function responseValidator(operation: "catalog" | "charts"): (data: unknown) => boolean {
  const paths = openapi.paths as Record<string, { get?: { responses?: Record<string, unknown> } }>;
  const path = operation === "catalog" ? "/api/v1/catalog" : "/api/v1/charts/{songId}";
  if (!paths[path]?.get?.responses?.["200"]) {
    throw new Error(`contracts/openapi.v1.yaml has no 200 response for ${path}`);
  }
  const validate = ajv.compile({
    $ref: `${openapiId}#/${pointer("paths", path, "get", "responses", "200", "content", "application/json", "schema")}`,
  });
  return (data: unknown) => {
    const valid = validate(data);
    if (!valid) {
      throw new Error(`response does not match the contract: ${ajv.errorsText(validate.errors)}`);
    }
    return true;
  };
}

/** The chart of the seeded song, exactly as `db/seed.ts` stores it. */
export function chartFixture(): Record<string, unknown> {
  return JSON.parse(
    readFileSync(
      fileURLToPath(new URL("../../db/fixtures/who.chart.json", import.meta.url)),
      "utf-8",
    ),
  ) as Record<string, unknown>;
}

/** Number of notes of the seeded chart. */
export function chartFixtureNoteCount(): number {
  return (chartFixture().notes as unknown[]).length;
}
