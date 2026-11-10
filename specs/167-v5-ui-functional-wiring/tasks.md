---
description: "Task list template for feature implementation"
---

# Tasks: [Game/P0] Unity 全画面の描画だけのUIを排除し全ボタンを実動作へ接続

**Input**: [`specs/167-v5-ui-functional-wiring/spec.md`](spec.md), [`plan.md`](plan.md)
**Tests**: EditMode テスト (`Assets/Tests/Editor/V5UiWiringTests.cs`) を T5 で追加する。
**上限**: 5 タスク (Issue #167 の指定)。

- [x] **T1 機能 UI 契約表**
  `specs/167-v5-ui-functional-wiring/spec.md` に Opening / Title / Select (List, Typing, Ready, History,
  Ranking) / Game (HUD, Ready, Loading, Error) / Pause (Setting, Display, Ranking, Keys) / Option /
  Result (Register, Ranking) の **全 visible control** を
  `control → 入力 → ハンドラ → 状態/API/永続化効果 → フィードバック → 退出` の 1 行ずつで列挙する。
  未接続・偽値・誤ラベル・空画面は Findings 表 (F1〜F18) として明示する。
  **完了条件**: spec.md に上記表と Findings が存在し、各行に実装根拠 (ファイル:行 or シンボル名) が書いてある。

  > 検証結果 (2026-11-10): **充足** (契約表 56 行 + 本作業で不足 2 行を追加 = 58 行)。
  > `spec.md:104-226` に契約表と Findings F1〜F18 が存在し、全行に実装根拠 (シンボル名) あり。
  > 以前の検証 (`docs/ui/issue-167-verify-2026-11-10.md`) で指摘された列挙漏れ 2 行を追加した:
  > ① **Game の Loading** — `GameHudRuntime.cs:98-100` (`GameLoadingView.Build` / `LoadingOverlayHook.Bind`) と
  > 表示可否の単一情報源 `GameLoadingRoute.UsesLegacyOverlay` (`LoadingOverlay.cs:32`) を根拠に
  > Game 節 (`spec.md:159`) へ追加 (v6 既定では `V6LoadingView` が単独表示である旨も記載)。
  > ② **Result の Ranking** — `ResultRankingOverlay.Update` (`ResultRankingOverlay.cs:120-143`) を根拠に
  > Result 節 (`spec.md:204`) へ追加。ローカル固定曲で開けない点は F18 (Known Issue)、
  > v6 Result 既定ではオーバーレイ停止 (`:100-103`) である旨を列に明記。

- [x] **T2 Title / Select / Ready を実接続**
  `Assets/Game/Scripts/UI/V5/TitleView.cs`, `SelectView.cs`, `SelectViewLogic.cs`,
  `Assets/Game/Scripts/UI/{Title,Select}SceneManager.cs`, 新規
  `Assets/Game/Scripts/UI/V5/PlaySession.cs` を変更し、
  (a) v5 Canvas に `GraphicRaycaster` を追加してクリックが実 Button に届く、
  (b) Ready の Key Guide / Lyrics / NoteSpeed を `PlaySession` 経由で実値として保持し、
  (c) Select の Online ランキングを cloud 曲で実取得 (非 cloud _curve はセグメント非表示)、
  (d) Ready の重複 `Start` ボタンを 1 個に整理、対象曲が無いときは遷移ボタンを `interactable=false` にする。
  **完了条件**: Title → Select → Ready → Game の遷移がマウスでもキーボードでも成立し、
  Ready の設定値が `PlaySession` に実値として残る (EditMode テストで検証)。

  > 検証結果 (2026-11-10): **充足** (実装コミット `467fe8a`、後続 `5f11ce8` / `36da73c` / `d4d758a`)。
  > (a) `V5GameUi.NewScreenCanvas` (`V5GameUi.cs:32`) が `GraphicRaycaster` 付き Canvas を生成。
  > (b) Key Guide / Lyrics は `PlaySession` に実値で保持 (`PlaySession.cs`)。
  > (c) Online ランキングは cloud 曲だけ `RankingClient.GetLeaderboard` で実取得 (`SelectView.cs:1689-1698`)。
  > (d) Ready 内の `Start` は削除し画面右下の 1 個に統一 (`SelectView.cs:1261`)、
  > 対象曲が無ければ `interactable=false` (`SelectView.cs:968` → `HasPlayableSong()` `:601`)。
  > **文言とコードの食い違い (機能欠落ではない)**: 完了条件の「`PlaySession` に実値が残る」は
  > **NoteSpeed だけ実装と一致しない** — NoteSpeed は `PlaySession` ではなく
  > `GManager.noteSpeed` + `OptionSettings.Save()` で保持 (`SelectView.cs:378-379`)。
  > 実値はゲーム側 (`Notes` / `NotesManager`) に届き 1..50 clamp・永続化もされる (spec.md F4 の決着どおり)。
  > 根拠: `docs/ui/issue-167-verify-2026-11-10.md`

- [x] **T3 Game / Pause / Option / Result を実接続**
  `Assets/Game/Scripts/UI/V5/{PauseMenuView,OptionView,ResultView,GameHudView,GameHudRuntime}.cs`,
  `Assets/Game/Scripts/UI/{Game,Option,Result}SceneManager.cs` を変更し、
  (a) Pause Display タブの機能未実装 4 行 (Live Rank / Hit Timing / Judge Text / Progress Bar) を削除、
  (b) Pause Ranking / Select Ranking / Result の名前登録を「ローカル限定」と明記、
  (c) Option の音量 / NoteSpeed / NoteTiming / Flash を実動作 + `PlayerPrefs` 永続化 (noteSpeed は 1..50 clamp)、
  (d) Title 起動時の noteTiming リセット (`GManager.noteTiming--` + `TimingUp()`) を廃止し保存値で復元、
  (e) GameScene の Esc を v5 Pause のみが所有 (レガシー MenuPanel と二重処理をやめる)、
  (f) レガシー Canvas の `GraphicRaycaster` を無効化して v5 HUD のみがクリックを受ける、
  (g) Result のベストスコア記録更新を `Awake` に移して保存順序を確定。
  **完了条件**: 上記 (a)〜(g) がすべてコード反映され、押しても変化しない行が Pause に残っていない。

  > 検証結果 (2026-11-10): **充足** (実装コミット `467fe8a`)。
  > (a) `PauseMenuView.BuildDisplayPage` (`PauseMenuView.cs:325`) は Key Guide / Lyrics の 2 行のみ
  > (削除 4 行の記載は `:344`)。(b) Pause Ranking `:442`/`:466`・Select Ranking `SelectView.cs:1608`・
  > Result 名前登録 `ResultView.cs:664`/`:787`/`:803` で「ローカル」を明記。
  > (c) `OptionSceneManager.OptionSettings` (`:26-100`: Save / Load / NoteSpeed 1..50・NoteTiming ±10 clamp)、
  > `ApplyVolumes` (`:237`)、Flash の保存 (`:70-72`)。(d) `TitleSceneManager.Start` (`:46-50`) の
  > `noteTiming--` + `TimingUp()` を削除し `OptionSettings.Load()` で復元。
  > (e) `GameSceneManager` (`:297`) は `HasV5PauseMenu()` (`:235`) でレガシー MenuPanel を開かない。
  > (f) `GameHudRuntime.DisableLegacyRaycaster` (`:201-213`) がレガシー Canvas の Raycaster を無効化。
  > (g) `ResultSceneManager.Awake` (`:46-58`) でベストスコア更新。
  > 根拠: `docs/ui/issue-167-verify-2026-11-10.md`

- [x] **T4 Scene / Canvas / EventSystem の実接続 (監査 + 最小差分)**
  `Assets/Game/Scenes/*.unity` に対し EventSystem・Canvas・GraphicRaycaster・CanvasRenderer を機械監査し、
  1 画面 1 EventSystem・CanvasRenderer 欠損ゼロであることを spec に記録する。
  監査で問題が見つかった場合のみ **意味のある最小差分** (行追加) で修正する (全面再保存は禁止)。
  **完了条件**: 監査結果が spec.md に記録され、修正要否の判断理由が明記されている。

  > 検証結果 (2026-11-10): **充足**。
  > `spec.md:286-302` に Scene ごとの EventSystem / InputModule / TMP / CanvasRenderer の機械監査表と
  > 「**シーン YAML の変更は不要**」という判断理由が記録されている (`.unity` は `467fe8a` で未変更)。
  > 検証ワーカー側が `Assets/Game/Scenes/*.unity` を再集計し、EventSystem 数 (1 画面 1 個、MakeFileScene のみ 0) と
  > CanvasRenderer 数 (Title 26 / Select 83 / Game 32 / Result 58 / Option 65 / Opening 0 / Typing 0) が
  > 監査表と完全一致することを確認した。
  > ただし監査表の「TMP テキスト」列は TMP が GUID 参照のため grep で再導出できず、**再検証できていない**。
  > 根拠: `docs/ui/issue-167-verify-2026-11-10.md`

- [x] **T5 完了ゲート Preparation (証跡手順書 + EditMode テスト)**
  `docs/ui/issue-167-ui-acceptance.md` に Win64 実機での操作ログ表
  (画面 / 押すキーやボタン / 期待される結果 / スクリーンショット取得位置) と、
  `Assets/Tests/Editor/V5UiWiringTests.cs` に **実ハンドラが呼ばれる** ことを検証する EditMode テスト
  (実 `Button.onClick.Invoke()` / 実 View 生成 / `PlaySession` 受け渡し / 死んだ行が無いこと) を追加する。
  **完了条件**: 手順書に操作表があり、テストが本 Issue の受入条件 (F1〜F18 の修正) を直接検証している。

  > 検証結果 (2026-11-10): **EditMode 実装分は完了 / 実機受入 (Win64) は未了**。
  > 実装したもの:
  > ① `Assets/Tests/Editor/V5UiWiringTests.cs` に Findings **F2 / F6 / F7 / F8 / F10 / F15 / F16 / F17**
  > を直接検証する 8 テストを追加 (実 View 生成 → 実 `Button.onClick.Invoke()` → 実状態 Assert、
  > `finally` で `DestroyImmediate` + `GManager` 静的状態の復元)。spec.md:92 が名指しする
  > `ResultRegister_IsLabelledLocal` を含む。テスト可能化のための最小抽出のみ実施:
  > `GameHudController.DisableLegacyRaycastersIn(Scene)` (F2)、`SelectView.CanFetchOnlineRanking` を
  > public static 化 (F7)、`GameSceneManager.LegacyBackOpensMenu` (F15)、
  > `ResultSceneManager.ApplyBestScore` (F16)。挙動は変えていない。
  > ② `docs/ui/issue-167-ui-acceptance.md` を v6 UI 既定 (`V6UiMode.GameHud = true` /
  > `SelectScreen = true` / `V6TitleView.Enabled` / `V6PauseView.Enabled` / `V6ResultView.Enabled` が
  > すべて true) に合わせて書き直した。旧記述 (Draft PR #177 / base `release-0-1-11` /
  > `git checkout 167`) は削除。S5 / S6 の「この後 F5 で Game へ」の誤りを修正
  > (Game へは Ready / `Enter` / `Space`)。解像度要件 (FHD 1920×1080 / 2K 2560×1440 /
  > Mac 2560×1664 の実ディスプレイ全画面のみ有効) と「Windows 実機の操作受入は未実施」を明記。
  > **残る未了 (本チェックの対象外)**: T5 の完了条件のうち **実機 (Win64) での一巡受入**。
  > 手順書 §4 がオーナー実施節として残っている (cc は展示 PC を操作しない)。
  > EditMode テストの緑は実機合格の代用ではない。

## Dependencies & Execution Order

```text
T1 (契約表)  → T2 (Title/Select/Ready) ┐
                T3 (Game/Pause/Option/Result) ┘→ T4 (Scene 監査) → T5 (証跡 + テスト)
```

- T2 と T3 は別ファイル群 (Select 系 / Pause-Option-Result 系) を触るため並列可。
- T4 は T2/T3 の Canvas 変更を踏まえて監査するため T2/T3 後。
- T5 は T2/T3 の実挙動を検証するので最後。

## Notes

- 各タスクの完了条件は `spec.md` の Requirements (R1〜R10) と Findings 表 (F1〜F18) に紐づく。
- 見た目だけの変更は完了条件に数えない。
- テストはフェイクイベント (自前発火) ではなく **実 Button / 実 View / 実ハンドラ** を使う。
