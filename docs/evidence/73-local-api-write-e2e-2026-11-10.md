# 隔離環境での実 HTTP publish E2E — 2026-11-10 (#73 / #135)

PR #334 の 4 テストは **fetch をスタブしたコンポーネントテスト**であり、本番 API の動作確認ではない。
そのため、隔離したローカル環境 (実 HTTP / 実 D1 / 実 Worker) で
`web/tests/e2e/run.ts` の write ステージを通した結果をここに記録する。

- 実施日: 2026-11-10
- 対象: `origin/main` = `8a54e03`
- 実行場所: worktree `/Users/rebui/work/FlashBeat.wpub/web` (main と同一内容 + テスト 1 ファイル)
- **本番 (`flashbeat.rebuildup.dev`) へは一切書き込んでいない。** 送信先は `http://localhost:5301` のローカル Worker

## 1. 環境 (隔離)

```sh
cd web
npx void db seed                              # ローカル D1 を reset + migrate + seed (song id = who)
FLASHBEAT_ADMIN_TOKEN=local-dev-only \
  npx vp dev --port 5301 --strictPort          # ローカル Worker + D1 バインディング
curl -s -o /dev/null -w '%{http_code}' http://localhost:5301/api/v1/catalog   # → 200
```

- トークンは**ローカル専用の固定文字列** (`local-dev-only`)。Infisical の本番 secret は使っていない。
- `void db seed` はこの web ディレクトリのローカル D1 だけを対象にする (リモート D1 には触れない)。

## 2. 読み取りのみ (既定モード)

```sh
node tests/e2e/run.ts --base-url http://localhost:5301
```

`catalog.*` / `chart.*` / `chart.schema` / `leaderboard.*` の **14/14 ステージ pass**。
`chart.schema` は `contracts/schemas/chart.v1.schema.json` (Ajv draft 2020) に対する検証。

## 3. write ステージ (実 PUT → 読み戻し)

```sh
FLASHBEAT_ADMIN_TOKEN=local-dev-only \
  node tests/e2e/run.ts --base-url http://localhost:5301 --write --song-id who
```

```
summary: {"total": 21, "passed": 21, "failed": 0}    exit 0
write.publishResponse   {ok: true, songId: who, chartVersion: 2, catalogVersion: 2}
write.catalogVersion    2
write.catalogChartVersion 2
write.catalogNoteCount  139
write.snapshot.songId   who
write.snapshot.chartVersion 2
write.snapshot.exact    name = "Who [e2e #97]" ほか全フィールド一致
```

確認できたこと:

1. `PUT /api/v1/admin/songs/who` が **実 HTTP で**通り、応答が契約どおり (`ok` / `songId` / `chartVersion` / `catalogVersion`)
2. 読み戻し (`GET /api/v1/catalog`, `GET /api/v1/charts/who?version=2`) が **chartVersion / catalogVersion とも +1** を返す
3. 公開スナップショットが送信内容と**完全一致** (`snapshot.exact`)
4. 譜面スキーマ検証 (Ajv) も通る

## 4. 失敗時の挙動 (ゲートが本当に落ちるか)

同じ write を**誤ったトークン**で実行:

```sh
FLASHBEAT_ADMIN_TOKEN=wrong-token node tests/e2e/run.ts --base-url http://localhost:5301 --write --song-id who
```

```
summary: {"total": 15, "passed": 14, "failed": 1}    exit 1
FAIL write.publishResponse
  expected {ok: true, …}
  actual   {ok: false, code: "unauthorized", message: "The staff token was rejected. …"}
```

- **プロセスの終了コードは 1**。CI のジョブはこの失敗で落ちる。
- 失敗後も `GET /api/v1/catalog` の `catalogVersion` は **2 のまま** (部分書き込みなし)。

## 5. 復帰 (失敗の次の成功)

失敗の直後に正しいトークンで再実行:

```
summary: {"total": 21, "passed": 21, "failed": 0}    exit 0
write.publishResponse {ok: true, chartVersion: 3, catalogVersion: 3}
```

`catalogVersion` は 2 → 3 に進み、失敗した試行が状態を壊していないこと (再試行で復帰できること) を確認した。

## 6. 後始末

```sh
npx void db seed     # ローカル D1 を seed 状態に戻す (write の痕跡を消す)
lsof -ti tcp:5301 | xargs kill
```

## 7. この記録で言えないこと

- **本番環境**での同等の確認 (本番 D1 / 本番 Worker / 本番トークン) は行っていない。書き込みを伴うため、
  対象データ・復旧方法・必要権限を明示したうえで別途実施する。条件と手順は
  [`docs/operations/production-api-acceptance.md`](../operations/production-api-acceptance.md) にまとめた。
- ブラウザ操作 (Playwright) での通し確認はこの記録の範囲外。`web/tests/e2e/*.spec.ts` のブラウザ E2E と
  CI 実行は別 Issue (#75 / #73) で扱う。
- #135 の「ブラウザだけで登録が完結する」の**受入そのもの**は、この API 往復 + 画面操作の両方が揃って初めて言える。
