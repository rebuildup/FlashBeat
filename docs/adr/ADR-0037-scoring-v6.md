# ADR-0037: Scoring v6 (absolute additional score) and scoringVersion 6

- Status: Proposed
- Date: 2026-11-21
- Resolves: Issue #825 (parent Program Issue #803); builds on ADR-0033
  (v4 two-half split), ADR-0035 (v5 milestone-only combo bonus)
- Related: Issue #784 (combo bonus display), Issue #788 (grade/gauge
  consistency), ADR-0028 §3 (versions never recomputed)

## Context

Issue #825 asks for a scoring model that separates a base score from an
additional score where the additional part is an **absolute** score earned
from actual play performance, its maximum grows with the chart's note
count, normal C/B/A/S grades stay distinct from SS as a special
evaluation, and A-or-higher grades remain realistically attainable.

It also corrects the version number: #825 originally said
`scoringVersion 2`, which collides with the existing v2 formula. The new
model is **scoringVersion 6**; v1–v5 records stay untouched and
interpretable under their own formulas.

### Why the v4/v5 normalization fails the requirement

v4/v5 compute

```
bonusScore = floor(bonusRatio * 1_000_000 / maxBonusRatio(noteCount))
```

The bonus half is a **fraction of a per-chart theoretical maximum**,
renormalized to a fixed 1,000,000 ceiling. Consequences:

- The "additional score" carries no absolute meaning: earning half of the
  possible bonus yields 500,000 whether the chart has 100 or 1,000 notes.
  Two plays on different-length charts cannot be compared by the bonus
  figure itself.
- The chart-length signal the requirement wants ("more notes → larger
  maximum additional score") is deliberately divided out by the
  denominator.
- The total is capped at a fixed 2,000,000 for every chart, which is the
  "just swap the fixed cap" shape the request rejects.

## Decision

scoringVersion 6 keeps the v4/v5 **base half unchanged** and replaces the
normalized bonus half with **absolute score points**:

```
baseScore      = floor(judgment * 1_000_000 / (5 * noteCount))   // 0..1,000,000, unchanged

additionalScore = sum over judged notes of qualityPoints(note)
                + comboMilestones * MilestoneScoreV6
                + (judgmentsComplete && miss == 0 ? FullClearScoreV6 : 0)

qualityPoints:  Perfect → PerfectScoreV6
                Great   → GreatScoreV6
                Bad     → BadScoreV6
                Miss    → 0
```

- `additionalScore` is never divided by a chart maximum — it is the raw
  point total the play earned. `MaxAdditional(noteCount)` is defined only
  for display/rate purposes:

  ```
  MaxAdditional(N) = N * PerfectScoreV6
                   + floor(N / 50) * MilestoneScoreV6
                   + FullClearScoreV6
  ```

  which is strictly increasing in N, satisfying "longer charts allow a
  larger maximum additional score".

- Candidate constants (final values fixed by the distribution simulation
  required in the implementation ticket): `PerfectScoreV6 = 1000`,
  `GreatScoreV6 = 600`, `BadScoreV6 = 200`, `MilestoneScoreV6 = 5000`,
  `FullClearScoreV6 = 25000`. For N = 139 this gives
  `MaxAdditional = 174,000` (139,000 quality + 10,000 milestones +
  25,000 full clear; ~17% of the base ceiling); the bonus share
  stays meaningful without letting it dominate judgment quality.

- `score = baseScore + additionalScore`. There is no fixed total cap;
  the maximum is per-chart (`1,000,000 + MaxAdditional(N)`).

### Grades and SS

Grades are computed on the **fraction of the chart maximum**
`score / (1,000,000 + MaxAdditional(N))`, so variable chart maximums do
not distort comparisons:

- `SS` — special evaluation: `baseScore == 1,000,000` **and**
  `additionalScore == MaxAdditional(N)` (all-Perfect, full combo, every
  milestone). Equivalent to "the maximum this chart can give".
- `S` / `A` / `B` / `C` — fraction bands TBD by the distribution
  simulation; starting proposal S ≥ 0.90, A ≥ 0.75, B ≥ 0.55 of chart
  max, with the simulation required to confirm A-or-higher is not
  unrealistically strict across chart lengths and miss rates.

### Gauge, rankings, record comparison

- The in-play gauge keeps showing progress toward the **next combo
  milestone** (unchanged UX); the score display shows absolute
  `baseScore` and `additionalScore` separately.
- Rankings and records are always per `songId`; a song's chart (and thus
  `noteCount` and `MaxAdditional`) is fixed per published version, so raw
  `score` comparisons stay fair within a version. Cross-version or
  cross-song score comparison uses the fraction of chart max, never raw
  totals.
- `scoringVersion` is stored with every score record exactly as today;
  v1–v5 records are never recomputed. Unity, Web (`_score-model.ts`),
  API and D1 must implement the identical integer arithmetic — every
  component is an integer, so platform independence follows the v3/v4
  precedent (Issue #631).

## Consequences

- New `Scoring.Version6`, `BreakdownV6`, `ScoreV6`,
  `MaxAdditionalV6`, `ComponentScoreV6` mirroring the v4/v5 structure;
  `LatestVersion = 6`.
- Web `_score-model.ts` gains `computeUnityScoreV6`; parity fixtures pin
  Unity and server results.
- `V6GradeLogic`/`V6ResultRules` gain v6 bands; `ScoreMaxOf` becomes
  per-chart for v6.
- Implementation split: (a) ADR + this design, (b) Unity scoring +
  tests, (c) Web/API parity + contract, (d) distribution simulation
  validating grade bands, (e) result/HUD display of the two score parts.
