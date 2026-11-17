# Unity ↔ Web parity fixtures (Issue #612)

Shared input→output vectors that both score/timing implementations must
reproduce. There is exactly one copy of each file — nothing is generated or
copied — so drift between the two sides is impossible by construction; what
can drift is an implementation, and the tests below catch that.

| File | Contract | Unity consumer | Web consumer |
| --- | --- | --- | --- |
| `score-vectors.json` | scoring v1/v2/v3 (`Scoring.*`, `Judge.ComputeScore`) and the v3 `comboMilestones` submission bounds | `Assets/Tests/Editor/ParityFixtureTests.cs` | `web/tests/parity-score.test.ts` |
| `chart-timing.json` | note start / hold end times (`NotesManager.NoteTimeSeconds`, `LongNote.EndSeconds`) | `Assets/Tests/Editor/ParityFixtureTests.cs` | `web/tests/parity-timing.test.ts` |

## Format

- `score-vectors.json` → `cases[]`: one play each (`perfect`, `great`, `bad`,
  `miss`, `maxCombo`, `comboMilestones`, `noteCount`, `judgmentsComplete`).
  `scoreV1`/`scoreV2`/`scoreV3` are the Unity-side scores — `scoreV3` honours
  the case's `judgmentsComplete` flag (Issue #618: the full-combo bonus vests
  only once every note is judged). `scoreV3Settled` is the same play as a
  completed submission and equals the Web `computeUnityScoreV3` result for
  every case. `v3SubmissionChecks[]` pins the server-side accept/reject
  bounds of `scoreSemanticsDiagnostics`; only the Web suite consumes them
  (the Unity client produces the count, it does not verify submissions).
- `chart-timing.json` → `cases[]`: `bpm`, `num`, `lpb` and optional `endNum`.
  `seconds`/`endSeconds` are the **float32** results of the Unity operation
  order, not the mathematically exact values.

## Float semantics

Both implementations work in C# `float` (single precision). The Web side
emulates it with `Math.fround` in the same operation order (see
`web/routes/api/v1/_score-model.ts` and the parity tests) and asserts the
fixture values **exactly**. Expected values were computed once with that
emulation and are pinned as contract vectors.

The Unity EditMode suite asserts the same vectors through the real C# code
(`Scoring.*`, `Judge.ComputeScore`, `NotesManager.NoteTimeSeconds`,
`LongNote.EndSeconds`) but with a small tolerance: the Editor Mono JIT has
been observed to keep double intermediates in some float expressions,
producing last-ulp differences versus strict float32 (a ±1 score point or
~1e-6 relative on times). That slack is far below any real formula drift;
if a platform ever needs bit-exact parity, verify on the shipping backend.

## Updating

Add a case to the relevant JSON file when the scoring or timing contract
changes (a new scoring version, a new timing field). Both test suites must
keep passing; if a deliberate contract change alters an expected value,
update the fixture in the same commit as the implementation change and note
it in the commit message.
