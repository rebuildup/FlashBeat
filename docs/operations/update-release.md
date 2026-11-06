# Win64 リリース運用手順 (Issue #35)

展示会場の Windows 機に Unity 本体 (`FlashBeat.exe`) のパッチを配布する手順。
アプリ自体の起動・更新フロー (起動時非同期確認 → 終了時適用) は Issue #36 (`tools/update/**`) が実装する。
本ドキュメントは **配布物の仕様 = #36 の UpdateChecker / Update.ps1 が消費する契約** を固定する。
常設ランチャー・独自更新サーバーは作らない。更新元は GitHub Releases のみ。

## 配布仕様 (#36 との契約・固定)

### タグ / アセット命名

- タグ: `v<semver>-win64-<shortsha>` (例: `v0.1.1-win64-743c88d`)
  - `<semver>` = `X.Y.Z`、`<shortsha>` = ビルド元 commit の短縮 SHA (7〜40 hex)
- アセット: `FlashBeat-<semver>-win64-<shortsha>.zip` と `FlashBeat-<semver>-win64-<shortsha>.zip.sha256` の 2 点
- ZIP ルートに `version.json` を同梱 (パッケージスクリプトが自動生成):

  ```json
  {
    "version": "0.1.1",
    "buildSha": "<40 桁フル commit SHA>",
    "tag": "v0.1.1-win64-743c88d",
    "mediaPolicy": "bundled | free",
    "createdAt": "2026-11-06T15:39:27Z"
  }
  ```

- UpdateChecker は実行中ビルドの版識別子 (`Application.version` = `bundleVersion` = `<version>+<shortsha>`) を読み、
  GitHub Releases の候補タグと比較して新版を判定する (`version.json` は人が読むための同梱メタデータで、判定には使わない)。

### .sha256 sidecar 形式

- `<64 桁小文字 hex>  <ファイル名>` + LF、UTF-8 (BOM 無し)。`sha256sum -c` / `Get-FileHash` 両対応。

### 候補の判定 (実装に合わせる)

- `GET https://api.github.com/repos/rebuildup/FlashBeat/releases?per_page=30` (認証不要) を使う。
  GitHub の返却順は新しい順とは限らない (実測) ため、`/releases/latest` の 1 件には依存しない。
- 候補の採用条件: `draft = false` かつ `tag_name` が `^v\d+\.\d+\.\d+-win64-[0-9a-f]{7,40}$` に一致し、
  対応 ZIP + `.sha256` の 2 アセットが存在すること。**prerelease はチャンネルで決まります** (stable は対象外、preview は対象)。
- 候補の順序付けは **version を主キー・published_at を副キーに最大**。
- **自動更新の対象は「バージョンが厳密に大きい」Release のみ** (ダウングレードもしない)。
  実装側はこの方針に揃える前提で、次の「版数ルール」を運用するときの基準にしてください。
- 不一致・取得失敗・SHA 検証失敗時は更新を中止し現行版を残す (ゲーム動作は止めない)。

### 版数ルール (バージョンを上げてリリースする)

**同じバージョンで別の SHA の Release を作らない。** 必ず semver を上げてから Release します。

- 理由: 自動更新の候補判定はバージョンを主キーにします。**同じバージョンで複数の SHA の Release が
  公開されると、「どの SHA が新しい版か」があいまい**になり、展示 PC が更新対象を確定できなくなります
  (公開順ではなくバージョンを比較する以上、同一バージョンの 2 候補は区別できません)。
- 運用ルール:
  - 修正版を出すたびに **patch を上げて** Release します (`0.1.0` → `0.1.1` → `0.1.2`、機能追加は `0.2.0`)。
  - ビルド時は `FLASHBEAT_VERSION_BASE` で基準バージョンを指定します
    (`Assets/Editor/BuildScript.cs` が `bundleVersion` = `<version>+<shortsha>` に埋め込みます)。
    CI からは `.github/workflows/build-win64.yml` の `version_base` 入力で渡します
    (空欄だと既定の `0.1.0` が使われます)。
  - 已在の Release は**差し替えず**、archive に残します（検証済みの成果物を保持するため）。
- 結果: 展示 PC の更新対象は「バージョンが大きい Release」に一意に決まります。
  同一バージョンの候補が 2 つ公開される運用を禁じているのは、この判定を壊さないためです。

### メディア排除 (第三者楽曲)

