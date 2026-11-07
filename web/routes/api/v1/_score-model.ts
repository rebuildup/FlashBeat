/**
 * Shared model for the score submission and leaderboard endpoints (issue #133).
 *
 * Like `_publish-model.ts`, the request is validated in two layers:
 *
 *   1. Structure — against the *frozen* contract
 *      (`contracts/openapi.v1.yaml` → `ScoreSubmission`). The schema is
 *      bundled at build time (a Worker has no filesystem) and a unit test
 *      fails when the bundled copy drifts from the contract. The runtime
 *      validator is `@cfworker/json-schema` because Ajv compiles validators
 *      with `new Function`, which the Workers runtime forbids.
 *   2. Semantics — what the JSON schema cannot express: the nickname rules
 *      (1-12 characters after trim, no control characters), the judgment sum
 *      against the note count of the submitted chart version, the combo cap
 *      and the server-side score recomputation.
 *
 * The score formula is the one `Assets/Game/Scripts/Gameplay/Judge.cs`
 * computes on every hit (`GManager.score`):
 *
 *   maxScore   = noteCount * 5                       (NotesManager.OnEnable)
 *   ratioScore = 5 * perfect + 3 * great + 1 * bad   (Judge.Judgement)
 *   score      = (int)Math.Round(1e6 * Floor(ratioScore / maxScore * 1e6) / 1e6)
 *
 * The combo counter is displayed but never feeds the score, so the server
 * reproduces exactly this formula — the same IEEE-754 double operations in
 * the same order — and rejects a client score that disagrees.
 */
import { Validator, type Schema } from "@cfworker/json-schema";

/** One actionable problem of a rejected score submission. */
export interface ScoreDiagnostic {
  /** JSON path of the offending item, e.g. `/perfect`. */
  path: string;
  /** What is wrong and what a valid value looks like. */
  message: string;
}

/** Result of validating a score submission body. */
export type ScoreValidation =
  | { ok: true; request: ScoreSubmission }
  | { ok: false; diagnostics: ScoreDiagnostic[] };

/** Body of `POST /api/v1/scores` (`ScoreSubmission` in the contract). */
export interface ScoreSubmission {
  songId: string;
  chartVersion: number;
  nickname: string;
  perfect: number;
  great: number;
  bad: number;
  miss: number;
  maxCombo?: number;
  score?: number;
}

/** One entry of `GET /api/v1/leaderboards/{songId}`. */
export interface LeaderboardEntry {
  nickname: string;
  score: number;
}

/** Body of `GET /api/v1/leaderboards/{songId}`. */
export interface LeaderboardResponse {
  songId: string;
  chartVersion: number;
  entries: LeaderboardEntry[];
}

/**
 * Ajv schema for `ScoreSubmission`.
 *
 * `SONG_PUBLISH_REQUEST_SCHEMA` of `_publish-model.ts` documents the pattern:
 * the frozen request schema of `contracts/openapi.v1.yaml`
 * (`#/components/schemas/ScoreSubmission`); a test asserts the copy matches
 * the contract, so a contract change fails the suite instead of silently
 * diverging.
 */
export const SCORE_SUBMISSION_SCHEMA = {
  type: "object",
  additionalProperties: false,
  required: ["songId", "chartVersion", "nickname", "perfect", "great", "bad", "miss"],
  properties: {
    songId: { type: "string" },
    chartVersion: { type: "integer", minimum: 1 },
    nickname: { type: "string", minLength: 1, maxLength: 16 },
    perfect: { type: "integer", minimum: 0 },
    great: { type: "integer", minimum: 0 },
    bad: { type: "integer", minimum: 0 },
    miss: { type: "integer", minimum: 0 },
    maxCombo: {
      type: "integer",
      minimum: 0,
      description:
        "Optional. Largest combo reached; the server rejects values above the note count of the submitted chart version.",
    },
    score: {
      type: "integer",
      minimum: 0,
      maximum: 1000000,
      description:
        "Optional. Score the client computed with the Unity formula; the server recomputes it from the judgment counts and rejects a mismatch (422), so a broken or manipulated client score is never stored.",
    },
  },
} as const;

