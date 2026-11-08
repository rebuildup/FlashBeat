# Unity CI ライセンス設定 (GitHub Actions / Win64 ビルド)

対象: `.github/workflows/build-win64.yml` (GitHub-hosted `windows-latest` ランナー)

## 現状

- リポジトリに Unity 用の Actions Secret は **1件も設定されていない** (`gh secret list` が空)。
- そのため `build-win64` は毎回ライセンス確認で失敗する (Secret 未設定は blocker)。
- ワークフローは Secret 設定後に **再実行不会再に失敗しない** よう、2段構えにしている。
  - `license-precheck` ジョブ (ubuntu-latest, 数秒): Secret の有無だけを検査し、
    不足していれば **手順を書いた job summary** を出して即座に fail する (Unity を導入しないので速い)。
  - `build` ジョブ (windows-latest): ライセンス認証 → EditMode テスト → Win64 ビルド → ZIP + SHA-256。

## 必要な Secret (オーナー操作)

Settings → **Secrets and variables → Actions** → **New repository secret**

| Secret | 必須 | 内容 |
|---|---|---|
| `UNITY_LICENSE` | (A) or (B) のどちらか | Unity の `.ulf` ライセンスファイルの**中身 (XML)**。base64 でも可 (ワークフローは両方を受け付ける) |
| `UNITY_EMAIL` | (A) or (B) のどちらか | Unity アカウントのメール |
| `UNITY_PASSWORD` | (A) or (B) のどちらか | Unity アカウントのパスワード |
| `UNITY_SERIAL` | 有償ライセンスのみ | `XX-XXXX-XXXX-XXXX-XXXX-XXXX` 形式 |

### (A) Unity Personal (推奨・無料)

1. Unity Hub → 対象アカウントでログイン → 「Preferences → Licenses → Add → Get a free personal license」。
2. 発行された `.ulf` を開く:
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
3. **ファイルの中身をそのまま** `UNITY_LICENSE` に貼り付ける (そのままだと XML になる)。

ワークフローはこの `.ulf` を `Unity.exe -batchmode -quit -manualLicenseFile <ulf>` に渡して
認証する。Personal ライセンスは `UNITY_EMAIL` / `UNITY_PASSWORD` が無くても有効。

### (B) Unity Plus / Pro (有償)

`UNITY_SERIAL` + `UNITY_EMAIL` + `UNITY_PASSWORD` を登録する。
ワークフローは `Unity.exe -batchmode -quit -username <email> -password <password> [-serial <serial>]`
で認証する。

> 資格情報 (ライセンスファイル・パスワード・シリアル) の内容を、チャット / Issue / PR /
> ログ / ソースコードに **never** 出力しない。Secret としてのみ登録する。

## 設定後に実行すること

```bash
# 手動実行 (検証用 candidate。stable Release は自動公開しない)
gh workflow run build-win64.yml \
  -f version_base=0.1.0 \
  -f update_channel=preview \
  -f run_tests=true
```

- `update_channel=preview` を指定する (`stable` は配布用。CI から自動公開はしない)。
- `run_tests=true` の場合、ビルド前に **EditMode テストが実実行**され、
  `TestResults.xml` が生成されたことを確認してからビルドに進む
  (`TestResults.xml` が無ければテスト未実行として失敗させる)。
- 成果物は `flashbeat-win64-<sha>` Artifact (ZIP + `.sha256`) としてアップロードされる。
- 成功 = **ビルド成功** だけ意味し、実機プレイ成功とは別ゲート。

## 実装メモ

- `game-ci/unity-activate` は **Docker 前提** の action なので、GitHub-hosted の
  `windows-latest` ランナーではそのまま使えない。代わりに Unity 公式のライセンス引数を
  直接呼ぶ方式にした (`-manualLicenseFile` / `-username -password [-serial]`)。
- 展示用 Windows PC は CI の self-hosted runner として **使わない** (展示と干渉するため)。

## 関連

- `.github/workflows/build-win64.yml`
- `docs/operations/release-approval-checklist.md`
- `docs/operations/update-release.md`
