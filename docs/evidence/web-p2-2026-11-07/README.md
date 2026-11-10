# Web ↔ API ↔ Unity カタログの実経路検証 (P2 / Issue #41)

- 本番: `https://flashbeat.rebuildup.dev` (Worker `flashbeat-web`, D1 `flashbeat`)
- 作業ブランチ: `feature/p2-catalog-e2e` (base `52`)
- デプロイ: `void deploy --platform cloudflare` → deployment `5f36f0e2-751e-49ee-85cf-259886e3f435` (2026-11-07 03:59 JST)
- 実行: 2026-11-07 03:58–04:02 JST / macOS arm64 / Node 26.13.0

## 1. 失敗していた境界

### 1.1 Web Studio の Publish が本番 API に届いていなかった (修正済み)

デプロイ済みバンドル (`/assets/publish-*.js`) は `apiOrigin: ``` (空文字) でビルドされており、
`src/lib/publish/transport.ts` の `resolvePublishTransport()` が「origin 未設定 → fixture」を返していた。
公開画面の Publish ボタンは **ブラウザ内の fixture** (`src/lib/publish/mock.ts`) に回答し、
画面上は "Published" と出ながら **D1 には何も書かれなかった**。

修正: 既定を「同一オリジンの実 API」に変更 (`VITE_FLASHBEAT_API_ORIGIN=fixture` の明示指定時のみ fixture)。
デプロイ後のバンドルは `t.length===0?{mode:\`live\`,baseUrl:\`\`,...}` になっている。

### 1.2 Web Studio のカタログタブが fixture のままだった (修正済み)

`web/src/app/catalog.ts` の `loadCatalog()` はローカルの fixture 配列 (manimani / euthanasia など) を
返していた (README / AGENTS に「#15 が配備されたら fetch に置き換えろ」と書かれていた状態)。
公開済みカタログ (who / lost-umbrella) とは別の曲が表示されていた。

修正: `/api/v1/catalog` を読み、表示するフィールドを実行時 narrowing する。
`?state=loading|empty|error` のレビュー状態は残す。

## 2. 実経路の検証 (1 回通した記録)

### 2.1 ローカルスイート

```
cd web && npm test
ℹ tests 19  ℹ pass 19  ℹ fail 0   (node --test tests/)
Test Files 10 passed (10)  Tests 89 passed (89)   (vitest run)
```

### 2.2 本番 E2E (read + publish) — `web/tests/production-api.test.ts`

```
cd web && ../script/with-secrets bash -c 'FLASHBEAT_E2E=1 FLASHBEAT_E2E_PUBLISH=1 \
  FLASHBEAT_E2E_SONG_ID=p2-e2e-probe FLASHBEAT_D1_DATABASE_ID=7de6fd23-... \
  node --test tests/production-api.test.ts'

✔ answers the catalog with the contract shape and without caching
✔ counts every published chart snapshot in the catalog version
✔ lists only songs whose current version is published
✔ rejects a missing or invalid version with 400
✔ answers 404 for an unknown song and for an unknown version
✔ answers every catalog entry the way the Unity client reads it
✔ serves the chart snapshot in the shape the Unity player loads
✔ rejects a wrong staff token with 401
✔ rejects an invalid payload with 422 and a diagnostic
✔ publishes the fixture chart and counts up on the next publish
ℹ tests 10  ℹ pass 10  ℹ fail 0
```

publish チェックは Web Studio と同じクライアント (`web/src/lib/publish/client.ts`) を通す。

### 2.3 Web Studio の publish パス (編集 → 検証 → publish)

スタジオのモジュールだけを使って (エディタが開く譜面 → `moveNote` で 1 ノーツ移動 →
`exportChartJson` → `buildPublishRequest` → `publishSong`) 本番へ publish した。

```
publish result: {"ok":true,"response":{"songId":"p2-e2e-probe","chartVersion":2,"catalogVersion":12}}
catalog: {"catalogVersion":12,"songs":[... {"id":"p2-e2e-probe","chartVersion":2,"noteCount":53} ...]}
stored note 0: {"type":1,"num":208,"block":7,"LPB":4} notes: 53
```

D1 (`cf d1 query`) にも `p2-e2e-probe` v2 が書き込まれていることを確認。
編集したノーツ (block 4→7, num 48→208) がそのまま保存・配信されている。

### 2.4 Unity 側への受け渡し (コード + 応答の形)

| 項目 | 場所 | 内容 |
|---|---|---|
| 取得経路 | `Assets/Game/Scripts/UI/SelectSceneManager.cs` (`RefreshCatalogLoop` / `RefreshCatalogOnce`) | 選曲画面入場時と 8 秒ごとに `GET /api/v1/catalog` (`Resources/CloudCatalogConfig.json` の `baseUrl` = `https://flashbeat.rebuildup.dev`) |
| キャッシュ | サーバは `Cache-Control: no-store` / クライアントは直近の合成結果のみ保持 | `GManager.Merge()` が固定カタログへ cloud を合成 (未知の公開曲が末尾に追加) |
| バージョン取得タイミング | `SelectSceneManager.StartRemoteSong` | 曲開始時にカタログの `chartVersion` を固定し、`GET /api/v1/charts/{songId}?version=N` を 1 回取得。`CloudChartHolder` がリトライ含め保持 |
| 再ビルド | 不要 | 公開曲・chartVersion は実行時に API から取得するため、Unity の再ビルドなしで新しい公開曲が選曲画面に出現する |

本番 E2E の 2 テストが API 応答を Unity 側の読み取り規則で検査している
(曲 id `^[a-z0-9-]{3,64}$` / タイトル非空 / video id `^[A-Za-z0-9_-]{11}$` /
`chartVersion >= 1` / ノーツの `type` `num` `block` `LPB`)。実測: 10 pass / 0 fail。

### 2.5 検証後の本番状態 (残渣なし)

```
$ curl -s https://flashbeat.rebuildup.dev/api/v1/catalog
{"catalogVersion":10,"songs":[
  {"id":"lost-umbrella","title":"ロストアンブレラ","artist":"稲葉曇","youtubeId":"DeKLpgzh-qQ","level":8,"bpm":274,"durationSec":89,"chartVersion":1,"noteCount":268},
  {"id":"who","title":"Who","artist":"Azari","youtubeId":"8JXiXt0D6tw","level":5,"bpm":128,"durationSec":68,"chartVersion":9,"noteCount":139}]}

GET /api/v1/charts/who?version=9          -> 200
GET /api/v1/charts/lost-umbrella?version=1 -> 200
GET /api/v1/charts/p2-e2e-probe?version=1  -> 404   (検証用残渣は削除済み)
```

検証用の `p2-e2e-probe` (songs 1 行 + chart_versions 4 行) は D1 から削除済み。
公開済みの実曲 2 件の chartVersion は **who v9 / lost-umbrella v1** のまま。

## 3. 未証明 / 残作業

- **Unity 実機の選曲 → 再生 → Result**: 本 PR の範囲外 (Issue #48 / #31 の Worker)。
  本 PR は API 側とカタログ側の受け渡しまで (応答の形と取得経路のコード確認まで)。
- **Unity Editor での実行**: この Mac には 6000.5.6f1 が無く (6000.6.8f1 のみ)、
  `ProjectSettings/ProjectVersion.txt` を改変せずに検証できないため未実施。
- **スコアの投稿 / リーダーボード**: `contracts/openapi.v1.yaml` には
  `POST /api/v1/scores` と `GET /api/v1/leaderboards/{songId}` があるが未実装 (Issue #20)。
  本 PR の対象外。
