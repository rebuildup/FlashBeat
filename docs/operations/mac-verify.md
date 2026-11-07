# Mac での確認手順 (macOS 検証環境)

Windows 実機は展示用のため操作しない。Mac 側でビルド・起動・プレイ確認を行う。
本手順の実施は検証ワーカー (#58 / #59) の担当。

## 前提ツール

|ツール|導入方法|確認コマンド|
|---|---|---|
|Unity 6000.6.8f1|Unity Hub (`/Applications/Unity/Hub/Editor/6000.6.8f1`)|—|
|yt-dlp|`brew install yt-dlp` (`/opt/homebrew/bin/yt-dlp`)|`yt-dlp --version`|
|deno|`mise install deno` (`~/.local/share/mise/installs/deno/<ver>/bin/deno`)|`~/.local/share/mise/installs/deno/*/bin/deno --version`|
|ffmpeg|`brew install ffmpeg`|`ffmpeg -version`|

`YouTubeStreamResolver` は macOS でも拡張子なしバイナリを探索する
(PATH → mise インストール木 → `/usr/local/bin` `/opt/homebrew/bin`)。
個別指定したい場合は `FLASHBEAT_YTDLP_PATH` / `FLASHBEAT_DENO_PATH` / `FLASHBEAT_FFMPEG_PATH` を使う。

## ビルド (batchmode)

```sh
/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath <worktree> \
  -executeMethod BuildScript.BuildStandaloneMacOS \
  -logFile Builds/macOS-build.log
```

- 出力: `Builds/macOS/FlashBeat.app`
- 失敗時はプロセス終了コード 1。`Builds/macOS-build.log` の `[BuildScript]` 行を確認する。
- 配布物向けの後処理 (update-macos.sh 複製 / update-channel.txt / メディア除外マニフェスト) は
  Windows ビルドと同じく macOS ビルドでも行われる (`Contents/Resources/` 配下)。
  これにより `Builds/macOS/FlashBeat.app` も Release 配布・アプリ内更新の経路にそのまま載せられる。

## 起動と画面確認

```sh
open Builds/macOS/FlashBeat.app
screencapture -x /tmp/flashbeat-title.png
```

確認項目:

1. Title / Select / Option / Game / Result の各シーンが**ディスプレイ全体 (フルスクリーン)** で表示されること
2. 各画面で UI が欠けず、マウス/キーボードで操作できること
3. キー入力は `osascript -e 'tell application "System Events" to keystroke "a"'` 等で送ってよい
   (8 レーンキーマップは `Judge.LaneKeys`: Lane 0 = 1/Q/A/Z …)

## 1 曲実プレイ確認

1. Select で曲を選び Game へ遷移 → Space で開始
2. ノーツが**音と同期**して流れ、判定 (Perfect ≤0.10s / Great ≤0.15s / Bad ≤0.20s) が正しいこと
3. YouTube の**映像と音声が出る**こと、BPM と同期していること
   (yt-dlp が googlevideo からダウンロードしたローカル mp4 を再生する。初回はダウンロード待ちがある)
4. 曲末まで到達して **Result** に遷移すること
5. Result の **Retry** で再選曲 (Select) に戻ること

## EditMode テスト (batchmode)

```sh
/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath <worktree> \
  -runTests -testPlatform editmode -testResults TestResults.xml
```

結果は `TestResults.xml` に記録される。

## 既知の注意

- `mise` を実行すると `mise.lock` / `.mise/` が更新されることがある (cf / void のロック)。macOS 検証の差分には含めない。
- LFS ファイルは `git status` に stat 只有りの modified が出ることがある。内容一致 (`git diff` 空) なら `git add -u -- . ':(exclude)mise.lock'` で解消する。
- `git reset` / `git checkout` / `git clean` は使用しない。