const requestValidator = new Validator(SCORE_SUBMISSION_SCHEMA as unknown as Schema, "2020-12");

/**
 * Validate the request body against the contract. Returns the parsed request
 * or one diagnostic per schema error, keyed by JSON path.
 */
export function validateScoreRequest(body: unknown): ScoreValidation {
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
  return { ok: true, request: body as ScoreSubmission };
}

/**
 * The nickname rules of the festival: 1-12 characters after trimming
 * surrounding whitespace, no control characters (C0 plus DEL). The database
 * CHECK allows up to 16 characters; this check is the tighter product rule,
 * so both hold for every stored row.
 */
export function nicknameDiagnostics(nickname: string): ScoreDiagnostic[] {
  const trimmed = nickname.trim();
  const diagnostics: ScoreDiagnostic[] = [];
  if (trimmed.length < 1) {
    diagnostics.push({
      path: "/nickname",
      message: "must contain at least 1 character besides whitespace",
    });
  } else if (trimmed.length > 12) {
    diagnostics.push({
      path: "/nickname",
      message: `must be at most 12 characters after trimming, got ${trimmed.length}`,
    });
  }
  if (/[\u0000-\u001F\u007F]/.test(trimmed)) {
    diagnostics.push({
      path: "/nickname",
      message: "must not contain control characters",
    });
  }
  return diagnostics;
}

/**
 * Semantic checks that need the note count of the submitted chart version:
 * the judgment sum, the combo cap and the client score cross-check.
 * `noteCount` comes from the `chart_versions` row; the caller has already
 * rejected an unknown song/version.
 */
export function scoreSemanticsDiagnostics(
  request: ScoreSubmission,
  noteCount: number,
): ScoreDiagnostic[] {
  const diagnostics: ScoreDiagnostic[] = [];
  const sum = request.perfect + request.great + request.bad + request.miss;
  if (sum !== noteCount) {
    diagnostics.push({
      path: "/(judgments)",
      message: `perfect + great + bad + miss must equal the ${noteCount} notes of chart version ${request.chartVersion}, got ${sum}`,
    });
  }
  if (request.maxCombo !== undefined && request.maxCombo > noteCount) {
    diagnostics.push({
      path: "/maxCombo",
      message: `must not exceed the ${noteCount} notes of chart version ${request.chartVersion}, got ${request.maxCombo}`,
    });
  }
  return diagnostics;
}

/**
 * The score of a play, computed exactly like Unity
 * (`Judge.deleteData` → `GManager.score`).
 *
 * Returns `null` for a chart without notes: the Unity division
 * `ratioScore / maxScore` would be 0/0, and a chart with zero notes cannot
 * be played, so the caller rejects the submission instead.
 */
export function computeUnityScore(
  perfect: number,
  great: number,
  bad: number,
  noteCount: number,
): number | null {
  const maxScore = noteCount * 5;
  if (maxScore <= 0) {
    return null;
  }
  const ratioScore = perfect * 5 + great * 3 + bad;
  // Same operation order as the C# expression: divide, scale, floor, scale,
  // round. Both runtimes use IEEE-754 doubles, so the results are identical.
  const floored = Math.floor((ratioScore / maxScore) * 1000000);
  return Math.round((1000000 * floored) / 1000000);
}

/**
 * Build the leaderboard body. `rows` must already be ordered (the query
 * orders by score desc, then earliest submission first).
 */
export function buildLeaderboardResponse(
  songId: string,
  chartVersion: number,
  rows: readonly LeaderboardEntry[],
): LeaderboardResponse {
  return { songId, chartVersion, entries: rows.map((row) => ({ ...row })) };
}
