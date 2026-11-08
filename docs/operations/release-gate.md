# Release ゲート — 公開ルールと更新適用の契約

対象 Issue: **#41** (展示成立・継続開発) / #38 (パッケージング) / #39 (リリースゲート) / #40 (更新機構)

この文書は **「何を公開してよいか」「展示 PC でどう適用するか」** の契約です。
判断できないものは **未証明** と書き、green を作りません。
作業の分担: パッケージングと Release 公開 = release worker、検証ドライバと証跡 = Windows ワーカー、
承認 = 監督 / オーナー。

---

## 1. Release 公開のルール

### 1.1 タグ命名規則

```
v<version>-win64-<sha>
```

- `<version>` = `X.Y.Z` (semver)。`<sha>` = ビルド元 commit の短縮 SHA (7〜40 hex)。
- 例: `v0.1.2-win64-4b0685d`
- 実体は `Assets/Game/Scripts/Update/UpdateVersion.cs` の `ParseTag` が読む形式
  (`-win64-` 区切り・version は `X.Y.Z`・SHA は hex)。**この形式以外**のタグは
  更新候補から除外されます (`ParseTag` が invalid を返す)。

### 1.2 必ずバージョンを上げる

自動更新は **バージョンが真に大きい Release だけ** を適用します。
**同一バージョンで異なる SHA の Release は「更新」ではありません。**

- 判定: `UpdateVersion.IsNewerThan` = `CompareVersions(candidate, current) > 0`（`UpdateVersion.cs:66-70`）
- 同一バージョン・別 SHA は `UpdateChecker.cs:120-128` で明示的に中断し、
  「自動更新はしません（適用は運営者が `Update.ps1 -Tag` で行えます）」とログを出します。
  理由は SHA に大小関係がなく「新旧」を判定できないため（古いビルドへ戻すのを防ぐ）。
- したがって **修正版を出すたびに `FLASHBEAT_VERSION_BASE` を上げて** ビルドし直します
  （0.1.1 → 0.1.2 → 0.1.3）。同じバージョンで別 SHA の Release を公開すると、
  自動更新の経路が Dead になり、手動適用運用になります。

### 1.3 更新チャンネル

| チャンネル | 対象 | prerelease |
|---|---|---|
| `stable` | 展示用ビルド (既定) | **自動適用しない** |
| `preview` | 開発・検証用ビルド | 対象 |

- チャンネルはビルド時に `FlashBeat_Data/StreamingAssets/update-channel.txt` へ
  書き出されます (`BuildScript.WriteUpdateChannelFile`)。
  `stable` / `preview` の 2 値だけ。**stable ビルドがテスト用 prerelease を
  自動適用しないことが保証されるのはこの境界**です。
- 実行時: `UpdateChannel.ReadInstalledChannel()` がファイルを読み、
  ファイルが無い / 読めない / 未知の値なら **safe 側 (`stable`)** に倒します。
- 確認方法: 起動時の `Player.log` に
  `[UpdateChecker] 更新チャンネル: stable (prerelease を更新候補に含める: no)` が出ます。
- `preview` ビルドは環境変数 `FLASHBEAT_UPDATE_CHANNEL=preview` を付けてビルドします。

### 1.4 添付アセット (必須)

| アセット | 内容 |
|---|---|
| `FlashBeat-<version>-win64-<sha>.zip` | ビルド出力フォルダ一式 + `version.json` |
| `FlashBeat-<version>-win64-<sha>.zip.sha256` | `<64 桁小文字 hex>  <ファイル名>` (LF / BOM 無し) |

- **ZIP と SHA-256 は必ず両方**。片方だけの Release は公開しません。
- 公開前に **実際に ZIP を取得して** SHA-256 を照合します
  (sidecar と一致しない ZIP は公開しない)。
- Release は `draft=false` / `prerelease=false` (= **stable**) で公開します。
  検証用の Release は `prerelease=true` として公開し、展示用 stable ビルドの
  自動更新候補から外します。

### 1.5 第三者楽曲のメディアを同梱しない

- 配布 ZIP に第三者楽曲の **動画・音声** を含めてはいけません
  (`Assets/Game/Resources/Musics`, `Assets/Game/Resources/Videos/UnityYoutube`)。
- 実装: ビルド (`BuildScript.ExcludeMediaFromBuild`) が該当フォルダをビルド中だけ退避し、
  配布物には `flashbeat-media-policy.json` (除外パスの記録) を同梱します。
- パッケージング (`tools/release/package-win64.ps1`) は `-MediaPolicy song-free` で
  (a) manifest に `Assets/Game/Resources/Musics` と
  `Assets/Game/Resources/Videos/UnityYoutube` が含まれること
  (b) `resources.resource` が `MaxResourcesBytes` (既定 300MB) 以下のこと
  を検査し、違反すれば失敗します。
- 自作の背景アニメーション (`Resources/Videos/*.mp4`、約 196MB) は同梱して構いません
  (media-rights-audit 3: 権利ではなくサイズの問題)。
- 検品: 公開前に ZIP 内の `*.wav/*.mp3/*.mp4/*.mov/*.m4a/*.webm/*.ogg` エントリが 0 件であること。

### 1.6 旧 Release の扱い

