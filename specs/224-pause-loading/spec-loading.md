# Issue #224 — Loading (07c) 実進捗 / jacket / Retry / loading.mp4 表示

親 Issue #224 ([P1/v6] Game Pause・楽曲ローディングをPencilデザインに置換) の Loading レーン。
監査で判明した未達 4 点を実装する:

1. **実進捗が無い** — `V6LoadingView.SetLoading` は進捗値を受け取らず、
   `NotesManager` が計算済みの `done / total` は v6 モードで捨てられていた。
2. **jacket が常に null** — `V6GameRuntime.UpdateLoading` がハードコードで `null` を渡す。
3. **再試行が無い** — 失敗状態の操作は「選曲にもどる」のみ。
4. **loading.mp4 が表示されない** — `VideoRenderMode.APIOnly` かつ `targetTexture` 未設定
   (デコードされるだけで画面に出ない)。

## 契約

- 進捗の値の源は `NotesManager.LoadRoutine` の `(float)done / total` (譜面準備)。
  媒体 (動画/音源) の準備は `MusicManager` が段階名しか持たないため数値進捗は無い
  (MusicManager は本レーンの編集対象外)。
- 進捗の仲介は新設の静的クラス `FlashBeat.UI.V6.Game.Loading.GameLoadingProgress`
  (Reporter: NotesManager / Consumer: V6GameRuntime)。旧 `LoadingOverlay` と
  `GameLoadingRoute` の分割規約に倣う。
- 進捗の意味論: `null` = 不明 → 不定インジケータへフォールバック。
  `0..1` = 既知 → 確定バー + 割合表示。**0 は既知** (準備が始まったばかり) で、
  不明扱いにしない。
- 進捗テキスト: 既知時は「譜面を準備中… NN%」(数値の正体を正直に示す)。
  不明時は従来どおり「動画と音声を読み込んでいます」。
- jacket: 選曲画面と同じ対応表 (`V6SelectDataSource.JacketResourcePathOf` の写し。
  Select/** は編集不可のため V6ResultView と同じ規約で写しを置く)。
  画像が無い曲 (22 など) は「無し」ラベルを正直に表示する (空欄・壊れた画像にしない)。
- Retry: 実在する操作のみ。`GameSceneManager.RetryGame()` (GManager.ResetSession +
  GameScene 再読み込み) に割り当てる。死んだボタンは作らない。
- loading.mp4: `VideoRenderMode.APIOnly` のまま `RenderTexture` (1920×1080, 実素材と
  同寸) を割り当てて `RawImage` に出す。素材は実在 (LFS 実体 8.7MB, 1920×1080, 8s) を確認済み。
- 純ロジック (進捗→文言/幅、jacket 対応表、進捗の既知/不明判定) は
  `V6LoadingViewLogic` / `GameLoadingProgress` に置き、EditMode テストで検証する。

## 非目的 (他レーン)

- Pause (07b) は別担当。`V6GameRuntime` の Pause 部分は触らない。
- `MusicManager.cs` / `Judge.cs` / `V6SelectDataSource.cs` は読むだけ。
