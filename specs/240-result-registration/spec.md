# Spec: 240 — v6 Result のローカル登録契約の復元 / ローカル曲とオンライン曲の結果分離

Issue: #240 — branch `w/w240` / worktree `/Users/rebui/work/FlashBeat.w240`。

## 目的

v6 Result 画面で失われている **ローカル登録の契約** を復元し、
**ローカル曲 (送信不可) にオンラインの判定結果が出ない** ようにする。

## 背景 (監査結果)

- v5 は `Assets/Game/Scripts/UI/V5/ResultView.cs:38` で PlayerPrefs キー
  `"V5PlayerName"` に名前を書き、`:957` の `ConfirmRegister()` で保存していた。
  v5 のローカル名は「端末内の名前」であり、オンライン順位への送信とは別の契約。
- v6 は `V6ResultView` が v5 の `ResultView` を作らないため、`"V5PlayerName"` を
  **どこにも書いていない**。`Assets/Game/Scripts/UI/ResultSceneManager.cs:116-120` で
  `ResultRankingOverlay` も無効化されているので、ローカル保存の経路が到達不能。
- `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:816-831` (旧 `SubmitCoroutine`) は
  送信対象でない (ローカル) 曲に対して `OnSubmitResult(null)` を呼び、
  これはオンライン側の **「オフライン」判定** に分類される。结果、ローカル曲で
  登録すると「オフラインのようです」と表示され、端末内保存は不上達。

## 要件

1. **ローカル登録の契約の復元**
   - v6 Result のモーダルで、送信対象でない (ローカル) 曲でも
     v5 と **同じ PlayerPrefs キー `"V5PlayerName"`** に名前を保存する。
   - 保存形式は v5 と同じ (`ResultView.NormalizeName`: 大文字化 + A-Z0-9 のみ +
     最大 8 文字、空入力は `"YOU"`)。
   - 既に保存済みの名前を読み出して **モーダルの初期値** に表示する。
   - v6 側の保存は `V6LocalNameStore` が担う (PlayerPrefs の読み書きのみ)。
2. **ローカル曲とオンライン曲の結果表示の分離**
   - ローカル曲: ローカルの結果 (例「この端末に『YOU』を保存しました」) を出す。
     **「オフライン」「送信」などオンライン語彙を出さない。**
   - オンライン曲: 既存の 201 / 429 duplicate / オフライン / その他 HTTP の
     4 区分を維持する。
   - 方式判定と表示の語彙は `V6ResultLogic` の **純ロジック** として切り出し、
     EditMode テストから直接検証できるようにする。
3. **譜面版 (chartVersion) は pin された値を使う**
   - ライブのカタログ (`GManager.ActiveSongs`) ではなく、プレイ開始時に固定した
     (pinned) 版を使う。
   - 固定の実際は `GManager.BeginPlaySession()` → `PlaySessionSnapshot.Begin()`。
     `V6SelectDataSource.CommitReady` は選択の確定だけを行い、固定は既存の
     `SelectSceneManager` → `ResetSession` → `BeginPlaySession` 経路で行われる
     (実装を確認済み。evidence に記録する)。
   - `ResultScene` / `GameScene` では `GManager.GetSong()` が固定を優先する
     (`PlaySessionSnapshot.PinnedSongFor`)。
4. **古いコメントの修正**
   - `V6ResultView.cs` の `// --- 登録モーダル (05b lqHaN): 見た目と開閉のみ
     (入力機能は未実装) ---` は実装と矛盾しているため、入力・送信・ローカル保存・
     結果表示を実装済みである旨に更新する。
5. **EditMode テスト**
   - ローカル曲: `"V5PlayerName"` に保存される / オンライン語彙が出ない。
   - オンライン曲: 4 区分が保たれる。
   - 実際の `RankingClient` 通信 (`PostScore` / `GetLeaderboard`) は **呼ばない**。
     純ロジックと PlayerPrefs の読み書きだけを検証する。

## 非対象

- 実機 (Windows 展示 PC) での画面確認・オンライン送信 (Owner / Supervisor 作業)。
  `docs/evidence/240-result-registration.md` に「未検証」と明記する。
- v5 の `ResultView` の変更 (読むだけ)。
- `V6SelectDataSource` / `V6/Common` の変更 (読むだけ)。
- 新機能 (ランキング表示の追加・他画面への波及) の追加。

## 完了条件

- 上記 1–5 の実装と EditMode テストが揃い、
  `script/compile-check.sh --assembly flashbeat` と `--assembly tests` が通ること。
- `docs/evidence/240-result-registration.md` に実装・検証・残存リスクの記録があること。
