# 134 — Festival ranking (Unity side)

Festival ranking on trunk (`release-0-1-3`) and the exhibit patch line (`109-patch`).
Implemented as scene-independent, code-built overlays (KeyConfigUI pattern): no scene edits,
no dependency on trunk-only classes.

## Scope
- `RankingClient`: static client for `POST /api/v1/scores` and `GET /api/v1/leaderboards/{songId}?version=`.
  UnityWebRequest coroutines, 5 s timeout, JsonUtility, never throws, offline => result "unavailable".
  Origin from `Resources/CloudCatalogConfig.json` (same file the cloud catalog uses), fallback
  `https://flashbeat.rebuildup.dev`. Nickname 1..12 chars, persisted in PlayerPrefs.
- `ResultRankingOverlay` + `RankingPanel`: code-built Canvas (ScaleWithScreenSize 1920x1080, match 0.5,
  sortingOrder 4000 — above scene UI, below KeyConfigUI's 5000).
  - ResultScene only: after the result appears, for cloud songs (`SongData.isRemote/remoteId/chartVersion`)
    with score > 0 → modal 「ランキングに登録」 with nickname input from `Input.inputString`
    (Backspace delete, Enter submit, Esc skip). On submit: Top 10 panel, player row highlighted,
    「あなたの順位: N位」. On failure: one-line message + 「もう一度送る [Enter]」 / 「スキップ [Esc]」.
    While the modal is open, `*SceneManager` behaviours and EventSystem navigation are suspended
    (KeyConfigUI input-isolation fix); restored on close so Retry/Back keep working.
  - Title/Select/Result: F5 opens the read-only Top 10; hint 「F5: ランキング」.
- `GManager.maxCombo` counter (reset in `ResetSession` and `Retry`), maintained in `Judge` next to the
  existing `combo++` sites — no timing change.

## Out of scope
Scenes, Judge timing values, KeyConfigUI, UpdateNoticeUI, server (#133), PR for the patch branch.

## Acceptance
- Pure logic (payload, parsing, nickname validation, persistence, URL building) unit-tested under
  `Assets/Tests/Editor/RankingClientTests.cs`.
- Same files compile on both lines; patch port pushed as `134-patch`, SHA commented on #134.
