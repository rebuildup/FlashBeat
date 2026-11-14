# #369 macOS ネイティブ全画面の実機受入

## 背景

Mac 実機 (3420x2224、 Retina ) で Unity の `FullScreenMode.FullScreenWindow` は
`Screen.fullScreen=True` / `fullScreenMode=FullScreenWindow` を報告するにもかかわらず、
実ウィンドウは `NSScreen.visibleFrame` (メニューバーを除く作業領域) サイズの
3420x2148 に留まり、上部約 76 px にメニューバーが見えていた。

Unity が提供する `Screen` API と `setFrame:` 系の公開 API はすべて
`visibleFrame` にクランプされるため、C# 側だけでは被覆を実現できない。

## 実装

`Assets/Plugins/macOS/FlashBeatNative.bundle` (ソース: `FlashBeatNative.m`) が
起動直後 (`DisplayModeBootstrap.Apply` → `NativeFullscreen.TryEnter`) に:

1. `toggleFullScreen:` で macOS の全画面化を要求する
2. `NSApp.presentationOptions` でメニューバー/Dock の自動非表示を有効にする
3. Unity がウィンドウを作業領域サイズへ戻すのを、プライベート API
   `_setFrameCommon:display:fromServer:` で 0.2 秒周期に矯正する

`-autotest` 指定時とプラグイン不在時は何もせず、従来のボーダーレス表示に
フォールバックする。Windows では `RuntimePlatform` 判定で即 false を返すため
既存の Win64 FHD/2K 受入経路には影響しない。

## 実機結果 (Mac、 3420x2224 )

5 回連続起動で `CGWindowListCopyWindowInfo` 上のアプリウィンドウが
`1710x1112` pt (ディスプレイ `screen.frame` と一致) を維持した。
スクリーンショット上部 90 px は全面ゲーム描画色 (45,45,45) で、
メニューバー・Dock・タイトルバーは写っていない。

- `window-bounds.txt`: `CGWindowList` によるウィンドウ矩形
- `launch1.png` / `launch3.png`: 起動後の全画面スクリーンショット
- `native-plugin.log`: プラグインの動作ログ

## 既知の制約

- Unity の `Screen.width/height` は依然 3420x2160 を報告し、
  `[FullscreenBootstrap] event=still-uncovered` が出る。これは Unity の
  メトリクスがウィンドウサーバー側の実被覆を反映しないためで、
  実ピクセル被覆は上記のとおり確認済み。描画は約 3% の縦方向スケール差を伴う。
- 表示スケーリング別 (2 構成) の検証はこの環境では未実施。
- Win64 実機での回帰確認は Windows 側の別途受入に委ねる
  (コードパスは macOS のみで有効)。
