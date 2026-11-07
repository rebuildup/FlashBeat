# 展示引き渡しドキュメント — 2026-11-08 朝 開始分

対象: 展示 (2026-11-08 朝開始) に渡すビルドの確認手順と記録方法。
元ドキュメント: [`three-resolution-fullscreen-verification.md`](three-resolution-fullscreen-verification.md)
(本ドキュメントはこれと矛盾しない。詳細は元ドキュメントを優先)。

## 0. 対象 SHA と PR 一覧

| 項目 | 値 |
|---|---|
| 対象 SHA (release-0-1-14 merge 済み head) | `09b87dd` (Merge PR #176) |
| 含まれる PR (merge 済み) | #172 (BPM/Duration DraftInput 化 + 実操作テスト) / #174 (mac プロファイル 2560x1664 再構築) / #175 (Worker #163) / #176 (FullscreenDiagnostics Canvas.rectTransform 修正 + フルスクリーン方針テスト) |
| 未着地 (本ビルドに **含まれない**) | #173 (draft, base=release-0-1-14) / #170・#171 (base=main — release-0-1-14 未着地) |

- 展示に使う ZIP は **この SHA からビルドされたもの** に限定する。別 SHA を使う場合は
  この表を更新してから引き渡す (実績のない SHA を「検証済み」と書かない)。
- **未証明**: 上記対象 SHA の実機 (Windows / Mac) での全画面・完走検証は
  本ドキュメント作成時点で **未実施**。§5 の手順を完了させるまで「検証済み」と断定しない。

## 1. 大原則 — 擬似全画面は禁止

- 検証は **その解像度の実ディスプレイ上でアプリが全画面表示している状態** でのみ有効。
- **ウィンドウのドラッグ / リサイズで「そのサイズに見せかける」擬似再現は禁止**。
  リサイズで得たスクリーンショットは証拠にならない (元ドキュメント §1 と同一)。
- 実装側の契約: `PlayerSettings.fullscreenMode = FullScreenWindow (1)` +
  `defaultIsNativeResolution = 1` → ディスプレイのネイティブ解像度で起動する。
  `Screen.SetResolution` による擬似全画面実装は禁止 (コード内 0 件を確認済み)。
- 起動直後の実効値 (`Screen.width/height/fullScreenMode/dpi`) は
  `FullscreenDiagnostics` が起動時に Player.log へ出力する。**必ず実測値を採取する**
  (PlayerSettings の設定値を「修正済み」と言い換えない)。

## 2. 対象は 3 解像度だけ

| ID | 解像度 | 縦横比 | 入手方法 |
|---|---|---|---|
| FHD | 1920×1080 | 16:9 | 実ディスプレイ (展示PC / FHD モニタ) |
| 2K | 2560×1440 | 16:9 | 実ディスプレイ (2K モニタ) |
| MAC | 2560×1664 | 16:10 | Mac 本機の内蔵 Liquid Retina (ネイティブ) |

- FHD 未満 (1024×768 / 1280×720 等) は対象外。
- 旧 1440×900 (Mac) 設計は Issue #162 により **無効** — 検証にも使わない。
- **未証明**: 2K モニタが展示当日に確保できるかは未確定。確保できない場合は
  2K を「未証明」と記録する (Web Studio のビューポート確認のみでは実機証明の代わりにならない)。

## 3. 展示PC (Windows) での確認手順

手順実行者が可能な操作のみで構成する (Unity Editor / 開発ツール不要)。

1. **ZIP 展開**: 配布 ZIP を展開する (Git LFS 云々は ZIP には無関係 — ビルド済み)。
2. **起動**: `FlashBeat.exe` を **引数なし** で起動する。
3. **全画面スクリーンショット**: 起動直後に **画面全体** を撮る
   (Win + Shift + S ではなく PrintScreen で全画面。ウィンドウ枠 / タスクバー / 黒帯が
   1px でも見えたら不合格)。
4. **Player.log 採取**:
   `%USERPROFILE%\AppData\LocalLow\rebuildup\FlashBeat\Player.log` をコピーする。
   `[FullscreenDiagnostics]` 行から `screen=` / `fullScreenMode=` / `dpi=` /
   `displays=` を確認する。
5. **1 曲完走の操作ログ**: 選曲 → Space 開始 → ノーツ操作 → Result → Retry を
   実際に操作し、いつ / 何を押した / 何が起きたを時系列でメモする。

### 3.1 記録するファイル名

`docs/operations/exhibit-evidence-2026-11-08/` 配下に保存する:

| ファイル | 内容 |
|---|---|
| `win-fhd-fullscreen.png` | FHD 実全画面スクリーンショット |
| `win-fhd-Player.log` | FHD 実行の Player.log コピー |
| `win-fhd-operation.md` | FHD 操作ログ (1 曲完走を含む) |
| `win-2k-fullscreen.png` / `win-2k-Player.log` / `win-2k-operation.md` | 2K 同上 |
| `win-fhd-fullplay-01.png` … | 1 曲完走の各場面 (Select / Game / Result) |

(2K が未確保の場合は `win-2k-*` を作らず「未証明」と §6 の表に記す。)

## 4. Mac (2560×1664) の確認手順

元ドキュメント §3 と同一。要約:

```sh
# ビルド (Unity 6000.5.6f1, release-0-1-14 ワークツリー)
/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath <worktree> \
  -executeMethod BuildScript.BuildStandaloneMacOS \
  -logFile Builds/macOS-build.log

# 起動 → 実全画面 → スクリーンショット
open Builds/macOS/FlashBeat.app
screencapture -x /tmp/flashbeat-mac-2560x1664.png
```

- `screencapture -x` の画像サイズが **2560×1664** であること
  (`-menubar` / `-window` を付けない = 全画面)。
- Dock / menu bar が隠れる (fullscreen) ことを確認。
- `~/Library/Logs/Unity/Player.log` から `[FullscreenDiagnostics]` 行を採取する。
- 記録先: `exhibit-evidence-2026-11-08/mac-2560x1664-fullscreen.png` /
  `mac-2560x1664-Player.log` / `mac-2560x1664-operation.md`。
- **未証明**: Mac ビルド (`BuildStandaloneMacOS`) の実行と実全画面は本 SHA で未実施。

## 5. Web Studio の 3 ビューポート確認

Web は実ディスプレイではなく **実ブラウザのビューポート** で 3 尺寸を確認する
(元ドキュメント §4 と同一。実機証明の代わりにはならない)。

1. 1920×1080 / 2560×1440 / 2560×1664 のビューポートで実ブラウザを起動する
   (DevTools のデバイスツールバーではなく、リサイズ可能な実ウィンドウを推奨。
   DevTools を使う場合はビューポート寸法をスクリーンショットに写す)。
2. 入力欄 / ドロップダウン / 譜面エディタを **実際に操作** する
   (入力・フォーカス・スクロール・重なり。見た目の比較だけで終わらせない)。
3. 各尺寸のスクリーンショットを `web-1920x1080.png` / `web-2560x1440.png` /
   `web-2560x1664.png` として保存する。

## 6. 完了条件と「未証明は未証明と書く」運用ルール

- 完了条件 (元ドキュメント §5 と同一): 3 解像度それぞれに
  「実全画面スクリーンショット + Player.log の実効解像度 + 操作ログ + commit SHA」。
  1 曲完走 (選曲 → 演奏 → Result → Retry) のスクリーンショット。
- **運用ルール**: 1 つでも未確認があれば **未証明** と明記する。
  推測や設定値の引用で「動くはず」「修正済み」と書かない。
  green を作らない — 未証明の項目は展示当日の口頭説明でも「未確認」と言う。
- 記録テンプレート (§3〜§5 の実施後に埋める):

| 確認項目 | 結果 | 証拠ファイル | 実施者 / 日時 |
|---|---|---|---|
| FHD 実全画面 (1920×1080) | 未証明 | — | — |
| 2K 実全画面 (2560×1440) | 未証明 | — | — |
| MAC 実全画面 (2560×1664) | 未証明 | — | — |
| 1 曲完走 (FHD) | 未証明 | — | — |
| Web 3 ビューポート | 未証明 | — | — |

## 7. 失敗時の切り分け

| 症状 | 第一推測 | 切り分け |
|---|---|---|
| 動画が再生されず Player.log に `0x80070005` (Access Denied) | Windows Media Foundation のコーデック / DRM / ファイルアクセス権 | 同一 ZIP を別 PC で試す。`Player.log` の該当行をそのまま記録する。コーデック導入は展示PC の管理者に確認 |
| 音が出ない / 曲が選べない | 音源未同梱 (`Assets/Game/Resources/Musics/` がビルドに含まれていない) | ZIP 内の `FlashBeat_Data/StreamingAssets` / Resources の有無を確認。音源ファイルの同梱はビルド側の問題 — ZIP を作り直す |
| 全画面にならずウィンドウ表示 | `fullscreenMode` / `defaultIsNativeResolution` が想定外, または OS 側のスケーリング | Player.log の `[FullscreenDiagnostics]` 実測値を記録。Windows 側は推奨解像度 + スケーリング 100% を確認 |
| `[FullscreenDiagnostics]` が Player.log に無い | 対象 SHA より前のビルドを使っている | ZIP のビルド SHA を §0 の表と照合 |
| YouTube 動画が読み込めない | ネットワーク / YoutubePlayer の接続先 | オフライン展示では音源同梱版で運用するか、事前にキャッシュされた環境を用意する (現状の依存は未証明 — 当日確認) |
| その他 | — | `Player.log` 全文とスクリーンショットを `exhibit-evidence-2026-11-08/` に保存し、Issue として起票する |

## 8. 引き渡しチェックリスト (配布者用)

- [ ] 対象 ZIP のビルド SHA が `09b87dd` (または §0 表の更新後の値) と一致
- [ ] §3〜§5 の手順が実施済み / 未実施の区別が §6 の表に反映されている
- [ ] 未証明項目が「未証明」と明記されている (空欄や「OK」の曖昧表記がない)
- [ ] 失敗があった場合、Player.log とスクリーンショットが保存されている
