---
description: "Task list template for feature implementation"
---

# Tasks: [Game/P0] Unity 全画面の描画だけのUIを排除し全ボタンを実動作へ接続

**Input**: [`specs/167-v5-ui-functional-wiring/spec.md`](spec.md), [`plan.md`](plan.md)
**Tests**: EditMode テスト (`Assets/Tests/Editor/V5UiWiringTests.cs`) を T5 で追加する。
**上限**: 5 タスク (Issue #167 の指定)。

- [ ] **T1 機能 UI 契約表**
  `specs/167-v5-ui-functional-wiring/spec.md` に Opening / Title / Select (List, Typing, Ready, History,
  Ranking) / Game (HUD, Ready, Loading, Error) / Pause (Setting, Display, Ranking, Keys) / Option /
  Result (Register, Ranking) の **全 visible control** を
  `control → 入力 → ハンドラ → 状態/API/永続化効果 → フィードバック → 退出` の 1 行ずつで列挙する。
  未接続・偽値・誤ラベル・空画面は Findings 表 (F1〜F18) として明示する。
  **完了条件**: spec.md に上記表と Findings が存在し、各行に実装根拠 (ファイル:行 or シンボル名) が書いてある。

  > 検証結果 (2026-11-10): **部分充足**。`spec.md:104-226` に契約表 (56 行) と Findings F1〜F18 が存在し、
  > **56 行すべてに実装根拠 (シンボル名) あり** (未接続・除外行の F5 / F8 / F9 は Findings にも記載済)。
  > 残る不足は 2 行分の列挙漏れ:
  > ① **Game の Loading ビュー (`GameLoadingView`) の行が無い** — spec.md の Game 節 (`:154-171`) にも
  > `docs/ui/issue-167-handler-audit.md` §1b の Game 節 (`:126-140`) にも存在しない。
  > ② **Result の Ranking (F5 ボード / `ResultRankingOverlay`) の行が spec.md の Result 節 (`:196-203`) に無い**
  > (F18 に既知問題として記載されているだけ。§1b Result 節には「ランキング登録 (cloud 曲)」の行がある)。
  > 根拠: `docs/ui/issue-167-verify-2026-11-10.md`

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

- [ ] **T5 完了ゲート Preparation (証跡手順書 + EditMode テスト)**
  `docs/ui/issue-167-ui-acceptance.md` に Win64 実機での操作ログ表
  (画面 / 押すキーやボタン / 期待される結果 / スクリーンショット取得位置) と、
  `Assets/Tests/Editor/V5UiWiringTests.cs` に **実ハンドラが呼ばれる** ことを検証する EditMode テスト
  (実 `Button.onClick.Invoke()` / 実 View 生成 / `PlaySession` 受け渡し / 死んだ行が無いこと) を追加する。
  **完了条件**: 手順書に操作表があり、テストが本 Issue の受入条件 (F1〜F18 の修正) を直接検証している。

  > 検証結果 (2026-11-10): **部分充足**。
  > 存在するもの: `docs/ui/issue-167-ui-acceptance.md` (Title / Select / Game / Result / Option の操作ログ表
  > T1〜T7 / S1〜S11 / G1〜G15 / R1〜R6 / O1〜O8、期待動作・証跡 shot 名・§2 取得位置・§3 トラブルシュート)、
  > `Assets/Tests/Editor/V5UiWiringTests.cs` (8 テスト、実 `Button.onClick.Invoke()` / 実 View 生成 /
  > `PlaySession` 受け渡し / 死んだ行が無いことの検証を含む)。
  > 残る不足:
  > ① **spec.md:92 が名指しする `V5UiWiringTests.ResultRegister_IsLabelledLocal` がリポジトリに存在しない**
  > (`grep` 済み)。F2 / F6 / F8 / F10 / F15 / F16 / F17 / F7 に対応する EditMode テストも無い
  > = 完了条件の「F1〜F18 の修正を直接検証」全覆盖には届いていない (F18 は仕様どおり未修正 = Known Issue)。
  > ② 手順書が古い: ヘッダが Draft PR #177 / `git checkout 167` を指し、S5/S6 の
  > 「**この後 F5 で Game へ**」は誤り (F5 は Ranking 画面。Game へは `Enter` / `Space` / 右下 `Start`)。
  > ③ 手順書が **v6 UI 既定** (`V6UiMode.GameHud = true` / `SelectScreen = true`) を考慮しておらず、
  > v5 HUD / v5 ポーズ / v5 Select を前提にした操作が実機ではそのまま再現できない。
  > 根拠: `docs/ui/issue-167-verify-2026-11-10.md`

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
