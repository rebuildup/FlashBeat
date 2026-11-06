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
 *   2. Semantics — the checks the JSON schema cannot express: every note must
 *      land inside `durationSec`, and the legacy note fields must stay in the
 *      range the Unity gameplay code supports (8 lanes, positive beat
 *      positions, Tap notes only).
 *
 * Both layers return the same actionable diagnostic shape, so a rejected
 * publish always answers *which* item failed and *why*.
 */
import { Validator, type Schema } from "@cfworker/json-schema";
// The frozen chart schema is bundled at build time: a Worker has no
// filesystem, so copying it through the module graph is the only way to
// validate against the real contract in the runtime.
import chartSchema from "../../../../contracts/schemas/chart.v1.schema.json" with { type: "json" };

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
}

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
 * Semantic checks on a schema-valid request. The JSON schema already bounds
 * the individual fields; this layer enforces what only the *combination* of
 * fields can violate, and repeats the gameplay-critical ranges with
 * per-note messages so a rejected chart says exactly which note is at fault:
 *
 *   - 8 lanes: `block` must be 0-7 (`maxBlock` is fixed at 8).
 *   - Beat positions are positive: `num >= 0` and `LPB >= 1`.
 *   - BPM (1-600) and `level` (1-99) inside the supported ranges.
 *   - Tap notes only: `type` must be 1, the only type the Unity judge loads.
 *   - Note count vs chart length: the last note must land at or before
 *     `durationSec`. The Unity loader computes the note time as
 *     `num * 60 / (BPM * LPB)` seconds (`NotesManager.Load`), so that is the
 *     time checked here.
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
    if (note.type !== 1) {
      diagnostics.push({
        path: `${path}/type`,
        message: `only Tap notes (type 1) are supported, got type ${note.type}`,
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

    const noteSec = (note.num * 60) / (bpm * note.LPB);
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

  return diagnostics;
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
