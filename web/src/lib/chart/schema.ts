import Ajv2020 from "ajv/dist/2020.js";
import type { ErrorObject } from "ajv";
import schema from "./chart.v1.schema.json";
import type { Chart, ValidationIssue } from "./types";
import {
  validateChartSemanticsWithStrictness,
  type SemanticStrictness,
  type SemanticValidationOptions,
} from "./validate";

/**
 * Chart validation against the frozen contract.
 *
 * `chart.v1.schema.json` is a byte copy of `contracts/schemas/chart.v1.schema.json`.
 * The contract is owned by issue #3 and must not change, so the copy is guarded
 * by `src/lib/chart/__tests__/contract-schema.test.ts`, which fails when the
 * two files drift apart.
 *
 * The **semantic** layer (the rules the JSON schema cannot express) lives in
 * `validate.ts` and is shared with the publish API (issue #191), so the
 * editor and the server always judge a chart the same way.
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
 *
 * The schema covers the shape of a chart. The relations between the notes
 * (a hold that ends before it starts, two holds on the same lane) are
 * checked by `validateChartSemantics` on top of it, so the editor refuses
 * a chart the game could not play.
 *
 * `strictness` decides what happens to a chart whose *shape* is fine but whose
 * *relations* are broken:
 *
 * - `strict` (the default) reports the relation violations as errors.
 * - `lenient` reports them as warnings, so a chart that was published
 *   before the rules were unified can still be read back and repaired
 *   (issue #191, requirement R7). The chart is never silently dropped.
 *
 * The structural problems (the shape itself) are always errors: a chart that
 * does not match the contract cannot be repaired by editing a hold.
 */
export function validateChart(
  value: unknown,
  options: SemanticValidationOptions = {},
  strictness: SemanticStrictness = "strict",
): ValidationIssue[] {
  if (!validate(value)) {
    return (validate.errors ?? []).map(toIssue);
  }

  return validateChartSemanticsWithStrictness(value as Chart, strictness, options);
}
