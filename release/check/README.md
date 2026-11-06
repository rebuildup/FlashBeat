# `release/check` — release gate scripts (issue #22)

Read-only helpers for the Win64 exhibition release gate. They **never build, never copy, never delete**
an artifact and never start the Unity Editor.

| Script | What it does |
|---|---|
| `stage-manifest.json` | Machine-readable index of every stage: artifact paths, SHA-256, evidence files, proven / unproven gaps |
| `verify-stage.ps1` | Re-computes the artifact fingerprint and the evidence hashes and compares them with the manifest; also checks that `docs/evidence/index.md` mentions every fingerprint |
| `drive-gameplay.ps1` | Gameplay smoke driver (the #11 driver, parameterized): artifact path, output directory, window geometry; writes `run-summary.json` |
| `redact-log.ps1` | Masks signed YouTube URLs / video ids in a player log, or `-Check`s an already masked log |

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

## Rules

* The fingerprint is the **folder** hash, not `FlashBeat.exe`: every Unity build ships the same
  launcher stub exe, so only the folder identifies the artifact.
* A stage without an artifact is reported as **UNPROVEN** — that is a normal result, not a script error.
* The Unity Editor must not be started in parallel with the #13 worker; these scripts never start it.
* `tools/**` belongs to #35 (`tools/release/**`) and #36 (`tools/update/**`) — do not add files there.
