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
 * Scoring versions (ADR-0028):
 *
 *   v1 (scoringVersion 1, the default when the field is omitted):
 *     maxScore   = noteCount * 5                       (NotesManager.OnEnable)
 *     ratioScore = 5 * perfect + 3 * great + 1 * bad   (Judge.Judgement)
 *     score      = (int)Math.Round(1e6 * Floor(ratioScore / maxScore * 1e6) / 1e6)
 *
 *   v2 (scoringVersion 2):
 *     judgment    = 5 * perfect + 3 * great + 1 * bad
 *     comboBonus  = maxCombo * 5
 *     noMissBonus = miss == 0 ? (perfect + great + bad) * 5 : 0
 *     ratioScore  = judgment + comboBonus + noMissBonus
 *     maxScore    = noteCount * 15                     (theoretical max)
 *     score       = same ComputeScore rounding as v1
 *
 * The server reproduces the formulas — the same IEEE-754 operations in the
 * same order, emulated with `Math.fround` because the Unity accumulators are
 * C# `float` — and rejects a client score that disagrees.
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

/**
 * Scoring formula versions the server can recompute (ADR-0028).
 * `1` is the original formula, `2` adds the combo-continuation and
 * no-miss bonuses. A submission without `scoringVersion` is version 1.
 */
export const SCORING_VERSION_V1 = 1;
export const SCORING_VERSION_V2 = 2;

/** The newest scoring version this server supports. */
export const LATEST_SCORING_VERSION = SCORING_VERSION_V2;

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
  scoringVersion?: number;
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
    scoringVersion: {
      type: "integer",
      minimum: 1,
      description:
        "Optional. Scoring formula version the score was computed with: 1 is the original formula (5P+3G+B over 5*N), 2 adds the combo continuation and no-miss bonuses (see ADR-0028). Omitted means 1, so clients that predate scoring versions are still accepted. New clients send 2.",
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
  // The version selects the formula the server recomputes with. A version
  // the server does not know cannot be verified, so it is rejected instead
  // of being stored unchecked (ADR-0028).
  const scoringVersion = request.scoringVersion ?? SCORING_VERSION_V1;
  if (scoringVersion !== SCORING_VERSION_V1 && scoringVersion !== SCORING_VERSION_V2) {
    diagnostics.push({
      path: "/scoringVersion",
      message: `scoring version ${scoringVersion} is not supported by this server (latest is ${LATEST_SCORING_VERSION})`,
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
  if (noteCount * 5 <= 0) {
    return null;
  }
  // GManager.maxScore / GManager.ratioScore are C# `float` (single precision), and
  // `ratioScore / maxScore * 1000000` is evaluated in single precision as well. Emulating that with
  // Math.fround is required: with plain doubles ~19,500 of the (noteCount <= 700, ratioScore) pairs
  // differ by one point (e.g. 5 notes, ratioScore 13: Unity 519999, double 520000) and every such
  // legitimate score would be rejected with 422.
  const maxScore = Math.fround(noteCount * 5);
  const ratioScore = Math.fround(perfect * 5 + great * 3 + bad);
  const ratio = Math.fround(ratioScore / maxScore);
  const scaled = Math.fround(ratio * 1000000);
  const floored = Math.floor(scaled);
  return Math.round((1000000 * floored) / 1000000);
}

/**
 * The scoring v2 score of a play (ADR-0028), computed exactly like Unity:
 *
 *   judgment    = 5 * perfect + 3 * great + 1 * bad
 *   comboBonus  = maxCombo * 5               (0 when maxCombo is omitted)
 *   noMissBonus = miss == 0 ? (perfect + great + bad) * 5 : 0
 *   ratioScore  = judgment + comboBonus + noMissBonus
 *   maxScore    = noteCount * 15             (theoretical maximum ratio)
 *   score       = ComputeScore(ratioScore, maxScore) — same rounding as v1
 *
 * `maxCombo` is optional in the contract; a v2 submission that omits it is
 * treated as a combo bonus of 0. Returns `null` when the chart has no notes,
 * like `computeUnityScore`.
 */
export function computeUnityScoreV2(
  perfect: number,
  great: number,
  bad: number,
  miss: number,
  maxCombo: number | undefined,
  noteCount: number,
): number | null {
  if (noteCount * 15 <= 0) {
    return null;
  }
  // Same single-precision emulation as the v1 formula: every accumulator the
  // Unity side holds as a C# `float` is rounded with Math.fround, in the
  // same order (see computeUnityScore).
  const maxScore = Math.fround(noteCount * 15);
  const judgment = Math.fround(perfect * 5 + great * 3 + bad);
  const comboBonus = Math.fround((maxCombo ?? 0) * 5);
  const hits = perfect + great + bad;
  const noMissBonus = miss === 0 ? Math.fround(hits * 5) : 0;
  const ratioScore = Math.fround(judgment + comboBonus + noMissBonus);
  const ratio = Math.fround(ratioScore / maxScore);
  const scaled = Math.fround(ratio * 1000000);
  const floored = Math.floor(scaled);
  return Math.round((1000000 * floored) / 1000000);
}

/**
 * Recompute the score of a submission with the formula of its declared
 * scoring version. `scoringVersion` omitted means 1 (backward compatibility,
 * ADR-0028). Returns `null` when the chart has no notes.
 */
export function computeScoreForVersion(request: ScoreSubmission, noteCount: number): number | null {
  if ((request.scoringVersion ?? SCORING_VERSION_V1) === SCORING_VERSION_V2) {
    return computeUnityScoreV2(
      request.perfect,
      request.great,
      request.bad,
      request.miss,
      request.maxCombo,
      noteCount,
    );
  }
  return computeUnityScore(request.perfect, request.great, request.bad, noteCount);
}

/**
 * Parse the optional `scoringVersion` query parameter of the leaderboard and
 * rankings endpoints (ADR-0028). An absent parameter means the latest scoring
 * version the server supports, so boards default to the current formula and
 * never mix versions. A present but invalid value returns `null`; the caller
 * answers 400, like the `version` parameter.
 */
export function parseScoringVersionQuery(raw: string | undefined): number | null {
  if (raw === undefined) {
    return LATEST_SCORING_VERSION;
  }
  if (!/^[0-9]+$/.test(raw)) {
    return null;
  }
  const value = Number(raw);
  if (!Number.isSafeInteger(value) || value < 1) {
    return null;
  }
  return value;
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
