# 本番 API 受け入れ (issue #73 / #135) — 実施条件と手順

この文書は、Web Studio の公開・カタログ反映を**本番環境**
(`https://flashbeat.rebuildup.dev` / 本番 D1) で受け入れるための条件を定めます。

**現状: 未実施。** 隔離したローカル環境での実 PUT 往復・契約検証・読み戻し・
失敗時の復帰は実施済みで、記録は
[`docs/evidence/73-local-api-write-e2e-2026-11-10.md`](../evidence/73-local-api-write-e2e-2026-11-10.md)
にあります。本番への書き込みは、この文書の条件を満たし、**オーナーの明示的な承認を
得た場合にだけ**行います (勝手に本番データを書き換えない)。

---

## 1. なぜ本番確認が必要か

- #73 の T5「本番 Worker に対する実 PUT」と #135 の条件 6「ブラウザだけで登録が
  完結する」の受入は、本番の Worker / D1 / トークンでしか言えません。
- ローカル確認は「実装と契約が一致している」ことの証明であって、本番の
  デプロイ状態・シークレット・D1 マイグレーション適用状態の証明ではありません。

## 2. 変更するデータ (これ以外は変更しない)

| 項目 | 値 |
|---|---|
| 対象曲 ID | `e2e-publish-flow` (ブラウザ経路) / `e2e-acceptance-<YYYYMMDD>` (ハーネス経路) |
| 曲名 | `Who [e2e]` (ブラウザ経路) または既存曲のコピー + `[e2e #97]` マーカー (ハーネス経路) |
| 変更の種類 | **新規 1 曲の公開**と、その曲の `chartVersion` の更新 (1 → 2) |
| 既存曲 | **触れない。** `who` など本番カタログの曲の上書き・アーカイブは行わない |
| `catalogVersion` | 書き込みのたびに単調増加する (戻せない)。曲の集合は復旧で元に戻る |

D1 のテーブルを直接書き換える操作は行いません。すべて公開 HTTP API
(`PUT /api/v1/admin/songs/{songId}`, `POST /api/v1/admin/songs/{songId}`) 経由です。

## 3. 復旧方法

曲は**削除しない** (アーカイブが論理削除)。復旧は次の 1 リクエストです。

```sh
curl -sS -X POST "$BASE/api/v1/admin/songs/e2e-publish-flow" \
  -H "Authorization: Bearer $FLASHBEAT_ADMIN_TOKEN" \
  -H "content-type: application/json" \
  -d '{"archived": true}'
```

- 200 が返り、公開カタログ (`GET /api/v1/catalog`) から当該曲が消えることを確認する。
- スコアと曲の行は D1 に残る (staff 一覧 `GET /api/v1/admin/songs` で確認できる)。
- `catalogVersion` は復旧でも 1 進む。**公開カタログの中身**が元に戻ることが復旧の判定基準。
- 誤って既存曲を対象にしてしまった場合は、同じ `POST` に `{"archived": false}` を
  送って戻す。ただし本手順は既存曲を対象にしない (上の表)。

ブラウザ経路 (`web/tests/e2e/publish-flow.spec.ts`) は同じ復旧を `afterAll` で
自動実行します。失敗して中断した場合は、上の curl を手で 1 回実行してください
(冪等: すでにアーカイブ済みでも 200)。

## 4. 必要な権限

| 権限 | 用途 | 入手方法 |
|---|---|---|
| `FLASHBEAT_ADMIN_TOKEN` (staff トークン) | 公開 API の認証 | Infisical `secrets.rebuildup.dev` の FlashBeat プロジェクト (env `dev`) から `./script/with-secrets` で注入 |
| Cloudflare API トークン / D1 直接アクセス | **不要** | 使わない (D1 を直接触らない) |
| 本番 URL への到達性 | 読み取り | ブラウザ / curl |

トークンは環境変数からのみ渡し、**ログ・Issue・PR・この文書に値を書かない**。
`--token-env` の既定は `FLASHBEAT_ADMIN_TOKEN`。

## 5. 実施手順 (承認後)

前提: オーナーの承認、実施日時・実行者・対象曲 ID の記録、出力ログの保存
(トークンは写らない)。

### 5.1 ハーネス (API 往復 + 契約検証 + 読み戻し)

```sh
cd web
# 1) 専用曲を作る (1 回だけ)。ブラウザ経路 (5.2) で作ってもよい。
#    PUT /api/v1/admin/songs/e2e-acceptance-YYYYMMDD に最小の譜面を送る。
# 2) 実 PUT → 契約検証 → Catalog/Chart 読み戻し
./script/with-secrets -- sh -c 'cd web && node tests/e2e/run.ts \
  --base-url https://flashbeat.rebuildup.dev \
  --write --song-id e2e-acceptance-YYYYMMDD'
# 3) 復旧
./script/with-secrets -- sh -c 'cd web && curl -sS -X POST \
  https://flashbeat.rebuildup.dev/api/v1/admin/songs/e2e-acceptance-YYYYMMDD \
  -H "Authorization: Bearer $FLASHBEAT_ADMIN_TOKEN" \
  -H "content-type: application/json" -d "{\"archived\":true}"'
```

判定: `summary.failed === 0` (exit 0)、`chartVersion +1`、`catalogVersion +1`、
読み戻しの snapshot が送った内容と完全一致、復旧後にカタログから消えること。

### 5.2 ブラウザ経路 (Studio の UI だけで完結)

```sh
cd web
E2E_BASE_URL=https://flashbeat.rebuildup.dev FLASHBEAT_ADMIN_TOKEN=... \
  npx playwright test --config tests/e2e/playwright.config.ts \
  --grep "catalog → editor → publish"
```

`publish-flow.spec.ts` が Catalog → Editor → Publish → Catalog を実ブラウザで
通し、専用曲 `e2e-publish-flow` を作成 → 公開 → 読み戻し → `afterAll` でアーカイブ
します。本番に対しては `webServer` の seed は走りません (既存サーバを再利用)。

判定: テストが 1 件以上 pass、失敗 0、復旧後のカタログに `e2e-publish-flow` が
無いこと。

## 6. やらないこと

- 既存曲の上書き・アーカイブ (対象は専用 ID のみ)。
- D1 への直接 SQL 実行、`void db seed` のリモート適用。
- 本番 Worker のコード変更・再デプロイ (この受け入れは現在のデプロイの検証)。
- トークン値の出力・記録。
- Windows 実機の受入をこの手順で代替すること (別ゲート)。

## 7. 記録の書式

実施した場合は [`docs/evidence/`](../evidence/) に次の 1 ファイルを追加します。

- 実施日時 / 実行者 / 承認の記録
- 対象曲 ID、`catalogVersion` の前後、`chartVersion` の前後
- 実行コマンド (トークンは伏せる) と `summary` の JSON
- 復旧の実行結果 (カタログから消えたことの確認)
- 言えないこと (例: 実ディスプレイでの表示、Windows プレイヤーの挙動)
