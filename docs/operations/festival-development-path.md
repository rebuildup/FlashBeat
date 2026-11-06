# 展示中の継続開発経路（開発者・リリース担当向け）

対象 Issue: **#41**（展示成立と継続開発）の残りタスク「展示中の継続開発経路」。
読み手: 展示を止めずに開発とパッチ配布を続けたい **開発者・リリース担当**。

運営者向けの操作は [`exhibit-runbook.md`](exhibit-runbook.md) に分けてあります。
こちらは「誰が何をするか」「展示 PC を壊さずにどう進めるか」です。

---

## 0. 要点

- **展示を止めずに進められるのは、譜面・カタログ・曲メタ（= Web/API 側）だけ**です。
  Unity の再ビルドが要るのは**コード修正**だけで、そのビルドは **展示 PC と別の GitHub Actions ランナー**で行います。
- **展示 PC（Windows 実機）は現状 1 台しかなく、その上に Unity Editor が入っています。**
  展示中は新規作業に使いません（Issue #41 の決定: 「明日から Windows 実機は展示専用」）。
- **CI は Unity ライセンスの secret が未設定ならジョブ全体が skip されます。**
  この 2 点は隠さず前提として運用に載せます（§2）。

---

## 1. 役割分担

| 担い手 | 担当 | Unity の再ビルド | 展示への影響 |
|---|---|---|---|
| **Mac（開発者）** | Web / API / 譜面 / カタログ / ドキュメント。`web/` の実装、`void deploy` による本番反映、publish | **不要** | **なし**（反映は即時） |
| **Windows 実機（展示 PC）** | 展示そのもの + 実機検証（起動・プレイ・ログ確認）。**展示中は新建作業に使わない** | 展示時間外なら可 | 展示中は触らない |
| **GitHub Actions** | 展示 PC と独立した Win64 ビルド環境（`.github/workflows/build-win64.yml`）。`windows-latest` ランナーで Unity 6000.5.6f1 を入れてビルドし、ZIP + `.sha256` を artifact として出す | 必要（代わりにこれを使う） | **なし**（別マシンで動く） |

- リリース（GitHub Release の作成・公開）は**監督者の承認が前提**です
  （[`update-release.md` §パッチ版の発行手順](update-release.md)）。
  CI は **Release を作りません**。成果物を **artifact として出すだけ**です。
  Release 化（タグ付け + ZIP/`.sha256` アップロード + 公開）は別途、監督者の承認の下で手順を踏みます。

---

## 2. 現実的な前提（隠さない）

1. **Windows 実機は 1 台だけ**で、その一台に Unity Editor が入っています。
   展示中に Editor を起動すると展示の挙動に影響し得ます（入力・CPU・画面）。
   よって **展示 PC での新規ビルドは展示時間外**に限定します。
2. **CI は展示 PC とは別のマシン（GitHub の `windows-latest` ランナー）でビルドします。**
   したがって CI が動いても展示 PC の Unity Editor とは競合しません
   （展示 PC 側はビルドしません）。
3. **CI は Unity ライセンスの secret を要求し、未設定ならジョブは skip されます。**
   `.github/workflows/build-win64.yml` の「Unity ライセンスの secret を確認」ステップが
   `UNITY_LICENSE`（base64 化した `.ulf`）または `UNITY_EMAIL` + `UNITY_PASSWORD`
   の有無を確認し、どちらも無ければ **skip=true** にして以降のステップを全部飛ばします
   （通知として `::notice::` が出ます）。
   - secret は **Infisical の FlashBeat プロジェクト**に置き、必要な secret 名だけを
     GitHub Actions の secret として登録します（値はリポジトリ・ログ・この文書に出しません）。
   - **skip は「ビルド成功」ではありません。** 「ビルドが成功した証拠」として扱わないでください。
4. **CI は Release を作りません**（§1）。artifact（`flashbeat-win64-<sha>`、保持 30 日）が出ます。
   Release 化は別工程です。