- **配布 ZIP に第三者楽曲の音声・動画を含めない** (オーナー要件 2026-11-07)。
- ルーズなメディア (`*.wav/*.mp3/*.mp4/...`) がビルド出力にあればスクリプトが検知して失敗する。
- `Assets/Game/Resources/**` のメディアは Unity が `resources.resource` に焼き込むため ZIP レベルでは除去不能。
  そこでビルド (`BuildScript.ExcludeMediaFromBuild`) が `Resources/Musics` と `Resources/Videos/UnityYoutube`
  (第三者楽曲の音声・動画) をビルド中だけ退避し、焼き込まないようにしている。
- **不含の証明**: ビルドは `flashbeat-media-policy.json` (除外したパス) を配布物に出力する。
  `-MediaPolicy song-free` はこの manifest を確認し、第三者楽曲メディアが外されていないと失敗する。
  さらに `resources.resource` のサイズ上限 (既定 300MB) でも検出する (漏れていれば 1.4GB になる)。
- ゲーム自身の背景アニメーション (`Resources/Videos/*.mp4`、約 196MB) は自作素材なので同梱する
  (media-rights-audit 3: 権利ではなくサイズの問題)。

### 更新フローの契約 (実装は #36)

1. 起動時に latest を非同期確認 (`version.json` の `tag` と比較)
2. 新版があれば ZIP + `.sha256` を `%LOCALAPPDATA%\FlashBeat\update\staging` へ取得し SHA-256 検証
3. アプリ終了時に `tools\update\Update.ps1` を分離プロセスで起動:
   アプリ PID の終了待機 → 別ディレクトリへ展開 → 検証 → 一括切替 → 新 exe 再起動
4. 旧版は 1 世代残す (切戻し用。ユーザー向け UI は不要)
5. 失敗時 (SHA 不一致 / ネットワーク断 / 二重起動) は更新を中止し現行版を維持

## 取得先

