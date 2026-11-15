# 本番 API 書き込み受け入れ (issue #73 / #135) — 2026-11-15 実施記録

`docs/operations/production-api-acceptance.md` の手順どおりに、本番
(`https://flashbeat.rebuildup.dev` / 本番 D1) へ**専用曲 1 曲だけ**を書き込み、
読み戻し・契約検証・復旧まで実施した。既存曲は一切変更していない。
D1 への直接 SQL は使わず、すべて公開 HTTP API 経由。

- 実施日時: 2026-11-15 22:18–22:19 JST
- 実行者: Devin (オーナーの明示承認あり — セッション内で「本番 API への書き込み
  テストを実施してよいか」に対し許可)
- トークン: Infisical (`./script/with-secrets`) から環境変数注入。値は記録していない

## 対象

| 項目 | 値 |
|---|---|
| 曲 ID | `e2e-acceptance-20261115` (ハーネス経路) |
| 曲名 | `E2E Acceptance 20261115` / artist `FlashBeat E2E` |
| 譜面 | 8 ノート (tap、BPM 120、LPB 4、durationSec 10) |

## catalogVersion / chartVersion の前後

| 時点 | catalogVersion | 対象曲 chartVersion |
|---|---|---|
| 実施前 | 38 | (曲なし) |
| 曲の新規公開 (PUT) | 39 | 1 |
| write ステージの再公開 (PUT) | 40 | 2 |
| アーカイブ復旧 (POST) | 40 (単調増加・戻らない) | 2 |

`catalogVersion` は復旧でも進むのが仕様 (公開カタログの**中身**が元に戻ることが
復旧の判定基準)。復旧後の `GET /api/v1/catalog` は songs=28・対象曲なしを確認。

## 実行コマンド (トークンは伏せる)

```sh
# 1) 専用曲の新規公開
./script/with-secrets bash -c 'curl -sS -X PUT \
  "https://flashbeat.rebuildup.dev/api/v1/admin/songs/e2e-acceptance-20261115" \
  -H "Authorization: Bearer $FLASHBEAT_ADMIN_TOKEN" \
  -H "content-type: application/json" --data @/tmp/e2e-song.json'
# → {"songId":"e2e-acceptance-20261115","chartVersion":1,"catalogVersion":39}

# 2) write ステージ (実 PUT → 契約検証 → Catalog/Chart 読み戻し)
./script/with-secrets bash -c 'cd web && node tests/e2e/run.ts \
  --base-url https://flashbeat.rebuildup.dev \
  --write --song-id e2e-acceptance-20261115'
# → summary: total=21 passed=21 failed=0

# 3) 復旧 (アーカイブ)
./script/with-secrets bash -c 'curl -sS -X POST \
  "https://flashbeat.rebuildup.dev/api/v1/admin/songs/e2e-acceptance-20261115" \
  -H "Authorization: Bearer $FLASHBEAT_ADMIN_TOKEN" \
  -H "content-type: application/json" -d "{\"archived\": true}"'
# → {"songId":"e2e-acceptance-20261115","archived":true,"catalogVersion":40}
```

## 結果

- write ステージ **21/21 pass** (catalog shape / chart schema 適合 / PUT 応答 /
  catalogVersion 39→40 / chartVersion 1→2 / 読み戻し snapshot の完全一致を含む)
- 復旧後 `GET /api/v1/catalog`: songs=28、`e2e-acceptance-20261115` は非公開

## 言えないこと

- ブラウザ経路 (`publish-flow.spec.ts`、Studio UI のみでの登録完結) は本記録の
  対象外。#135 の条件 6 は別途ブラウザ E2E が必要。
- Unity プレイヤー側のカタログ反映・実ゲームでの選曲は未検証 (Web API のみ)。
- Windows 実機・実ディスプレイでの表示は対象外。
