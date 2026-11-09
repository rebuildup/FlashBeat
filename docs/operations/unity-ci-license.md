# Unity CI ライセンス設定 (GitHub Actions / Win64 ビルド)

対象: `.github/workflows/build-win64.yml` (GitHub-hosted `windows-latest` ランナー)

## 現状 (2026-11-08 監査結果)

- 2026-11-08 時点の監査では、登録されている Unity 用 Secret は **`UNITY_LICENSE` のみ**で、
  `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL` は **未登録**だった。
  ただしこの状態は **登録直後に変わる** ので、Secret の有無永远是固定された前提で
  書いてはいけない。以降の挙動は「登録されているか」に応じて変わる。
- **Secret が未設定の場合** (アカウント認証も `.ulf` も無い):
  `license-precheck` ジョブは **期待どおりに blocker として fail する**
  (Unity を 1 度も起動する前に落ちる)。CI が緑になることは期待しない。
- **Secret が設定済みで `UNITY_EMAIL` / `UNITY_PASSWORD` が揃っている場合**:
  `license-precheck` は成功し、`build` ジョブは **アカウント認証**でライセンスを
  取得する (この 2 つが揃えば `UNITY_LICENSE` は不要)。CI が緑になる。
- **Secret が設定済みでも `UNITY_EMAIL` / `UNITY_PASSWORD` が未登録で `.ulf` だけの場合**:
  `license-precheck` は **機械紐付け不一致** として fail する。理由:
  GitHub-hosted ランナーに `.ulf` (Unity 人工ライセンス XML) だけを渡す方式は
  **機械に紐づく** ため、毎回 次のようなエラーで失敗する:

  ```
  [Licensing::Client] Error: Code 400 : ... (status: Machine bindings don't match)
  ```

  → **Actions の secret には「ライセンスのあり/なし」しか持てない**。CI 側での回避は不可能。
- したがって **CI が緑になるのは、オーナーが `UNITY_EMAIL` / `UNITY_PASSWORD` を Secret 登録した後のみ**
  (この 2 つが揃えば `UNITY_LICENSE` は不要。ワークフローはアカウント認証を優先する)。
- **Secret の値 (ライセンスファイルの中身・パスワード・シリアル) は、このドキュメントにも
  CI ログにも一切記録しない**。名前と挙動だけを扱う。

### ライセンス失敗 と ソースビルド失敗 の区別 (重要)

CI が落ちたとき、**原因がライセンスなのかソースなのかを必ず切り分ける**。
`build-win64.yml` はこの切り分けを自動化した:

1. `license-precheck` が失敗 → **ライセンス未設定**。Unity は 1 度も起動していない。
   コードの問題ではない。
2. `license-precheck` が通り、`build` がライセンス認証で失敗 → **ライセンス問題**。
   ログに `Machine bindings don't match` があれば機械紐付け不一致、
   `ライセンスはファイルとして存在するが Unity が認識しません` を出していれば
   `.ulf` の形式か Secrets の中身が不正。
3. 認証後の **短時間のバッチ起動** が失敗 → ここまでがライセンス。以降の失敗は **ソースビルド** の問題。

各ケースの job summary 本文は `.github/workflows/summaries/` に置き、
ワークフローからは `cat` / `Get-Content` で出力する ( heredoc を `run: |` の中に置くと
YAML として壊れるため **使わない** )。

- ワークフローは Secret 設定後に **再実行不会再に失敗しない** よう、2段構えにしている。
  - `license-precheck` ジョブ (ubuntu-latest, 数秒): Secret の有無だけを検査し、
    不足していれば **手順を書いた job summary** を出して即座に fail する (Unity を導入しないので速い)。
  - `build` ジョブ (windows-latest): ライセンス認証 → EditMode テスト → Win64 ビルド → ZIP + SHA-256。

## 必要な Secret (オーナー操作)

Settings → **Secrets and variables → Actions** → **New repository secret**

| Secret | 必須 | 内容 |
|---|---|---|
| `UNITY_EMAIL` | 必須 | Unity アカウントのメール |
| `UNITY_PASSWORD` | 必須 | Unity アカウントのパスワード |
| `UNITY_SERIAL` | 有償ライセンスのみ | `XX-XXXX-XXXX-XXXX-XXXX-XXXX` 形式 |
| `UNITY_LICENSE` | 任意 (フォールバック) | Unity の `.ulf` ライセンスファイルの**中身 (XML)**。base64 でも可 (ワークフローは両方を受け付ける)。GitHub-hosted runner では後述の理由で期待どおりに働かない |

### (A) Unity アカウント認証 (推奨・主経路)

**GitHub-hosted ランナーではこれが唯一のサポートされた経路**。

1. `UNITY_EMAIL` / `UNITY_PASSWORD` を Actions Secrets に登録する
   (有償ライセンスなら `UNITY_SERIAL` も)。
2. ワークフローは
   `Unity.exe -batchmode -quit -username <email> -password <password> [-serial <serial>]`
   で認証する。ワークフローは **アカウント認証を優先** する (`.ulf` が同時に設定されていても)。

Personal (無料) でも Unity Hub でアカウントを作り、同じ 2 つの Secret を登録すればよい。

### (B) `.ulf` 手動発行 (フォールバック・GitHub-hosted では期待どおりに失敗する)

1. Unity Hub → 対象アカウントでログイン → 「Preferences → Licenses → Add → Get a free personal license」。
2. 発行された `.ulf` を開く:
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
3. **ファイルの中身をそのまま** `UNITY_LICENSE` に貼り付ける (そのままだと XML になる)。
   base64 化した内容を貼り付ける形式も、ワークフローは受け付ける
   (UTF-8 BOM 付きの XML も判定できるようにしている)。

ワークフローはこの `.ulf` を `Unity.exe -batchmode -quit -manualLicenseFile <ulf>` に渡して
認証するが、**この経路は次の理由で GitHub-hosted ランナーでは成立しない**:

- `.ulf` は **発行元マシンに紐づく**。GitHub-hosted ランナーは毎回別マシンなので
  機械結合 (machine binding) が一致せず、
  `[Licensing::Client] Error: Code 400 : ... (status: Machine bindings don't match)`
  で失敗する (実測 2026-11-08)。
- さらに Unity 6000.5 系では **Personal の手動 `.ulf` 激活はサポートされた認証経路ではない**。

ワークフローは (B) が渡された `.ulf` の内容を検査し、**Personal ライセンス
(`DeveloperData Value="Personal"` / `SerialMasked Value="XXXX..."`) なら
`-manualLicenseFile` に渡さずに、その場で理由と対処を明示して失敗する**
(推測による失敗にしないため)。Personal の `.ulf` を secret に登録した状態で
CI を走らせると、このエラーで即座に停止する。

したがって (B) は **自前の固定マシン / self-hosted runner で手元検証したい場合**の
フォールバックとしてのみ残す。GitHub-hosted CI で緑を期待してよい経路は **(A) のみ**。

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
  なお **`push` (main) トリガのビルドは `update_channel` 入力の値に関わらず常に `stable`** になる
  (展示用候補は preview にならない)。preview を選べるのは `workflow_dispatch` の手動実行のみ。
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
- `.github/workflows/build-macos.yml`
- `docs/operations/release-approval-checklist.md`
- `docs/operations/update-release.md`
