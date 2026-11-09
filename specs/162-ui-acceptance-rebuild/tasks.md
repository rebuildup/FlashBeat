---
description: "FlashBeat lean tasks — Issue 162"
---

# Tasks: v0.1.13 の UI 受け入れ撤回 (Issue #162)

**Issue**: #162 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** 全画面の実装 (PlayerSettings 側) が main にあり、EditMode テストで固定されていることの確認。
  — **done when**: 設定値とテスト名が file:line 付きで記録されていること
  — **実測**: `ProjectSettings/ProjectSettings.asset` の `fullscreenMode: 1` (FullScreenWindow) /
  `resizableWindow: 0` / `defaultIsNativeResolution: 1`。
  `Assets/Game/Tests/FullscreenPolicyTests.cs` の
  `ProjectSettings_FullScreenMode_IsFullScreenWindow` /
  `ProjectSettings_DefaultIsNativeResolution_IsTrue` /
  `ProjectSettings_ResizableWindow_IsFalse` / `GameScripts_HaveNoScreenSetResolution`。
  **設定だけ変えて「修正済み」と言わない** (Issue 本文)

- [x] **T2** 機能 UI 契約表 (タスク 2) が作成されていることの確認。
  — **done when**: 対象シーンと defect の識別子が記録されていること
  — **実測**: `docs/ui/issue-162-ui-contract.md` が 6 シーン
  (`ProjectSettings/EditorBuildSettings.asset:9-24`) × 入力/ハンドラ/効果/フィードバック/根拠 を
  file:line 付きで記録 (`F1–F18` / `A-1`–`A-10` / `B-1`–`B-11` / `C-1`–`C-9`)。
  ただし同ファイル 5 行目の自己申告どおり **「コード読解による静的監査」であり実プレイ証拠は無い**

- [x] **T3** 実操作の実装状況 (子 #167) と旧 Canvas 排他の確認。
  — **done when**: 修正済みの defect とテストが file:line 付きで記録されていること
  — **実測**: `docs/ui/issue-167-handler-audit.md` (F1–F7 の修正記録)、
  `Assets/Tests/Editor/V5UiWiringTests.cs` (8 テスト)、
  `Assets/Tests/Editor/GameUiExclusivityTests.cs` (旧 Canvas を `SetActive(false)` にして
  v5 動的 Canvas を出す画面での旧機能の脱落防止)。
  **実機 E2E は未実施** (`docs/ui/pr204-playtest-checkpoints.md` は手順のみ)

- [x] **T4** 検証解像度 3 条件 (FHD / 2K / Mac) のプロファイル一致が固定されていることの確認。
  — **done when**: 3 条件とプロファイル名の対応が記録されていること
  — **実測**: `FullscreenPolicyTests.cs` の `JsonMacScreen_Is2560x1664` /
  `JsonFhdScreen_Is1920x1080` / `JsonTwoKScreen_Is2560x1440` /
  `FhdProfile_MatchesJson` / `TwoKProfile_MatchesJson` / `MacProfile_MatchesJson`。
  FHD 未満 (1024×768 等) は Issue 本文で対象外

- [ ] **T5** 残 defect の決着マップ: 契約表の A-1〜A-10 / B-1〜B-11 / C-1〜C-9 を 1 行ずつ
  「修正済み (file:line)」「#167 の残タスク」「別 Issue」「残存 (理由)」に分類する。
  — **done when**: 全 defect にラベルが付いており、**未決のまま残.defect が 0** であること
  — **既知の要確認例**: A-1 (Title の Ranking メニューが永久に非表示)、
  A-2 (`GameErrorView.Retry` が `GManager.ResetSession()` を呼ばない)、
  A-3 (Option のメディアキャッシュパネルが不可視)、
  A-4 (レガシー pause メニュー一式が到達不能)、
  B-2 (`PAUSE` が何も一時停止しない)、C-4 (検索フィールドに直接入力しても何も起きない)
  (`docs/ui/issue-162-ui-contract.md:207-246`)。
  **本 Issue では修正しない** (子 #167 の所有)。実装タスクは #167 側へ回す

- [ ] **T6** 実 Win64 の 3 解像度で全画面が成立することの確認。
  実 exe hash / monitor resolution・scale・DPI / effective `Screen.width/height` /
  `Screen.fullScreenMode` / Win32 window bounds・style を同じ run から採取する。
  — **done when**: 3 条件それぞれの採取記録と実画像が
  `docs/evidence/162-ui-acceptance/` に残り、画面ピクセル被覆が観測されていること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  候補は `docs/evidence/index.md` §7.2 の Win64 配布候補
  (`0.1.22+0bab549-win64-0bab549.zip`、SHA-256
  `a23c8c4af6983ffc6b19b1c69329fd2ebab3137f9d77e3a01598ca9c831b1c5d`)。
  手順は `docs/operations/win64-candidate-acceptance-2026-11-10.md`。
  窓リサイズによる擬似全画面は証拠にならない

- [ ] **T7** 機能 E2E: Title → Select → Game → Result → Retry の一旋と、
  各画面の「見える control → 入力 → 状態変化 → フィードバック」の確認 (入力ログ付き)。
  — **done when**: 押しても動かない control が 1 件も無いことの記録があること
  — **スクリーンショットだけでボタンが動くと判定しない** (Issue 本文)。
  **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**

- [ ] **T8** 実 Win64 での 1 曲完走 (Result 到達)。
  — **done when**: 1 曲完走の実ログと画像が `docs/evidence/162-ui-acceptance/` に残ること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  macOS 側は PR #258 で実証済み (`docs/evidence/233-song-end-harness.md`) が
  **Win64 の代替にはしない**

- [ ] **T9** v0.1.13 からの安全な更新の適用と起動確認。
  — **done when**: 更新通知 → 適用 → 再起動 → 起動確認の記録があること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**

- [ ] **T10** EditMode テストを緑にして、その結果を記録する (緑だけでは合格にしない)。
  — **done when**: 実行結果が件数付きで残ること。
  **「CI 緑」「Release 公開済み」を UI 合格の根拠にしない** (Issue 本文)
  — **macOS 実機 (実ディスプレイ全画面) で EditMode 実行は可能**
  (batchmode: `-runTests -testPlatform editmode`。`-quit` を付けない)

- [ ] **T11** 完了ゲートの判定表 (8 つの受入条件 × OK / NG / 未検証) を作り、
  PR で報告する。
  — **done when**: 判定表に「未検証」が残っていても **合格に書き換えられていない** こと、
  Windows 実機項目が「未検証 (オーナー実施)」で固定されていること
  — **Issue コメントの投稿は cc が行わない** (PR を作成して cc に報告する)
