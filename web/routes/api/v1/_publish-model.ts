/**
 * Shared model for `PUT /api/v1/admin/songs/{songId}` (issue #16).
 *
 * The publish request is validated in two layers:
 *
 *   1. Structure — against the *frozen* contract
 *      (`contracts/openapi.v1.yaml` → `SongPublishRequest`, which references
 *      `contracts/schemas/chart.v1.schema.json`). A drift between the API and
 *      the contract fails here instead of publishing something Unity cannot
 *      read. The validator is `@cfworker/json-schema`, an interpreter: Ajv
 *      compiles schemas with `new Function`, which the Workers runtime
 *      forbids (`EvalError: Code generation from strings disallowed`), so the
 *      same Ajv instance the seed and the test suite use cannot run here.
 *   2. Semantics — the checks the JSON schema cannot express. **These live in
 *      `web/src/lib/chart/validate.ts` and are shared with the Web studio**
 *      (issue #191). Before that, the API and the editor had their own rules,
 *      so a chart the API accepted could be rejected by the editor
 *      ("Published chart failed validation"), and the editor's overlap check
 *      compared raw line numbers instead of beats and missed overlapping
 *      holds whose `LPB` differed.
 *
 * Both layers return the same actionable diagnostic shape, so a rejected
 * publish always answers *which* item failed and *why*.
 */
import { Validator, type Schema } from "@cfworker/json-schema";
// The frozen chart schema is bundled at build time: a Worker has no
// filesystem, so copying it through the module graph is the only way to
// validate against the real contract in the runtime.
import chartSchema from "../../../../contracts/schemas/chart.v1.schema.json" with { type: "json" };
// The shared semantic validator (issue #191). It is a pure module without any
// runtime dependency, so it runs unchanged in the Worker, in the browser and
// in Node.
import {
  validateChartSemantics,
  type SemanticValidationOptions,
} from "../../../src/lib/chart/validate.ts";
import type { Chart } from "../../../src/lib/chart/types.ts";

/** One actionable problem of a rejected publish request. */
export interface PublishDiagnostic {
  /** JSON path of the offending item, e.g. `/chart/notes/3/block`. */
  path: string;
  /** What is wrong and what a valid value looks like. */
  message: string;
}

/** Result of validating a publish request body. */
export type PublishValidation =
  | { ok: true; request: SongPublishRequest }
  | { ok: false; diagnostics: PublishDiagnostic[] };

/** Body of `PUT /api/v1/admin/songs/{songId}` (`SongPublishRequest`). */
export interface SongPublishRequest {
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  durationSec: number;
  chart: Record<string, unknown>;
}

/** One note of the legacy chart format (`Assets/Resources/<song>.json`). */
export interface LegacyNote {
  type: number;
  num: number;
  block: number;
  LPB: number;
  /**
   * Line index where a Hold ends (issue #164). Only read for `type` 2; a Tap
   * ignores it.
   */
  endNum?: number;
}

/** Note kind of the legacy chart format: a Tap. */
export const NOTE_TYPE_TAP = 1;

/** Note kind of the legacy chart format: a Hold (long note, issue #164). */
export const NOTE_TYPE_HOLD = 2;

/**
 * Ajv validator for `SongPublishRequest`.
 *
 * The chart schema is registered under the URL the contract's relative
 * reference points to, exactly like the test helper does for response
 * validation. `SONG_PUBLISH_REQUEST_SCHEMA` is the frozen request schema of
 * `contracts/openapi.v1.yaml` (`#/components/schemas/SongPublishRequest`)
 * with the chart reference rewritten to that URL; a test asserts the copy
 * matches the contract, so a contract change fails the suite instead of
 * silently diverging.
 */
const chartSchemaId = "https://flashbeat.local/contracts/schemas/chart.v1.schema.json";

