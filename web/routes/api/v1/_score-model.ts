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
 *      (1-16 characters after trim, no control characters), the judgment sum
 *      against the note count of the submitted chart version, the combo cap
 *      and the server-side score recomputation.
 *
 * Scoring versions (ADR-0028, ADR-0029):
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
 *   v3 (scoringVersion 3, ADR-0029, deterministic since Issue #631):
 *     judgment       = 5 * perfect + 3 * great + 1 * bad   (unchanged)
 *     comboBonus     = maxCombo * 5                       (unchanged)
 *     noMissBonus    = miss == 0 ? (perfect + great + bad) * 5 : 0  (unchanged)
 *     milestoneBonus = comboMilestones * 25               (NEW; the client
 *                      reports only the crossing count. With miss == 0 the
 *                      count is verified exactly — a single unbroken run
 *                      crosses floor(hits / 50) times. With misses the
 *                      exact history is not reconstructible from the
 *                      aggregates, so the server applies the tightest
 *                      verifiable bound: at most miss + 1 runs of at most
 *                      maxCombo combo each, i.e. crossings <=
 *                      min(floor(hits / 50), (miss + 1) * floor(maxCombo / 50)))
 *     finalBonus     = miss == 0 ? 100 : 0                (NEW; a submission
 *                      requires perfect+great+bad+miss == noteCount, so
 *                      miss == 0 already means the play was completed —
 *                      the same condition the Unity client vests the bonus
 *                      on, see Issue #618)
 *     totalRatio     = judgment + comboBonus + noMissBonus
 *                      + milestoneBonus + finalBonus
 *     maxRatio       = noteCount * 15 + floor(noteCount / 50) * 25 + 100
 *     score          = floor(totalRatio * 1e6 / maxRatio) — exact integer
 *                      arithmetic (Issue #631), identical on every Unity
 *                      backend and this Worker
 *
 *   v4 (scoringVersion 4, ADR-0033): the judgment-derived base keeps the
 *     1,000,000 ceiling and the v3 bonus components are added on top, so a
 *     full-combo run legitimately exceeds 1,000,000 (max 2,000,000):
 *     judgment       = 5 * perfect + 3 * great + 1 * bad   (unchanged)
 *     comboBonus     = maxCombo * 5                       (unchanged, ratio points)
 *     noMissBonus    = miss == 0 ? (perfect + great + bad) * 5 : 0  (unchanged)
 *     milestoneBonus = comboMilestones * 25               (unchanged; the same
 *                      bound checks as v3 apply — see scoreSemanticsDiagnostics)
 *     finalBonus     = judgmentsComplete && miss == 0 ? 100 : 0
 *                      (ADR-0033: submissions are always settled, so the
 *                      server recompute vests it exactly when miss == 0)
 *     baseScore      = floor(judgment * 1e6 / (5 * noteCount))        ≤ 1,000,000
 *     bonusRatio     = comboBonus + noMissBonus + milestoneBonus + finalBonus
 *     maxBonusRatio  = noteCount * 10 + floor(noteCount / 50) * 25 + 100
 *     bonusScore     = floor(bonusRatio * 1e6 / maxBonusRatio)        ≤ 1,000,000
 *     score          = baseScore + bonusScore                          ≤ 2,000,000
 *     All integer arithmetic — identical on every platform.
 *
 *   v5 (scoringVersion 5, ADR-0035): same two halves as v4, but the
 *     continuous combo bonus is removed and the combo reward vests only
 *     at milestone crossings (250 ratio points each):
 *     judgment       = 5 * perfect + 3 * great + 1 * bad   (unchanged)
 *     comboBonus     = — (removed: combo continuation pays nothing)
 *     noMissBonus    = miss == 0 ? (perfect + great + bad) * 5 : 0  (unchanged)
 *     milestoneBonus = comboMilestones * 250              (was * 25 in v4;
 *                      the same bound checks as v3/v4 apply — see
 *                      scoreSemanticsDiagnostics)
 *     finalBonus     = judgmentsComplete && miss == 0 ? 100 : 0  (unchanged)
 *     baseScore      = floor(judgment * 1e6 / (5 * noteCount))        ≤ 1,000,000
 *     bonusRatio     = milestoneBonus + noMissBonus + finalBonus
 *     maxBonusRatio  = floor(noteCount / 50) * 250 + noteCount * 5 + 100
 *     bonusScore     = floor(bonusRatio * 1e6 / maxBonusRatio)        ≤ 1,000,000
 *     score          = baseScore + bonusScore                          ≤ 2,000,000
 *     All integer arithmetic — identical on every platform. Every
 *     completed miss == 0 play scores exactly the same total as v4
 *     (bonusRatio saturates its denominator in both formulas).
 *
 *   v6 (scoringVersion 6, ADR-0037): the judgment-derived base keeps the
 *     1,000,000 ceiling and the bonus half becomes an ABSOLUTE additional
 *     score earned from actual play — per-note quality points plus
 *     milestone and full-clear bonuses — whose chart maximum grows with
 *     the note count instead of being normalized to a fixed ceiling:
 *     judgment       = 5 * perfect + 3 * great + 1 * bad   (unchanged)
 *     qualityScore   = perfect * 1000 + great * 600 + bad * 200   (NEW;
 *                      Miss contributes 0)
 *     milestoneScore = comboMilestones * 5000              (the same
 *                      bound checks as v3/v4/v5 apply — see
 *                      scoreSemanticsDiagnostics)
 *     clearScore     = judgmentsComplete && miss == 0 ? 25000 : 0
 *                      (same vesting condition as the v3–v5 final bonus)
 *     additionalScore = qualityScore + milestoneScore + clearScore
 *     baseScore      = floor(judgment * 1e6 / (5 * noteCount))   ≤ 1,000,000
 *     score          = baseScore + additionalScore
 *     maxAdditional  = noteCount * 1000 + floor(noteCount / 50) * 5000
 *                      + 25000                                (chart-specific)
 *     scoreMax       = 1,000,000 + maxAdditional              (per chart —
 *                      there is no fixed total ceiling in v6)
 *     All integer arithmetic — identical on every platform.
 *
 * v1 and v2 keep the single-precision emulation below: their stored records
 * were produced by the float formula and must keep their meaning. v3 moved
 * to integer arithmetic because float32 evaluation order differs between
 * the Editor Mono JIT, IL2CPP and this `Math.fround` emulation, which could
 * reject a legitimate score with 422 over a one-ulp difference (Issue #631).
 * Clients built before the change still send the legacy float-path score;
 * the submission endpoint accepts that value as well (see scores.ts).
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
 * Scoring formula versions the server can recompute (ADR-0028, ADR-0029,
 * ADR-0033, ADR-0035, ADR-0037). `1` is the original formula, `2` adds the
 * combo-continuation and no-miss bonuses, `3` adds the combo-milestone and
 * full-combo bonuses, `4` splits the score into a judgment-derived base
 * (≤ 1,000,000) plus additive bonuses (≤ 1,000,000, total ≤ 2,000,000),
 * `5` keeps the v4 two-half structure but removes the continuous combo
 * bonus so only milestone crossings pay (ADR-0035), `6` replaces the
 * normalized bonus half with an absolute additional score whose chart
 * maximum grows with the note count (ADR-0037). A submission without
 * `scoringVersion` is version 1.
 */
export const SCORING_VERSION_V1 = 1;
export const SCORING_VERSION_V2 = 2;
export const SCORING_VERSION_V3 = 3;
export const SCORING_VERSION_V4 = 4;
export const SCORING_VERSION_V5 = 5;
export const SCORING_VERSION_V6 = 6;

/** The newest scoring version this server supports. */
export const LATEST_SCORING_VERSION = SCORING_VERSION_V6;

/**
 * Score ceilings per formula family. v1–v3 normalize the whole play into
 * 1,000,000; v4 and v5 split it into a base and a bonus half of 1,000,000
 * each, so their submissions may reach 2,000,000 (ADR-0033, ADR-0035).
 * v6 has no fixed total ceiling — the chart maximum is
 * `scoreMaxV6(noteCount)` (ADR-0037); SCORE_MAX_V6 is only a sanity bound
 * for the request schema and the database CHECK. The published chart
 * contract caps notes at 10,000, so the largest reachable score is
 * scoreMaxV6(10000) = 12,025,000; the exact per-chart bound is enforced by
 * the recomputation.
 */
export const SCORE_MAX_LEGACY = 1000000;
export const SCORE_MAX_V4 = 2000000;
export const SCORE_MAX_V5 = 2000000;
export const SCORE_MAX_V6 = 12025000;

/** Ratio points one v5 milestone crossing pays (ADR-0035). */
export const MILESTONE_BONUS_POINTS_V5 = 250;

/** Absolute score points one v6 milestone crossing pays (ADR-0037). */
export const MILESTONE_SCORE_V6 = 5000;

/** Absolute score points of the v6 quality judgments (ADR-0037). */
export const PERFECT_SCORE_V6 = 1000;
export const GREAT_SCORE_V6 = 600;
export const BAD_SCORE_V6 = 200;

/** Absolute score points of the v6 full-clear bonus (ADR-0037). */
export const FULL_CLEAR_SCORE_V6 = 25000;

/**
 * The largest additional score a chart with `noteCount` notes can give
 * (ADR-0037): every note Perfect, every possible milestone crossing and
 * the full-clear bonus. Strictly increasing in noteCount — longer charts
 * allow a larger maximum additional score.
 */
export function maxAdditionalScoreV6(noteCount: number): number {
  if (noteCount <= 0) {
    return 0;
  }
  return (
    noteCount * PERFECT_SCORE_V6 +
    Math.floor(noteCount / 50) * MILESTONE_SCORE_V6 +
    FULL_CLEAR_SCORE_V6
  );
}

/** The largest total score of a chart with `noteCount` notes (ADR-0037). */
export function scoreMaxV6(noteCount: number): number {
  return SCORE_MAX_LEGACY + maxAdditionalScoreV6(noteCount);
}

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
  comboMilestones?: number;
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
        "Optional. Largest combo reached; the server rejects values above the note count of the submitted chart version and, for scoringVersion 3, 4, 5 and 6, above the hit count (perfect + great + bad), which the combo can never exceed.",
    },
    comboMilestones: {
      type: "integer",
      minimum: 0,
      description:
        "Optional. Number of combo milestone crossings (every 50 combo) during the play; only meaningful for scoringVersion 3, 4, 5 and 6. When miss is 0 the server verifies the count exactly against floor((perfect+great+bad)/50); when misses occurred it applies the tightest bound the aggregate fields allow — at most miss + 1 runs of maxCombo — because the exact crossing history cannot be reconstructed from the totals (see ADR-0029). Omitted is treated as 0.",
    },
    score: {
      type: "integer",
      minimum: 0,
      maximum: SCORE_MAX_V6,
      description:
        "Optional. Score the client computed with the Unity formula; the server recomputes it from the judgment counts and rejects a mismatch (422), so a broken or manipulated client score is never stored. scoringVersion 4 and 5 scores may exceed 1,000,000 (base + bonus, up to 2,000,000); scoringVersion 6 scores are bounded per chart by 1,000,000 + maxAdditionalScoreV6(noteCount) with no fixed total ceiling (ADR-0037); versions 1-3 stay capped at 1,000,000 by the recomputation. The 12,025,000 schema bound is the largest reachable chart maximum (10,000 notes, the published contract cap) — the exact per-chart bound is enforced by the recomputation.",
    },
    scoringVersion: {
      type: "integer",
      minimum: 1,
      description:
        "Optional. Scoring formula version the score was computed with: 1 is the original formula (5P+3G+B over 5*N), 2 adds the combo continuation and no-miss bonuses (see ADR-0028), 3 adds combo milestone and full-combo bonuses (see ADR-0029), 4 splits the score into a judgment-derived base capped at 1,000,000 plus additive bonuses so a full combo exceeds it (see ADR-0033), 5 keeps the v4 two-half structure but removes the continuous combo bonus so only milestone crossings pay (see ADR-0035), 6 replaces the normalized bonus half with an absolute additional score whose chart maximum grows with the note count (see ADR-0037). Omitted means 1, so clients that predate scoring versions are still accepted. New clients send 6.",
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
 * The nickname rules of the festival: 1-16 characters after trimming
 * surrounding whitespace, no control characters (C0 plus DEL). The limit
 * matches the frozen contract (`nickname.maxLength: 16`), the database CHECK
 * and the Unity client (`RankingClient.NicknameMaxLength`).
 */
export function nicknameDiagnostics(nickname: string): ScoreDiagnostic[] {
  const trimmed = nickname.trim();
  const diagnostics: ScoreDiagnostic[] = [];
  if (trimmed.length < 1) {
    diagnostics.push({
      path: "/nickname",
      message: "must contain at least 1 character besides whitespace",
    });
  } else if (trimmed.length > 16) {
    diagnostics.push({
      path: "/nickname",
      message: `must be at most 16 characters after trimming, got ${trimmed.length}`,
    });
  }
  // oxlint-disable-next-line no-control-regex -- nickname must reject control characters
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
  if (
    scoringVersion !== SCORING_VERSION_V1 &&
    scoringVersion !== SCORING_VERSION_V2 &&
    scoringVersion !== SCORING_VERSION_V3 &&
    scoringVersion !== SCORING_VERSION_V4 &&
    scoringVersion !== SCORING_VERSION_V5 &&
    scoringVersion !== SCORING_VERSION_V6
  ) {
    diagnostics.push({
      path: "/scoringVersion",
      message: `scoring version ${scoringVersion} is not supported by this server (latest is ${LATEST_SCORING_VERSION})`,
    });
  }
  // Scoring v3 (ADR-0029, Issue #618): the client reports only the number
  // of combo milestone crossings; the server prices each at 25 ratio
  // points itself. What the aggregate fields can verify:
  //
  //   - maxCombo only grows on head hits, so it can never exceed hits.
  //   - miss == 0: the whole play is one unbroken run, so the crossing
  //     count is exactly floor(hits / 50) — verified by equality.
  //   - miss > 0: the exact crossing history cannot be reconstructed
  //     from the totals (49 hits + Miss + 49 hits and 50 + Miss + 48 are
  //     indistinguishable). The tightest verifiable bound is at most
  //     miss + 1 runs of at most maxCombo combo each:
  //     crossings <= (miss + 1) * floor(maxCombo / 50), and never more
  //     than floor(hits / 50). A claim inside this bound is still a
  //     client-declared value the server cannot disprove — the same
  //     trust model as the maxCombo cap, documented in ADR-0029.
  //
  // The field is ignored for versions 1 and 2, whose formulas cannot
  // use it. Version 4 (ADR-0033), version 5 (ADR-0035) and version 6
  // (ADR-0037) reuse the v3 bonus components and the identical bounds —
  // only the score mapping differs.
  if (
    scoringVersion === SCORING_VERSION_V3 ||
    scoringVersion === SCORING_VERSION_V4 ||
    scoringVersion === SCORING_VERSION_V5 ||
    scoringVersion === SCORING_VERSION_V6
  ) {
    const hits = request.perfect + request.great + request.bad;
    const milestones = request.comboMilestones ?? 0;
    if (request.maxCombo !== undefined && request.maxCombo > hits) {
      diagnostics.push({
        path: "/maxCombo",
        message: `must not exceed the ${hits} hits (perfect + great + bad), got ${request.maxCombo}`,
      });
    }
    if (request.miss === 0) {
      const expected = Math.floor(hits / 50);
      if (milestones !== expected) {
        diagnostics.push({
          path: "/comboMilestones",
          message: `must equal floor((perfect + great + bad) / 50) = ${expected} when miss is 0, got ${milestones}`,
        });
      }
    } else {
      const maxCombo = request.maxCombo ?? 0;
      const bound = Math.min(Math.floor(hits / 50), (request.miss + 1) * Math.floor(maxCombo / 50));
      if (milestones > bound) {
        diagnostics.push({
          path: "/comboMilestones",
          message: `must not exceed ${bound} milestone crossings for ${request.miss} misses and maxCombo ${maxCombo}, got ${milestones}`,
        });
      }
    }
  }
  // Per-version score cap (ADR-0033, ADR-0035, ADR-0037): v1–v3 normalize
  // the whole play into 1,000,000, so a client-sent score above that can
  // never match the recomputation — reject it here with a clear
  // diagnostic instead of relying on the generic mismatch error. v4 and
  // v5 legitimately reach 2,000,000 (SCORE_MAX_V4 / SCORE_MAX_V5,
  // enforced by the schema maximum). v6 has no fixed ceiling: its chart
  // maximum is scoreMaxV6(noteCount), enforced exactly here.
  if (
    scoringVersion !== SCORING_VERSION_V4 &&
    scoringVersion !== SCORING_VERSION_V5 &&
    scoringVersion !== SCORING_VERSION_V6 &&
    request.score !== undefined &&
    request.score > SCORE_MAX_LEGACY
  ) {
    diagnostics.push({
      path: "/score",
      message: `must not exceed ${SCORE_MAX_LEGACY} for scoring version ${scoringVersion}, got ${request.score}`,
    });
  }
  if (
    scoringVersion === SCORING_VERSION_V6 &&
    request.score !== undefined &&
    request.score > scoreMaxV6(noteCount)
  ) {
    diagnostics.push({
      path: "/score",
      message: `must not exceed the chart maximum ${scoreMaxV6(noteCount)} for scoring version 6 (${noteCount} notes), got ${request.score}`,
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
 * The scoring v3 score of a play (ADR-0029), computed with the deterministic
 * integer arithmetic the Unity client uses since Issue #631:
 *
 *   judgment       = 5 * perfect + 3 * great + 1 * bad   (unchanged from v2)
 *   comboBonus     = maxCombo * 5                       (unchanged from v2)
 *   noMissBonus    = miss == 0 ? (perfect + great + bad) * 5 : 0  (unchanged)
 *   milestoneBonus = comboMilestones * 25               (NEW; the count was
 *                    already bound-checked against floor(hits / 50) by
 *                    scoreSemanticsDiagnostics, omitted means 0)
 *   finalBonus     = miss == 0 ? 100 : 0                (NEW)
 *   totalRatio     = judgment + comboBonus + noMissBonus
 *                    + milestoneBonus + finalBonus
 *   maxRatio       = noteCount * 15 + floor(noteCount / 50) * 25 + 100
 *   score          = floor(totalRatio * 1e6 / maxRatio)
 *
 * Every term is an integer, so the result is the exact floor of the rational
 * — no floating-point rounding exists on either side (C# `long` division on
 * Unity, IEEE-754 doubles here; both operands stay far below 2^53). Returns
 * `null` when the chart has no notes, like `computeUnityScoreV2`.
 */
export function computeUnityScoreV3(
  perfect: number,
  great: number,
  bad: number,
  miss: number,
  maxCombo: number | undefined,
  comboMilestones: number | undefined,
  noteCount: number,
): number | null {
  if (noteCount <= 0) {
    return null;
  }
  const maxScore = noteCount * 15 + Math.floor(noteCount / 50) * 25 + 100;
  const judgment = perfect * 5 + great * 3 + bad;
  const hits = perfect + great + bad;
  const ratioScore =
    judgment +
    (maxCombo ?? 0) * 5 +
    (miss === 0 ? hits * 5 : 0) +
    (comboMilestones ?? 0) * 25 +
    (miss === 0 ? 100 : 0);
  if (maxScore <= 0) {
    return null;
  }
  return Math.floor((ratioScore * 1_000_000) / maxScore);
}

/**
 * The scoring v4 score of a play (ADR-0033), computed with the same
 * deterministic integer arithmetic the Unity client uses:
 *
 *   judgment       = 5 * perfect + 3 * great + 1 * bad   (unchanged)
 *   comboBonus     = maxCombo * 5                       (unchanged, ratio points)
 *   noMissBonus    = miss == 0 ? (perfect + great + bad) * 5 : 0  (unchanged)
 *   milestoneBonus = comboMilestones * 25               (unchanged; bound-checked
 *                    by scoreSemanticsDiagnostics, omitted means 0)
 *   finalBonus     = judgmentsComplete && miss == 0 ? 100 : 0
 *                    (ADR-0033 / Issue #618: vests only once every chart
 *                    note has been judged — the live in-progress
 *                    projection excludes it)
 *   baseScore      = floor(judgment * 1e6 / (5 * noteCount))        ≤ 1,000,000
 *   bonusRatio     = comboBonus + noMissBonus + milestoneBonus + finalBonus
 *   maxBonusRatio  = noteCount * 10 + floor(noteCount / 50) * 25 + 100
 *   bonusScore     = floor(bonusRatio * 1e6 / maxBonusRatio)        ≤ 1,000,000
 *   score          = baseScore + bonusScore                          ≤ 2,000,000
 *
 * Unlike v3 nothing is normalized into a single ceiling: the judgment part
 * keeps the 1,000,000 cap and the bonuses are added on top, so a full-combo
 * run scores up to 2,000,000. `judgmentsComplete` mirrors the Unity
 * `Scoring.ScoreV4` parameter: a submission always implies it
 * (perfect+great+bad+miss == noteCount is enforced by
 * scoreSemanticsDiagnostics), so the server recompute passes `true` — the
 * settled view — while the parity vectors pin both the live and the
 * settled values. Returns `null` when the chart has no notes, like
 * `computeUnityScoreV3`.
 */
export function computeUnityScoreV4(
  perfect: number,
  great: number,
  bad: number,
  miss: number,
  maxCombo: number | undefined,
  comboMilestones: number | undefined,
  noteCount: number,
  judgmentsComplete: boolean,
): number | null {
  if (noteCount <= 0) {
    return null;
  }
  const judgment = perfect * 5 + great * 3 + bad;
  const hits = perfect + great + bad;
  const bonusRatio =
    (maxCombo ?? 0) * 5 +
    (miss === 0 ? hits * 5 : 0) +
    (comboMilestones ?? 0) * 25 +
    (judgmentsComplete && miss === 0 ? 100 : 0);
  const baseScore = Math.floor((judgment * 1_000_000) / (noteCount * 5));
  const maxBonusRatio = noteCount * 10 + Math.floor(noteCount / 50) * 25 + 100;
  const bonusScore = Math.floor((bonusRatio * 1_000_000) / maxBonusRatio);
  return baseScore + bonusScore;
}

/**
 * The scoring v5 score of a play (ADR-0035), computed with the same
 * deterministic integer arithmetic the Unity client uses:
 *
 *   judgment       = 5 * perfect + 3 * great + 1 * bad   (unchanged)
 *   comboBonus     = — (removed: combo continuation pays nothing)
 *   noMissBonus    = miss == 0 ? (perfect + great + bad) * 5 : 0  (unchanged)
 *   milestoneBonus = comboMilestones * 250              (bound-checked by
 *                    scoreSemanticsDiagnostics, omitted means 0)
 *   finalBonus     = judgmentsComplete && miss == 0 ? 100 : 0  (unchanged)
 *   baseScore      = floor(judgment * 1e6 / (5 * noteCount))        ≤ 1,000,000
 *   bonusRatio     = milestoneBonus + noMissBonus + finalBonus
 *   maxBonusRatio  = floor(noteCount / 50) * 250 + noteCount * 5 + 100
 *   bonusScore     = floor(bonusRatio * 1e6 / maxBonusRatio)        ≤ 1,000,000
 *   score          = baseScore + bonusScore                          ≤ 2,000,000
 *
 * `maxCombo` stays in the submission contract (bound-checked and used
 * for statistics) but no longer enters the formula — the combo reward
 * vests only at milestone crossings. Returns `null` when the chart has
 * no notes, like `computeUnityScoreV4`.
 */
export function computeUnityScoreV5(
  perfect: number,
  great: number,
  bad: number,
  miss: number,
  comboMilestones: number | undefined,
  noteCount: number,
  judgmentsComplete: boolean,
): number | null {
  if (noteCount <= 0) {
    return null;
  }
  const judgment = perfect * 5 + great * 3 + bad;
  const hits = perfect + great + bad;
  const bonusRatio =
    (comboMilestones ?? 0) * MILESTONE_BONUS_POINTS_V5 +
    (miss === 0 ? hits * 5 : 0) +
    (judgmentsComplete && miss === 0 ? 100 : 0);
  const baseScore = Math.floor((judgment * 1_000_000) / (noteCount * 5));
  const maxBonusRatio =
    Math.floor(noteCount / 50) * MILESTONE_BONUS_POINTS_V5 + noteCount * 5 + 100;
  const bonusScore = Math.floor((bonusRatio * 1_000_000) / maxBonusRatio);
  return baseScore + bonusScore;
}

/**
 * The scoring v6 score of a play (ADR-0037), computed with the same
 * deterministic integer arithmetic the Unity client uses:
 *
 *   qualityScore    = perfect * 1000 + great * 600 + bad * 200   (Miss = 0)
 *   milestoneScore  = comboMilestones * 5000        (bound-checked by
 *                     scoreSemanticsDiagnostics, omitted means 0)
 *   clearScore      = judgmentsComplete && miss == 0 ? 25000 : 0
 *   additionalScore = qualityScore + milestoneScore + clearScore
 *   baseScore       = floor(judgment * 1e6 / (5 * noteCount))     ≤ 1,000,000
 *   score           = baseScore + additionalScore
 *   chart maximum   = 1,000,000 + maxAdditionalScoreV6(noteCount)
 *
 * `maxCombo` stays in the submission contract (bound-checked and used
 * for statistics) but does not enter the formula, like v5.
 * `judgmentsComplete` mirrors the Unity `Scoring.ScoreV6` parameter: a
 * submission always implies it (perfect+great+bad+miss == noteCount is
 * enforced by scoreSemanticsDiagnostics), so the server recompute passes
 * `true` — the settled view — while the parity vectors pin both the live
 * and the settled values. Returns `null` when the chart has no notes,
 * like `computeUnityScoreV5`.
 */
export function computeUnityScoreV6(
  perfect: number,
  great: number,
  bad: number,
  miss: number,
  comboMilestones: number | undefined,
  noteCount: number,
  judgmentsComplete: boolean,
): number | null {
  if (noteCount <= 0) {
    return null;
  }
  const judgment = perfect * 5 + great * 3 + bad;
  const qualityScore = perfect * PERFECT_SCORE_V6 + great * GREAT_SCORE_V6 + bad * BAD_SCORE_V6;
  const milestoneScore = (comboMilestones ?? 0) * MILESTONE_SCORE_V6;
  const clearScore = judgmentsComplete && miss === 0 ? FULL_CLEAR_SCORE_V6 : 0;
  const additionalScore = qualityScore + milestoneScore + clearScore;
  const baseScore = Math.floor((judgment * 1_000_000) / (noteCount * 5));
  return baseScore + additionalScore;
}

/**
 * The v3 score as computed by Unity clients built before Issue #631: the
 * single-precision `Math.fround` emulation of the legacy `ComputeScore`
 * path. Fielded builds still send this value; the submission endpoint
 * accepts it as an alternative to the deterministic score so their plays
 * are not rejected with 422 (the stored score is always the deterministic
 * one). Not used for new contract vectors.
 */
export function computeUnityScoreV3Legacy(
  perfect: number,
  great: number,
  bad: number,
  miss: number,
  maxCombo: number | undefined,
  comboMilestones: number | undefined,
  noteCount: number,
): number | null {
  if (noteCount <= 0) {
    return null;
  }
  // Same single-precision emulation as the v1/v2 formulas: every accumulator
  // the pre-#631 Unity side held as a C# `float` is rounded with Math.fround,
  // in the same order (see computeUnityScore).
  const maxScore = Math.fround(noteCount * 15 + Math.floor(noteCount / 50) * 25 + 100);
  const judgment = Math.fround(perfect * 5 + great * 3 + bad);
  const comboBonus = Math.fround((maxCombo ?? 0) * 5);
  const hits = perfect + great + bad;
  const noMissBonus = miss === 0 ? Math.fround(hits * 5) : 0;
  const milestoneBonus = Math.fround((comboMilestones ?? 0) * 25);
  const finalBonus = miss === 0 ? 100 : 0;
  const ratioScore = Math.fround(judgment + comboBonus + noMissBonus + milestoneBonus + finalBonus);
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
  const scoringVersion = request.scoringVersion ?? SCORING_VERSION_V1;
  if (scoringVersion === SCORING_VERSION_V6) {
    return computeUnityScoreV6(
      request.perfect,
      request.great,
      request.bad,
      request.miss,
      request.comboMilestones,
      noteCount,
      // A submission is always a completed play (same rule as v4/v5).
      true,
    );
  }
  if (scoringVersion === SCORING_VERSION_V5) {
    return computeUnityScoreV5(
      request.perfect,
      request.great,
      request.bad,
      request.miss,
      request.comboMilestones,
      noteCount,
      // A submission is always a completed play (same rule as v4).
      true,
    );
  }
  if (scoringVersion === SCORING_VERSION_V4) {
    return computeUnityScoreV4(
      request.perfect,
      request.great,
      request.bad,
      request.miss,
      request.maxCombo,
      request.comboMilestones,
      noteCount,
      // A submission is always a completed play: the judgment sum must equal
      // the note count (scoreSemanticsDiagnostics), so the full-combo bonus
      // vests exactly like the settled Unity evaluation.
      true,
    );
  }
  if (scoringVersion === SCORING_VERSION_V3) {
    return computeUnityScoreV3(
      request.perfect,
      request.great,
      request.bad,
      request.miss,
      request.maxCombo,
      request.comboMilestones,
      noteCount,
    );
  }
  if (scoringVersion === SCORING_VERSION_V2) {
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