- **第三者楽曲を同梱した past Release は、展示配布の対象から外します。**
  該当: `v0.1.0-win64-743c88d` (1,459,717,135 bytes / `mediaPolicy` 記載なし = 同梱)。
- 降格 (prerelease 化) か削除かは **オーナーの判断** に委ねます
  (参考 Issue: [#77](https://github.com/rebuildup/FlashBeat/issues/77))。
  ワーカーは勝手に削除も降格もしません。
- 判断が下りるまでは、その Release を「展示で配るもの」として案内しません。
- 新しい Release を公開しても **古い Release を差し替えません**（proven artifact を保持）。

### 1.7 CI (GitHub Actions) — ライセンス未設定は失敗、緑は成果物の証拠

- `.github/workflows/build-win64.yml` は Unity ライセンスの secret が無いとき
  ビルドを **行わず `exit 1` で失敗** します (#76 対応済み:
  `::error title=Unity license required::...` と `GITHUB_STEP_SUMMARY` への
  「Win64 build blocked」明記、ジョブは red)。
- したがって **「Actions が緑」は ZIP + SHA-256 が生成された証拠** です。
  それでも最終確認として **アーティファクトの有無** を確認します
  (パッケージ step が ZIP 1 件 + `.sha256` を検証し、欠けていれば throw)。
- ライセンス secret (`UNITY_LICENSE` の base64 .ulf か `UNITY_EMAIL` + `UNITY_PASSWORD`)
  が無い限り、**CI での Win64 ビルドは成立しません**。公開済み Release は
  Windows 実機ビルド (Unity 所有者的) で作成しています。

---

## 2. 展示 PC への適用手順

展示 PC のゲームは **常設ランチナー無し**。運営者は `FlashBeat.exe` を直接起動するだけです。

1. **適用前**: ゲームを終了しておく (更新は「終了時」に走るため)。
2. **取得**: GitHub Releases から対象 Release の ZIP と `.sha256` を取得する。
3. **検証**: `Get-FileHash .\FlashBeat-<version>-win64-<sha>.zip -Algorithm SHA256` が
   `.sha256` の値と一致すること。不一致なら再ダウンロード (公開しない)。
4. **配置**:
   - 手動: 既存フォルダを `FlashBeat-backup-<stamp>` へ退避 → ZIP をその場に展開。
   - スクリプト: 配布物内の `tools\update\Update.ps1 -Tag <tag>`
     (SHA-256 検証 → 別ディレクトリ展開 → 一括切替 → 退避 1 世代保持 → 新 exe 再起動)。
5. **起動**: `FlashBeat.exe` を直接起動する。
6. **自動更新 (反映済みビルドの場合)**: 起動時に裏で Releases を確認し、
   **バージョンが真に大きい** Release があれば ZIP を staging して SHA-256 を検証し、
   **終了時に** `Update.ps1 -Apply -WaitPid -Relaunch` で展開・切替・再起動します。
   つまり「アプリを終了して再起動すると更新済み」になります。
7. **失敗時**: SHA 不一致 / ネットワーク失敗 / 二重起動では既存配置を変更せず、
   ゲームは止まりません。前の世代は `FlashBeat-backup-*` に 1 世代残ります。

運用禁止: `Play.cmd` / `Update.cmd` / `Rollback.cmd`、常設ランチナー、
ユーザー向けロールバック UI・コマンド、独自更新サーバー。

---

## 3. 公開直後の監査チェックリスト（監査担当が使う）

| # | 確認 | 合格の根拠 |
|---|---|---|
| A1 | ZIP に第三者楽曲メディアが無い | ZIP 内のメディア拡張子エントリ **0 件**、`flashbeat-media-policy.json` の除外パス、resources.resource が上限以下 |
| A2 | SHA-256 が実物と一致 | 公開 ZIP を取得し `.sha256` と照合 |
| A3 | ZIP + SHA-256 の両方が添付 | Release の assets に 2 件 |
| A4 | タグが `v<version>-win64-<sha>` 形式 | `UpdateVersion.ParseTag` が invalid にならない |
| A5 | バージョンが前回より真に大きい | `CompareVersions` が > 0。**同一バージョン・別 SHA は自動更新されない** (1.2) |
| A6 | stable / prerelease の区別 | 展示用は `prerelease=false`、テスト用は `prerelease=true` |
| A7 | 更新チャンネル | 配布物の `update-channel.txt` = `stable` (展示用) |
| A8 | 秘密情報混入なし | ZIP 内にトークン / 署名付き URL / video id が無い (`release/check/redact-log.ps1 -Check`) |

判定: 1 項目でも確認できないものは **未証明**。緑を作りません。

---

## 4. 関連文書

- パッケージング手順: [`update-release.md`](update-release.md)
- 更新の運用 (チャンネル・手動適用・復旧): [`update-patch.md`](update-patch.md)
- 承認チェックリスト (F: 承認記録): [`release-approval-checklist.md`](release-approval-checklist.md)
- 展示 runbook: `festival-runbook.md` / `exhibit-runbook.md` (PR #51 側)
- 成果物インデックス: `release/check/stage-manifest.json`
- リリース記録例: `release-0-1-2-record.md`
