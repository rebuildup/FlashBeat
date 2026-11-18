# アプリの書き込み境界 監査 (issue #670 再監査 / v0.1.31)

対象: Unity プレイヤー (Windows / macOS) が制御するファイルシステムへの書き込み全般。
「更新処理の外部書き込み禁止」と「アプリ全体の外部書き込み禁止」は別の条件であり、
この文書は両者を混同せず、現行実装が保証する範囲と保証できない範囲を列挙する。

## 1. 絶対条件の適用範囲

v0.1.31 の絶対条件は **「アップデータが制御するファイル操作を
`<install>/.flashbeat-update/` とインストール先フォルダ内に限定する」** である
(Issue #670 / 親 #669-1)。これは更新機構 (アプリ内 `UpdateChecker` /
`UpdateStaging` / `UpdateResultFile` / `UpdatePaths` と配布物の
`Update.ps1` / `update-macos.sh`) に適用され、以下で保証する:

- すべての更新アーティファクト (staging ZIP / manifest / backup / work /
  logs / results / updater 一時領域) は `<install>/.flashbeat-update/` 配下。
- `-WorkDir` / `-LogDir` / `-LogPath` / `-ResultDir` の境界外指定は拒否。
- ZIP エントリのトラバーサル・絶対パス・ドライブ修飾・`.flashbeat-update`
  同梱を拒否。swap/backup/restore は `.flashbeat-update` を除外。
- reparse point (シンボリックリンク / ジャンクション) はスクリプト側・
  アプリ側 (`UpdatePaths.IsReparsePoint` / `UpdateAreaIsSafe`) の両方で拒否。
- 異常終了時は backup から復元。境界外に書けない場合は更新自体を中止し、
  現行版を維持する (フォールバックで境界外へ逃げない)。
- 旧レイアウト (persistentDataPath 系 `update/`、親フォルダの
  `.flashbeat-update` / `FlashBeat-backup-*`) の残存物は**移動も削除もしない**。

## 2. 更新以外のアプリ書き込みの棚卸し

以下は更新機構ではないが、アプリ (または同梱コード) がファイルシステムへ
書き込む経路の全量。分類: **U** = ユーザーデータ (OS 公認のユーザー領域)、
**C** = キャッシュ (再生成可能)、**D** = 開発/検証用 (既定で不活性)、
**E** = OS/エンジン管理 (アプリは書き込み先を選べない)。

