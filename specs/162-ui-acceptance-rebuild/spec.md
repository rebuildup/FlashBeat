---
description: "FlashBeat lean spec — Issue 162"
---

# Spec: v0.1.13 の UI 受け入れ撤回 — 全画面・全画面遷移・実操作を機能から再構築

**Issue**: #162 | **Branch**: `w/spec-gameui` | **Date**: 2026-11-10 | **Status**: Draft

## Input

`[P0/REBUILD] v0.1.13 のUI受け入れ撤回：全画面・全画面遷移・実操作を機能から再構築`

## Objective

#162 は **rebuild の親 Issue** であり、**5 つの子 Issue** に分割済みである
(1. 実 Win64 画面とウィンドウの基準化 / 2. 機能 UI 契約表 / 3. 本物の操作を先に実装 /
4. UI の全面再構築 / 5. 完了ゲート)。

**#162 自身には spec が無い** (子は `specs/167-v5-ui-functional-wiring` と
`specs/169-fullscreen-canvas-foundation` にある)。本 spec は **親 Issue の *,
「まだ spec が無い領域」 と「受入条件が未充足のまま残る領域」を明示する **umbrella spec** として作る。

  具体的には:

- (A) **全画面が実機で成立する** — コード (PlayerSettings + テスト) は揃っているが、**実機未検証**。
- (B) **画面配置・操作可能領域が成立する** — 契約表は A-1〜A-10 / B-1〜B-11 / C-1〜C-9 の
  defect を静的監査として記録済み。**一部は #167 で修正済みだが、残りがある**。
- (C) **完了ゲート (実 Win64 画像 + 機能 E2E + 全画面ピクセル被覆 + 1 曲完走 + 更新適用 + EditMode)** が未達。

本 spec の価値は、**「何が既に fix 済みで、何が親 Issue としてまだ開いているか」を 1 枚にすること**。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 親 / 子の境界 | #162 の 5 プログラムのうち、**子 Issue (#167 の実操作配線 / #169 の全画面基盤) は spec 済み**。本 spec は **親の残り (実機受入ゲート) だけ** を扱う。**#167 / #169 の spec を書き直さない** |
| 全画面の正本 | `ProjectSettings/ProjectSettings.asset` の `fullscreenMode: 1` (FullScreenWindow) / `resizableWindow: 0` / `defaultIsNativeResolution: 1`。**設定だけ変えて「修正済み」と言わない** (Issue 本文の明文)。Windows 実機での Win32 window bounds / style / effective `Screen.width/height` の採取が受入条件 |
| 検証解像度 | **FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664 の 3 条件のみ**。1024×768 等の FHD 未満は対象外。Mac は 16:10 の明示例外 |
| 画面品質ゲート | **GreCI 緑や「Release 公開済み」は UI 合格ではない** (Issue 本文)。**スクリーンショットだけでボタンが動くと判定しない** — 実入力 → 状態変化の E2E が必要 |
| 静的監査の位置 | `docs/ui/issue-162-ui-contract.md` は **「コード読解による静的監査」であり実プレイ証拠ではない** (同ファイル 5 行目の自己申告)。**EditMode 緑も描画上の問題を検出しない**。両方を「実機受入」に読み替えない |
| 残 defect の扱い | 契約表に残っている defect (A-1 / A-2 / B-2 / C-4 など) は **#167 の残タスク** として扱う。親 Issue 側で新たに実装しない |
| 実機 | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。macOS 実機 (実ディスプレイ全画面 2560×1664) は **Win64 の代替にならない** |
| 期限 | Issue 本文の「2026-11-08 朝・展示開始前まで」は **既に経過している**。本 spec では期限を再設定しない (オーナーが別途設定する)。**cc は期限を独断で延長しない** |
| 証跡 | `docs/evidence/162-ui-acceptance/` に集める (新規)。`docs/evidence/index.md` / `docs/audit/open-issues-audit.md` は **他 Worker 所有** なので触らない |

## 非目標 (Out of scope)