5. **Web/API 側に秘密値は要りません。** publish 用トークンはブラウザのメモリ内で扱い、
   サーバー側は Infisical から注入した secret を見ます（[`web/AGENTS.md`](../../web/AGENTS.md)）。

---

## 3. 展示中に反映できるもの / 再ビルドが要るもの

| 対象 | 反映手段 | Unity 再ビルド | 体感の反映 |
|---|---|---|---|
| 譜面（chart JSON） | Web Studio → publish → D1 → API | **不要** | 選曲画面（`refreshSeconds` 秒ごと再取得） |
| カタログ（曲の一覧・曲メタ） | 同上（publish で，曲のメタデータも同時に公開） | **不要** | 選曲画面 |
| Web Studio の UI・編集機能 | `void deploy --platform cloudflare` で本番へ | **不要** | ブラウザのみ |
| API の仕様 v1 の範囲での拡張 | 同上（契約は凍結。破壊的変更はしない） | **不要** | ブラウザ・Unity とも |
| **Unity 側のコード修正**（判定・描画・UI・再生まわり） | **CI でビルド → Release ZIP → 運営者が適用** | **必要** | 運営者が適用して再起動したあと |
| Unity のフォント SDF 再生成 | 同上（Editor 作業が必要なので展示時間外 + CI or 実機） | **必要** | 同上 |

- 「再ビルド不要」の根拠: Unity は `Assets/Game/Resources/CloudCatalogConfig.json` の `baseUrl` から
  API を参照し、**選曲画面**でカタログを取り、以後 `refreshSeconds` 秒ごとに取り直します
  （[`CloudCatalog.cs`](../../Assets/Game/Scripts/Songs/CloudCatalog.cs) /
  [`SelectSceneManager.cs`](../../Assets/Game/Scripts/UI/SelectSceneManager.cs)）。

---

## 4. コンテンツ更新（譜面・カタログ）の手順 — Mac だけ、Unity 再ビルド不要

1. **開発 Mac で `web/` を直します**（譜面 JSON の編集・曲メタの入力は Web Studio `/chart` と `/publish`）。
   ローカル確認:

   ```sh
   cd web
   vp install
   vp check          # format + lint
   npm test          # read API 統合テスト (node --test) + vitest
   ```

2. **本番へデプロイします**（API 側の変更がある場合。譜面の publish だけならデプロイは不要です）:

   ```sh
   mise install
   ./script/with-secrets bash -c 'mise exec -- void deploy --platform cloudflare'
   ```

   D1 の migration が必要な場合は先に適用します（**リモート D1 を reset してはいけません**
   — 後続 migration で ship する）:

   ```sh
   ./script/with-secrets bash -c 'mise exec -- void db migrate --remote'
   # または: mise exec -- cf d1 migrations apply <database-id> --dir web/db/migrations
   ```

3. **譜面と曲メタを公開します**。Web Studio の `/publish` 画面から publish します
   （staff トークンは運用者が入力し、ブラウザのメモリにだけ置かれます）。
   API へ直接 `curl` で叩く運用はしません。

4. **Unity が拾います**。公開後、次の選曲画面でカタログが取り直されます
   （`refreshSeconds` 秒ごと）。**Unity の再ビルドも展示 PC への再配置も不要**です。
   公開結果の確認は API 側で行います（`GET /api/v1/catalog` / `GET /api/v1/charts/{songId}?version=N`）。

5. **展示中に反映してよい範囲**: 曲の選択可否・曲名・アーティスト・譜面・歌詞。
   判定・再生・描画の挙動は Unity 側なので、ここは触れません。

> 注意: publish すると `chartVersion` は +1 されます。同じ曲を再公開すると新しい版ができるので、
> 展示中に同じ曲を二度書き換えない運用にします（版の混在を避ける）。

---

