# スコア登録 (#134) のオンライン送信 — 検証手順と実施記録

対象: 展示候補 `0.1.23+b6860a80` / Issue #242 (受入) / #134 (Result 登録)。
この文書は「端末内保存」と「本番 API 送信」を区別し、**本番への書き込みはオーナー承認後に
1 回だけ**行うための手順を定める。開発側は承認前に本番へ書き込まない。

---

## 0. 送信の仕様 (コード上の事実)

| 項目 | 値 | 根拠 |
|---|---|---|
| 送信先 | `Resources/CloudCatalogConfig.json` の `baseUrl` (既定 `https://flashbeat.rebuildup.dev`) | `RankingClient.Origin()` |
| 経路 | `POST /api/v1/scores`、`GET /api/v1/leaderboards/{songId}?version=` | `RankingClient.cs` |
| 送信する JSON | `songId` / `chartVersion` / `nickname` / `score` / `perfect` / `great` / `bad` / `miss` / `maxCombo` | `RankingClient.ScoreRequest` |
| ニックネーム | 1〜16 文字 (前後空白は trim) | `RankingClient.NicknameMaxLength` |
| スコア | サーバが判定回数から Unity 式で**再計算**し、一致しなければ 422 | `web/routes/api/v1/_score-model.ts` |
| 判定回数 | `perfect + great + bad + miss == 譜面のノーツ数` でなければ 422 | 同上 (`scores.ts`) |
| 端末内保存 | オンライン不可 / ローカル曲では `PlayerPrefs "V5PlayerName"` に保存するだけ | `V6LocalNameStore` / `V6ResultView.SaveLocalName` |

`JudgeScoreboard` は「1 ノーツ = 1 判定」を保証しているため、1 曲を完走すれば
判定回数の合計は譜面のノーツ数と一致する (途中終了や未判定ノーツが残る状態で送ると 422)。

---

## 1. ローカル API での検証 (2026-11-11 実施 / 本番書き込みなし)

本番と同じ Worker 実装をローカルの D1 で起動し、`RankingClient` が送る形の JSON を
そのまま投げて契約を確認した。**本番環境には送信していない。**

```sh
cd web
void db seed                                  # ローカル D1 を初期化 (song: who / noteCount 139)
FLASHBEAT_ADMIN_TOKEN=local-dev-only vp dev   # http://localhost:5173 (token はダミー)
```

| ケース | 送信 | 結果 |
|---|---|---|
| 正常 | `{"songId":"who","chartVersion":1,"nickname":"ACCEPT-LOCAL","score":1000000,"perfect":139,"great":0,"bad":0,"miss":0,"maxCombo":139}` | `201 {"score":1000000,"rank":1}` |
| ランキング表示 | `GET /api/v1/leaderboards/who?version=1` | `200 {"entries":[{"nickname":"ACCEPT-LOCAL","score":1000000}]}` |
| 判定数の不一致 | `perfect+great+bad+miss = 113` (ノーツ 139) | `422 perfect + great + bad + miss must equal the 139 notes` |
| スコアの不一致 | 138 perfect + 1 bad に対し `score=999999` | `422 the judgment counts score 994244 with the Unity formula` |
| ニックネーム 17 文字 | `0123456789abcdefg` | `422 String is too long (17 > 16)` |

**観測**: 同じ nickname で 2 回送ると、ランキングには **2 行**が並ぶ (個人ベストへの
置換ではない)。契約上は「送信履歴をスコア順に返す」実装であり、これは仕様どおり。
展示で複数人が同名を入力すると同じ名前が並ぶため、運営注意事項として扱う。

**確認できたこと**: クライアントの JSON 形式・ニックネーム上限・Unity 式スコア
(単精度エミュレーション込み) が、本番と同じサーバ実装に受け入れられる。
**確認できていないこと**: 実機の Result 画面から 1 曲完走 → Register を押す操作 (本番書き込み)。

---

## 2. 本番での確認手順 (オーナー承認後 / 1 回だけ)

前提: 展示候補 `FlashBeat-0.1.23-win64-b6860a80.zip` (`SHA-256 9ee7e4a6…`) を展開した
**新しいフォルダ**で実行する。

1. 実機で 1 曲を完走する (どの曲でもよい。判定回数の合計がノーツ数と一致する状態)
2. Result 画面で名前を入力する — **`ACCEPT` を使う** (後から見つけやすい)
3. **Register** を押す → 送信中 → 自分の順位 (例: `1 位`) が出る
4. ランキング表示 (Result のランキング / Select の Ranking タブ) に `ACCEPT` が出る
5. 記録するもの:
   - スクリーンショット: Result の登録モーダル (送信後) とランキング表示
   - `Player.log` (`%USERPROFILE%\AppData\LocalLow\rebuildup\FlashBeat\Player.log`)
   - 曲名 / スコア / 判定数 / 入力した名前
6. 失敗した場合は画面の案内に従う (モーダルに `再送 [Enter] / スキップ [Esc]` が出る)。
   - `オフラインのようです …` — 通信できていない。端末内保存だけが行われ、本番には何も書かれない
   - `同じ内容を送信済みです …` (HTTP 429, 10 秒窓) — **同じスコアを二重送信したときの重複防止**。
     押し直しではなく、この表示自体が「1 回目は受理された」ことを意味する
   - `送信できませんでした (HTTP nnn) …` — サーバ側の拒否。画面と `Player.log` を保存して報告し、
     展示中はスキップして続行する

**戻せない点**: 本番 D1 のスコアは削除 API が無く、送信するとランキングに残る。
そのため「1 回だけ」「名前は `ACCEPT`」を守る。展示本番のプレイで付くスコアは
そのまま記録として残る (想定どおり)。

---

## 3. 端末内保存との区別 (オンラインなしでも成立する経路)

| 状況 | 表示 | 保存先 |
|---|---|---|
| オンライン登録成功 | `登録しました` + 順位 / ランキング | 本番 D1 + `PlayerPrefs "RankingNickname"` (次回の初期値) |
| ローカル曲 / 譜面未取得 | `この端末に「(名前)」を保存しました` | `PlayerPrefs "V5PlayerName"` のみ |
| 通信失敗 | `オフラインのようです  再送 [Enter] / スキップ [Esc]` | なし (端末内保存もされない) |
| 二重送信 (HTTP 429) | `同じ内容を送信済みです  再送 [Enter] / スキップ [Esc]` | 1 回目の送信は受理済み |

この 3 経路は `V6ResultView` の送信前判定 (`V6ResultLogic.IsSubmittable`) と
`V6ResultLogic` のメッセージで分岐する (EditMode `V6ResultSubmitTests` / `V6ResultRegistrationTests`)。
