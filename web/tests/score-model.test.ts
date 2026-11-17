/**
 * Unit test of the score model (issue #133).
 *
 * Two concerns:
 *
 *   1. `SCORE_SUBMISSION_SCHEMA` is a bundled copy of the frozen contract
 *      schema (`contracts/openapi.v1.yaml` → `ScoreSubmission`), because a
 *      Worker has no filesystem to read the contract from at runtime. This
 *      test reads the real contract file and fails when the copy drifts.
 *   2. `computeUnityScore` / `computeUnityScoreV2` reproduce the Unity score
 *      formulas exactly (v1 and v2 of ADR-0028). The reference values are
 *      hand-computed:
 *
 *        v1: maxScore   = noteCount * 5
 *            ratioScore = 5 * perfect + 3 * great + 1 * bad
 *            score      = (int)Math.Round(1e6 * Floor(ratioScore / maxScore * 1e6) / 1e6)
 *
 *        v2: judgment    = 5 * perfect + 3 * great + 1 * bad
 *            comboBonus  = maxCombo * 5
 *            noMissBonus = miss == 0 ? (perfect + great + bad) * 5 : 0
 *            ratioScore  = judgment + comboBonus + noMissBonus
 *            maxScore    = noteCount * 15
 *            score       = same ComputeScore rounding as v1
 */
import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { openapi } from "./helpers/contract.ts";
import {
  LATEST_SCORING_VERSION,
  SCORE_SUBMISSION_SCHEMA,
  computeScoreForVersion,
  computeUnityScore,
  computeUnityScoreV2,
  nicknameDiagnostics,
  parseScoringVersionQuery,
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

  it("accepts scoring versions 1 and 2 and an omitted version", () => {
    assert.deepEqual(scoreSemanticsDiagnostics({ ...request, scoringVersion: 1 }, 139), []);
    assert.deepEqual(scoreSemanticsDiagnostics({ ...request, scoringVersion: 2 }, 139), []);
    assert.deepEqual(scoreSemanticsDiagnostics(request, 139), []);
  });

  it("rejects a scoring version the server cannot recompute", () => {
    const diagnostics = scoreSemanticsDiagnostics({ ...request, scoringVersion: 3 }, 139);
    assert.equal(diagnostics.length, 1);
    assert.equal(diagnostics[0]?.path, "/scoringVersion");
  });
});

describe("computeUnityScore single precision (GManager.ratioScore/maxScore are C# float)", () => {
  // Expected values computed with float32 arithmetic (numpy.float32) exactly like Unity:
  // floor(float32(float32(ratioScore / maxScore) * 1000000)). Plain doubles give 520000, 512499, 831578.
  it("5 notes, 2 perfect + 1 great (ratioScore 13) is 519999", () => {
    assert.equal(computeUnityScore(2, 1, 0, 5), 519999);
  });
  it("16 notes, 8 perfect + 1 bad (ratioScore 41) is 512500", () => {
    assert.equal(computeUnityScore(8, 0, 1, 16), 512500);
  });
  it("19 notes, 15 perfect + 1 great + 1 bad (ratioScore 79) is 831579", () => {
    assert.equal(computeUnityScore(15, 1, 1, 19), 831579);
  });
  it("a perfect run is always exactly 1,000,000 for any note count", () => {
    for (const notes of [1, 7, 53, 139, 262, 603, 999]) {
      assert.equal(computeUnityScore(notes, 0, 0, notes), 1000000);
    }
  });
});

describe("computeUnityScoreV2 (ADR-0028)", () => {
  it("a perfect run with full combo and no miss scores 1000000", () => {
    // 139 notes, all perfect, maxCombo 139:
    //   judgment 695 + comboBonus 695 + noMissBonus 695 = 2085, maxRatio 2085.
    assert.equal(computeUnityScoreV2(139, 0, 0, 0, 139, 139), 1000000);
  });

  it("adds the combo and no-miss bonuses to the judgment score", () => {
    // 10 notes: 9 perfect + 1 great, maxCombo 10, no miss.
    //   judgment = 45 + 3 = 48, comboBonus = 50, noMissBonus = 10 * 5 = 50
    //   ratioScore = 148, maxScore = 150 -> 986666.66... -> 986666
    assert.equal(computeUnityScoreV2(9, 1, 0, 0, 10, 10), 986666);
  });

  it("awards no no-miss bonus when a note was missed", () => {
    // 10 notes: 8 perfect + 1 great + 1 miss, maxCombo 9.
    //   judgment = 40 + 3 = 43, comboBonus = 45, noMissBonus = 0
    //   ratioScore = 88, maxScore = 150 -> 586666.66... -> 586666
    assert.equal(computeUnityScoreV2(8, 1, 0, 1, 9, 10), 586666);
  });

  it("treats an omitted maxCombo as a zero combo bonus", () => {
    // Same play without maxCombo: ratioScore = 48 + 0 + 50 = 98 -> 653333
    assert.equal(computeUnityScoreV2(9, 1, 0, 0, undefined, 10), 653333);
  });

  it("a chart without notes cannot be scored", () => {
    assert.equal(computeUnityScoreV2(0, 0, 0, 0, 0, 0), null);
  });
});

describe("computeScoreForVersion", () => {
  const base = {
    songId: "who",
    chartVersion: 1,
    nickname: "abc",
    perfect: 9,
    great: 1,
    bad: 0,
    miss: 0,
    maxCombo: 10,
  };

  it("uses the v1 formula when scoringVersion is omitted or 1", () => {
    // v1 of the same play: ratioScore 48, maxScore 50 -> 960000.
    assert.equal(computeScoreForVersion({ ...base }, 10), 960000);
    assert.equal(computeScoreForVersion({ ...base, scoringVersion: 1 }, 10), 960000);
  });

  it("uses the v2 formula when scoringVersion is 2", () => {
    assert.equal(computeScoreForVersion({ ...base, scoringVersion: 2 }, 10), 986666);
  });
});

describe("parseScoringVersionQuery", () => {
  it("defaults to the latest scoring version", () => {
    assert.equal(LATEST_SCORING_VERSION, 2);
    assert.equal(parseScoringVersionQuery(undefined), 2);
  });

  it("accepts positive integers", () => {
    assert.equal(parseScoringVersionQuery("1"), 1);
    assert.equal(parseScoringVersionQuery("2"), 2);
  });

  it("rejects missing, non-numeric and non-positive values", () => {
    for (const raw of ["", "0", "-1", "1.5", "abc"]) {
      assert.equal(parseScoringVersionQuery(raw), null, `expected null for "${raw}"`);
    }
  });
});
