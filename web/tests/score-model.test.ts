/**
 * Unit test of the score model (issue #133).
 *
 * Two concerns:
 *
 *   1. `SCORE_SUBMISSION_SCHEMA` is a bundled copy of the frozen contract
 *      schema (`contracts/openapi.v1.yaml` → `ScoreSubmission`), because a
 *      Worker has no filesystem to read the contract from at runtime. This
 *      test reads the real contract file and fails when the copy drifts.
 *   2. `computeUnityScore` reproduces the Unity score formula exactly. The
 *      reference values are hand-computed from
 *      `Assets/Game/Scripts/Gameplay/Judge.cs`:
 *
 *        maxScore   = noteCount * 5                       (NotesManager)
 *        ratioScore = 5 * perfect + 3 * great + 1 * bad   (Judge.Judgement)
 *        score      = (int)Math.Round(1e6 * Floor(ratioScore / maxScore * 1e6) / 1e6)
 */
import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { openapi } from "./helpers/contract.ts";
import {
  SCORE_SUBMISSION_SCHEMA,
  computeUnityScore,
  nicknameDiagnostics,
  scoreSemanticsDiagnostics,
} from "../routes/api/v1/_score-model.ts";

describe("SCORE_SUBMISSION_SCHEMA", () => {
  it("matches the contract's ScoreSubmission schema", () => {
    const components = openapi.components as unknown as {
      schemas: Record<string, Record<string, unknown>>;
    };
    const contractSchema = components.schemas.ScoreSubmission;

    assert.deepEqual(SCORE_SUBMISSION_SCHEMA, contractSchema);
  });
});

describe("computeUnityScore", () => {
  it("example 1: a full perfect combo of the seeded Who chart scores 1000000", () => {
    // The seeded chart has 139 notes; all perfect:
    //   maxScore = 139 * 5 = 695, ratioScore = 139 * 5 = 695
    //   695 / 695 * 1e6 = 1000000 -> floor 1000000 -> 1000000
    assert.equal(computeUnityScore(139, 0, 0, 139), 1000000);
  });

  it("example 2: 80 perfect / 15 great / 5 bad of 100 notes scores 900000", () => {
    //   maxScore = 500, ratioScore = 5*80 + 3*15 + 1*5 = 450
    //   450 / 500 * 1e6 = 900000 -> floor 900000 -> 900000
    assert.equal(computeUnityScore(80, 15, 5, 100), 900000);
  });

  it("example 3: 100 perfect / 30 great / 9 bad of 139 notes scores 861870", () => {
    //   maxScore = 695, ratioScore = 500 + 90 + 9 = 599
    //   599 / 695 * 1e6 = 861870.5035971... -> floor 861870 -> 861870
    //   (the floor before the rounding is what makes this differ from 861871)
    assert.equal(computeUnityScore(100, 30, 9, 139), 861870);
  });

  it("misses score nothing, and a chart without notes cannot be scored", () => {
    // 5 perfect + 3 great + 1 bad + 1 miss of 10 notes:
    //   ratioScore = 25 + 9 + 1 = 35, maxScore = 50 -> 700000
    assert.equal(computeUnityScore(5, 3, 1, 10), 700000);
    assert.equal(computeUnityScore(0, 0, 0, 1), 0);
    assert.equal(computeUnityScore(0, 0, 0, 0), null);
  });
});

describe("nicknameDiagnostics", () => {
  it("accepts 1 to 12 characters after trimming", () => {
    assert.deepEqual(nicknameDiagnostics("abc"), []);
    assert.deepEqual(nicknameDiagnostics("  ok  "), []);
    assert.deepEqual(nicknameDiagnostics("abcdefghijkl"), []);
    assert.deepEqual(nicknameDiagnostics("あいうえおかきくけこ"), []);
  });

  it("rejects an empty or whitespace-only nickname", () => {
    const diagnostics = nicknameDiagnostics("   ");
    assert.equal(diagnostics.length, 1);
    assert.equal(diagnostics[0]?.path, "/nickname");
  });

  it("rejects more than 12 characters after trimming", () => {
    const diagnostics = nicknameDiagnostics("abcdefghijklm");
    assert.equal(diagnostics.length, 1);
    assert.match(diagnostics[0]?.message ?? "", /12/);
  });

  it("rejects control characters", () => {
    for (const nickname of ["a\u0000b", "a\u001Fb", "a\u007Fb"]) {
      const diagnostics = nicknameDiagnostics(nickname);
      assert.ok(
        diagnostics.some((diagnostic) => /control/.test(diagnostic.message)),
        `expected a control-character diagnostic for ${JSON.stringify(nickname)}`,
      );
    }
  });
});

describe("scoreSemanticsDiagnostics", () => {
  const request = {
    songId: "who",
    chartVersion: 1,
    nickname: "abc",
    perfect: 100,
    great: 30,
    bad: 9,
    miss: 0,
  };

  it("accepts a judgment sum equal to the note count and a combo within it", () => {
    // 100 + 30 + 9 + 0 = 139 notes.
    assert.deepEqual(scoreSemanticsDiagnostics({ ...request, maxCombo: 139 }, 139), []);
  });

  it("rejects a judgment sum that differs from the note count", () => {
    const diagnostics = scoreSemanticsDiagnostics(request, 138);
    assert.equal(diagnostics.length, 1);
    assert.match(diagnostics[0]?.message ?? "", /138/);
  });

  it("rejects a combo above the note count", () => {
    const diagnostics = scoreSemanticsDiagnostics({ ...request, maxCombo: 140 }, 139);
    assert.equal(diagnostics.length, 1);
    assert.equal(diagnostics[0]?.path, "/maxCombo");
  });
});