| 書き込み先 | 内容 | 分類 | 根拠 |
|---|---|---|---|
| `persistentDataPath/FlashBeatMedia/` | YouTube メディアキャッシュ (動画/音声 + done マーカー) | C | `YouTubeMediaCache.CacheRoot` / `MediaCacheManager`。再生に必要な一時データで再取得可能。OS 公認のユーザー領域 |
| `persistentDataPath/settings/` | 曲ごとの設定 JSON (`*.setting.json`) | U | `OptionSceneManager.SettingsDirectory()`。ビルド版はこちら。**Editor では `<project>/UserSettings/song-settings`** (開発専用) |
| `persistentDataPath/` 直下 `keyconfig*.json` | キーコンフィグ | U | `KeyConfigFile`。旧 PlayerPrefs キーからの移行元でもある |
| `persistentDataPath/` (AutoTest* の `outDir` 既定) | 自動検証ハーネスのスクリーンショット/ログ | D | `-autotest*` 系フラグ指定時のみ動作。出荷ビルドでは既定不活性 |
| `-autoshot <dir>` 指定先 | `AutoShot` の PNG 出力 | D | 呼び出し側が明示した検証用パス。フラグ無しでは一切書かない |
| F3 スクリーンショット (FullscreenDiagnostics) | 開発ビルド/Editor の診断 PNG | D | Development ビルド限定。相対パスは `persistentDataPath` 基準 |
| PlayerPrefs (実体: Windows レジストリ `HKCU\...\Unity\...`、macOS `~/Library/Preferences/unity.<company>.<product>.plist`) | 音量・ノーツ速度・タイミング・フラッシュ・フルスクリーン・スコア (`ScoreStorage`)・旧キーコンフィグ | E | OS 管理の設定ストア。アプリはキー/値のみを渡し、実ファイルパスを制御しない |
| `Player.log` / クラッシュダンプ | Unity エンジンのログ・異常終了レポート | E | Windows: `%USERPROFILE%\AppData\LocalLow\<company>\<product>\`、macOS: `~/Library/Logs/Unity/` 等。エンジンが決める場所で、アプリコードから変更不可 |
| Unity/GPU シェーダキャッシュ・OS temp | エンジン/ドライバ内部 | E | アプリのコードパスに存在しない |
| yt-dlp / ffmpeg / deno (外部プロセス) | YouTube 解決・ダウンロード・mux | C | `--ignore-config` でユーザー設定を読まず、出力は `-o` テンプレート経由でキャッシュ領域のみ。probe はタイムアウト時 kill (#642)。yt-dlp は `--cache-dir` 未指定 = 独自キャッシュを書かない |
| `Update.ps1` / `update-macos.sh` (配布物) | 更新の全ファイル操作 | §1 の境界 | インストール先フォルダ内に限定済み |

### 更新スコープとの区別 (明示)

- **保証する**: 更新機構が書くものはすべてインストール先フォルダ内。
  境界外へのフォールバックは存在しない。
- **保証しない (設計上のユーザーデータ)**: スコア・設定・キーコンフィグ・
  メディアキャッシュは `persistentDataPath` (Windows:
  `%USERPROFILE%\AppData\LocalLow\<company>\<product>`、macOS:
  `~/Library/Application Support/<company>/<product>`) に置く。
  これは Unity 標準の「ユーザーごとのデータ領域」であり、
  **インストール先 (例: `C:\Program Files\...`) は標準ユーザーが
  書き込めない**ため、これらをアプリフォルダ内へ移すことは
  標準的なインストール形態では成立しない。ユーザーデータを
  インストール先へ集約する要件が出た場合は、インストール形態
  (per-user install / portable モード) の再設計が前提になる。
- **保証できない (OS/エンジン管理)**: PlayerPrefs の実体、
  Player.log、クラッシュダンプ、シェーダキャッシュはアプリが
  書き込み先を制御しない。これらは「アプリが制御するファイル操作」
  の範囲外であり、本監査の保証対象に含めない (隠さず分類として記録)。

## 3. macOS の更新拒否と将来の設計余地

現行: `update-macos.sh` は `.app` を `-InstallDir` に受けると安全側で拒否し、
アプリ側も `UpdatePaths.InstallRoot` が macOS で null のため staging しない。
macOS の更新は手動手順 (`update-patch.md` 参照)。

「署名済みバンドルの保全」と「更新境界」を両立する設計余地:

- **案 A (現行維持)**: `.app` 内への書き込みを一切行わず、更新は
  「新しい `.app` を別途入手してユーザーが差し替える」手動経路。
  境界・署名ともに完全に安全。自動更新の UX は失う。
- **案 B (.app 全体スワップ)**: 「インストールルート」を
  `.app` の**親フォルダ** (例: `~/Applications/` や `/Applications/`) と
  再定義し、`<parent>/.flashbeat-update/` に新 `.app` を展開・
  SHA-256/署名 (`codesign --verify` + `spctl`) 検証後、
  `mv` によるアトミックなリネームで差し替える。`.app` バンドル内部は
  一切書き換えないため署名は保たれる。前提: 親フォルダがユーザー
  書き込み可能であること (/Applications は管理者権限が要る場合がある)、
  quarantine 属性 (`com.apple.quarantine`) の扱い、差し替え中の
  プロセス終了待ち。Windows の「フォルダごと退避→差し替え→失敗時復元」
  と同型の構造にできる。
- **案 C (Sparkle 等のフレームワーク導入)**: macOS 標準の更新機構に
  委譲。実装コストと依存が増える。

現時点の判断: 案 B は Windows updater と同じ境界モデル
(「更新ファイルはインストールルート内」) に自然に乗るが、
**macOS 実機での署名検証・差し替え・Relaunch の検証が必須**であり、
未検証のまま実装すると「安全側で拒否」より悪い結果 (中途半端な
差し替え) になり得る。v0.1.31 では案 A を維持し、案 B は
macOS 実機検証を伴う別 Issue として扱うのが妥当 (要オーナー判断:
macOS の自動更新を v0.1.31 に求めるか)。

## 4. 監査方法

- `rg` で `File.Write|File.Move|File.Copy|File.Delete|Directory.Create|
  Directory.Delete|Directory.Move|ScreenCapture|PlayerPrefs.Set|
  Process.Start|persistentDataPath|temporaryCachePath|dataPath` を
  `Assets/Game/Scripts` 全体に適用し、更新機構・AutoTest・
  AutoShot・FullscreenDiagnostics を除く全ヒットを個別に確認した。
- 外部プロセス (yt-dlp / ffmpeg / deno) の引数を確認し、
  `--ignore-config` と出力先テンプレートの境界を確認した。
- Editor 専用経路 (`OptionSceneManager.SettingsDirectory` の
  `Application.isEditor` 分岐、`Assets/Editor/`) はビルド版に
  含まれないため対象外とした。