export const SONG_PUBLISH_REQUEST_SCHEMA = {
  type: "object",
  additionalProperties: false,
  required: ["title", "artist", "youtubeId", "level", "durationSec", "chart"],
  properties: {
    title: { type: "string", minLength: 1, maxLength: 160 },
    artist: { type: "string", maxLength: 160 },
    youtubeId: { type: "string", pattern: "^[A-Za-z0-9_-]{11}$" },
    level: { type: "integer", minimum: 1, maximum: 99 },
    durationSec: { type: "number", exclusiveMinimum: 0, maximum: 1800 },
    chart: { $ref: chartSchemaId },
  },
} as const;

const requestValidator = new Validator(SONG_PUBLISH_REQUEST_SCHEMA as unknown as Schema, "2020-12");
requestValidator.addSchema(chartSchema as unknown as Schema, chartSchemaId);

/**
 * Validate the request body against the contract. Returns the parsed request
 * or one diagnostic per schema error, keyed by JSON path.
 */
export function validatePublishRequest(body: unknown): PublishValidation {
  const result = requestValidator.validate(body);
  if (!result.valid) {
    // The interpreter reports every level of the tree ("A subschema had
    // errors." on the parents); only the leaf units say *what* is wrong, so
    // keep the errors no deeper error refines.
    const locations = result.errors.map((error) => error.instanceLocation);
    return {
      ok: false,
      diagnostics: result.errors
        .filter(
          (error) =>
            !locations.some((location) => location.startsWith(`${error.instanceLocation}/`)),
        )
        .map((error) => ({
          path: error.instanceLocation || "(root)",
          message: error.error,
        })),
    };
  }
  return { ok: true, request: body as SongPublishRequest };
}

/**
 * Semantic checks on a schema-valid request.
 *
 * The JSON schema already bounds the individual fields; this layer enforces
 * what only the *combination* of fields can violate. It is a **thin adapter**
 * on top of the shared validator (`web/src/lib/chart/validate.ts`, issue
 * #191): the rules live in exactly one place, so the publish API and the Web
 * studio always judge a chart the same way.
 *
 * The shared validator speaks in **chart-relative JSON pointers**
 * (`/notes/3/endNum`); the publish API answers in **request-relative** ones
 * (`/chart/notes/3/endNum`), so the paths are rewritten here. The messages
 * are passed through unchanged.
 *
 * On top of the shared rules, the request-level values are checked:
 *
 *   - 8 lanes: `block` must be 0-7 (`maxBlock` is fixed at 8).
 *   - Beat positions are positive: `num >= 0` and `LPB >= 1`.
 *   - BPM (1-600) and `level` (1-99) inside the supported ranges.
 *   - Note kinds: `type` must be 1 (Tap) or 2 (Hold). A Hold additionally
 *     needs `endNum`, and its end must be *after* its start (`endNum > num`).
 *   - Note count vs chart length: the last note must land at or before
 *     `durationSec`. The Unity loader computes the note time as
 *     `num * 60 / (BPM * LPB)` seconds (`NotesManager.Load`), so that is the
 *     time checked here. A Hold is checked at its *end*, because the note is
 *     still on screen while it is held.
 */