- GitHub Releases: `https://github.com/rebuildup/FlashBeat/releases`
- 現行版: [`v0.1.0-win64-743c88d`](https://github.com/rebuildup/FlashBeat/releases/tag/v0.1.0-win64-743c88d)
  - `FlashBeat-0.1.0-win64-743c88d.zip`
  - SHA-256: `3dbdfecbdb91d916178666045d0c62e1fae717f7df0b77e4f9ddc2af1c036a45`
  - ビルド元 commit: `743c88d14a1931d4637c34b1ec5d1bec5e502d44` (#27 フォント修正済み)
  - **注記**: このリリースは第三者楽曲を `resources.resource` に焼き込みで含む (`version.json` 無し・旧仕様)。
    扱い (公開維持/差し替え/非公開化) はオーナー決定事項 — ワーカーは触らない。
- アセット命名: `FlashBeat-<version>.zip` + `FlashBeat-<version>.zip.sha256`
  - 例: `FlashBeat-0.1.0-win64-743c88d.zip` (version は `<semver>-win64-<build commit SHA 短縮形>`)
- 展示運営者はブラウザでダウンロードするだけ。GH/CLI/PowerShell・管理者権限はプレイヤーに要求しない。

## ZIP の中身

Unity Win64 ビルド出力フォルダ**一式** (exe 単体ではない) + `version.json`:

- `FlashBeat.exe` / `UnityPlayer.dll` / `UnityCrashHandler64.exe`
- `FlashBeat_Data/` (Managed DLL 群・リソース一式)
- `MonoBleedingEdge/`, `D3D12/`, `DirectML.dll`, `dstorage*.dll`
- `version.json` (配布仕様参照)
- 除外: `FlashBeat_BurstDebugInformation_DoNotShip/` (Unity の指示により ship 対象外)、ルーズなメディアファイル (第三者楽曲)

## SHA-256 検証手順

```powershell
# ZIP と .sha256 を同じフォルダに置いてから
Get-Content .\FlashBeat-<version>.zip.sha256   # 例: "abc123...  FlashBeat-0.1.0-win64-743c88d.zip"
Get-FileHash .\FlashBeat-<version>.zip -Algorithm SHA256
# 2 つのハッシュが一致すること。不一致ならダウンロード破損 → 再ダウンロード。
```

## パッチ版の発行手順

1. Unity 6000.5.6f1 で Win64 ビルド (`Build → Build Windows 64` または `BuildScript.BuildStandaloneWindows64`)。
   **このとき必ず `FLASHBEAT_VERSION_BASE` で「上の版数ルールに従った、より大きい基準バージョン」を
   指定します** (未指定なら `0.1.0` が使われます)。指定した値が `bundleVersion` = `<version>+<shortsha>` になります。
   展示用のビルドは**既定のまま (stable チャンネル)** で(Build: `FlashBeat_Data\StreamingAssets\update-channel.txt` = `stable`)。
   検証用の prerelease を取り込みたいときだけ環境変数 `FLASHBEAT_UPDATE_CHANNEL=preview` を付けてビルドする。
   展示用ビルドがテスト用 prerelease を自動適用しないことはチャンネルの責務であり、公開前に Player.log の
   `[UpdateChecker] 更新チャンネル: stable` を確認して証跡に残す。
2. 実機 smoke (起動→曲選択→プレイ→リザルト) を確認。**実機証拠がなければリリース完了としない** (Issue #22 がゲート)。
3. パッケージ化:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File tools/release/package-win64.ps1 `
       -SourceDir <Unity出力フォルダ> -OutDir <distフォルダ> `
       -Version <semver>-win64-<buildSHA> -CommitSha <build commit SHA (フル 40 桁)> `
       -MediaPolicy free
   ```

   スクリプトが ZIP + `.sha256` sidecar を作り、内容検査 (exe/Data/Managed DLL の存在・数, arch integrity) を自動実行する。
4. Release 作成 (オーナー承認済みなら `--draft` を外して即公開。未承認なら `--draft` で作り 5 へ。
   タグは実在しない新タグを使い、draft の間はタグが push されない):

   ```powershell
   gh release create v<version> --target <build commit SHA (フル SHA)> `
       --title "FlashBeat <version>" --latest `
       --notes "SHA-256: <hash>`nSource commit: <full SHA>`n実機検証: <証跡リンク or 未証明>"
   gh release upload v<version> FlashBeat-<version>.zip FlashBeat-<version>.zip.sha256
   ```

5. **公開 (publish) の権限境界**: 原則オーナー手動承認。オーナーが明示的に承認した場合のみ
   ワーカーが直接公開してよい (2026-11-06 の v0.1.0-win64-743c88d はオーナー指示により即時公開):

   ```powershell
   # オーナー承認後に実行
   gh release edit v<version> --draft=false
   ```

6. 不具合が出た場合は既存 Release を差し替えず、**修正版を新しいドラフト Release として発行**する。
   ただし **基準バージョンは必ず上げる** (`FLASHBEAT_VERSION_BASE=0.1.2` → タグ `v0.1.2-win64-<sha>`)。
   自動更新はバージョンの大小で判定するため、同じバージョンで別 SHA の Release を作ると
   「同じバージョンの別ビルド」として扱われ、自動更新が効かなくなる (運営者が手動適用する運用になる)。
   旧版は archive に残して検証済み成果物を保持する。

> ビルドを CI で作る場合: `.github/workflows/build-win64.yml` を手動起動 (または `main` への push) すると
> `windows-latest` ランナーで Win64 ビルドが走り、ZIP + `.sha256` が artifact (`flashbeat-win64-<sha>`、
> 保持 30 日) として出ます。**CI は Release を作りません** — 上記 3〜5 のパッケージングと Release 化の工程は
> 別途、監督者の承認の下で実行します。Unity ライセンスの secret が未設定だと CI ジョブは skip されます
> (skip は成功ではありません)。

## 更新の種別 (Stage 区別)

| 種別 | 対象 | 配布方法 |
|---|---|---|
| 本体更新 (S0/S1/S2/S3 の Unity 側変更) | exe / FlashBeat_Data | 本手順の ZIP + Release |
| コンテンツのみの更新 (楽曲カタログ・譜面) | Cloudflare 側 | Unity 本体 ZIP とは独立に即時反映 (ZIP 再発行不要) |

## 既存バイナリ互換

- パッチ ZIP はフォルダ全置換。レジストリ書き込み・常駐プロセス・既存の起動経路変更なし。
- 本体が依存する backend API **v1 の契約を壊す変更はリリースしない**。
  破壊的変更が必要な場合は v2 を追加し、v1 を deprecated 期間中維持してから削除する。
- 旧 Unity 本体が新 API を呼んでも壊れないこと (未知フィールド追加は可、必須フィールドの意味変更は不可)。
