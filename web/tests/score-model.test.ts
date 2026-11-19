/**
 * Unit test of the score model (issue #133).
 *
 * Two concerns:
 *
 *   1. `SCORE_SUBMISSION_SCHEMA` is a bundled copy of the frozen contract
 *      schema (`contracts/openapi.v1.yaml` → `ScoreSubmission`), because a
 *      Worker has no filesystem to read the contract from at runtime. This
 *      test reads the real contract file and fails when the copy drifts.
 *   2. `computeUnityScore` / `computeUnityScoreV2` / `computeUnityScoreV3`
 *      reproduce the Unity score formulas exactly (v1 of ADR-0028, v2 of
 *      ADR-0028, v3 of ADR-0029). The reference values are hand-computed:
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
 *
 *        v3: judgment       = 5 * perfect + 3 * great + 1 * bad
 *            comboBonus     = maxCombo * 5
 *            noMissBonus    = miss == 0 ? (perfect + great + bad) * 5 : 0
 *            milestoneBonus = comboMilestones * 25
 *            finalBonus     = miss == 0 ? 100 : 0
 *            totalRatio     = judgment + comboBonus + noMissBonus
 *                             + milestoneBonus + finalBonus
 *            maxRatio       = noteCount * 15 + floor(noteCount / 50) * 25 + 100
 *            score          = same ComputeScore rounding as v1/v2
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
  computeUnityScoreV3,
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
  it("accepts 1 to 16 characters after trimming", () => {
    assert.deepEqual(nicknameDiagnostics("abc"), []);
    assert.deepEqual(nicknameDiagnostics("  ok  "), []);
    assert.deepEqual(nicknameDiagnostics("abcdefghijkl"), []);
    assert.deepEqual(nicknameDiagnostics("abcdefghijklmnop"), []);
    assert.deepEqual(nicknameDiagnostics("あいうえおかきくけこ"), []);
  });

  it("rejects an empty or whitespace-only nickname", () => {
    const diagnostics = nicknameDiagnostics("   ");
    assert.equal(diagnostics.length, 1);
    assert.equal(diagnostics[0]?.path, "/nickname");
  });

  it("rejects more than 16 characters after trimming", () => {
    const diagnostics = nicknameDiagnostics("abcdefghijklmnopq");
    assert.equal(diagnostics.length, 1);
    assert.match(diagnostics[0]?.message ?? "", /16/);
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

  it("accepts scoring versions 1, 2, 3 and an omitted version", () => {
    assert.deepEqual(scoreSemanticsDiagnostics({ ...request, scoringVersion: 1 }, 139), []);
    assert.deepEqual(scoreSemanticsDiagnostics({ ...request, scoringVersion: 2 }, 139), []);
    // v3 on a no-miss play must report the exact crossing count (Issue #618).
    assert.deepEqual(
      scoreSemanticsDiagnostics({ ...request, scoringVersion: 3, comboMilestones: 2 }, 139),
      [],
    );
    assert.deepEqual(scoreSemanticsDiagnostics(request, 139), []);
  });

  it("rejects a scoring version the server cannot recompute", () => {
    for (const scoringVersion of [0, 4]) {
      const diagnostics = scoreSemanticsDiagnostics({ ...request, scoringVersion }, 139);
      assert.equal(diagnostics.length, 1);
      assert.equal(diagnostics[0]?.path, "/scoringVersion");
    }
  });

  it("verifies comboMilestones exactly when miss is 0 (ADR-0029, Issue #618)", () => {
    // 139 notes, 139 hits, no miss: the whole play is one unbroken run, so
    // the crossing count is exactly floor(139 / 50) = 2 — over- and
    // under-claims are both inconsistent.
    const base = { ...request, maxCombo: 139 };
    assert.deepEqual(
      scoreSemanticsDiagnostics({ ...base, scoringVersion: 3, comboMilestones: 2 }, 139),
      [],
    );
    for (const comboMilestones of [1, 3]) {
      const diagnostics = scoreSemanticsDiagnostics(
        { ...base, scoringVersion: 3, comboMilestones },
        139,
      );
      assert.equal(diagnostics.length, 1, `expected one diagnostic for ${comboMilestones}`);
      assert.equal(diagnostics[0]?.path, "/comboMilestones");
    }
    // Omitted is treated as 0 — also wrong for a no-miss play with hits >= 50.
    const omitted = scoreSemanticsDiagnostics({ ...base, scoringVersion: 3 }, 139);
    assert.equal(omitted.length, 1);
    assert.equal(omitted[0]?.path, "/comboMilestones");

    // Versions 1 and 2 ignore the field entirely, however large it is.
    for (const scoringVersion of [1, 2, undefined]) {
      assert.deepEqual(
        scoreSemanticsDiagnostics({ ...base, scoringVersion, comboMilestones: 999 }, 139),
        [],
      );
    }
  });

  it("bound-checks comboMilestones against the runs a missed play allows", () => {
    // 139 notes: 100P + 30G + 8B + 1M (hits 138), maxCombo 60, miss 1.
    // At most miss + 1 = 2 runs of at most floor(60/50) = 1 crossing each,
    // and at most floor(138/50) = 2 overall → bound 2.
    const missed = { ...request, bad: 8, miss: 1, maxCombo: 60 };
    assert.deepEqual(
      scoreSemanticsDiagnostics({ ...missed, scoringVersion: 3, comboMilestones: 2 }, 139),
      [],
    );
    const over = scoreSemanticsDiagnostics(
      { ...missed, scoringVersion: 3, comboMilestones: 3 },
      139,
    );
    assert.equal(over.length, 1);
    assert.equal(over[0]?.path, "/comboMilestones");
  });

  it("rejects milestone claims that need longer runs than maxCombo allows", () => {
    // The reported exploit shape (Issue #618): hits split by a miss so no
    // run reaches 50 combo, yet a crossing is claimed. With maxCombo 30
    // and 1 miss, at most (1 + 1) * floor(30/50) = 0 crossings are
    // possible; the old floor(hits/50) = 2 bound accepted the claim.
    // (The aggregates still cannot prove the run structure — this is the
    // tightest bound they allow, see ADR-0029.)
    const missed = { ...request, bad: 8, miss: 1, maxCombo: 30 };
    const diagnostics = scoreSemanticsDiagnostics(
      { ...missed, scoringVersion: 3, comboMilestones: 1 },
      139,
    );
    assert.equal(diagnostics.length, 1);
    assert.equal(diagnostics[0]?.path, "/comboMilestones");
  });

  it("rejects a v3 maxCombo above the hit count", () => {
    // Combo only grows on head hits: 138 hits can never reach maxCombo 139
    // (the note-count bound of 139 still passes, so this is the tighter
    // scoringVersion-3 check).
    const missed = { ...request, bad: 8, miss: 1, maxCombo: 139 };
    const diagnostics = scoreSemanticsDiagnostics({ ...missed, scoringVersion: 3 }, 139);
    assert.equal(diagnostics.length, 1);
    assert.equal(diagnostics[0]?.path, "/maxCombo");

    // Versions 1 and 2 keep the note-count bound only.
    assert.deepEqual(scoreSemanticsDiagnostics({ ...missed, scoringVersion: 2 }, 139), []);
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

describe("computeUnityScoreV3 (ADR-0029)", () => {
  it("a perfect run with full combo, milestones and no miss scores 1000000", () => {
    // 139 notes, all perfect, maxCombo 139, 2 milestone crossings (bound
    // floor(139/50) = 2):
    //   judgment 695 + comboBonus 695 + noMissBonus 695 + milestoneBonus 50
    //   + finalBonus 100 = 2235, maxRatio = 15*139 + 2*25 + 100 = 2235.
    assert.equal(computeUnityScoreV3(139, 0, 0, 0, 139, 2, 139), 1000000);
  });

  it("adds the milestone and full-combo bonuses to the v2 components", () => {
    // 139 notes: 100 perfect + 30 great + 9 bad, maxCombo 139, 2 milestones,
    // no miss.
    //   judgment 599 + comboBonus 695 + noMissBonus 695 + milestoneBonus 50
    //   + finalBonus 100 = 2139, maxRatio 2235 -> 957046 (deterministic
    //   integer floor since Issue #631; the legacy float path gave 957047)
    assert.equal(computeUnityScoreV3(100, 30, 9, 0, 139, 2, 139), 957046);
  });

  it("awards no final bonus and no no-miss bonus when a note was missed", () => {
    // Same play with one miss instead of a bad: 100P + 30G + 8B + 1M.
    //   judgment 598 + comboBonus 695 + noMissBonus 0 + milestoneBonus 50
    //   + finalBonus 0 = 1343, maxRatio 2235 -> 600894
    assert.equal(computeUnityScoreV3(100, 30, 8, 1, 139, 2, 139), 600894);
  });

  it("treats omitted maxCombo and comboMilestones as zero bonuses", () => {
    // 10 notes, 9 perfect + 1 great, no miss:
    //   judgment 48 + comboBonus 0 + noMissBonus 50 + milestoneBonus 0
    //   + finalBonus 100 = 198, maxRatio = 15*10 + 0 + 100 = 250 -> 792000
    assert.equal(computeUnityScoreV3(9, 1, 0, 0, undefined, undefined, 10), 792000);
  });

  it("a chart without notes cannot be scored", () => {
    assert.equal(computeUnityScoreV3(0, 0, 0, 0, 0, 0, 0), null);
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

  it("uses the v3 formula when scoringVersion is 3", () => {
    // Same 10-note play plus the v3 bonuses: maxCombo 10, 0 milestones
    // (bound floor(10/50) = 0), no miss -> finalBonus 100.
    //   totalRatio 148 + 0 + 100 = 248, maxRatio 250 -> 992000
    assert.equal(computeScoreForVersion({ ...base, scoringVersion: 3 }, 10), 992000);
  });
});

describe("parseScoringVersionQuery", () => {
  it("defaults to the latest scoring version", () => {
    assert.equal(LATEST_SCORING_VERSION, 3);
    assert.equal(parseScoringVersionQuery(undefined), 3);
  });

  it("accepts positive integers", () => {
    assert.equal(parseScoringVersionQuery("1"), 1);
    assert.equal(parseScoringVersionQuery("2"), 2);
    assert.equal(parseScoringVersionQuery("3"), 3);
  });

  it("rejects missing, non-numeric and non-positive values", () => {
    for (const raw of ["", "0", "-1", "1.5", "abc"]) {
      assert.equal(parseScoringVersionQuery(raw), null, `expected null for "${raw}"`);
    }
  });
});
