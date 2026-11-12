# PR #387 未解決レビュー指摘の検証と修正 (2026-11-12)

CodeRabbit が PR #387 に付けた指摘 16 件を **現行コードで 1 件ずつ検証**し、
妥当なものを修正した記録。指摘をそのまま適用したのではなく、コードを読んで
「今も成立するか」を確かめてから直している。

- 対象ブランチ: `w/v6-368-impl`
- 検証時 HEAD: `2c647417`
- 対象: `https://github.com/rebuildup/FlashBeat/pull/387`

## 1. 判定の一覧

| # | 指摘 | ファイル | 判定 | 対応 |
|---|---|---|---|---|
| 1 | 結果遷移が曲長 1 秒前の判定を使う | `MusicManager.cs` | **妥当** | `leadOutSeconds: 0f` で修正 |
| 2 | 開始待ち完了後も待機処理へ戻る | `MusicManager.cs` | **妥当 (重大)** | 待ち済みフラグを分離 |
| 3 | 末尾ノーツ判定前に暗転する | `MusicManager.cs` | **妥当** | フェード下限に確定時刻 |
| 4 | 保存失敗時に適用済み割り当てを戻さない | `V6KeyConfigLogic.cs` | **妥当** | 失敗時に旧表へ復元 |
| 5 | キーボードで「適用」できない | `V6KeyConfigModal.cs` | **妥当** | Ctrl/Cmd + Enter を追加 |
| 6 | Esc で取り消した名前入力が登録を始める | `V6ResultView.cs` | **妥当** | `wasCanceled` を確認 |
| 7 | ドラッグ中の値を検証していない (テスト) | `AutoTestV6Interact.cs` | 修正済み | 変更なし |
| 8 | カウントダウン数字の順序比較 (テスト) | `AutoTestV6Interact.cs` | 修正済み | 変更なし |
| 9 | 準備済みメディアで 60 秒待つ (テスト) | `AutoTestV6SongEnd.cs` | 修正済み | 変更なし |
| 10 | 撮影待機中の表示時間 (テスト) | `AutoTestV6SongEnd.cs` | 修正済み | 変更なし |
| 11 | ウォームアップ終了時に区間開始 | `FrameTimeProbe.cs` | 修正済み | 変更なし |
| 12 | 過去最大値より小さいスパイク | `FrameTimeProbe.cs` | 修正済み | 変更なし |
| 13 | 区間切替時に `samples` を消去 | `FrameTimeProbe.cs` | 修正済み | 変更なし |
| 14 | 代替曲長を実メディアのフェードに使う | `NotesManager.cs` | **妥当** | 実メディア長を採用 |
| 15 | 分離キャッシュを結合可能環境でも再利用 | `YouTubeMediaCache.cs` | **妥当** | `canMerge` を追加 |
| 16 | HUD 再構築時に表示値キャッシュ | `V6GameHudView.cs` | 修正済み | 変更なし |

**妥当 8 件 / 修正済み 8 件。**

## 2. 修正した 8 件

### 2.1 `startTiming` を持つ譜面が開始できない (重大)

`StartAfterTiming` の末尾で `startTimingPending = false` に戻してから
`StartPlayback()` を呼んでいた。`StartPlayback` の待機条件は
`startTiming > 0 && !startTimingPending` なので、戻した直後に条件が再び成立し、
待機コルーチンが永久に再起動して `playbackStarted` に到達しない。

- 修正: 待ち済みを `startTimingSatisfied` として分離。
- 純ロジックを `StartTimingGate.ShouldWait` へ出し、EditMode で固定。
- 回帰の実証: `StartTimingGate` の修正を外すと
  `ItDoesNotWaitAgainAfterTheWaitFinished` が **FAIL** することを確認した。

### 2.2 Result 遷移が曲長の 1 秒前

`MediaTimeline.HasSongEnded` の既定 `leadOutSeconds = 1.0` を使っていたため、
遷移要求が曲長の 1 秒前に出て、最後の 1 秒が表示されないまま暗転していた。

- 修正: 本番の終端判定だけ `leadOutSeconds: 0f`。フェード位置は
  `ChartTiming.FadeOut` が持つ別の関心事として分離。
- 既定引数は変えていないので既存の呼び出しは壊れない。

