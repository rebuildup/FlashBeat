# 受入フェーズのトリアージ — open Issue 37 件 (2026-11-10)

対象: `origin/main` = `7af9242` (PR #339〜#344 merge 後) / open Issue **37 件** / open PR **0 件**。
この文書は「統合作業の区切り」として、37 件を **実装完了 / 未検証 / 仕様判断待ち** に分け、
**閉じてよいか** を 1 件ずつ判定した結果である。

## 0. 結論

| 区分 | 件数 | クローズ |
|---|---|---|
| **実装完了 — 受入証拠が 1 つ足りないだけ** (`#221` `#224` `#239` `#240` `#241`) | 5 | **0 件** (下記 §2 の理由) |
| **未検証** — 実装はあるが、受入条件に未達の項目が残る | 28 | 0 |
| **仕様判断待ち** — オーナーの決定がないと合否を付けられない | 4 (`#30` `#129` `#219` `#222`) | 0 |
| **今回クローズした件数** | — | **0 件** |

**クローズした Issue は 0 件。** 未実装の Issue も 0 件 (「コードがある」ではなく
「Issue 自身の受入条件が証拠で閉じているか」で判定した)。閉じられない理由は
ほぼすべて **Windows 実機 / 3 解像度の実ディスプレイ / 本番 API** のいずれかで、
いずれも cc が単独では実行できないゲートである。

## 1. 判定基準

- **実装完了** = Issue の受入条件が、実装・テスト・証拠ファイルで閉じている。
- **未検証** = 実装はあるが、受入条件の中に **実機・実サービスでしか作れない証拠**が残る。
- **仕様判断待ち** = Issue 本文の前提 (曲数・ソース SHA・プレイフィールド方式) が
  現行 main と食い違い、**どちらを正とするかをオーナーが決めていない**。
- EditMode / Vitest / Playwright の緑は **実機・実 API の代用にしない**。
- 3 解像度の証拠は **実ディスプレイ全画面**の撮影のみ有効。ウィンドウリサイズの擬似全画面は
  証拠にならない ([`../operations/three-resolution-fullscreen-verification.md`](../operations/three-resolution-fullscreen-verification.md))。
  `docs/evidence/96-241-resolution-matrix/` の PNG は macOS のウィンドウサイズ撮影であり、
  **この基準を満たさない**。

## 2. 「実装完了」5 件をクローズしなかった理由

`#221` `#224` `#239` `#240` `#241` は、実装・回帰テスト・証拠ファイルが揃っている
(監査台帳でも「クローズ提案可」)。しかし 4 件は **受入条件そのもの**に 3 解像度の実画面が入っており、
残る 1 件も作業条件に入っている。

| # | 受入条件の該当箇所 | 状態 |
|---|---|---|
| 221 | タスク 4「1920×1080 / 2560×1440 / 2560×1664 で重なり/欠け/キー表記・読みやすさを撮影比較」 | **未実施** (2K の実ディスプレイが無い) |
| 224 | タスク 4「FHD/2K/Mac 画像とキーボード/マウス操作」 | **未実施** |
| 239 | 受け入れ条件「**3解像度で**重なり・欠け・判読不能・押せない操作部品がない」 | **未実施** |
| 240 | 受け入れ条件「**3解像度で**結果・ボタン・モーダルに重なりやクリップがない」 | **未実施** |
| 241 | 受け入れ条件「**3解像度で**重なり・欠け・押せないhitboxがない」 | **未実施** |

この 5 件の残条件は #242 (v6 全画面の実 Win64 受入) の「完了対象」に明記されている。
したがって **#242 が閉じるときに一緒に閉じる**のが筋であり、先に子だけ閉じると
「実機未検証のまま完了」に見える。オーナー指示「実機検証が必要な Issue も閉じません」に従い、
**今回は閉じない**。

## 3. 37 件の区分

`残る条件` は「閉じるために足りないもの」。`ゲート` は W=Windows 実機 / R=3 解像度の実ディスプレイ /
A=本番 API (オーナー承認後) / S=仕様判断。

| # | 区分 | 今そろっている証拠 | 残る条件 | ゲート |
|---|---|---|---|---|
| 3 | 未検証 | `contracts/openapi.v1.yaml`, `web/routes/api/v1/**` | S1〜S4 の実 Win64 証跡なし (`docs/evidence/index.md` §1) | W, A |
| 4 | 未検証 | `YouTubeStreamResolver.cs`, `MediaCacheManager.cs` | Win64 の S1 完走 (映像・音声) | W |
| 5 | 未検証 | `CloudCatalog.cs:91,96,105` | 本番 PUT 1 往復 + Win Unity の描画 | W, A |
| 6 | 未検証 | `web/pages/publish.tsx`, `new-song/**` | 公開 chartVersion を Win Unity で再生 | W, A |
| 7 | 未検証 | `scores.ts`, `leaderboards/**` | 2 台目クライアントでの表示観測 | A |
| 13 | 未検証 | ローカルキャッシュ再生へ設計変更済み (`YouTubeStreamResolver.cs:74-80`, `MediaCacheManager`) | Win64 で音声が鳴ること (`audioMode=Stream`) | W |
| 17 | 未検証 | `GManager.ActiveSongs`, `CloudCatalog.cs` | デプロイ済み API → Win Unity の E2E | W, A |
| 19 | 未検証 | `song-publisher/**`, 5/7 充足 | Windows Unity 再生 + S3 ゲート | W, A |
| 21 | 未検証 | `RankingClient.cs`, `ResultRankingOverlay.cs` | 実 Win64 送信 + 2 台目表示 | W, A |
| 22 | 未検証 | `release/check/stage-manifest.json`, `verify-stage.ps1` | S1〜S4 の実機証跡 (S0 のみ partial) | W |
| 30 | 仕様判断待ち | `docs/ui/issue-30-spec-decisions.md` (HOLD 4 項目は凍結) | D1–D6 の決定 (HOLD 判定・歌詞・キーガイド・pen 正本・scoringVersion・ふりがな) | S |
| 58 | 未検証 | `CatalogMergeTests.cs`, macOS 2 曲完走 | Win の映像視認・A/V 同期・Retry・全曲完走 | W |
| 59 | 未検証 | 4 機能実装 + `docs/evidence/59-feature-acceptance.md` | 実機の目視記録 (HOLD の OK/NG は #30 D1 待ち) | W, S |
| 73 | 未検証 | ローカル実 HTTP E2E 21/21 (`docs/evidence/73-local-api-write-e2e-2026-11-10.md`) | 本番書き込み E2E (T5・隔離 ID・復旧手順) | A |
| 75 | 未検証 | `web/tests/visual/capture.mjs`、ゲート exit 0 (PR #257) | 人手 redline レビュー (T3–T5) + 本番 E2E | R(人), A |
| 88 | 未検証 | `ProjectSettings.asset:119 fullscreenMode:1` / `:107 resizableWindow:0`, `FullscreenPolicyTests.cs` | `displayCoverage ≥0.99` と 1024×768 / 1920×1080 の実機撮影 | W, R |
| 96 | 未検証 | `UISizeProfile.cs`, `V6UiProfile.cs:73-75` | 1024×768 と 1920×1080 の実ディスプレイ撮影 | W, R |
| 118 | 未検証 | `UpdateFlowModel.cs:136`, `UpdateChecker.cs:224,320,658` (自動適用は実コード 0 件) | 実機での更新 E2E (検出→押下→適用→再起動) | W |
| 129 | 仕様判断待ち | `~/Desktop/FlashBeat-Mac/FlashBeat.app` 実在 (README は `0.1.0+fffc399`) | どちらのソース SHA を正とするか (`b677f8f` か現行 main か) | S |
| 134 | 未検証 | `RankingClient.cs`, `ResultRankingOverlay.cs:186,307` | 実 Win64 の名前入力→登録→Top10 | W, A |
| 135 | 未検証 | `new-song-flow.tsx`, `slug.ts`, `oembed.ts` (実ブラウザ 5/6) | デプロイ済み API → Unity カタログへの反映 | A, W |
| 140 | 未検証 | 条件 1–5 充足 (`docs/evidence/140-font-code-strings.md`), macOS で豆腐 0 | Windows 実機での豆腐 0 の目視 | W |
| 162 | 未検証 | `issue-162-ui-contract.md`, live 欠陥 0 (PR #339/#343), EditMode 1401/1401 | 実機の実操作受入 | W |
| 167 | 未検証 | `V5UiWiringTests.cs` 17 テスト, `issue-167-ui-acceptance.md` | Win64 1 循環での実操作 | W |
| 217 | 未検証 | `UI/V6/**` 実装は進行済み | `Playfield=false` の決定下での条件 1/4 の再定義 + 実機 | W, S |
| 219 | 仕様判断待ち | `V6PlayfieldView.cs` 等は main に残置 | 本文の受入条件 (v6 canvas プレイフィールド) の **re-scope かクローズか** | S |
| 221 | 実装完了 (受入待ち) | `V6SelectView.cs`, `V6SelectViewLogicTests.cs` 23, `specs/221-222-select-v6` 23/23 | 3 解像度の実画面比較 | R |
| 222 | 仕様判断待ち | 結線完了 (PR #272), `SongCatalogSelectionTests` | 「45 曲以上」を撤回するか維持するか (現カタログ 29 エントリ / 選曲可 26) | S, W |
| 223 | 未検証 | `GameSceneManager.cs:87`, `SelectSceneManager.cs:53`, `GameUiExclusivityTests.cs` | Win64 の Title→…→Result 1 曲完走 | W |
| 224 | 実装完了 (受入待ち) | `V6PauseView.cs`, `V6PauseRuntimeTests.cs`, `docs/evidence/224-*.md` | FHD/2K/Mac の実画面 + 実機操作 | R, W |
| 226 | 未検証 | `SongEndStateMachine.cs:78`, 受入テスト 20 件, macOS 130/130 | Win64 完走 + 実ランキング送信 | W, A |
| 233 | 未検証 | `PlayfieldLaneGuides.cs`, `MediaTimeline.cs:161`, macOS `pass=29 fail=0` | Windows での「ノーツ・映像が見える」 | W |
| 239 | 実装完了 (受入待ち) | `V6TitleLogic`/`History`/`Ranking`, `specs/239` 12/12 | 3 解像度の実画面 | R |
| 240 | 実装完了 (受入待ち) | `V6ResultView.cs`, `V6ResultRegistrationTests.cs`, `docs/evidence/240-*.md` | 3 解像度の実画面 + 実登録は #21/#134 | R, A |
| 241 | 実装完了 (受入待ち) | 4 タブ + `V6OptionWiringTests.cs` (実 View を生成して押す 8 テスト) | 3 解像度の目視 | R |
| 242 | 未検証 | 受入手順 `win64-candidate-acceptance-2026-11-10.md` 準備済み | 3 解像度 + 実 Win64 1 曲完走 (最終ゲート) | W, R |
| 250 | 未検証 | ロードマップ本文 | 条件 1/4/5 (実機・CI ライセンス secret・本番 API) | W, A |

## 4. ゲート別の作業リスト

### 4.1 Windows 実機 (展示 PC — cc は操作しない)

候補は **`FlashBeat-0.1.22-win64-7af9242d.zip`**
(ソース `7af9242d` / SHA-256 `33acd106…7a01` / 手順は
[`../operations/win64-candidate-acceptance-2026-11-10.md`](../operations/win64-candidate-acceptance-2026-11-10.md))。

- 最優先: **#233** (ノーツ・映像が見える) → **#223** (1 曲完走) → **#242** (最終受入)
- 同時に拾えるもの: #13 / #4 (音声・映像)、#162 / #167 (実操作)、#241 / #239 / #240 / #221 / #224 (画面)
- **Release は公開しない。** Windows 受入が通るまで `v0.1.22` は作らない。

### 4.2 本番 API (#73 / #135 / #5 / #7 / #21 / #134 / #226)

**オーナー承認後にのみ実行する。** 隔離 ID (`e2e-publish-flow` / `e2e-acceptance-<YYYYMMDD>`) と
復旧 (`POST /api/v1/admin/songs/{id}` `{"archived":true}`) は
[`../operations/production-api-acceptance.md`](../operations/production-api-acceptance.md) に固定済み。
**本番データを黙って書き換えない。** 承認がない限り、この 7 件は未検証のままとする。

### 4.3 3 解像度の実ディスプレイ (#88 / #96 / #217 / #221 / #223 / #224 / #239 / #240 / #241 / #242 / #233)

FHD 1920×1080 と 2K 2560×1440 は**実ディスプレイ**が必要。Mac 2560×1664 は取得済み。
ウィンドウリサイズの擬似再現は証拠にしない。

### 4.4 仕様判断 (オーナー)

| # | 決めること |
|---|---|
| 30 | D1 HOLD 判定 / D2 歌詞 / D3 キーガイド / D4 pen 正本 / D5 scoringVersion / D6 ふりがな |
| 129 | macOS 独立アプリのソース SHA を `b677f8f` にするか現行 main にするか |
| 219 | v6 canvas プレイフィールドの Issue を re-scope するか、展示後の再評価としてクローズするか |
| 222 | 「45 曲以上」を撤回するか維持するか (現カタログ 29 エントリ / 選曲可 26) |

## 5. 本文・台帳が stale な箇所 (引用前に直す)

| 対象 | 記述 | 現状 |
|---|---|---|
| #118 | 「UpdateChecker は `OnApplicationQuit` で自動適用する」 | **誤り**。自動適用のコードは 0 件 (PR #255 / #273 で明示式へ変更) |
| #88 | 「原因は `fullscreenMode: 2`」 | 現 main は `fullscreenMode: 1` + `resizableWindow: 0`。受入条件自体は未検証のまま |
| #13 | 受入条件 T1「no download-to-local fallback」 | **意図的な設計変更**(ローカルキャッシュ再生)と矛盾。本文の更新が必要 |
| #223 | 統合作業の指示書 | 統合は main に存在 (`GameSceneManager.cs:87`, `SelectSceneManager.cs:53`)。残るのは実 Win64 ゲートのみ |
| #219 / #222 | 受入条件が現行仕様と食い違う | §4.4 の判断待ち |
| 台帳 #129 | 「`~/Desktop` に `FlashBeat*.app` が存在しない」 | **誤り**。`~/Desktop/FlashBeat-Mac/FlashBeat.app` が存在 (README `0.1.0+fffc399`) |

## 6. 次の一手

1. オーナーが §4.4 を決める (4 件)。
2. オーナーが Windows 実機で §4.1 を実施し、ログとスクリーンショットを
   `docs/operations/exhibit-evidence-2026-11-10/` に置く。
3. cc が証拠を取り込み、**受入証拠が揃った Issue だけ**を根拠付きで閉じる
   (#221/#224/#239/#240/#241 は #242 と同時)。
4. 本番 API は承認後に §4.2 を 1 往復だけ実行する。
5. 進行中に新しい不具合が見つかったら、実装・レビュー・テスト・統合を再開する
   (受入フェーズでも実装は止めない)。
