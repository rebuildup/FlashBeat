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

### 1. シーン遷移中のローディング表示を復旧 (`LoadingOverlay`)

`Initiate.LoadingFeedback.Begin` → `ShowImmediately(SceneLabel)` の経路は
以前からあったが、`Show(label, owner: null)` が `activeOwners` を Clear するだけで
オーナーを追加しなかったため、`activeOwners.Count == 0` となり Update が
「全オーナー解除」と見なして **シーン遷移のオーバーレイは一度も表示されていなかった**。

- `Show(null)` が排他オーナー (`ExclusiveOwner`) を登録するよう修正。
  これで全シーン遷移 (v5/v6 共通) で、非同期ロード中にオーバーレイが出る。
- #431 で入れた「GameScene かつ v6 では Begin フックで出さない」抑制を解除。
  非同期ロード中はオーバーレイ、シーン活性化後は `End → Hide` で退き、
  `V6LoadingView` に引き継ぐ。メディア / 譜面準備中のオーバーレイは
  `GameLoadingRoute.UsesLegacyOverlay` が別途止めるので二重表示にならない。

### 2. GameScene 起動直後の隙間をローディング画面で埋める (`V6GameRuntime`)

`UpdateLoading` は `HudPlayPhase.Preparing` / `Failed` の時だけローディング画面を
出していた。シーン活性化から `Preparing` に立つまでの隙間でも、
`MusicManager.MediaReady` / `PlaybackStarted` を見て「セッション開始後に
メディア未準備」の間は `V6LoadingView` を表示する。
Ready 待ち (MediaReady 後) とプレイ中は従来どおり隠れる。

### 3. ハーネス

- `AutoTestV6`: Ready 押下からフェード開始・オーバーレイ表示・GameScene 到達・
  ローディング画面表示をフレーム単位で実測し、Player.log に
  `ready->fade=… ready->overlay=… ready->game=… ready->loading=…` を出す。
  「視覚変化 1 秒以内」「遷移中にローディング表示」「GameScene 活性化から
  1 秒以内にローディング画面が出る」を assert。
- `AutoTestV6Screens` (GameScene-Loading): `ShowLoadingForAudit` 後に
  `VideoPlayer.isPrepared` と映像面 (RawImage) の有効化を最大 15 秒待ち、
  `VideoSurfaceForAudit == "Video"` (フラットなフォールバック面ではない) を assert してから撮影。
- `V6LoadingView.IsShownForAudit` / `LoadingOverlay.IsVisibleForAudit` を追加。

## 実行

```
./Builds/macOS/FlashBeat.app/Contents/MacOS/FlashBeat \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -autotest v6 -autotest-out docs/evidence/378-ready-loading/fhd

./Builds/macOS/FlashBeat.app/Contents/MacOS/FlashBeat \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -autotest v6screens -autotest-out docs/evidence/378-ready-loading/v6screens

./Builds/macOS/FlashBeat.app/Contents/MacOS/FlashBeat \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -autotest v6interact -autotest-out <dir>   # 遷移回帰
```

## 結果 (1920x1080)

### Ready → 表示までの実測 (Player.log)

| 計測 | before (main 相当) | after |
| --- | --- | --- |
| ready→fade (視覚変化の開始) | 同系 (damp 6f は main 済み) | 0.446s |
| ready→ローディング系の表示 | **1.392s** (遷移オーバーレイは不発、Preparing 待ちの黒い隙間あり) | **0.446s** (オーバーレイ) → 0.759s (V6LoadingView) |
| ready→GameScene 活性化 | — | 0.759s |

- before の 1.392s は本ブランチのハーネス追加コミット (ランタイムは main と同一)
  での実測値。after は最終コードでの実測値。
- `fhd/autotest-v6-result.txt`: **pass=32 fail=0**
- `v6screens/autotest-v6screens-result.txt`: **pass=60 fail=0** (21 screenshots)
- `v6interact`: **pass=97 fail=0** (実遷移の回帰なし)
- EditMode: **1821/1821 pass**
- `GameScene-LoadingVideo` レポート: loading.mp4 の prepare 完了・映像面表示・
  `Video` surface を確認 (pass=2 fail=0)。
- `v6screen-GameScene-Loading-21.png`: ローディング画面。loading.mp4 は全編が
  暗いトーンのクリップで、画面のピクセル分散は映像内容と一致。単色面ではない。

## フォールバックの明文化 (5 分待っても映像が準備できない場合)

`V6LoadingVideoPolicy.Decide` の規則により、`VideoPlayer` が prepare できない・
エラー・未準備の間は常に `Background` surface (設計済み背景画像 + UI) が出る。
白い `RawImage` が残ることはない。準備が極端に長引いても表示は Background のまま
継続し、メディア準備側の失敗は `HudPlayPhase.Failed` → `SetError` の
「選曲へ戻る」導線に落ちる (タイムアウトは `HudPlayState.FailureTimeoutSeconds = 45s`)。

## 副次的な修正 (レビューで発覚)

- `LoadingOverlay.Show(null)` が実際には表示されない潜在バグを修正
  (上記 1.)。これにより v5 経路のシーン遷移でも、本来意図されていた
  ローディングオーバーレイが出るようになる。

## 残課題

- Windows 実機での確認は未実施 (Mac のみ)。
- オーバーレイの最短表示 (400ms) とフェードアウト (0.2s) により、
  高速なシーン遷移では v6 ローディングの上に短く重なることがある
  (ローディング画面→ローディング画面のクロスフェードとして許容範囲と判断)。
