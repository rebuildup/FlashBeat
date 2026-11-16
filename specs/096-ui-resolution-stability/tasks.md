# Tasks: #96 low-resolution UI reachability (2026-11-16)

Spec: [spec.md](spec.md). Plan: [plan.md](plan.md). No physical-device acceptance gate. Prior T1–T10 observation-only task list superseded; PR #520/#521 evidence already exists.

- [ ] **T1 Shared viewport contract**: `Assets/Game/Scripts/UI/V6/Common/V6UiProfile.cs` と `Assets/Game/Tests/V6CanvasTests/V6UiProfileTests.cs` で 1024×768 / 1280×720 / 1366×768 / 1600×900 の境界 geometry・必須操作の到達性と FHD/2K/Mac の 1:1 保存を固定。共通 API を変更する場合は #523 owner と排他調整する。
- [ ] **T2 Title/Result/Option/Opening**: `Assets/Game/Scripts/UI/V6/Title/`, `Assets/Game/Scripts/UI/V6/Result/`, `Assets/Game/Scripts/UI/V6/Option/` の action/header/footer に responsive reflow/scrolling を適用。V5 Opening は `Assets/Game/Scripts/UI/V5/OpeningView.cs` に実際の欠陥がテストで成立したときだけ変更。既存 Game 判定・動画同期・HOLD は変更しない。
- [ ] **T3 #523 handoff**: `specs/096-ui-resolution-stability/plan.md` に shared helper API と owned paths を記載、`https://github.com/rebuildup/FlashBeat/issues/523` に呼出方法・自動検証の追跡先・統合順序を反映。#96 の完了は **Title/Result/Option/Opening の #96 担当分が green** かつ #523 に Select/Game/Pause 固有の failing/acceptance tests の実装担当が明示されていること。#523 を PASS と偽らない。
- [ ] **T4 Hard-failing interaction regressions**: `Assets/Game/Scripts/Common/AutoTestV6Screens.cs` と対応する `Assets/Game/Tests/V6CanvasTests/` 内のテストで essential-control inventory / ScrollRect・Mask / header・footer clipping を FAIL へ昇格。**両入力経路** (keyboard/pointer) の input→handler→state→feedback→exit を状態遷移/ハンドラテストとして同じ 1024×768, 1280×720, 1366×768, 1600×900 条件で確認。表示位置だけでは PASS にしない。#523 固有の画面操作は #523 のテストへ委譲する。
- [ ] **T5 Verify, integrate, rollback readiness**: `docs/operations/development-convergence.md` に従って一括 Unity compile・関連 EditMode と独立レビューを実施し、`docs/evidence/96-screen-clip-2026-11-16.md` を履歴として参照しつつ実測 SHA / 結果 / 未観測事項を #96 の PR と Issue に記録。共通変更は別 commit にして単独 revert 可能にし、rollback 時には #523 owner と shared API を確認する。実機画像収集を前提にしない。

**Done**: T1/T2/T4/T5 が Unity compile・EditMode とコードレビューで green、T3 の #523 の仕様・テスト追跡が成立していること。#96 は自身の Owned Paths の修正が終われば独立 close 可能だが、#523 の未実装を全画面 PASS と見なさず、#523 は独立に完了する。単なる AuditScreenClip report-only の成功や Unity compile 未実施は不可。

## Rollback

#96 の共通 viewport/helper 変更は単独 revert し、#523 の Select/Game/Pause 専用 UI を別途壊さない。戻す前に #523 の依存 API を調べ、必要なら呼出互換を保つ補正を先に行う。revert 後の低解像度機能不全は再び未完了として追跡し、hardware gate へ退避しない。
