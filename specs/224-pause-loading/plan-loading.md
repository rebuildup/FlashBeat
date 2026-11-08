# Plan — Issue #224 Loading レーン

## 現状 (監査, integration/v6-followup 87d723f 基準)

- `V6LoadingView.cs:125-150` `SetLoading` は進捗値を受け取らない。
- `V6LoadingView.cs:463-472` `Tick` は不定インジケータのみ。
- `NotesManager.cs:209-215` の `LoadingOverlay.SetProgress(...)` は v6 モードでは
  実行されない (`GameLoadingRoute.UsesLegacyOverlay == false`)。
- `V6GameRuntime.cs:228` は jacket に常に `null`。
- `V6LoadingView.cs` に Retry/Cancel は存在しない。
- `V6LoadingView.cs:216-229` `APIOnly` + `targetTexture` 未設定 → loading.mp4 が見えない。

## 設計

```
NotesManager.LoadRoutine ──ReportNotes(done/total)──▶ GameLoadingProgress (静的)
                                                          │
V6GameRuntime.UpdateLoading ◀──HasNotesProgress/NotesProgress─┘
        │  LoadJacket(song) = Resources.Load(V6LoadingViewLogic.JacketResourcePathOf(id))
        ▼
V6LoadingView.SetLoading(title, artist, jacket, float? chartProgress)
        ├─ 既知: 確定バー (DeterminateFillWidth) + 「譜面を準備中… NN%」
        ├─ 不明: 不定バー (IndeterminateX, 従来動作)
        └─ 失敗: 「再試行」(RetryGame) + 「選曲にもどる」(RetireGame)
```

- 状態の寿命: `NotesManager.OnEnable` で `ResetNotes()` (シーン再読み込み = Retry のたびに
  不明に戻る)。`GameLoadingProgress` は状態を持つだけの静的クラス (DontDestroyOnLoad 不要)。
- スプライトの再生成: `SetLoading` は毎フレーム呼ばれるため、テクスチャが変わったときだけ
  `Sprite.Create` する (旧実装の毎フレーム生成 = テクスチャリークを修正)。
  `Dispose` でスプライト / RenderTexture を破棄。
- ジャケットテクスチャ: `V6GameRuntime` が songId ごとにキャッシュ (null もキャッシュ)。
- loading.mp4: RT はビューが所有し `Dispose` で Release + Destroy。

## リスク

- 実機 (表示・再生) は検証できない → EditMode テスト + compile-check で代替。
  実機検証は未検証として evidence に明記。
- `V6SelectDataSource.JacketResourcePathOf` が対応表の正本。曲追加時は写し
  (Loading / Result の 2 箇所) の更新が必要 — 既存の規約 (Result と同じ) を踏襲。
- RawImage はアスペクト比を保持しない (uGUI RawImage に preserveAspect は無い)。
  16:9 以外の画面では動画が伸びる — 背景動画なので許容 (evidence に記載)。