- `#167` / `#169` の spec / plan / tasks の書き直し・上書き (別 Worker 所有)。
- v5 / v6 の UI コードの実装変更 (本 Issue は受入ゲート + 残 defect の切り出し)。
- 音声再生・譜面判定・API・永続化などのバックエンドの再構築 (Issue 本文が明示的に対象外)。
- 1 曲完走の実装 (=#233 / `#226` のスコープ)。
- FHD 未満の解像度への個別対応 (Issue 本文が明示的に除外)。
- `docs/audit/open-issues-audit.md` / `docs/evidence/index.md` の編集 (他 Worker 所有)。

## Acceptance criteria

1. **全画面** — Win64 実機 (FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664) で、
   起動引数なしの配布 ZIP から起動した exe が borderless / fullscreen になり、
   effective `Screen.width/height` が実ディスプレイの native 解像度と一致し、
   全画面ピクセル被覆が観測されていること。
2. **画面配置・操作可能領域** — 契約表 (`docs/ui/issue-162-ui-contract.md`) に残る defect が
   すべて「修正済み (file:line)」または「別 Issue へ切り出し済み (Issue 番号)」の
   どちらかで決着していること。**未決のまま残る defect が 0**。
3. **実操作 E2E** — Title → Select → Game → Result → Retry が一巡し、
   各画面の **クリック / キー入力が実機能 (状態変化) に結びつく** ことが、
   入力ログ (または同等の観測) 付きで証明されていること。
4. **1 曲完走** — 実 Win64 で 1 曲が完走し、Result まで到達すること (=#233 の macOS 実証済の実機の Windows 版)。
5. **更新の安全適用** — v0.1.13 からの安全な更新が実機で適用・確認されていること。
6. **EditMode** — Unity EditMode テストが緑であること (緑だけでは UI 合格ではない)。
7. **完了ゲート** — 上記 1–6 の実 Win64 画像 (複数解像度) + 機能 E2E が
   `docs/evidence/162-ui-acceptance/` に揃っていること。
8. 受入判定が OK / NG / 未検証 の 3 値で記録され、**「Release 公開済み」や「CI 緑」を合格の根拠にしていない**こと。

## 現在の実装状況 (2026-11-10 時点)

**充足済み**

| 受入条件 | 判定 | 根拠 (file:line / 証跡) |
|---|---|---|
| 1. 実 Win64 画面とウィンドウの基準化 (コード側) | 充足 (コード) | `ProjectSettings/ProjectSettings.asset` の `fullscreenMode: 1` / `resizableWindow: 0` / `defaultIsNativeResolution: 1`。テスト `Assets/Game/Tests/FullscreenPolicyTests.cs` (`ProjectSettings_FullScreenMode_IsFullScreenWindow`, `ProjectSettings_DefaultIsNativeResolution_IsTrue`, `ProjectSettings_ResizableWindow_IsFalse`, `GameScripts_HaveNoScreenSetResolution`) |
| 2. 機能 UI 契約表 | 充足 | `docs/ui/issue-162-ui-contract.md` — 6 シーン (`ProjectSettings/EditorBuildSettings.asset:9-24`) × 入力/ハンドラ/効果/フィードバック/根拠 を file:line 付きで F1–F18 / A-1〜A-10 / B-1〜B-11 / C-1〜C-9 として記録 |
| 3. 本物の操作の実装 (一部) | 充足 (コード) | `docs/ui/issue-167-handler-audit.md` (F1–F7 の修正記録)、`Assets/Tests/Editor/V5UiWiringTests.cs` (8 テスト)、`Assets/Tests/Editor/GameUiExclusivityTests.cs` (旧 Canvas を `SetActive(false)` にして v5 動的 Canvas を出す画面での旧機能の脱落防止) |
| 4. UI の全面再構築 (基盤) | 充足 (コード) | `specs/167-v5-ui-functional-wiring` / `specs/169-fullscreen-canvas-foundation` に spec があり、実装が main にある |
| 解像度プロファイル | 充足 (コード) | `Assets/Game/Tests/FullscreenPolicyTests.cs` の `JsonMacScreen_Is2560x1664` / `JsonFhdScreen_Is1920x1080` / `JsonTwoKScreen_Is2560x1440` / `FhdProfile_MatchesJson` / `TwoKProfile_MatchesJson` / `MacProfile_MatchesJson` |
| 1 曲完走 (macOS) | 充足 (macOS のみ) | PR #258 / `docs/evidence/233-song-end-harness.md` / `docs/evidence/index.md` §7.1 (130/130 確定 / `Completed` / `ResultScene` 到達 / 実効 2560×1664 全画面) |
| EditMode テスト | 充足 | `Assets/Tests/Editor/WindowBaselineTests.cs`, `V5UiTests.cs`, `V5GameHudTests.cs`, `TitleOptionV5Tests.cs`, `Assets/Game/Tests/FullscreenPolicyTests.cs`。直近 `docs/evidence/index.md` §1.7 (1071/1071) |

**未充足**

| 受入条件 | 判定 | 未充足の理由 |
|---|---|---|
| 全画面表示 (実機) | **未充足 (Windows 実機必須)** | `docs/evidence/index.md` §7.3-3 が「3 解像度の実全画面 …… macOS 側で証明できるのは実ディスプレイの 2560×1664 だけで、1920×1080 / 2560×1440 は **窓をリサイズした擬似全画面**であり証拠にならない」と明記。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** |
| 画面配置・操作可能領域が成立 | **未充足** | 契約表が A-1 (Title の Ranking メニューが永久に非表示)、A-2 (`GameErrorView.Retry` が `GManager.ResetSession()` を呼ばない)、B-2 (`PAUSE` が何も一時停止しない)、C-4 (検索フィールドに直接入力しても何も起きない) 等の defect を記録 (`docs/ui/issue-162-ui-contract.md:207-246`)。**残 defect の決着状況の記録が無い** |
| 見た目だけで内部機能がない UI がない | 充足 (コード) のみ | `Assets/Tests/Editor/V5UiWiringTests.cs` (8 テスト) + `docs/ui/issue-167-handler-audit.md` の F1–F7 修正。**実機 E2E は未実施** |
| 実機での受入 (完了ゲート) | **未充足** | `docs/ui/pr204-playtest-checkpoints.md` は **手順のみ**。実 Win64 画像が `docs/evidence/` に無い |
| 1 曲完走 (Windows 実機) | **未充足 (Windows 実機必須)** | `docs/evidence/index.md` §7.3-1 が「未証明」と明記 |
| v0.1.13 からの安全な更新適用 | **未充足** | 適用と起動確認の記録が無い。候補 ZIP は `docs/evidence/index.md` §7.2 (`0.1.22+0bab549-win64-0bab549.zip`、SHA-256 `a23c8c4a…`) |
| Issue 本文の期限 (2026-11-08 朝) | **経過済み** | Issue 本文の締切は既に過ぎている。**cc は独断で延長しない**。再設定はオーナーの判断事項 |
