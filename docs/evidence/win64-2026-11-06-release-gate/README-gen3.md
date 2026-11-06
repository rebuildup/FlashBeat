# Run D — 展示第 3 世代 `FlashBeatExhibit_s2`（実機 / 完全完走 / **映像あり** / 音あり）

日付: 2026-11-07 00:01–00:03 JST / ドライバ: [`release/check/drive-gameplay.ps1`](../../../release/check/drive-gameplay.ps1)（`-PlayLanes` 付き）

| 項目 | 値 |
|---|---|
| 実行した exe | `D:\4_apps\Unity\Build\FlashBeatExhibit_s2\FlashBeat.exe`（**第 3 世代 = 統合ビルド + #13 のストリーム修正**） |
| コード状態 | `b77b63c`（merge of `origin/13` + `origin/27`、`--no-ff`、squash / rebase / force push なし） |
| ビルド | `[BuildScript] Build succeeded: 1588358518 bytes` / **0 error CS** / Unity 6000.5.6f1（[`build-log-excerpt-gen3.txt`](build-log-excerpt-gen3.txt)） |
| `FlashBeat.exe` SHA-256 | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` |
| **フォルダ SHA-256** | `2907102fcab0142e8d93b7bfb025d74a98cc274fe6ebbac55843cdca14cbedd9` |
| ファイル数 / 総バイト | 199 / 1 588 795 255 |
| 起動 → タイトル | **14.1 秒**（実測） |
| 実到達 | Title → Select（No.2 まにまに）→ GameScene → **判定 76**（譜面ノーツ全部）→ **Result** → `R` で **Retry**（シーンロード計 10 回） |
| ウィンドウ | windowed 1280×720（クライアント領域 1280×720） |
| **映像（YouTube ストリーム）** | **再生されている（実証）**: `media plan 'まにまに': video-only mp4/H.264 itag=232 1280x720 + audio-only m4a/AAC itag=140` → `VideoPlayer prepared=True file=video.mp4 after 0.4s` → `STATUS … video[playing=True frame=8069 prepared=True]`（#13 の「拡張子なしキャッシュを VideoPlayer が認識しない」修正 = キャッシュがコンテナ拡張子を保持する、が効いている） |
| **音** | **鳴っている（実証）**: `STATUS … rms=0.4352 … 0.5579 peak=0.8249`。ただし `audioMode=Bundled` = 音源は **同梱 wav**（`Resources/Musics/まにまに.wav`）。ストリームの音声トラック（m4a itag=140）は `Failed to recognize video file format for: …audio.m4a` → `stream audio prepared=False after 20.0s` で **準備できず**、フォールバックが効いた。**「YouTube ストリームの音声そのもの」は未証明**（音は鳴るが、音源が同梱 wav） |
| 判定（実入力） | `Perfect 1 / Great 0 / Bad 2 / Miss 73`（ドライバがレーンキー `Q W D F J` を約 1 秒間隔で実入力。全 Miss ではない = **実入力で判定が変わる** ことまで実証） |
| 同期 | `mediaTime` と判定カウンタが同じログ行で進む（`STATUS mediaTime=45.78 … video[playing=True …] perfect=1 bad=2 miss=73`）＝ 判定クロックがメディア時計に追従 |
| 開始条件 | #13 ビルドはメディア準備後に `press Space to start` を要求 → ドライバは準備完了後に Space を送る（`-PlayLanes` 実装の一部）。準備前に押しても始まらない |

証跡（**Git 外**。スクリーンショットは LFS バイナリのため Git に入れない）:

| ファイル | SHA-256 | 内容 |
|---|---|---|
| `D:\4_apps\Unity\Build\FlashBeatEvidence\2026-11-07-exhibit-s2\01_title.png` | `3d48238d11528776ed89bcae55dfec9d30b10ae02400f494062efce682d46d99` | タイトル |
| `…\02_select.png` | `d8a07ecc7bb052c1d76f808481981227142c41e2eb26a9f2de531a244ca2f593` | 選曲画面 |
| `…\03_select.png` | `98e7f4697d655a8b79724f556b22c2ec941b9f42d11f4166f96c6e19ec80a0b3` | 選曲画面（安定後） |
| `…\04_select_song2.png` | `0d885908807162f17ef676ff8e0b2d452eee378780658966eca8eab84ddf77aa` | No.2 へ移動（日本語タイトル表示） |
| `…\05_game.png` | `d5049cdb67bf17b9be107467a70fc92f18b485a7fdbac842e820d6929983f1ac` | GameScene（メディア準備中） |
| `…\06_game_playing.png` | `f282f1d3651842686873f22741aa94f7b6298e976501ad3485c6bd9f148dba4c` | 開始直後 |
| `…\06b_game_playing.png` | `4e10e5b38c54dbe3cbd7b7cce21d57ea54aecec66d82031d6ff94c25194299aa` | 譜面進行中（**YouTube 映像が背景**） |
| `…\07_result.png` | `518c2016dbd4db0aec13b5bf7904e5f2dae7b625a9474019ea7f2a9664bfa2a9` | リザルト画面 |
| `…\08_retry.png` | `ea3b7abf0a96e38dc7ed7892f19396a1c8092577e730b1afea0ae8068f52e6cb` | `R` によるリトライ後の GameScene |
| `…\Player.log` | `0f0bde4cac6cc35b3f949a5050693ca13f3cd6cdf861a2491a9b194480f17f16` | 生ログ（**未コミット**。video id を含む） |
| `…\Player.redacted.log` | `343446ce5e75cf78e70970b9c1e930f916442c36fbdbfd562e470256e4e9261f` | マスク済みログ（`docs/evidence/win64-2026-11-06-release-gate/Player-exhibit-s2.redacted.log` としてコミット） |
| `…\run-summary.json` | `68aad09d27e9b9e851568329286b0ed4f82ae02a66665b46ae889f0fab6c7660` | ドライバサマリ（startup 14.1 s / result / retry / 判定 76） |

マスク結果: video id 2 箇所を置換 → `redact-log.ps1 -Check` = `0 / 0 / 0 / 0 → OK`。

## 第 3 世代で残る未証明

1. **YouTube ストリームの音声トラック（m4a）の再生** — Unity は `audio.m4a` を
   `Failed to recognize video file format` で拒否する（音声は同梱 wav で代替されているため展示は成立する）。
   「音源がストリーム」を証明するには #13 側の追加修正（m4a コンテナの認識、または音声付き combined 形式の選択）が必要。
2. **実機 1280×720 ディスプレイでの fullscreen 描画** — この PC に 1280×720 パネルが無い。
3. **S2 / S3**（API 連携・公開譜面の再生）— 成果物なし。
