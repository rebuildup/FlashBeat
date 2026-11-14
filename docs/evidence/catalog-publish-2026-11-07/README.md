# 本番カタログへ全 27 曲の譜面データを publish した記録 (P2 / Issue #41)

- 本番: `https://flashbeat.rebuildup.dev` (Worker `flashbeat-web`, D1 `flashbeat` `7de6fd23-dd7b-4e21-bddd-b28b33ee0462`)
- 実行: 2026-11-07 04:2x JST / 作業ツリー `feature/p2-catalog-e2e`
- 手段: `web/scripts/publish-catalog.mjs` (新規) — 譜面は `Assets/Game/Resources/<曲名>.json`、
  メタデータは `Assets/Game/Scripts/Songs/GManager.cs` の並列配列から読む

## 1. 結果

| 項目 | publish 前 | publish 後 |
|---|---|---|
| カタログの曲数 | 2 | **26** |
| `catalogVersion` (= `COUNT(chart_versions)`) | 10 | **36** |
| publish 成功 | — | **26 曲** (既存 2 曲 + 新規 24 曲) |
| publish 拒否 (書き込みなし) | — | 1 曲 (`ニセモノ`) |

## 2. 曲ごとの照合 (元ファイル ↔ API が返すスナップショット)

`notes` / `BPM` / `offset` / `maxBlock` / `name` / `lyrics` を `GET /api/v1/charts/{id}?version=N`
で読み出して元ファイルと突き合わせた結果。**26 曲すべて `identical`** (差分 0)。

| id | 元ファイル | notes | BPM | offset | maxBlock | lyrics | chartVersion | noteCount | 照合 |
|---|---|---|---|---|---|---|---|---|---|
| 4th-smile | 4th smile.json | 105 → 105 | 140 → 140 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 105 | identical |
| agon-beats | 阿吽のビーツ.json | 262 → 262 | 206 → 206 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 262 | identical |
| bibibibi | ビビビビ.json | 184 → 184 | 175 → 175 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 184 | identical |
| calamity | Calamity.json | 111 → 111 | 200 → 200 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 111 | identical |
| chartreuse | Chartreuse.json | 343 → 343 | 180 → 180 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 343 | identical |
| dogbite | Dogbite.json | 603 → 603 | 195 → 195 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 603 | identical |
| euthanasia | エウタナシア.json | 243 → 243 | 128 → 128 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 243 | identical |
| guru | GURU.json | 295 → 295 | 138 → 138 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 295 | identical |
| hai-kurakubu | 灰Φ倶楽部.json | 275 → 275 | 143 → 143 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 275 | identical |
| ichikan | 一龠.json | 191 → 191 | 176 → 176 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 191 | identical |
| igaku | イガク.json | 196 → 196 | 170 → 170 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 196 | identical |
| konton-bugi | 混沌ブギ.json | 292 → 292 | 95 → 95 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 292 | identical |
| kyouki-ranbu | 狂喜蘭舞.json | 360 → 360 | 176 → 176 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 360 | identical |
| lost-umbrella | ロストアンブレラ.json | 268 → 268 | 274 → 274 | 5 → 5 | 8 → 8 | 5 配列 | 2 | 268 | identical |
| manimani | まにまに.json | 76 → 76 | 174 → 174 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 76 | identical |
| override | オーバーライド.json | 250 → 250 | 102 → 102 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 250 | identical |
| panopticon | パノプティコン.json | 319 → 319 | 174 → 174 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 319 | identical |
| poison | Poison.json | 82 → 82 | 120 → 120 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 82 | identical |
| psyched | Psyched.json | 256 → 256 | 150 → 150 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 256 | identical |
| selfie | セルフィー.json | 254 → 254 | 195 → 195 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 254 | identical |
| shunran | 春嵐.json | 315 → 315 | 140 → 140 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 315 | identical |
| shuuen-boukou | 終焉逃避行.json | 296 → 296 | 147 → 147 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 296 | identical |
| tele-caster-boy | テレキャスタービーボーイ.json | 163 → 163 | 182 → 182 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 163 | identical |
| tutorial | チュートリアル.json | 53 → 53 | 95 → 95 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 53 | identical |
| twitterland | ツイッターランド.json | 130 → 130 | 142 → 142 | 5 → 5 | 8 → 8 | 5 配列 | 1 | 130 | identical |
| who | Who.json | 139 → 139 | 128 → 128 | 5 → 5 | 8 → 8 | 5 配列 | 10 | 139 | identical |

`lyrics` は `mondai` / `startTime` / `endTime` / `furigana` / `romaji` の 5 配列を保持したまま
保存・配信されている (例: `who` v10 は 28 件 / `tutorial` v1 は 53 件 / `manimani` v1 は 23 件)。

