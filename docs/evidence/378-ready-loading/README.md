# #378 受入証跡 — Ready 即時遷移とローディング画面

実行日: 2026-11-15 / ブランチ `w/378-ready-loading` / macOS ビルド (`Builds/macOS/FlashBeat.app`)

## 背景 (main 時点の実装)

- `SelectSceneManager.GameScene()` は `Initiate.Fade("GameScene", Color.black, 6f, loadImmediately: true)`。
  フェードは約 0.17 秒で、シーンの非同期ロードはフェード完了を待たずに始まる。
- リモート曲は `LoadingOverlay.Show("譜面を準備しています")` が Ready 直後に出る。
- `V6LoadingView` のヘッダーは共通 `V6Header` / `V6Header.BackButton` を使用済み。
- ローディング映像は `Resources/Videos/loading.mp4` → `VideoPlayer` → `RenderTexture` → `RawImage`。
  prepare 完了までは `RawImage` を隠し、背景は設計済みの背景画像 (Background surface) を出す。

## 今回の変更

`V6GameRuntime.UpdateLoading` はこれまで `HudPlayPhase.Preparing` / `Failed` の時だけ
ローディング画面を出していた。GameScene 活性化から MusicManager の準備が
`Preparing` に立つまで約 0.7 秒、画面はフェードアウト後の黒のままだった。

`MediaReady` / `PlaybackStarted` (MusicManager の公開状態) を見て、
「セッション開始済みだがメディア未準備」の間もローディング画面を出すようにした。
Ready 待ち (MediaReady 後) とプレイ中は従来どおり隠れる。

ハーネス側:

- `AutoTestV6`: Ready 押下からフェード開始・GameScene 到達・ローディング画面表示を
  フレーム単位で実測し、Player.log に `ready->fade=… ready->game=… ready->loading=…` を出す。
  「視覚変化が 1 秒以内」「ローディング画面が出る」を assert する。
- `AutoTestV6Screens` (GameScene-Loading): `ShowLoadingForAudit` 後に
  `VideoPlayer.isPrepared` と映像面 (RawImage) の有効化を最大 15 秒待ち、
  `VideoSurfaceForAudit == "Video"` (フラットなフォールバック面ではない) を assert してから撮影する。
- `V6LoadingView.IsShownForAudit` を追加 (表示状態の観測用)。

## 実行

```
./Builds/macOS/FlashBeat.app/Contents/MacOS/FlashBeat \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -autotest v6 -autotest-out docs/evidence/378-ready-loading/fhd

./Builds/macOS/FlashBeat.app/Contents/MacOS/FlashBeat \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -autotest v6screens -autotest-out docs/evidence/378-ready-loading/v6screens
```

## 結果

### Ready → 表示までの実測 (Player.log、1920x1080)

| 計測 | before (main) | after |
| --- | --- | --- |
| ready→fade (視覚変化の開始) | フェード開始は次フレーム (damp 1.0 → 約 1 秒の暗転のあとロード) | 0.366s |
| ready→GameScene 活性化 | — | 0.700s |
| ready→ローディング画面表示 | 1.392s (Preparing 待ちの黒い隙間あり) | **0.700s** (GameScene 活性化と同時) |

- `fhd/autotest-v6-result.txt`: **pass=31 fail=0**
- `v6screens/autotest-v6screens-result.txt`: **pass=60 fail=0** (21 screenshots)
- `GameScene-LoadingVideo` レポート: loading.mp4 の prepare 完了・映像面表示・
  `Video` surface を確認 (pass=2 fail=0)。
- `v6screen-GameScene-Loading-21.png`: ローディング画面。loading.mp4 は全編が
  暗いトーンのクリップ (フレーム抽出で確認) で、画面のピクセル分散は映像内容と一致。
  単色面ではない。

## フォールバックの明文化 (5 分待っても映像が準備できない場合)

`V6LoadingVideoPolicy.Decide` の規則により、`VideoPlayer` が prepare できない・
エラー・未準備の間は常に `Background` surface (設計済み背景画像 + UI) が出る。
白い `RawImage` が残ることはない。準備が極端に長引いても表示は Background のまま
継続し、メディア準備側の失敗は `HudPlayPhase.Failed` → `SetError` の
「選曲へ戻る」導線に落ちる (タイムアウトは `HudPlayState.FailureTimeoutSeconds = 45s`)。

## 残課題

- before 値の fade→loading はコード上の damp 1.0 → 6f の変更履歴からの推定。
  同一マシンでの before 実測は取っていない (after のみ実測)。
- Windows 実機での確認は未実施 (Mac のみ)。
