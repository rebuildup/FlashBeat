# Unity ↔ Web parity fixtures (Issue #612)

Shared input→output vectors that both score/timing implementations must
reproduce. There is exactly one copy of each file — nothing is generated or
copied — so drift between the two sides is impossible by construction; what
can drift is an implementation, and the tests below catch that.

| File | Contract | Unity consumer | Web consumer |
| --- | --- | --- | --- |
| `score-vectors.json` | scoring v1/v2/v3 (`Scoring.*`, `Judge.ComputeScore`, `Scoring.ScoreV3`) and the v3 `comboMilestones` submission bounds | `Assets/Tests/Editor/ParityFixtureTests.cs` | `web/tests/parity-score.test.ts` |
| `chart-timing.json` | note start / hold end times (`NotesManager.NoteTimeSeconds`, `LongNote.EndSeconds`) | `Assets/Tests/Editor/ParityFixtureTests.cs` | `web/tests/parity-timing.test.ts` |

## Format

- `score-vectors.json` → `cases[]`: one play each (`perfect`, `great`, `bad`,
  `miss`, `maxCombo`, `comboMilestones`, `noteCount`, `judgmentsComplete`).
  `scoreV1`/`scoreV2` are the legacy float-emulated scores; `scoreV3` honours
  the case's `judgmentsComplete` flag (Issue #618: the full-combo bonus vests
  only once every note is judged) and `scoreV3Settled` is the same play as a
  completed submission. Both v3 values equal the Web `computeUnityScoreV3`
  result for every case. `v3SubmissionChecks[]` pins the server-side
  accept/reject bounds of `scoreSemanticsDiagnostics`; only the Web suite
  consumes them (the Unity client produces the count, it does not verify
  submissions).
- `chart-timing.json` → `cases[]`: `bpm`, `num`, `lpb` and optional `endNum`.
  `seconds`/`endSeconds` are the **float32** results of the Unity operation
  order, not the mathematically exact values.

## Float semantics

v1 and v2 work in C# `float` (single precision). The Web side emulates it
with `Math.fround` in the same operation order (see
`web/routes/api/v1/_score-model.ts` and the parity tests) and asserts the
fixture values **exactly**.

v3 is different (Issue #631): every component is an integer, so both sides
compute `floor(ratio * 1_000_000 / maxRatio)` in exact integer arithmetic —
C# `long` on Unity (`Scoring.ScoreV3`), IEEE-754 doubles on the Web. There
is no floating-point rounding on either side, so the values are bit-exact
on every platform by construction and both suites assert them **exactly**
(no tolerance). The pre-#631 float path survives only as
`computeUnityScoreV3Legacy` on the server, which accepts it from fielded
clients as a submission fallback.

The Unity EditMode suite asserts the v1/v2 vectors through the real C#
code (`Judge.ComputeScore`, `Scoring.ScoreV2`) with a small tolerance:
the Editor Mono JIT has been observed to keep double intermediates in
some float expressions, producing last-ulp differences versus strict
float32 (a ±1 score point). That slack is far below any real formula
drift; the Web suite still rejects v1/v2 drift exactly.

## Updating

Add a case to the relevant JSON file when the scoring or timing contract
changes (a new scoring version, a new timing field). Both test suites must
keep passing; if a deliberate contract change alters an expected value,
update the fixture in the same commit as the implementation change and note
it in the commit message.