### 2.3 末尾ノーツの判定前に暗転

`fadeOut` の区間に末尾ノーツがある譜面 (曲長 100 / fadeOut 2 / 末尾ノーツ 99.5 秒)
では、99 秒で黒オーバーレイが 0.5 になり、暗転中の判定をさせていた。

- 修正: `ChartTiming.FadeProgress(mediaTime, notBeforeSeconds)` を追加し、
  `SettleDeadline()` を下限にする。終端では必ず 1 になる。
- 回帰の実証: 下限を外すと `FadeProgress_DoesNotBlackOutWhileTheLastNoteIsStillJudged`
  が **FAIL** することを確認した。

### 2.4 保存失敗時に割り当てが戻らない

`KeyBindings.SetAll` は保存より先に実行中の表を書き換えて `Changed` を通知する。
`Save` が失敗しても `Commit` が `false` を返すだけで、判定キーだけが変わった状態が
残り、キャンセルでも復旧できなかった。

- 修正: 失敗時に旧表へ `SetAll` で戻す (キーガイドも追随する)。

### 2.5 キーボードで「適用」できない

モーダルの `Update` は Esc / Enter / 矢印しか扱わず、「適用」は `onClick` だけだった。
キーボード利用者は代表キーを変更しても保存できなかった。

- 修正: Ctrl / Cmd + Enter を「適用」に割り当てる。Enter 単独は行の待受開始の
  まま (二重に走らせないため Tab フォーカス方式は採らない)。

### 2.6 Esc で取り消した名前入力が登録を始める

`TMP_InputField` は Esc で `m_WasCanceled` を立て、`ReleaseSelection` 経由で
`onEndEdit` を通知する。`OnNameEditEnd` が `wasCanceled` を見ていなかったため、
保存済みの名前に戻った状態で自動登録が走る可能性があった。

- 修正: `modalNameInput.wasCanceled` を確認して確定しない。
- 根拠: 同梱 uGUI 2.5.0 の `TMP_InputField.cs` (`m_WasCanceled = true` →
  `ReleaseSelection` → `SendOnEndEdit`) を実読して順序を確認した。

### 2.7 代わりの曲長を実メディアのフェードに使う

曲長が未登録の譜面では「最終ノート + fadeOut + 0.8 秒」を代わりの曲長にする。
実メディアが 180 秒・最終ノート 100 秒だと 102.8 秒で暗転し、残りが黒くなっていた。

- 修正: `ChartTiming.IsFallbackEnd` で見積もりを区別し、実メディアの長さが
  取れた時点で `WithRealMediaDuration` により置き換える。
- 譜面が宣言した曲長は上書きしない (曲長はカタログの責務)。

### 2.8 分離キャッシュを結合可能環境で再利用

`TryGetReadyFiles` は ffmpeg の有無を見ずに分離ペアを返していた。
結合できる環境で分離ペアを使うと、Unity の `VideoPlayer` が開けない音声
コンテナを掴んだまま「音が出ない」状態で固定されうる。

- 修正: `canMerge` を追加し、結合可能なら分離ペアを採用せず形式解決へ進む。
  結合済みファイルは従来どおり再利用する。

## 3. テスト

| 種別 | 結果 |
|---|---|
| EditMode | **1669 件 / 失敗 0** (`TestResults.xml`) |
| 追加した回帰テスト | `StartTimingGateTests` (5) / `ChartTimingTests` (+9) / `MediaCacheReadyFilesTests` (+2) / `V6KeyConfigLogicTests` (+2) / `MediaTimelineTests` (+2) |

追加したテストは、修正を外すと失敗することを 2 系統で確認済み (§2.1 / §2.3)。

## 4. 未確認 (この修正で完了扱いにしない)

- **Win64 実機での再確認は未実施。** ここにあるのは EditMode とコード読解である。
  再生開始・曲終端・メディアキャッシュ・キー設定保存の実動作は、凍結候補
  `0.1.23+b6860a80` の受入とは別に、次のビルドで Windows 実機にかける必要がある。
- **キー設定モーダルの Ctrl + Enter は実機入力で未確認。**
- **`startTiming` を持つ譜面は現行カタログに存在しない** (29 譜面すべて未指定)。
  そのため 2.1 の不具合は実プレイでは発火しておらず、EditMode で固定した。