## 5. コード修正の手順 — CI でビルド → Release → 運営者が適用

1. **修正はブランチ + Draft PR で行います**（リポジトリ設定は merge commit のみ）。
   Unity シーン / プレハブの編集者は 1 プロセスだけ（Windows 実機では展示時間外）。

2. **CI ワークフローを手動起動します**（Actions の `build-win64` → `Run workflow`）:

   | 入力 | 値 |
   |---|---|
   | `version_base` | **必ず現在の展示版より大きい版数**（例: 現在の展示版が `0.1.0` なら `0.1.1`）。空欄だと既定の `0.1.0` が使われます |
   | `update_channel` | 展示用は `stable`。検証用の prerelease を取り込みたいときだけ `preview` |

   - ワークフローは `main` への push でも起動します（`Assets/**` / `Packages/**` /
     `ProjectSettings/**` / ワークフロー自身の変更時）。
   - **Unity ライセンスの secret が未設定なら skip されます**（§2.3）。skip 時のログに
     `::notice::` が出るので、成果物が無い理由をここに確認します。

3. **成果物（artifact）を取り出します**: `flashbeat-win64-<sha>`（保持 30 日）。
   中身は `tools/release/package-win64.ps1` が出した
   **ZIP（Unity 出力フォルダ一式 + `version.json`）と `.sha256` sidecar** です。
   CI 側は `-MediaPolicy free` でパッケージします（第三者楽曲を同梱しません）。

4. **Release を作ります**（承認済みなら公開、未承認なら draft）。手順は
   [`update-release.md` §パッチ版の発行手順](update-release.md) に従います。

   ```powershell
   gh release create v<semver>-win64-<shortsha> --target <build commit SHA (フル)> `
       --title "FlashBeat <semver>" --latest `
       --notes "SHA-256: <hash>`nSource commit: <full SHA>`n実機検証: <証跡リンク or 未証明>"
   gh release upload v<semver>-win64-<shortsha> FlashBeat-<semver>-win64-<shortsha>.zip FlashBeat-<semver>-win64-<shortsha>.zip.sha256
   # 公開は監督者の承認後: gh release edit v<semver>-win64-<shortsha> --draft=false
   ```

   - **版数ルール**: 同じバージョンで別の SHA の Release を作ると自動更新の判定があいまいになるため、
     **必ず版数を上げて** Release します（詳細は [`update-release.md`](update-release.md)）。

5. **運営者が展示 PC へ適用します**（[`exhibit-runbook.md` §6](exhibit-runbook.md)）。
   運営者への連絡は 1 文で足ります:

   > 「展示中のゲームを終了 → パッチを 1 回だけ適用 → `FlashBeat.exe` を直接起動してください。」

6. **実機確認が要ります**。Release 完了のゲートは「実機でのプレイ証拠」です
   （[`release-approval-checklist.md`](release-approval-checklist.md)）。
   展示を止められない時間帯は、展示時間外（閉店後）に実機検証します。

### CI が使えないとき（fallback）

- CI が skip される（ライセンス未設定）か失敗した場合は、**展示時間外の Windows 実機**で
  Unity 6000.5.6f1 から Win64 ビルドし、同じパッケージング手順を通します。
- 展示時間中にビルドが要る緊急修正は、原則「展示を止めてやる」のではなく、
  **展示閉店後の時間帯に実施**します。

---

## 6. 展示 PC を中断させない運用ルール

