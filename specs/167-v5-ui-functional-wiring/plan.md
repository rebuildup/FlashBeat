---
description: "FlashBeat lean per-issue implementation plan template (project-local override of the Spec Kit core plan template)"
---

# Plan: [Game/P0] Unity 全画面の描画だけのUIを排除し全ボタンを実動作へ接続

**Issue**: #167 | **Branch**: `167` | **Base**: `release-0-1-11` | **Date**: 2026-11-07
**Spec**: [`specs/167-v5-ui-functional-wiring/spec.md`](spec.md)

## Summary

v5 UI の「描画だけ」の状態を解消する。中心は 3 点。
(1) v5 Canvas に `GraphicRaycaster` を足し、マウスクリックを実 Button に届かせる (現状 Title/Select/Option/Result は
クリックが 100% 死んでいる = 「ゲームが進行できない」の直接原因)。
(2) Select の Ready 画面の設定 (Key Guide / Lyrics / NoteSpeed) を新規 `PlaySession` で GameScene へ渡し、
HUD / 歌詞パネル / ノーツ速度に実値を効かせる (現状 `private static` に書かれて消える偽値)。
(3) 実動作しない行 (Pause Display の 4 行、Select Ranking の未接続 Online、Result の二重登録導線) を
除外または「ローカル限定」明記に置き換える。既存の v5 設計・レイアウト・配色・トークンは一切変えない。

## Owned Paths

```text
Assets/Game/Scripts/UI/V5/PlaySession.cs            (新規: セッション設定 + 設定永続化)
Assets/Game/Scripts/UI/V5/{TitleView,SelectView,SelectViewLogic,OptionView,PauseMenuView,ResultView}.cs
Assets/Game/Scripts/UI/V5/{GameHudView,GameHudLogic,GameHudRuntime,GameReadyView,GameLoadingView,GameErrorView}.cs
Assets/Game/Scripts/UI/V5/{TitleLayout,TitleLinks,V5GameUi,ShortcutKeyCap,ActionKeyHint}.cs
Assets/Game/Scripts/UI/{Title,Select,Game,Result,Option}SceneManager.cs
Assets/Tests/Editor/V5UiWiringTests.cs              (新規: EditMode テスト)
specs/167-v5-ui-functional-wiring/{spec,plan,tasks}.md
docs/ui/issue-167-ui-acceptance.md                  (新規: 実操作証跡手順書)
```

**Structure Decision**: v5 View 群と SceneManager 群 (本 Worker の排他所有) と、
共有状態受け皿としての `PlaySession.cs` 1 ファイルに限定する。
`UISizeProfile.cs` / `V5Ui.cs` / `V5Tokens.cs` / `GManager.cs` / `RankingClient.cs` は
Worker A / 他 Issue 所有のため **1 行も書かない**。

## Dependencies & Gate

- **Depends on**: #162 (親) — v5 UI 基盤 (#120/#121/#122/#123/#153)。追加の外部依存なし。
  `RankingClient.GetLeaderboard(songId, chartVersion, Action<Result>)` は既存公共 API を利用。
- **Blocks**: #162 の「UI が実動作する」完了条件。
- **Gate**:
  1. EditMode テスト (`V5UiWiringTests` を含む) が全件 pass。
  2. `docs/ui/issue-167-ui-acceptance.md` に Title→Select→Game→Result→Retry の
     操作ログ表 (どの画面でどのキーを押すと何が起きるか) とスクリーンショット取得位置があること。
  3. Supervisor による実機 (Windows 64) でのクリック操作確認。
  4. Draft PR のままだが、SPEC.md の F1〜F18 がすべて「修正 / 据置き / Known Issues」で
     決着していること (未決のまま押し込まない)。

## Exclusions

- `Assets/**/*.unity` の編集 (シーン YAML は監査で EventSystem 1 画面 1 個・CanvasRenderer 欠損ゼロのため
  変更不要。最小差分原則に従いコード側に集約)。
- `ResultRankingOverlay` / `RankingPanel` / `RankingClient` の書き換え (外部所有)。
  Select 側から **呼ぶだけ**。
- `GManager.cs` へのフィールド追加 (他 Issue 所有) → 共有状態は `PlaySession.cs` に置く。
- 見た目 (座標 / 色 / スタイル) の変更。

## Constitution Check

*GATE: must pass before implementation.*

- Authority: [`constitution/CONSTITUTION.md`](../../../constitution/CONSTITUTION.md) と
  [`organization/profiles/release-driven-solo.md`](../../../organization/profiles/release-driven-solo.md) は
  本 Issue で変更しない (上位契約のまま)。
- Mutable Ownership Safety: 本 Issue は Worker B の排他所有 (v5 View / SceneManager / Scene) のみを触る。
  `PlaySession.cs` は本 Issue で新規作成する **唯一** の新規ソースファイルで、
  他 Worker の新規ファイル名と衝突しないことを `git ls-files` で確認済み。
  シーン YAML は触らないため、他 Worker の Scene 作業と競合しない。
- Evidence Integrity: 検証ログは PR に添付し、候補 SHA に紐づける。
  EditMode テストは実 `Button.onClick` / 実ハンドラを呼ぶ (フェイクイベントを使わない)。
- Violations: なし。

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | EditMode テスト (新規 + 既存) | `Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml` | TestResults.xml / ログ |
| V2 | v5 Canvas の `GraphicRaycaster` | `V5UiWiringTests.Canvas_*_HasGraphicRaycaster` | テスト結果 |
| V3 | Ready → Game の設定受け渡し | `V5UiWiringTests.ReadyOptions_AreForwardedToGameHud` | テスト結果 |
| V4 | 実操作 (人手) | `docs/ui/issue-167-ui-acceptance.md` の手順表 | Supervisor 実機スクリーンショット |
| V5 | コード規約 | 4 スペース / LF / UTF-8 BOM / 式形式メソッド禁止 | `git diff --check` |

## Rollback

`git revert` (merge 取り消し) で本 Issue のみを戻せる。Owned Paths が v5 View / SceneManager / 新規 2 ファイルに
閉じており、他ブランチの成果物を巻き戻すMigration がないため安全。

## Technical Approach (実装方針)

1. **Canvas**: `TitleView.Build` / `SelectView.Create` / `OptionView.Build` / `ResultView.Build` の Canvas 生成部で
   `GraphicRaycaster` を追加 (`CanvasConfigurator` は Worker A 所有なので触らない)。
   GameScene は `GameHudRuntime` でレガシー Canvas の `GraphicRaycaster` を無効化。
2. **セッション状態**: `PlaySession` に `KeyGuide` (Off/Compact/Full), `LyricsEnabled`, `NoteSpeed` を持ち、
   SelectView (書き込み) と GameHudView / GameSceneManager (読み) が参照する。
   `PlaySession.Options` に `PlayerPrefs` 永続化 (音量 / noteSpeed / noteTiming / Flash 系) を持たせる。
3. **入力の一意化**: v5 View が自前キー処理を持っているため、v5 Button の `Navigation` は `None` にし、
   Unity の自動ナビゲーションと二重に動かないようにする。TMP 入力欄 (Result の名前入力) は
   `EventSystem.current` の選択を自前で管理する既存挙動を維持する。
4. **偽値の除去**: Pause Display の 4 行を削除、Online セグメントは cloud 曲のみ実取得、
   ローカル限定パネルには明記、Result の登録はローカルを明記。
5. **テスト**: `Assets/Tests/Editor/V5UiWiringTests.cs` に、v5 View を生成して実 Button の `onClick` を
   `Invoke()` し、実ハンドラ (コールバック) が呼ばれることを検証するテストを書く。
