# `release/check` — release gate scripts (issue #22)

Read-only helpers for the Win64 exhibition release gate. They **never build, never copy, never delete**
an artifact and never start the Unity Editor.

| Script | What it does |
|---|---|
| `stage-manifest.json` | Machine-readable index of every stage: artifact paths, SHA-256, evidence files, proven / unproven gaps |
| `verify-stage.ps1` | Re-computes the artifact fingerprint and the evidence hashes and compares them with the manifest; also checks that `docs/evidence/index.md` mentions every fingerprint |
| `drive-gameplay.ps1` | Gameplay smoke driver (the #11 driver, parameterized): artifact path, output directory, window geometry; writes `run-summary.json` |
| `redact-log.ps1` | Masks signed YouTube URLs / video ids in a player log, or `-Check`s an already masked log |
| `tests/Run-VerifyStageTests.ps1` | Fixture tests for `verify-stage.ps1`: builds a throwaway stage + manifest in a temp dir and asserts good stage passes / missing `FlashBeat.exe` fails / fingerprint mismatch fails / evidence hash mismatch fails (no network, no real artifact) |

## Typical use

```powershell
# 1. does the artifact on this machine still match the manifest?
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/verify-stage.ps1

# 2. run the gameplay smoke on a build, windowed at 1280x720
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/drive-gameplay.ps1 `
    -Exe 'D:\4_apps\Unity\Build\FlashBeatBuild\Build0.1.0-win64-1c5137e\FlashBeat.exe' `
    -OutDir 'D:\4_apps\Unity\Build\FlashBeatEvidence\2026-11-06-S0-archive' `
    -Width 1280 -Height 720 -Fullscreen 0

# 3. mask the produced player log before it is published
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 `
    -InPath 'D:\4_apps\Unity\Build\FlashBeatEvidence\2026-11-06-S0-archive\Player.log' `
    -OutPath 'D:\4_apps\Unity\Build\FlashBeatEvidence\2026-11-06-S0-archive\Player.redacted.log'
```

## Shared machine

The driver refuses to start while another `FlashBeat.exe` is running (two drivers on one desktop fight
over the keyboard and the focus). `-AllowConcurrentPlayers` overrides that **only** when the other
instances are known to be idle; the run is then flagged as unreliable if the other automation
disturbs it. The foreground-window guard stays active in every case: the driver aborts instead of
sending keystrokes into somebody else's window.

## Rules

* The fingerprint is the **folder** hash, not `FlashBeat.exe`: every Unity build ships the same
  launcher stub exe, so only the folder identifies the artifact.
* A stage without an artifact is reported as **UNPROVEN** — that is a normal result, not a script error.
* The Unity Editor must not be started in parallel with the #13 worker; these scripts never start it.
* `tools/**` belongs to #35 (`tools/release/**`) and #36 (`tools/update/**`) — do not add files there.
