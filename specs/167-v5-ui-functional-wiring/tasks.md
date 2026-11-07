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

- [ ] **T2 Title / Select / Ready を実接続**
  `Assets/Game/Scripts/UI/V5/TitleView.cs`, `SelectView.cs`, `SelectViewLogic.cs`,
  `Assets/Game/Scripts/UI/{Title,Select}SceneManager.cs`, 新規
  `Assets/Game/Scripts/UI/V5/PlaySession.cs` を変更し、
  (a) v5 Canvas に `GraphicRaycaster` を追加してクリックが実 Button に届く、
  (b) Ready の Key Guide / Lyrics / NoteSpeed を `PlaySession` 経由で実値として保持し、
  (c) Select の Online ランキングを cloud 曲で実取得 (非 cloud _curve はセグメント非表示)、
  (d) Ready の重複 `Start` ボタンを 1 個に整理、対象曲が無いときは遷移ボタンを `interactable=false` にする。
  **完了条件**: Title → Select → Ready → Game の遷移がマウスでもキーボードでも成立し、
  Ready の設定値が `PlaySession` に実値として残る (EditMode テストで検証)。

- [ ] **T3 Game / Pause / Option / Result を実接続**
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

- [ ] **T4 Scene / Canvas / EventSystem の実接続 (監査 + 最小差分)**
  `Assets/Game/Scenes/*.unity` に対し EventSystem・Canvas・GraphicRaycaster・CanvasRenderer を機械監査し、
  1 画面 1 EventSystem・CanvasRenderer 欠損ゼロであることを spec に記録する。
  監査で問題が見つかった場合のみ **意味のある最小差分** (行追加) で修正する (全面再保存は禁止)。
  **完了条件**: 監査結果が spec.md に記録され、修正要否の判断理由が明記されている。

- [ ] **T5 完了ゲート Preparation (証跡手順書 + EditMode テスト)**
  `docs/ui/issue-167-ui-acceptance.md` に Win64 実機での操作ログ表
  (画面 / 押すキーやボタン / 期待される結果 / スクリーンショット取得位置) と、
  `Assets/Tests/Editor/V5UiWiringTests.cs` に **実ハンドラが呼ばれる** ことを検証する EditMode テスト
  (実 `Button.onClick.Invoke()` / 実 View 生成 / `PlaySession` 受け渡し / 死んだ行が無いこと) を追加する。
  **完了条件**: 手順書に操作表があり、テストが本 Issue の受入条件 (F1〜F18 の修正) を直接検証している。

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
