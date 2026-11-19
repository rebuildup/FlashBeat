# v0.1.31 candidate QA — 1ab0c33

Candidate SHA: `1ab0c335` (release-0-1-31 HEAD at freeze).
Artifacts (local, unpublished): `dist/FlashBeat-0.1.31-macos-1ab0c33.zip`, `dist/FlashBeat-0.1.31-win64-1ab0c33.zip` (+ `.sha256` sidecars).

## Automated verification (same SHA)

- `./script/compile-check.sh`: PASS (246 sources)
- Unity EditMode: **2063/2063 PASS**
- Unity PlayMode: **6/6 PASS** (`V6ResultInputPlayModeTests` — real EventSystem/TMP path)

## macOS build autotest (`-autotest`, built app @ 1ab0c33)

| Harness | Result |
|---|---|
| play | pass=22 fail=0 |
| keyguide | pass=5 fail=0 |
| v6screens | pass=90 fail=0 (clip partial=0 outside=0, 21 screenshots) |
| v6interact | pass=97 fail=0 |
| v6hold | pass=41 fail=0 (hold clear=3 broken=0) |
| v6songend | pass=32 fail=0 (judged=130==chartNotes, forcedByDeadline=False) |
| resultinput | modalOpened=True (physical typing not automated) |

Raw outputs: `/tmp/qa-1ab0c335/` (screenshots + result txt; ephemeral).

## Win64

- `BuildScript.BuildStandaloneWindows64` succeeded (242,869,323 bytes). Runtime execution on Windows: **not observed** (issue #22).

## Not verified (explicit)

- IME composition/commit, physical keyboard input, GPU color/gamma on device
- Visual/auditory acceptance on physical devices (notes brightness, verdict animation, preview parity, click sound)
- Real update apply + crash recovery end-to-end
