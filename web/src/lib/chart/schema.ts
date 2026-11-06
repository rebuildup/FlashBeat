import Ajv2020 from "ajv/dist/2020.js";
import type { ErrorObject } from "ajv";
import schema from "./chart.v1.schema.json";
import type { ValidationIssue } from "./types";

/**
 * Chart validation against the frozen contract.
 *
 * `chart.v1.schema.json` is a byte copy of `contracts/schemas/chart.v1.schema.json`.
 * The contract is owned by issue #3 and must not change, so the copy is guarded
 * by `src/lib/chart/__tests__/contract-schema.test.ts`, which fails when the
 * two files drift apart.
 */
const ajv = new Ajv2020({ allErrors: true, strict: false });

const validate = ajv.compile(schema);

/**
 * Turn an Ajv error into a display issue.
 *
 * Ajv reports `instancePath` as a JSON pointer (`/notes/12/block`). The editor
 * shows that pointer next to the message, so the path is kept verbatim.
 */
function toIssue(error: ErrorObject): ValidationIssue {
  if (error.keyword === "required") {
    const missing = String(error.params["missingProperty"]);
    return {
      path: `${error.instancePath}/${missing}`,
      message: `must have required property '${missing}'`,
      keyword: error.keyword,
    };
  }

  if (error.keyword === "additionalProperties") {
    const extra = String(error.params["additionalProperty"]);
    return {
      path: `${error.instancePath}/${extra}`,
      message: `unexpected field '${extra}'`,
      keyword: error.keyword,
    };
  }

  return {
    path: error.instancePath,
    message: error.message ?? "invalid value",
    keyword: error.keyword,
  };
}

/**
 * Validate an unknown value against the chart contract.
 *
 * Returns every problem instead of the first one, so the editor can show the
 * complete list at once.
 */
export function validateChart(value: unknown): ValidationIssue[] {
  if (validate(value)) {
    return [];
  }

  return (validate.errors ?? []).map(toIssue);
}