export function semanticDiagnostics(request: SongPublishRequest): PublishDiagnostic[] {
  const diagnostics: PublishDiagnostic[] = [];
  const chart = request.chart;
  const notes = chart.notes as LegacyNote[];
  const bpm = chart.BPM as number;

  if (bpm < 1 || bpm > 600) {
    diagnostics.push({ path: "/chart/BPM", message: `must be between 1 and 600, got ${bpm}` });
  }
  if (notes.length === 0) {
    diagnostics.push({
      path: "/chart/notes",
      message: "must contain at least one note; an empty chart cannot be played",
    });
  }
  if (request.level < 1 || request.level > 99) {
    diagnostics.push({ path: "/level", message: `must be between 1 and 99, got ${request.level}` });
  }

  let lastNoteSec = -1;
  let lastNotePath = "";
  for (const [index, note] of notes.entries()) {
    const path = `/chart/notes/${index}`;
    if (note.type !== NOTE_TYPE_TAP && note.type !== NOTE_TYPE_HOLD) {
      diagnostics.push({
        path: `${path}/type`,
        message: `must be a Tap (type ${NOTE_TYPE_TAP}) or a Hold (type ${NOTE_TYPE_HOLD}), got type ${note.type}`,
      });
    }
    if (note.block < 0 || note.block > 7) {
      diagnostics.push({
        path: `${path}/block`,
        message: `must be a lane between 0 and 7 (8 lanes), got ${note.block}`,
      });
    }
    if (note.LPB < 1) {
      diagnostics.push({
        path: `${path}/LPB`,
        message: `must be a positive beat subdivision, got ${note.LPB}`,
      });
    }
    if (note.num < 0) {
      diagnostics.push({
        path: `${path}/num`,
        message: `must be a non-negative beat position, got ${note.num}`,
      });
    }

    // The Hold itself is checked by the shared validator below (missing
    // `endNum`, an end that is not after its start, the length bounds and
    // the overlap with another Hold of the same lane). Keeping the rule in
    // one place is what makes the API and the editor agree (issue #191).

    // The last note of the chart decides how long the chart has to be. A Tap
    // ends at its own position, a Hold at the end of its hold.
    const isHold = note.type === NOTE_TYPE_HOLD;
    const endLine = isHold && note.endNum !== undefined ? note.endNum : note.num;
    const noteSec = (endLine * 60) / (bpm * note.LPB);
    if (noteSec > lastNoteSec) {
      lastNoteSec = noteSec;
      lastNotePath = path;
    }
  }

  if (lastNoteSec > request.durationSec) {
    diagnostics.push({
      path: lastNotePath,
      message: `note lands at ${lastNoteSec.toFixed(3)}s, beyond the ${request.durationSec}s chart length; shorten the chart or raise durationSec`,
    });
  }

  // The shared semantic rules (issue #191). The chart-relative pointers of
  // the shared validator are rewritten to request-relative ones, so the
  // operator sees the path inside the body they sent.
  diagnostics.push(...sharedSemanticDiagnostics(request));

  return diagnostics;
}

/**
 * The part of the publish validation that the Web studio shares.
 *
 * It is the **semantic** layer of the chart validation, and nothing else:
 * the request-level rules (the level range, the empty chart, the note kinds,
 * the lane range) belong to the publish request and have no counterpart in
 * the editor, which edits a chart and not a request.
 *
 * `web/src/lib/chart/__tests__/chart-validation-parity.test.ts` compares this
 * function with `validateChartSemantics` on the same fixtures, so the two
 * sides cannot drift apart (issue #191).
 */
export function sharedSemanticDiagnostics(request: SongPublishRequest): PublishDiagnostic[] {
  const options: SemanticValidationOptions = { durationSec: request.durationSec };
  return validateChartSemantics(request.chart as Chart, options).map((issue) => ({
    path: toRequestPath(issue.path),
    message: issue.message,
  }));
}

/**
 * Rewrite a chart-relative JSON pointer (`/notes/3/endNum`) into a
 * request-relative one (`/chart/notes/3/endNum`).
 */
function toRequestPath(chartPath: string): string {
  if (!chartPath) return "/chart";
  return `/chart${chartPath}`;
}

/**
 * Constant-time comparison of the presented bearer token against the runtime
 * secret. Both sides are hashed first: the digests have a fixed length, so
 * the XOR loop below leaks neither the token nor its length through timing.
 */
export async function adminTokenMatches(
  provided: string | undefined,
  expected: string,
): Promise<boolean> {
  if (provided === undefined || provided === "") {
    return false;
  }
  const [presented, secret] = await Promise.all([sha256(provided), sha256(expected)]);
  let diff = 0;
  for (let i = 0; i < presented.length; i++) {
    diff |= presented[i]! ^ secret[i]!;
  }
  return diff === 0;
}

async function sha256(value: string): Promise<Uint8Array> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  return new Uint8Array(digest);
}