| # | ルール | 理由 |
|---|---|---|
| 1 | **展示フォルダと作業フォルダを分けます**。展示物は `C:\Users\rebui\Desktop\FlashBeat-Exhibit\`（Release の ZIP を展開したもの）、作業物（Unity の出力・Evidence・ZIP）は別の場所に置きます | 展示物の誤削除・誤上書きを防ぐ。更新はフォルダ丸ごと差し替えになるため、展示フォルダに手置きしたファイルは消える |
| 2 | **展示中に Unity Editor を起動しません**（展示時間外 または 別のセッション） | Editor は重い・入力を使う。展示中の挙動を安定させたい |
| 3 | **展示 PC ではビルドしません**。ビルドは CI、緊急時のみ展示時間外の実機 | ビルドは CPU とディスク I/O を食う。展示を中断させず、Editor と実機の同時起動も避ける |
| 4 | **展示 PC の入力を奪う自動操作（ゲームドライバ）を展示中は実行しません**。`release/check/drive-gameplay.ps1` は他の `FlashBeat.exe` 実行を拒否しますが、**展示時間外**に実行します | キーボードのフォーカスを奪い、訪問者の操作を邪魔する |
| 5 | **診断は読み取り専用**。ログを書くなら展示フォルダではなく別の場所（`-logFile` で指定）に書きます | 展示フォルダは更新で丸ごと差し替えされる |
| 6 | **公開（publish）は展示中でも可能**ですが、公開内容の差し替えは一度きりで決めます | 同じ曲を二度書き換えると `chartVersion` が進み、展示中の版が混在する |
| 7 | **展示中に Release を公開しない**（原則）。公開の判断は監督者の承認済みの段階で行います | 公開直後に展示 PC が自動更新で入ると、検証していない版が展示に出る |
| 8 | **秘密値を展示 PC に置きません**。publish トークンは Mac 側のブラウザで入力し、その値は出力しません | 展示 PC は展示用・無防備 |

---

## 7. 現時点の制約（未確定を含む）

| # | 制約 | 影響 | 対処 |
|---|---|---|---|
| 1 | **Windows 実機は 1 台**（Unity Editor を含む） | 展示と新規ビルドを同時にできない | CI を使う。fallback は展示時間外 |
| 2 | **CI の Unity ライセンス secret が未整備の可能性**（未設定なら skip） | CI でビルドできない場合がある | Infisical から該当 secret を設定してから GitHub Actions の secret に登録する。skip を「成功」に見立てない |
| 3 | **CI は Release を作らない**（artifact のみ） | Release 化の工程は手動 | §5.4 の手順を承認プロセスに載せる |
| 4 | **自動更新の候補判定の実装を「バージョンが厳密に大きい Release のみ」に揃える対応は監督側の担当** | 同じバージョンの Release を作ると更新対象にならない | 版数を上げる運用ルール（§5.4） |
| 5 | **展示 PC の最終的な配置パス**が旧記録（`D:\4_apps\Unity\Build\FlashBeatExhibit*`）と新記録（`C:\Users\rebui\Desktop\FlashBeat-Exhibit\`）で食い違う | 運営者の手順がずれる | 監督者の決定事項。確定後は [`exhibit-runbook.md`](exhibit-runbook.md) §1 を更新する |
| 6 | **Web Studio の publish はブラウザ操作**（トークン手入力） | 无人化できない | 展示運営者ではなく開発者が担当する |
| 7 | **API v1 は凍結** | 破壊的変更は既存バイナリを壊す | 破壊的変更は v2 として追加（[`update-release.md` §既存バイナリ互換](update-release.md)） |

---

## 8. 関連

- 運営者向け: [`exhibit-runbook.md`](exhibit-runbook.md)
- 更新の仕組み・`Update.ps1`: [`update-patch.md`](update-patch.md)
- Release の配布仕様・版数ルール: [`update-release.md`](update-release.md)
- リリース承認ゲート: [`release-approval-checklist.md`](release-approval-checklist.md)
- 旧世代の展示記録: [`festival-runbook.md`](festival-runbook.md)
- Cloudflare / Infisical: [`cloudflare-bootstrap.md`](cloudflare-bootstrap.md)
- D1 スキーマと migration: [`d1-migrations.md`](d1-migrations.md)
- Web Studio の構成とコマンド: [`web/AGENTS.md`](../../web/AGENTS.md)
