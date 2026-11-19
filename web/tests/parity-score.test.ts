/**
 * Unity ↔ Web scoring parity contract test (Issue #612).
 *
 * Both sides verify the same shared vectors of
 * `contracts/parity/score-vectors.json`:
 *
 *   - `cases[]` — one play each. The Web `computeUnityScore` /
 *     `computeUnityScoreV2` / `computeUnityScoreV3` / `computeUnityScoreV4`
 *     must reproduce `scoreV1` / `scoreV2` / `scoreV3Settled` /
 *     `scoreV4Settled`. The `Settled` variants are the submission view of
 *     the play: a submission always implies judgmentsComplete
 *     (perfect+great+bad+miss == noteCount), which is exactly what the
 *     compute functions compute. The Unity EditMode suite
 *     (`Assets/Tests/Editor/ParityFixtureTests.cs`) checks `scoreV3` /
 *     `scoreV4` — the live in-progress scores that exclude the full-combo
 *     bonus until every note is judged (Issue #618, ADR-0033).
 *   - `v3SubmissionChecks[]` — the server-side `comboMilestones` bound
 *     checks of `scoreSemanticsDiagnostics` (ADR-0029). Only the Web side
 *     consumes them: the Unity client produces the count, it does not
 *     verify submissions.
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  computeUnityScore,
  computeUnityScoreV2,
  computeUnityScoreV3,
  computeUnityScoreV4,
  scoreSemanticsDiagnostics,
  type ScoreSubmission,
} from "../routes/api/v1/_score-model.ts";

interface ScoreCase {
  name: string;
  perfect: number;
  great: number;
  bad: number;
  miss: number;
  maxCombo: number;
  comboMilestones: number;
  noteCount: number;
  judgmentsComplete: boolean;
  scoreV1: number;
  scoreV2: number;
  scoreV3: number;
  scoreV3Settled: number;
  scoreV4: number;
  scoreV4Settled: number;
}

interface V3SubmissionCheck {
  name: string;
  perfect: number;
  great: number;
  bad: number;
  miss: number;
  maxCombo?: number;
  comboMilestones?: number;
  noteCount: number;
  expectValid: boolean;
}

const fixture = JSON.parse(
  readFileSync(new URL("../../contracts/parity/score-vectors.json", import.meta.url), "utf8"),
) as { cases: ScoreCase[]; v3SubmissionChecks: V3SubmissionCheck[] };

describe("score parity vectors (contracts/parity/score-vectors.json)", () => {
  it("has cases to check", () => {
    assert.ok(fixture.cases.length > 0);
    assert.ok(fixture.v3SubmissionChecks.length > 0);
  });

  for (const c of fixture.cases) {
    it(`${c.name}: v1/v2/v3/v4 settled scores match the Unity contract`, () => {
      assert.equal(computeUnityScore(c.perfect, c.great, c.bad, c.noteCount), c.scoreV1);
      assert.equal(
        computeUnityScoreV2(c.perfect, c.great, c.bad, c.miss, c.maxCombo, c.noteCount),
        c.scoreV2,
      );
      // The Web recompute is always the settled submission view; for cases
      // with judgmentsComplete=false and miss=0 it legitimately differs from
      // the Unity live scoreV3 (the +100 final bonus is not vested yet).
      assert.equal(
        computeUnityScoreV3(
          c.perfect,
          c.great,
          c.bad,
          c.miss,
          c.maxCombo,
          c.comboMilestones,
          c.noteCount,
        ),
        c.scoreV3Settled,
      );
      if (c.judgmentsComplete || c.miss !== 0) {
        // Once judgments are complete (or a miss already zeroed the bonus)
        // the settled and live values coincide.
        assert.equal(c.scoreV3, c.scoreV3Settled);
      } else {
        assert.ok(c.scoreV3 < c.scoreV3Settled, "live score must exclude the final bonus");
      }
      // v4 (ADR-0033) splits the score into a judgment base plus additive
      // bonuses. The Web function mirrors Scoring.ScoreV4's
      // judgmentsComplete flag, so it can reproduce both the live
      // in-progress score (c.judgmentsComplete) and the settled
      // submission view (always true).
      assert.equal(
        computeUnityScoreV4(
          c.perfect,
          c.great,
          c.bad,
          c.miss,
          c.maxCombo,
          c.comboMilestones,
          c.noteCount,
          c.judgmentsComplete,
        ),
        c.scoreV4,
      );
      assert.equal(
        computeUnityScoreV4(
          c.perfect,
          c.great,
          c.bad,
          c.miss,
          c.maxCombo,
          c.comboMilestones,
          c.noteCount,
          true,
        ),
        c.scoreV4Settled,
      );
      if (c.judgmentsComplete || c.miss !== 0) {
        assert.equal(c.scoreV4, c.scoreV4Settled);
      } else {
        // The unvested final bonus can floor to zero bonus points, so the
        // live score may legitimately equal the settled one.
        assert.ok(c.scoreV4 <= c.scoreV4Settled, "live score must not exceed settled");
      }
    });
  }
});

describe("v3 comboMilestones submission bounds (contracts/parity)", () => {
  for (const check of fixture.v3SubmissionChecks) {
    it(`${check.name}: ${check.expectValid ? "accepted" : "rejected"}`, () => {
      const request: ScoreSubmission = {
        songId: "parity",
        chartVersion: 1,
        nickname: "a",
        perfect: check.perfect,
        great: check.great,
        bad: check.bad,
        miss: check.miss,
        scoringVersion: 3,
      };
      if (check.maxCombo !== undefined) request.maxCombo = check.maxCombo;
      if (check.comboMilestones !== undefined) request.comboMilestones = check.comboMilestones;

      // ADR-0033: scoringVersion 4 applies the identical comboMilestones
      // bounds — the milestone term is unchanged, only the normalization is.
      for (const scoringVersion of [3, 4] as const) {
        const diagnostics = scoreSemanticsDiagnostics(
          { ...request, scoringVersion },
          check.noteCount,
        );
        if (check.expectValid) {
          assert.deepEqual(diagnostics, []);
        } else {
          assert.ok(diagnostics.length > 0, "expected at least one diagnostic");
        }
      }
    });
  }
});