publish 前の chartVersion は **who v9 / lost-umbrella v1**、他 24 曲は未公開
(表の chartVersion は publish 後の値)。

## 3. publish しなかった曲

| 曲 | 理由 |
|---|---|
| `E2EProbeSong.json` | テスト用プローブ (notes 0)。公表対象外。 |
| `ニセモノ.json` | notes 0 / maxBlock 5。API が 422 で拒否: `must contain at least one note; an empty chart cannot be played` (書き込みなし)。`GManager` 側にも 2 回 (index 27 と 28) 登録されている重複で、譜面データを創作しない限り公開できない。 |

実測 (1 回だけ送り、拒否された):

```
attempted nisemono (ニセモノ.json) -> 422 {"error":"invalid publish request","diagnostics":[{"path":"/chart/notes","message":"must contain at least one note; an empty chart cannot be played"}]}
```

## 4. メタデータで 1 曲だけ正規化した点

`チュートリアル` の `GManager.Slevel` は **0** で、契約 (`SongPublishRequest.level` 1..99) と
D1 の CHECK 制約 (`songs_level_range`) が 1..99 しか受け付けない。譜面はそのまま、
カタログの level だけを 1 にした (スクリプトの `LEVEL_OVERRIDES`、レポートに明記)。
それ以外の 25 曲はすべて `GManager` の値をそのまま publish した。

## 5. 残っている不整合 (未修正 / 報告のみ)

`GManager` の固定カタログ配列と譜面ファイルが食い違う曲。publish したカタログは
**譜面ファイルが正** (notes 数・BPM とも譜面側の値)。

| 曲 | 譜面 | GManager 配列 |
|---|---|---|
| まにまに | notes 76 | `Shit` 383 |
| Chartreuse | notes 343 | `Shit` 349 |
| Psyched | notes 256 | `Shit` 257 |
| パノプティコン | notes 319 | `Shit` 316 |
| 一龠 | notes 191 | `Shit` 193 |
| エウタナシア | BPM 128 | `SBPM` 127 |

Unity 側は cloud カタログを固定カタログへ合成する (`GManager.Merge` → `Overlay`) ので、
選曲画面に出るノーツ数と BPM は cloud 側 (譜面ファイル) の値になる。

## 6. 本番 API の実測

```
$ curl -s https://flashbeat.rebuildup.dev/api/v1/catalog
{"catalogVersion":36,"songs":[ … 26 曲 … ]}

$ curl -s "https://flashbeat.rebuildup.dev/api/v1/charts/who?version=10"
{"songId":"who","chartVersion":10,"chart":{"name":"Who","maxBlock":8,"BPM":128,"offset":5,
 "notes":[…139…],"lyrics":{"startTime":[…28…],"furigana":[…28…],"mondai":[…28…],"romaji":[…28…],"endTime":[…28…]}}}

$ curl -s "https://flashbeat.rebuildup.dev/api/v1/charts/manimani?version=1"
{"songId":"manimani","chartVersion":1,"chart":{"name":"まにまに (audio-extractor.net)","maxBlock":8,"BPM":174,"offset":5,"notes":[…76…],"lyrics":{…}}}

$ curl -s "https://flashbeat.rebuildup.dev/api/v1/charts/tutorial?version=1"
{"songId":"tutorial","chartVersion":1,"chart":{"name":"チュートリアル","maxBlock":8,"BPM":95,"offset":5,"notes":[…53…],"lyrics":{…}}}
```

D1 (`cf d1 query`) も同じ内容:

```
songs: 26 行 (who currentVersion 10 / lost-umbrella currentVersion 2 / 他 24 曲 currentVersion 1)
chart_versions: 36 行 (who 10 + lost-umbrella 2 + 他 24 = 36)
```

## 7. 契約テスト (本番)

```
cd web && ../script/with-secrets bash -c 'FLASHBEAT_E2E=1 FLASHBEAT_D1_DATABASE_ID=7de6fd23-… \
  node --test tests/production-api.test.ts'

✔ answers the catalog with the contract shape and without caching
✔ counts every published chart snapshot in the catalog version
✔ lists only songs whose current version is published
✔ rejects a missing or invalid version with 400
✔ answers 404 for an unknown song and for an unknown version
✔ answers every catalog entry the way the Unity client reads it
✔ serves the chart snapshot in the shape the Unity player loads
ℹ tests 7  ℹ pass 7  ℹ fail 0
```

26 曲すべてが Unity 側 (`CloudCatalog.cs` の読み取り規則、`Data` / `Note` が読む
`type` `num` `block` `LPB`) を満たすことを確認済み。
