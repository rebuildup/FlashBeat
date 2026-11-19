# v0.1.31 candidate QA — f527384

Candidate code SHA: `f5273844aa6eedd4145ab2510e6c4f958b890283` (release-0-1-31, built from this commit).
Release branch HEAD at evidence time: `5abc508b` (docs-only commits after `f5273844`; product code unchanged).
Artifacts (local, unpublished): `dist/FlashBeat-0.1.31-macos-f527384.zip`, `dist/FlashBeat-0.1.31-win64-f527384.zip` (+ `.sha256` sidecars).

## Package binding

| Artifact | SHA-256 | version.json |
|---|---|---|
| `FlashBeat-0.1.31-macos-f527384.zip` (95,689,021 B) | `bf101eb6b465a6fc744218ebe9c584295e356427ad328d2be36646be77e239eb` | version `0.1.31`, buildSha `f5273844…`, tag `v0.1.31-macos-f527384`, platform `macos`, mediaPolicy `song-free` |
| `FlashBeat-0.1.31-win64-f527384.zip` (95,927,428 B) | `1907a7c3d72e7a26b834375a70549801ce1c6f6b8a48b1fb7c66c84bf68689f8` | version `0.1.31`, buildSha `f5273844…`, tag `v0.1.31-win64-f527384`, platform `win64`, mediaPolicy `song-free` |

- `update-channel.txt` = `stable` in both packages.
- Media exclusion: `flashbeat-media-policy.json` (song-media-excluded) bundled in both; ZIP scan found **0** third-party media files (`*.wav/*.mp3/*.mp4/*.mov/*.m4a/*.webm/*.ogg`). Win64 was repackaged with `-MediaPolicy song-free` so the manifest/size gates ran (the first pass used the script default `bundled` and skipped them; build output itself was already media-excluded — only `version.json` metadata differed).
- Update compatibility: same-version different-SHA is not an automatic update; tag format `v<version>-<platform>-<sha>` matches `update-release.md`.
- Secrets: filename scan (`*.env`, `*secret*`, `*credential*`, `*.pem`, `*.key`, `id_rsa`, …) and content scan (`PRIVATE KEY`, `ghp_`, `xox*`, api-key literals) over both extracted packages: no hits (only Unity's `Unity.InferenceEngine.Tokenization.dll`, unrelated).

## Automated verification (same SHA)

- `./script/compile-check.sh`: PASS (246 sources)
- Unity EditMode: **2063/2063 PASS** (f5273844)
- Unity PlayMode: **6/6 PASS** (`V6ResultInputPlayModeTests` — real EventSystem/TMP path)
- Web (`release-0-1-31` @ `5abc508b`, web tree identical to `f5273844`): `web.yml` check **success**, `browser-e2e` (isolated local D1 + Playwright) **success** — run `44508934462`

## macOS build autotest (`-autotest`, built app @ f5273844)

| Harness | Result |
|---|---|
| play | pass=22 fail=0 |
| keyguide | pass=5 fail=0 |
| v6screens | pass=90 fail=0 (clip partial=0 outside=0, 21 screenshots) |
| v6interact | pass=97 fail=0 |
| v6hold | pass=41 fail=0 (hold clear=3 broken=0) |
| v6songend | pass=32 fail=0 (judged=130==chartNotes, forcedByDeadline=False) |
| resultinput | modalOpened=True (physical typing not automated) |
| v6screens (re-run @f5273844) | pass=90 fail=0 |
| v6interact (re-run @f5273844) | pass=97 fail=0 |

Raw outputs: `/tmp/qa-1ab0c335/` (1ab0c33), `/tmp/qa-f5273844/` (f5273844) (screenshots + result txt; ephemeral).

## Win64

- `BuildScript.BuildStandaloneWindows64` succeeded (242,869,323 bytes). Runtime execution on Windows: **not observed** (issue #22).

## Not verified (explicit)

- IME composition/commit, physical keyboard input, GPU color/gamma on device
- Visual/auditory acceptance on physical devices (notes brightness, verdict animation, preview parity, click sound)
- Real update apply + crash recovery end-to-end
