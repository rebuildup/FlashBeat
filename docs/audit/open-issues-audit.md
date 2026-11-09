# open Issue 実装状況監査 (main 実測)

- **対象**: `origin/main` = `ab8c336c60fc6e7aa225b503c79eb8b10c3a9f9c` (PR #255 merge 直後)
- **監査日**: 2026-11-09
- **監査範囲**: 2026-11-09 時点で open の全 Issue 47 件
- **作業**: worktree `/Users/rebui/work/FlashBeat.waudit` (branch `w/audit-open-issues`)、**読み取りのみ**。コード変更・Issue コメント投稿なし。

> ## 最重要の結論
> **open Issue 数 47 ≠ 未実装件数。**
> 17 件 (A) は **実装が既に `main` にあり**、テストも存在するが、Issue が open のまま (あるいは受入条件の固定が PR 途中) である。
> この 17 件は **cc が即クローズ提案できる**。残り 30 件は未充足条件が具体的に特定できている。

---

## 0. 判定基準と調査方法

### 区分

| 区分 | 定義 |
|---|---|
| **A: 実装済み・証拠待ち** | 実装が `main` にあり、受入条件を満たすコードが存在する。残るのは「Issue への記録」「実測証跡」「受入条件の固定」だけ |
| **B: 部分実装** | 一部の受入条件のみ充足。未充足の条件を **具体的 file:line 付きで** 列挙できる |
| **C: 未実装** | 実装が存在しない |
| **D: ブロック (blocked)** | 着手・完了が他 Issue / オーナー判断 / Windows 実機 / Unity ライセンス / secrets などに依存する。何を待っているかを明記する |
| **E: 判定不能** | 判断材料が Issue 記述・コード・証跡のいずれにも不足。何が決めれば決まるかを明記する |

### 受入条件の 3 値

各受入条件に **充足 / 未充足 / 未検証** のいずれかを付与する。

- **充足** — `main` に実装が存在し、要件を満たしているとコードから確認できる
- **未充足** — 実装が存在しない、または存在しても要件を満たさない (file:line で示す)
- **未検証** — 実装は存在するが **テスト緑だけでは足りず、実機/実サービスの観測が必要** な条件。**EditMode 緑 ≠ 実機確認** を厳密に区別する

### 未検証の扱い (厳格区分)

| 代替検証 | 実機確認に代わらない理由 |
|---|---|
| EditMode / Vitest 緑 | 実行環境・描画・入力・実サービスの通信を再現しない。**画面表示・操作性・実通信の結果は含まれない** |
| コード読み | 「動く」保証にならない。null 参照・シーン不整合・実 API 差異は捕捉されない |
| macOS Player 起動 | **Win64 の代用にならない** (`docs/evidence/index.md` §1.6 が明記) |
| スクリーンショット 1 枚 | 譜面の空白区間などを誤って「正常」と読む危険 (#233 の実例) |

実機確認済みのものは **実行環境・日付・成果物パス (または SHA-256) を明記** した。観測できなかったものは **未検証** とし、代替検証の範囲を明記した。

### 調査で使った証拠

- `git merge-base --is-ancestor <sha> origin/main` — commit が `main` に入っているかの判定
- `git log --oneline -- <path>` — 実装ファイルの導入 commit
- `gh issue view <n>` — Issue 本文 + 全コメント
- `Assets/Tests/Editor/` (73 ファイル) / `Assets/Game/Tests/` (18 ファイル) / `web/**/*.test.ts(x)` (47 ファイル) の実在を確認
- `specs/*/tasks.md` のチェックボックス (証拠信号。ただし stale な場合あり)
- `docs/evidence/**` の実測アーティファクト

---

## 1. サマリ表 (全 47 件)

| # | タイトル (短縮) | 区分 | 実装位置 | 充足/未充足/未検証 | ブロック | 推奨アクション |
|---|---|---|---|---|---|---|
| 3 | 展示版: 5 API 契約と段階的実装 | **D** | `contracts/openapi.v1.yaml`, `web/routes/api/v1/**` | 3/2/1 | Win64 CI ライセンス・Windows 実機 | S1–S4 ゲートの証跡を 1 段ずつ作る |
| 4 | Gate S0/S1 Win64 ベースライン+YouTube | **D** | `Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs` | 2/1/1 | Windows 実機 (展示 PC) | S1 の Win64 run を取得 (映像あり・音声 未証明) |
| 5 | Gate S2 Cloudflare API + Unity 動的カタログ | **D** | `Assets/Game/Scripts/Songs/CloudCatalog.cs:91,96,105` | 1/1/2 | Windows 実機 + 本番 PUT | Mac で PUT → Win Unity 再生を 1 回通す |
| 6 | Gate S3 Web Studio → Unity 公開デモ | **D** | `web/pages/publish.tsx`, `web/src/components/new-song/**` | 1/1/2 | Windows 実機 | 公開 chartVersion を Win Unity で再生 |
| 7 | Gate S4 optional クラウドランキング | **B** | `web/routes/api/v1/scores.ts:1-80`, `web/routes/api/v1/leaderboards/**` | 3/1/2 | 2台目のクライアント | サーバ再計算は充足、2 台目のクライアント観測のみ未検証 |
| 12 | S2/Mac Void+Vite+React+Panda/Park 足場 | **A** | `web/package.json`, `web/panda.config.ts:39,76` | 4/0/1 | — | クローズ提案可 (視覚確認のみ未検証) |
| 13 | S1/Win yt-dlp→VideoPlayer 1曲完走 | **B** | `Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs`, `MediaCacheManager.cs` | 3/2/1 | Windows 実機 | ストリーム音声 (m4a) 再生と `audioS.Play()` を実機で |
| 17 | S2/Win 固定楽曲配列 → Cloud Catalog | **B** | `Assets/Game/Scripts/Songs/CloudCatalog.cs`, `GManager.cs:88` | 4/1/1 | Windows 実機 | 窓口の E2E のみ未検証 |
| 19 | S3/Mac Park UI メタデータ+Publish 接続 | **A** | `web/src/components/song-publisher/**`, `web/pages/publish.tsx` | 3/0/1 | — | Web 側は充足。Win 側は #6 のゲート |
| 21 | S4/Win optional リザルト送信+ランキング | **B** | `Assets/Game/Scripts/UI/RankingClient.cs:113,143,240` | 3/1/1 | Windows 実機 | 2 台目のクライアント表示の実測 |
| 22 | Release/Win Win64 展示版 smoke/配布ゲート | **B** | `docs/evidence/index.md`, `release/check/**` | 4/4/2 | Windows 実機 | S0 のみ partial。S1–S4 を詰める |
| 30 | Product Spec ゲーム体験の理想形 | **E** | `docs/ui/lyrics-sync.md` | 0/0/1 | オーナーの仕様確定 | HOLD/歌詞の操作・採点形状の確定待ち |
| 56 | Infra AIエージェント開発環境再整備 | **B** | `mise.toml:14-37`, `skills-lock.json` | 2/3/0 | — | `skills-check` / `evals/` / `.config/wt.toml` / `agent-config.yml` が欠落 |
| 58 | 検証 カタログ合成(30曲)+再生クロック | **B** | `GManager.cs:88`, `Assets/Tests/Editor/CatalogMergeTests.cs` | 2/1/2 | — | 「30曲」前提が stale (現在 29 行/28 選曲可)。基準の更新が必要 |
| 59 | 検証 #30 各機能の受入確認 | **B** | `LyricsTimeline.cs`, `KeyGuideUI.cs`, `LongNote.cs`, `GameHudView.cs` | 0/1/4 | — | 機能別 OK/NG/未検証 の記録と証跡が未作成 |
| 60 | Web/UI Foundation Pen v5 → Panda/Park | **A** | `web/src/theme/colors/{gray,blue,red,green,amber}.ts`, `docs/design/web-token-mapping.md` | 4/0/1 | — | クローズ提案可 (視覚一致のみ未検証) |
| 61 | Web UI Product Spec (Studio 全体) | **B** | `web/src/**` | 2/1/2 | — | 子 Issue 実装済み。製品レベル E2E 受け入れが未完 |
| 68 | Web/Catalog 本番 Catalog API 接続 | **A** | `web/src/app/catalog.ts:21,28,32,57,79` | 4/0/1 | — | クローズ提案可 |
| 71 | Web/Chart Session 正しい版を開く | **A** | `web/src/lib/song-session/load.ts` | 4/0/1 | — | クローズ提案可 |
| 73 | Web/Publish Flow モック禁止+実API | **A** | `web/src/lib/publish/transport.ts:25,31-35`, `web/pages/publish.tsx:140` | 4/0/1 | — | クローズ提案可 (#98 側は別) |
| 74 | Web/Responsive 操作不能 UI をなくす | **B** | `web/src/app/drawer.tsx:16-48`, `web/src/app/drawer.test.ts` | 3/1/3 | 視覚ゲート (#75) | PR #257 (狭幅横スクロール) 統合 + 5 viewport 撮影 |
| 75 | Web/Visual Gate 画像レビュー/本番E2E | **B** | `web/tests/visual/capture.mjs` | 2/2/1 | #74 | ゲートは **現在 exit 1 (FAIL)**: `home@390x844` 横溢 |
| 88 | Win/P0 全画面にならず UI 崩れる | **B** | `ProjectSettings/ProjectSettings.asset:107,119`, `Assets/Game/Tests/FullscreenPolicyTests.cs` | 2/1/2 | Windows 実機 | `displayCoverage ≥ 0.99` を実測 (現状 run-summary は `fullscreen=0`) |
| 96 | Unity/P1 Title/Result/Option/Opening 崩れ | **B** | `Assets/Game/Scripts/UI/V5/UISizeProfile.cs`, `V6/Common/V6UiProfile.cs:73-75` | 2/1/2 | Windows 実機 | 1024×768 / 1280×720 の実機撮影が未取得 |
| 98 | Web 既存曲メタ編集 + Publish 回復動線 | **B** | `web/src/lib/publish/draft.ts`, `web/pages/publish.tsx:140` | 2/1/1 | — | **chart 側ドラフト復元が未実装** (`web/pages/chart.server.ts:6`) |
| 118 | Update/P0 明示式アップデート | **A** | `Assets/Game/Scripts/Update/UpdateFlowModel.cs:17-20,136`, `UpdateChecker.cs:224,320` | 8/0/1 | — | クローズ提案可 (PR #255 で受入条件 8 項目固定済み) |
| 129 | Dev/Mac Desktop に独立 macOS アプリ | **A** | `Assets/Editor/BuildScript.cs:50` (`BuildStandaloneMacOS`) | 2/0/1 | — | クローズ提案可 (配布物は `~/Desktop/FlashBeat-Mac/`) |
| 134 | Ranking/P0 リザルト登録+曲別ランキング | **B** | `Assets/Game/Scripts/UI/RankingClient.cs`, `ResultRankingOverlay.cs:186,307` | 3/1/1 | — | **展示 patch line `b677f8f` に未移植** |
| 135 | Registration/P0 ブラウザだけで新規曲登録 | **A** | `web/src/components/new-song/**`, `web/src/lib/new-song/**`, `web/pages/new-song.tsx` | 5/0/1 | — | クローズ提案可 (Unity 反映の確認手順のみ未検証) |
| 140 | Unity/Font 豆腐を根絶 + 動的フォールバック | **B** | `Assets/Editor/JapaneseFontAssetBuilder.cs:33,104,233,249` | 3/1/1 | — | **展示 patch line に未適用** (`b677f8f` に `CollectCodeTexts` なし) |
| 162 | P0/REBUILD v0.1.13 UI 受け入れ撤回 | **B** | `docs/ui/issue-162-ui-contract.md` (F1–F18) | 3/3/2 | Windows 実機 | 実操作での受け入れが未了。子 #167 が残作業 |
| 167 | Game/P0 描画だけ UI 排除+全ボタン実動作 | **B** | `Assets/Tests/Editor/V5UiWiringTests.cs` (8 tests), `docs/ui/issue-167-handler-audit.md` | 2/1/2 | Windows 実機 | `specs/167` の 5 タスクが未チェック。実機操作受入が未了 |
| 182 | Game/P1 判定キュー再設計 | **A** | `Assets/Game/Scripts/Gameplay/JudgeQueue.cs:21` | 2/0/1 | — | 実装は main。PR #256 (受入条件固定) が open |
| 217 | P0/v6 Pencil UI 並列移植 (umbrella) | **D** | `Assets/Game/Scripts/UI/V6/**` | 1/1/2 | Windows 実機 + 子 Issue | 子 (#219–#241) の完了待ち |
| 219 | P0/v6 Game 8レーン遠近法プレイフィールド | **B** | `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldView.cs`, `V6LaneGeometry.cs:45,66,72` | 2/2/1 | — | **`V6UiMode.Playfield = false`** で無効化。`GameScene.unity:2837 m_IsActive: 0` |
| 221 | P0/v6 Select 3カラム View 再実装 | **A** | `Assets/Game/Scripts/UI/V6/Select/View/V6SelectView.cs` | 4/0/1 | — | クローズ提案可 |
| 222 | P0/v6 Select 実カタログ/検索/Ready 結線 | **A** | `Assets/Game/Scripts/UI/SelectSceneManager.cs:53`, `V6/Select/Data/V6SelectDataSource.cs:547` | 4/0/1 | — | クローズ提案可 |
| 223 | P0/v6 Integration 排他統合+実Win64 | **D** | — (統合ゲート) | 0/1/3 | Windows 実機 + Unity Editor 単独所有者 | #219–#222 の統合 + Win64 1 曲完走 |
| 224 | P1/v6 Game Pause + ローディング | **A** | `Assets/Game/Scripts/UI/V6/**` (Pause/Loading), `Assets/Game/Tests/V6PauseRuntimeTests.cs` 等 | 4/0/1 | — | クローズ提案可 (`specs/224` 1 タスクは Supervisor 実行) |
| 225 | NoteEditor/P1 曲切替で旧曲ノーツ混入 | **A** | `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs` (650daa6) | 3/0/1 | — | クローズ提案可 (PR #252 は追加ガード) |
| 226 | Gameplay/P1 曲終了判定 | **A** | `Assets/Game/Scripts/Gameplay/SongEndStateMachine.cs:78` | 3/0/1 | — | クローズ提案可 |
| 233 | P0 BLOCKER ゲーム画面崩壊/プレイフィールド不可視 | **B** | `Assets/Game/Scripts/UI/V6/V6GameRuntime.cs:370-374`, `V5/GameUiExclusivity.cs` | 1/2/2 | Windows 実機 | 確認窓 0.86s / `Lane` 非active / 旧歌詞二重描画の 3 点が未修正 |
| 239 | P0/v6 Front Door Title/History/Ranking | **A** | `Assets/Game/Scripts/UI/TitleSceneManager.cs:53`, `Assets/Game/Tests/V6HistoryRankingTests.cs` | 5/0/1 | — | クローズ提案可 |
| 240 | P0/v6 Result + 登録モーダル | **A** | `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:124,741,990` | 6/0/1 | — | クローズ提案可 (実機/実API は `specs/240` 3 タスク) |
| 241 | P0/v6 Option 4タブ整合 | **B** | `Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs:1044-1053` | 5/1/1 | — | **ClearSaveData が `FlashBeat.ScoreStore` を消していない** |
| 242 | P0/v6 Acceptance 全画面実Win64受入 | **D** | — (受入ゲート) | 0/0/3 | Windows 実機 | #223/#233 が blocker |
| 250 | Roadmap 全Issue完遂+11/14 展示 | **D** | — (ロードマップ) | 0/0/1 | #233/#223/#217/#242/#249 | #233 を最優先で解決 |

### 区分別件数

| 区分 | 件数 | Issue 番号 |
|---|---|---|
| **A** | **17** | 12, 19, 60, 68, 71, 73, 118, 129, 135, 182, 221, 222, 224, 225, 226, 239, 240 |
| **B** | **21** | 7, 13, 17, 21, 22, 56, 58, 59, 61, 74, 75, 88, 96, 98, 134, 140, 162, 167, 219, 233, 241 |
| **C** | **0** | — |
| **D** | **8** | 3, 4, 5, 6, 217, 223, 242, 250 |
| **E** | **1** | 30 |
| 合計 | **47** | |

> **C (未実装) が 0 件**なのは「歴史的に全件が実装済みだったわけではない」からではなく、**47 件すべてに `main` 上のコード or ゲート定義 or 仕様書が存在する**ため。未充足なのは個別の受入条件であり、それは各詳細節で file:line を伴って列挙した。

---

## 2. Issue 間依存関係

```
#3 (umbrella: 5 API 契約 + S0–S4)
├── #4  (Gate S0/S1)  ──→ #13 (S1 実装)
│                         └──→ #5 (Gate S2)
│                                  ├── #17 (Unity 動的カタログ)
│                                  ├── #12 (Mac 足場) → #19 (Web Publish) → #6 (Gate S3)
│                                  └── #7 (Gate S4 optional) ← #21 (Unity 送信)
├── #22 (Release ゲート) ← #4 / #5 / #6 / #7 の実 Win64 証跡
└── #250 (Roadmap) ← #233 / #223 / #217 / #242 / #225 / #226 / #249

#217 (v6 umbrella)
├── #219 (Playfield)  ──┐
├── #221 (Select View) ──┼──→ #223 (排他統合 + 実 Win64) ──→ #242 (全画面受入)
├── #222 (Select 結線) ──┤
├── #233 (実機復旧)    ──┘
├── #239 / #240 / #241 (Front Door / Result / Option)
└── #224 (Pause / Loading)

#217 の前提: #182 (判定キュー) → #219 の判定同期
#223 の前提: #219 + #221 + #222 + #233

#162 (UI 受け入れ撤回) ──→ #167 (v5 配線) ──→ #96 / #88 (解像度・全画面)
#30 (Product Spec) ──→ #59 (機能受入確認) / #58 (カタログ回帰)
#3 の契約 ──→ #61 (Web Studio 親仕様) ──→ #68 / #71 / #73 / #74 / #75 / #135
#56 (Infra) は全 Worker の前提 (worktree:setup / skills-check)
```

---

---

# 3. Issue 別詳細

## 3.1 v6 UI クラスタ (#250 / #242 / #241 / #240 / #239 / #233 / #226 / #225 / #224 / #223 / #222 / #221 / #219 / #217)

### #250 — [Roadmap] 高専祭後の全Issue完遂と11/14 U-16山口大会展示

- **区分**: **D (ブロック)** — 集約 Issue。単独で実装できる単位ではない
- **実装/証拠**:
  - 対応 Issue: `docs/evidence/index.md` (S0–S4 ステージ表), `docs/evidence/250-hold-rules-single-source.md`
  - commit: `2a0c833` "docs(evidence): #250 の Unity EditMode 検証結果を 1071/1071 で記録" (main ancestor)
  - テスト: `docs/evidence/index.md` §1.7 に EditMode **1071/1071 成功** を記録
- **受入条件**:

| # | 条件 (Issue 本文) | 判定 | 根拠 |
|---|---|---|---|
| 1 | #233 / #223: 実Win64 の同一artifact/SHA で Title→Select→Ready→Game→Result→Retry が通る | 未検証 | `docs/evidence/index.md` §1.7「Windows 実機 … **未証明**」。Win64 CI は `license-precheck` で停止 |
| 2 | #217 / #242 と各v6子Issue: 実データ・実操作に結線、描画重複なし | 未充足 | #219 は `V6UiMode.Playfield = false`、#233 に歌詞二重描画 |
| 3 | #225 / #226 のデータ破壊・プレイ不能リスクを評価し修正を検証 | 充足 | #225 = `650daa6` (main), #226 = `cfcd59f` (main) |
| 4 | #249: Win64 CI の Unity ライセンス問題を切り分けて復旧 | 未充足 | `docs/operations/unity-ci-license.md`。CI は Secret 未設定で停止 |
| 5 | 運営/スタッフが `FlashBeat.exe` を直接起動でき、回復・再起動・配布更新の運用手順が明確 | 未検証 | `docs/evidence/index.md` §1.5 に第 1/2/3 世代の配置表あり。手順書の diff は PR #254 (open) |

- **未検証の区別**: 条件 1・5 は **Windows 実機 / 実配布** での観測が必要なため **未検証**。代替検証は EditMode 1071/1071 と Mac 実機ログのみ (`docs/evidence/mac-verify-run-2026-11-08/`)。
- **依存**: #233 → #223 → #242 が本 Issue の最短経路。#249 (CI ライセンス) は #22/#4/#5/#6 をすべてブロックする。

### #242 — [P0/v6 Acceptance] 全画面の視覚・機能品質を実Win64で確認

- **区分**: **D (ブロック)**
- **実装/証拠**:
  - 本 Issue は **受入ゲート** (実装単位ではない)
  - 前提: `docs/evidence/index.md` §1.7「Pencil デザイン全画面一致 = **未受入**。3 解像度の受入は Windows 実機に残ります」
  - 対象画面の実装は `Assets/Game/Scripts/UI/V6/**` に存在 (§3.1 各子 Issue 参照)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | #220 解像度テンプレートと実値 HUD、ゲーム既存レーン/効果/判定は回帰確認対象 | 充足 | `V6/Common/V6UiProfile.cs:73-75` (FHD/2K/MacBook), `V6/Game/Hud/V6GameHudView.cs` |
| 2 | #221 / #222 Select View と実カタログ/操作 | 充足 | いずれも §3.1 で A 判定 |
| 3 | #223 / #233 統合・Canvas 排他・実Win64 の Game/Select 復旧 | 未充足 | #233 の 3 欠陥が未修正 (§3.1 #233) |
| 4 | #224 Pause / Loading | 充足 | `V6/Game/Pause/*`, `V6/Game/Loading/*` |
| 5 | #239 Title/History/Ranking, #240 Result/登録, #241 Option 4タブ | 未充足 | #241 の ClearSaveData 欠陥 (§3.1 #241) |
| 6 | 実Win64 で 3 解像度 (1920×1080 / 2560×1440 / 2560×1664) の受入 | 未検証 | Windows 実機必須。`docs/evidence/index.md` §1.7 |

- **未検証の区別**: 条件 6 は実機必須。代替検証 = Mac 実機 2560×1664 起動 (`docs/evidence/mac-verify-run-2026-11-08/`)、EditMode 緑、Pencil ノード座標の読み取り (**.pen の描画画像は未確認** — Issue 本文 21 行目も明記)。
- **依存**: **#233 が blocker** (Issue 本文 19 行目)。#223 の統合完了が前提。

### #241 — [P0/v6 Option] 既存設定の意味・保存範囲を保って4タブのUIを整合

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs` — 4 タブ実装。Play の NoteSpeed (`:822,986`)/NoteTiming (`:828,1009`)/Key Layout・キー設定 (`:843`)、Display の Flash (`:863-895`)、Sound の Main/Effect/BGM 音量 (`:926-938`, `:1019-1028`)、Data の cache size (`:951`) / clear (`:799`)
  - `Assets/Game/Scripts/UI/V6/Option/V6OptionLogic.cs`, `V6OptionViewDriver.cs`
  - テスト: `Assets/Game/Tests/V6OptionLogicTests.cs` (存在)
  - 永続化: `OptionSettings.Save()` を各変更で呼んでいる (`:870,876,885,895,988,1011,1028`)。`OptionSettings` 定義は `Assets/Game/Scripts/UI/OptionSceneManager.cs:29` (`public static class OptionSettings`, `Save()` は `:53`)
  - キー設定: `KeyBindings.ResetToDefaults()` (`:843`)
  - 音量即時反映: `AudioListener.volume = GManager.mainVolume` (`:1020`)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | Play: NoteSpeed / NoteTiming / Key Layout / キー設定を既存APIと値域で操作 | 充足 | `V6OptionView.cs:822,828,986,1009` (`OptionSettings.NoteSpeedMin/Max`, `NoteTimingMin/Max` で clamp) |
| 2 | Display: Flash / FlashTiming / FlashSpeed / Key Guide / Lyrics を接続、既存描画アルゴリズムは変更しない | 充足 | `V6OptionView.cs:863-895` (`GManager.Flash` / `FlashT` / `FlashBG` を既存値のままトグル) |
| 3 | Sound: Main / Effect / BGM へ即時反映 | 充足 | `V6OptionView.cs:1019-1028` |
| 4 | 設定の寿命: OptionSettings 永続分は保存/復元、Key Guide/Lyrics はセッション設定 | 充足 | `OptionSettings.Save()` 呼び出し群 (`V6OptionView.cs:870,876,885,895,988,1011,1028`) |
| 5 | Data: cache size / cache clear は既存 `MediaCacheManager` を使う | 充足 | `V6OptionView.cs:951` (`MediaCacheManager.FormatSize` / `GetCacheSizeBytes`) |
| 6 | **Clear save data は既存のハイスコア・キー設定の消去範囲を維持する** | **未充足** | `V6OptionView.cs:1045-1053` — `PlayerPrefs.DeleteKey("Hiscore")` と `OptionView.KeyBindingsPrefsKey` のみ削除。**`FlashBeat.ScoreStore` を消していない** |
| 7 | 確認/キャンセルと結果表示を一致させる | 未検証 | モーダルの実表示は Win 実機必須 |

- **未充足の具体箇所 (Issue 本文 13 行目が名指しした欠陥そのもの)**:
  ```csharp
  // Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs:1045
  private void ClearSaveData()
  {
      PlayerPrefs.DeleteKey("Hiscore");
      PlayerPrefs.DeleteKey(FlashBeat.UI.V5.OptionView.KeyBindingsPrefsKey);
      PlayerPrefs.Save();
      Array.Clear(GManager.Hiscore, 0, GManager.Hiscore.Length);
      KeyBindings.ResetToDefaults();
  }
  ```
  実際のベスト保存は `ScorePersistence` / `ScoreStore` 側。`Assets/Game/Scripts/Persistence/ScorePersistence.cs:276` に
  `public static void ResetAll()` があり `:280-281` で `ScoreStore.StorageKey` と `ScoreStore.LegacyStorageKey` を削除するが、
  **これを呼ぶのは `Assets/Game/Scripts/Persistence/SaveLoadManager.cs:54 ResetHiscore()` だけ**で、
  呼び出し元は `Assets/Game/Scenes/OptionScene.unity:11105` の UnityEvent (`m_MethodName: ResetHiscore`) のみ。
  `Assets/Game/Scripts/UI/**` からの呼び出しは **0 件** → v6 Option の Clear save data は v5 と **挙動が食い違う**。
  - 既存テスト: `Assets/Tests/Editor/ScoreStoreTests.cs` は `ScoreStore` の永続化を検証するが、**v6 の ClearSaveData が Store を消すことは検証していない**。
- **未検証の区別**: 条件 7 は実機必須。代替検証 = `V6OptionLogicTests.cs` の EditMode 緑 + コード読み。
- **依存**: Parent #217。#223 (排他統合) の Scene 切替で effective になる。

### #240 — [P0/v6 Result] 既存結果・登録操作へv6画面とモーダルを接続

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs` — `submittedThisResult` (`:124`), `OpenModal()` (`:741`), `HandleModalInput()` (`:990`), `Input.inputString` 1 文字取り (`:1020-1022`), ローカル名保存 (`:876`), 二重送信ガード (`:805,815`)
  - `Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs` — `LocalPlayerNamePrefsKey = "V5PlayerName"` (`:297`), `LocalNameMaxLength = 8` (`:300`), `IsSubmittable` (`:309`), `RegistrationMode` (`:325`), `NormalizeLocalName` (`:338`), `ClassifySubmitResult` (`:380`, 201/429/422)
  - `Assets/Game/Scripts/UI/V6/Result/V6ResultRegistration.cs:33` — `public static class V6LocalNameStore`
  - `Assets/Game/Scripts/UI/V6/Result/V6ResultViewDriver.cs:23` — RankingClient coroutine host
  - テスト: `Assets/Game/Tests/V6ResultRegistrationTests.cs`, `V6ResultLogicTests.cs`, `V6ResultSubmitTests.cs`, `Assets/Tests/Editor/V6ResultBackgroundTests.cs`
  - 証跡: `docs/evidence/240-result-registration.md`
  - `specs/240-result-registration/tasks.md` = **6 完了 / 3 未完了**。未完了 3 件は (1) EditMode 実実行 (Supervisor)、(2) 実機 Windows 確認、(3) 実オンライン送信確認 — **いずれも本 Issue 外の実行者が担当**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | score / grade / P/G/B/M / max combo / 曲情報 / 保存前best 差分 | 充足 | `V6ResultView.cs:1411` 付近 (`GManager.ActiveSongs` / `ScorePersistence` の実値を描画) |
| 2 | Retry と OK は既存リセット/遷移経路を使う、マウスと表示キーの両方から一度だけ発火 | 充足 | `submittedThisResult` ガード (`:268,805,815`) |
| 3 | 登録 = ローカル名前保存 と オンライン送信 の **別契約**。v6 の Register を一律オンラインへ変えない | 充足 | `V6ResultLogic.cs:325-329` `RegistrationMode()` が Online / Local を判定 |
| 4 | 既存)v6 モーダルで名前編集・Enter/Register が成立、送信中/成功/失敗/再試行を引き継ぐ | 充足 | `V6ResultView.cs:990-1046`, `V6ResultLogic.cs:380 ClassifySubmitResult` |
| 5 | モーダル入力中に背面 Retry / 遷移が反応しない | 充足 | `V6ResultView.cs:247` `HandleModalInput()` 内の入力ゲート + `submittedThisResult` ガード |
| 6 | 実機での画面確認 | 未検証 | `specs/240` 2 番の未完了タスク。Windows 実機必須 |
| 7 | 実オンライン送信の確認 | 未検証 | `specs/240` 3 番。実 API 呼び出しは禁止された環境のため未実施 |

- **未検証の区別**: 条件 6/7 は実機・実サービス必須。**既存テストは EditMode で、描画と通信の結果を含まない**。代替検証 = `V6Result*Tests.cs` 緑 + `docs/evidence/240-result-registration.md` のコードレビュー。
- **依存**: Parent #217。#223 (Scene 統合) で実 Checker に載る。

### #239 — [P0/v6 Front Door] 既存記録・操作を保ってTitle・History・RankingのUIを整合

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - Title: `Assets/Game/Scripts/UI/TitleSceneManager.cs:53` — `if (FlashBeat.UI.V6.Title.V6TitleView.Enabled)`
  - `Assets/Game/Scripts/UI/V6/Title/**`, `V6/History/**`, `V6/Ranking/**`
  - テスト: `Assets/Game/Tests/V6HistoryLogicTests.cs`, `V6HistoryRankingTests.cs`, `V6RankingLogicTests.cs`, `V6TitleLogicTests.cs`, `V6TitleRuntimeTests.cs`
  - 証跡: `docs/evidence/239-v6-front-door.md`
  - `specs/239-v6-front-door/tasks.md` = **12 完了 / 0 未完了**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | History は「プレイ済み曲の記録一覧」基準。ベストスコア順→時系列履歴へ変更しない | 充足 | `V6HistoryLogic.cs` / `V6HistoryRankingTests.cs` |
| 2 | Title の開始/History/Ranking/Option/終了、戻る・選曲導線を既存ハンドラーに接続 | 充足 | `TitleSceneManager.cs:53` + `V6TitleView.Enabled` 経路 |
| 3 | 曲情報・ベスト・Local/Online 区分は既存 `ScorePersistence` / `RankingClient` を使う | 充足 | `Assets/Game/Scripts/UI/RankingClient.cs` を v6 から参照 |
| 4 | 通信中/記録なし/失敗/対象外を混同しない | 充足 | `V6HistoryLogicTests.cs` / `V6HistoryRankingTests.cs` |
| 5 | 解像度ごとの配置・文字・余白を共通フレームから一貫表示 | 充足 | `V6/Common/V6UiProfile.cs:73-75` + `V6Frame.cs` |
| 6 | 撤回要件 (PlayHistoryStore / 総合順位サービス / 試聴) を **新規実装** しない | 充足 | `PlayHistoryStore` は `Assets/Game/Scripts/UI/V6/**` に存在しない。集計 API は **既に main に存在** (`web/routes/api/v1/stats.ts:96,121,144` の `players` / best players / most played) なので新設ではなく既存利用。`V6SelectView.cs:908` の "Players" パネルは `:917` で "データがありません" 空状態を持つ |
| 7 | 実機 3 解像度での受入 | 未検証 | Windows 実機必須 |

- **未検証の区別**: 条件 7 は実機必須。代替検証 = `docs/evidence/239-v6-front-door.md` + EditMode 緑 + Mac 実機 Title 描画 (`docs/evidence/mac-verify-run-2026-11-08/shot-02-TitleScene.png`)。
- **依存**: Parent #217。

### #233 — [P0 BLOCKER/Owner real PC] ゲーム画面崩壊・プレイフィールド不可視・HUD重複

- **区分**: **B (部分実装)** — cc 実測確定 (レーン非表示はレンダリング失敗ではない)
- **実装/証拠**:
  - 既存実装: `Assets/Game/Scripts/UI/V5/GameHudRuntime.cs`, `Assets/Game/Scripts/UI/V5/GameUiExclusivity.cs` (旧 v5 Canvas 排他), `Assets/Game/Scripts/UI/V6/V6GameRuntime.cs:370-374` `HideLegacyPlayfield()` → `HideByName("Lane")`
  - テスト: `Assets/Tests/Editor/GameUiExclusivityTests.cs`, `Assets/Tests/Editor/V6GameHudViewTests.cs`
  - 証跡: `docs/evidence/233-legacy-ui-exclusivity.md`, `docs/evidence/233-hud-nre.md`, `docs/evidence/233-hud-nre/{before,after}-fix-gameplay-2560x1664.png`
  - commit: `00074c0` (コンボボーナス Text 参照欠落 NRE 修正), `6e01a6b` (証跡), `fffc399` (PR #251 残差 5 件) — すべて main ancestor
  - `specs/233-game-screen-recovery/tasks.md` = 8 完了 / **2 未完了** (EditMode 実実行 / 実 Win64 1 曲完走)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 8レーン・判定ライン・ノーツが画面に出る | **未充足** | `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` `public static bool Playfield = false;` (owner rollback 2026-11-08)。`V6GameRuntime.cs:370-372` が `if (!V6UiMode.Playfield) return;` で早期 return |
| 2 | シーン側のレーン構造が存在する | **未充足** | `Assets/Game/Scenes/GameScene.unity:2832 m_Name: Lane` / **`:2837 m_IsActive: 0`** (blame: 原始 commit `ee11d76` 2024-11-12)。コード内に `Lane` を active にする enabler は **0 件** |
| 3 | HUD 重複・毎フレーム NRE がない | 充足 | `00074c0` でコンボボーナス Text 参照を修正。`docs/evidence/233-hud-nre/after-fix-gameplay-2560x1664.png` (2560×1664 Mac 実機) |
| 4 | 判定ライン・3D 判定エフェクトの描画 | 充足 (cc 実測) | cc が実機スクリーンショットで正常描画を確認 |
| 5 | 確認窓が設計意図 1.6 s | **未充足** | 実測 **0.86 s**。該当コードの確定箇所は本監査では特定できず (cc 実測値)。要 Owner/Worker の再特定 |
| 6 | v6 中に旧 v5 歌詞が二重描画されない | **未充足** | `V6UiMode.HideLegacyKeyGuide()` / `HideLegacyTitleUi()` はあるが **歌詞 (Lyrics) の旧描画を消す同等処理は未確認**。`Assets/Game/Scripts/UI/GameSceneManager.cs` の `ApplyTextPanelVisibility` が要確認 |
| 7 | 実 Win64 で 1 曲完走 | 未検証 | `specs/233` 2 番。Windows 実機必須 |

- **未検証の区別**: 条件 7 は実機必須。**EditMode 緑は画面表示・操作性を含まない**。cc の実機スクリーンショット観察 (判定ライン/エフェクト正常、撮影時刻が譜面空白区間) はレーン可視性の **代替にならない**。
- **依存**: #223 の前提条件。**#250 の最短経路の入口**。

### #226 — [Gameplay/P1] 曲終了をdurationだけで決めるため末尾ノーツが未判定で確定

- **区分**: **A (実装済み・証拠待ち)** — cc 実測確定
- **実装/証拠**:
  - `Assets/Game/Scripts/Gameplay/SongEndStateMachine.cs:78` — `public sealed class SongEndMachine`
  - `Assets/Game/Scripts/Gameplay/MediaTimeline.cs` — `HasSongEnded`
  - commit: `cfcd59f` (main ancestor)
  - テスト: `Assets/Tests/Editor/SongEndStateMachineTests.cs`, `Assets/Tests/Editor/MediaTimelineTests.cs`
  - 証跡: `docs/evidence/226-song-end-state-machine.md`
  - `specs/226-song-end-state-machine/tasks.md` = **5 完了 / 0 未完了**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 末尾ノーツの Miss 確定前に Result へ遷移しない (判定合計が noteCount に届く) | 充足 | `SongEndStateMachine.cs` の状態遷移 + `JudgeQueue` 連携 |
| 2 | durationSec > 実動画長でも、終端 + 未判定 0 で Result へ自動遷移 | 充足 | `MediaTimeline.HasSongEnded` を `SongEndMachine` が判定 |
| 3 | 末尾 Tap/HOLD 終端が durationSec 以下の譜面が API で受理される | 充足 | `contracts/schemas/chart.v1.schema.json` の `type` enum `[1,2]` (HOLD 許容) |
| 4 | 実機 1 曲完走 | 未検証 | 実 Win64 未実施 |

- **未検証の区別**: 条件 4 は実機必須。代替検証 = `SongEndStateMachineTests.cs` の EditMode 緑 + `docs/evidence/226-song-end-state-machine.md`。
- **依存**: #250 の A ゲート (2 番目) の 1 要素。

### #225 — [NoteEditor/P1] 曲切替で前の曲のノーツが混入し、次の曲のResources JSONへ上書き保存される

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs` — Deserialize を **置換トランザクション** 化 (先行パース → 旧 `NoteObject` を `Dispose()` → `EditData.Notes.Clear()` → 新データ適用)
  - `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs` — 曲切替時の再生位置/再生状態リセット (`OnFileSelected` 成功パス)
  - commit: **`650daa6`** "fix(noteeditor): 曲切替時に旧曲のノーツを破棄して保存データ破壊を防ぐ (#225)" — `git merge-base --is-ancestor 650daa6 origin/main` = **true**
  - テスト: `Assets/Tests/Editor/NoteEditorSongSwitchTests.cs` (5 tests, 163 行) — `git log --oneline -- <path>` = `650daa6` / `5b55248`
  - 証跡: `docs/evidence/225-noteeditor-song-switch.md`
  - `specs/225-noteeditor-song-switch/tasks.md` = **5 完了 / 0 未完了**
  - 追加 PR: **#252 (open)** `w/w225` — 「曲切替時に未保存変更を確認し、別曲の JSON を上書きしない」。**PR #252 は main に未マージ**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 曲切替時に旧曲のノーツが残らない | 充足 | `EditDataSerializer.cs` (`650daa6`) で `EditData.Notes` を Clear してから新データを適用 |
| 2 | 旧 NoteObject は破棄前に `Dispose()` (UniRx 購読のリーク防止) | 充足 | 証跡 `docs/evidence/225-noteeditor-song-switch.md` の「所有責務の確認」節 |
| 3 | 壊れた JSON で切替した場合、旧曲の状態が保持される | 充足 | 先行パース + `ArgumentException` で失敗、既存 `Assets/Tests/Editor/FlashBeatSongLoaderTests.cs` |
| 4 | ロングノーツ編集途中状態 (`LongNoteTailPosition`) が持ち越されない | 充足 | `650daa6` の diff に初期化を追加 |
| 5 | 曲切替時に再生位置/再生状態がリセットされる | 充足 | `FlashBeatSongLoader.OnFileSelected` 成功パス |
| 6 | 別曲の JSON を上書きしない (未保存変更の警告) | 未充足 | **PR #252 が open** (`w/w225`)。main には未マージ |
| 7 | Unity Editor 実機での曲切替→保存 | 未検証 | 展示 PC 操作禁止のため未実施。代替 = EditMode 5 件の緑 |

- **未検証の区別**: 条件 6 は **PR #252 が未マージ = コード未マージ** なので「未充足」。条件 7 は実機必須。
- **依存**: #250 の A ゲート 3 番目 (評価必須)。独立。

### #224 — [P1/v6] Game Pause・楽曲ローディングをPencilデザインに置換

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - Pause: `Assets/Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs`, `V6PauseRuntime.cs`, `V6PauseViewLogic.cs`
  - Loading: `Assets/Game/Scripts/UI/V6/Game/Loading/V6LoadingView.cs`, `V6LoadingViewLogic.cs`, `GameLoadingProgress.cs`
  - 既存 Pause ゲート: `Assets/Game/Scripts/Gameplay/PauseGate.cs`, テスト `Assets/Tests/Editor/PauseGateTests.cs`, `PauseMenuViewTests.cs`
  - テスト: `Assets/Game/Tests/V6PauseRuntimeTests.cs`, `V6PauseViewLogicTests.cs`, `V6LoadingViewTests.cs`, `V6LoadingViewLogicTests.cs`, `V6LoadingProgressTests.cs`; `Assets/Tests/Editor/LoadingOverlayLogicTests.cs`, `MediaLoadingOverlayTests.cs`
  - 証跡: `docs/evidence/224-pause-gate.md`, `docs/evidence/224-loading-progress.md`
  - `specs/224-pause-loading/tasks.md` = **13 完了 / 1 未完了** (未完了は「(Supervisor) 統合時に EditMode テストを実行」= 実行者の作業)
  - Loading 映像の白画面回避: `168789a` "fix(v6/loading): ローディング映像のテクスチャを実接続し、白画面を避ける"
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | Pencil の Pause/Loading ノードから状態/ボタン/動的文言/座標を契約化 | 充足 | `specs/224-pause-loading/` に spec/plan/tasks |
| 2 | Pause の Resume/Retry/Select が実際に動作、時間と音声が Pause 中停止し正しく復帰 | 充足 | `V6PauseRuntime.cs` + `PauseGate.cs` (`Assets/Tests/Editor/PauseGateTests.cs`) |
| 3 | Loading は実進捗と例外/Retry/Cancel を表示、loading.mp4 互換を検証 | 充足 | `GameLoadingProgress.cs` + `168789a` (テクスチャ実接続) |
| 4 | FHD/2K/Mac 画像とキーボード/マウス操作、既存 Loading/Pause 回帰テスト | 充足 (コード) / 未検証 (実機) | `V6UiProfile.cs:73-75` の 3 プロファイル。撮影は `docs/evidence/224-loading-progress.md` |
| 5 | P0 owner へ後方互換性のある差替手順と PR を提出 | 未検証 | 統合 owner の作業 (`specs/224` 手順) |
| 6 | 実 Win64 での Pause/Loading 操作 | 未検証 | Windows 実機必須 |

- **未検証の区別**: 条件 4 の「画像比較」と 6 は **Mac 実機 / Win 実機** が必要。**EditMode 緑は Pause の見た目と操作感を保証しない**。
- **依存**: Parent #217。#242 の完了対象 1 つ。

### #223 — [P0/v6 Integration] Game/SelectのEditor排他統合・実Win64プレイ・展示候補配布ゲート

- **区分**: **D (ブロック)**
- **実装/証拠**: 本 Issue は **統合ゲート**。コード所有は `Assets/Game/Scenes/**`, `Assets/Game/Scripts/UI/GameSceneManager.cs`, `Assets/Game/Scripts/UI/V5/GameHudRuntime.cs` (Issue 本文 74 行目)
  - 既存統合の状態: `Assets/Game/Scripts/UI/GameSceneManager.cs:87` — `if (FlashBeat.UI.V6.V6UiMode.GameHud)`; `Assets/Game/Scripts/UI/SelectSceneManager.cs:53` — `if (V6UiMode.SelectScreen)`
  - `Assets/Game/Scripts/UI/V6/V6UiMode.cs:15,18` — `GameHud = true`, `SelectScreen = true` / `:27` `Playfield = false`
  - テスト: `Assets/Tests/Editor/GameUiExclusivityTests.cs` (二重 Canvas / Raycaster 排他)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 実行環境/base SHA/Editor Lock/Library/worktree/owned paths を確認 | 充足 (運用) | `docs/evidence/index.md` §1.6/§1.7 |
| 2 | 新 v6 UI をシーンに挿入、二重 Canvas/GraphicRaycaster/EventSystem/二重発火/旧 v5 前面化を防ぐ | 充足 | `GameUiExclusivityTests.cs` + `V6UiMode` フラグ |
| 3 | EditMode + 実バイナリビルド、FHD/2K/Mac 実測、Pencil 差分の記録 | 未検証 | Mac 実機 Title のみ (`docs/evidence/mac-verify-run-2026-11-08/`)。3 解像度のゲーム画面は未取得 |
| 4 | **Win64 で本番相当の Title→Select→Ready→Game→Pause/戻る→Result の 1 曲完走** | 未検証 | `docs/evidence/index.md` §1.7「Windows 実機 … **未証明**」。CI は `license-precheck` で停止 |
| 5 | 展示候補の配布ゲート | 未検証 | `docs/evidence/index.md` §1.5 に 3 世代の配置表ありだが、v6 統 合ビルドの配布記録なし |

- **未検証の区別**: 条件 3–5 は **Windows 実機 / Win64 ビルド** 必須。代替検証 = `GameUiExclusivityTests.cs` の EditMode 緑、Mac 実機ログ (`Player.log` 例外 0 行)、`V6UiProfile` の座標読み取り。
- **依存**: **#219 (Playfield 有効化) + #221 + #222 + #233** が前提。→ #242 → #250。

### #222 — [P0/v6 Select] 実カタログ・検索/絞り込み・Ready/Backと曲IDをv6画面に結線

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `Assets/Game/Scripts/UI/SelectSceneManager.cs:53` — `if (V6UiMode.SelectScreen)` で v6 経路へ
  - `Assets/Game/Scripts/UI/SelectSceneManager.cs:353-421` — Cloud Catalog 取得と反映
  - `Assets/Game/Scripts/UI/V6/Select/Data/V6SelectDataSource.cs:539,547` — `FetchCatalog` (`GET /api/v1/catalog`)
  - `Assets/Game/Scripts/UI/V6/Select/Data/V6SelectDataLogic.cs`, `IV6SelectDataSource.cs`
  - テスト: `Assets/Game/Tests/V6SelectDataLogicTests.cs`, `Assets/Tests/Editor/CloudCatalogTests.cs` (11 件), `CatalogMergeTests.cs`, `SceneVideoCatalogOrderTests.cs`
  - 証跡: `docs/evidence/221-222-select-controls.md`
  - `specs/221-222-select-v6/tasks.md` = **23 完了 / 0 未完了**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | `GManager.ActiveSongs` / CloudChartHolder / SelectSceneManager / KeyActions と v6 View の連携契約、元カタログの意図しない変更なし | 充足 | `GManager.cs:88 ActiveSongs`, `SelectSceneManager.cs:353-421` |
| 2 | 検索・非一致状態・絞り込み・曲送り・スクロール・詳細表示・Ready を実結線、マウスで開始できる | 充足 | `V6SelectDataLogic.cs` + `V6SelectViewLogic.cs` |
| 3 | BestScoreOf とランキング API の実値、emptiness/loading/failure を明示、例の数値を使わない | 充足 | `V6SelectDataSource.cs` の実 API 取得 + `V6SelectDataLogicTests.cs` |
| 4 | Cloud/内蔵曲の追加・並べ替え・削除、45曲以上、songId/chartVersion、フィルタ状態でも正しい曲へ | 充足 | `CloudCatalogTests.cs` / `CatalogMergeTests.cs` |
| 5 | Ready 連打・重複 Scene 遷移のテスト、#183 のプレイセッション固定と #189 stable score 移行を退化させない | 充足 | `Assets/Tests/Editor/PlaySessionSnapshotTests.cs`, `ScoreCatalogIntegrityTests.cs` |
| 6 | View PR を追跡し、統合 owner へ handoff | 充足 | `docs/evidence/221-222-select-controls.md` |
| 7 | **SelectScene 実機で選曲→Game シーン遷移** | 未検証 | Windows 実機必須 |

- **未検証の区別**: 条件 7 は実機必須。**EditMode 緑は遷移の描画・操作感を保証しない**。代替 = `V6SelectDataLogicTests.cs` 緑 + `CloudCatalogTests.cs` 緑。
- **依存**: Parent #217。#221 (View) が先行。

### #221 — [P0/v6 Select] 3カラム選曲画面のv6 Viewを再実装

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `Assets/Game/Scripts/UI/V6/Select/View/V6SelectView.cs` (View), `V6SelectViewLogic.cs` (状態), `V6SelectViewDriver.cs`
  - 共通: `Assets/Game/Scripts/UI/V6/Common/V6Frame.cs`, `V6Controls.cs`, `V6Layout.cs`, `V6Panel.cs`, `V6Lists.cs`, `V6Tokens.cs`
  - プロファイル: `V6/Common/V6UiProfile.cs:73-75` — FHD 1920×1080 origin (96,54) / 2K 2560×1440 (416,234) / MacBook 2560×1664 (416,346)
  - テスト: `Assets/Game/Tests/V6SelectViewLogicTests.cs`
  - 証跡: `docs/evidence/221-222-select-controls.md`
  - `specs/221-222-select-v6/tasks.md` = 23/0
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 共通 frame + OENRv descendants を合成し、左曲リスト(504×72 行)/中央選択中カード(536×780)/右 Ranking の原寸配置・色・フォント・部品を確定 | 充足 | `V6Frame.cs` + `V6Lists.cs` + `V6Panel.cs` の各プロファイル別配置 |
| 2 | uGUI/TMP で v6 画面構造・スクロール可能曲リスト・選択状態・ジャケット/サムネイル・曲メタデータ・Best/ランキングを実装。選択/曲数変化に追従する再描画 API | 充足 | `V6SelectView.cs` + `V6SelectViewLogic.cs` |
| 3 | 検索文字入力・絞り込み・0件状態・読込中/失敗・選択切替の View 状態と操作イベントを定義。装飾の疑似ボタン禁止。Ready/Back/Filter はクリック/キーボード両経路に接続可能 | 充足 | `V6SelectViewLogic.cs` (`V6SelectViewLogicTests.cs`) |
| 4 | 1920×1080 / 2560×1440 / 2560×1664 で重なり/欠け・キー表記・読みやすさを撮影比較 | 未検証 | 撮影は Win 実機必須。座標の **設計値** は `V6UiProfile.cs:73-75` で確認可能だが、**描画結果の比較は未実施** |
| 5 | Presenter から実データを渡せる明確な契約 | 充足 | `IV6SelectDataSource.cs` + `V6SelectDataSource.cs` |

- **未検証の区別**: 条件 4 は実機撮影必須。**プロファイル座標の読取は実描画の比較に代わらない** (clipping は座標からは判定できない)。
- **依存**: Parent #217。#222 が結線担当。

### #219 — [P0/v6 Game] 8レーン遠近法プレイフィールド・ノーツ・判定ラインを移植

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldView.cs`, `V6PlayfieldRuntime.cs`, `V6LaneGeometry.cs`
    - `V6LaneGeometry.cs:45` `JudgeLineY = 792f`, `:66 JudgeLineHeightDesignPx = 4f`, `:72 JudgeGlowY`, `:47` レーン数 = `Judge.LaneKeys` と同一定義
  - テスト: `Assets/Game/Tests/V6PlayfieldGeometryTests.cs`
  - 証拠の不存在: `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` — `public static bool Playfield = false;` (owner rollback 2026-11-08)。`Assets/Game/Scripts/UI/V6/V6GameRuntime.cs:370-372` — `if (!V6UiMode.Playfield) return;` のため `HideLegacyPlayfield()` → `:374 HideByName("Lane")` は **実行されない**
  - シーン側: `Assets/Game/Scenes/GameScene.unity:2832 m_Name: Lane` / `:2837 m_IsActive: 0`
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 既存 GameScene / NotesManager / Judge / 動画背景 / レーン描画の追跡、判定・HOLD の再実装なし | 充足 | `V6LaneGeometry.cs:47` が `Judge.LaneKeys` と同一定義を参照 |
| 2 | v6 の 8 レーン遠近形状・判定ライン・ノーツを **実プレイデータに投影** する描画にする (静かな設計を貼って誤魔化さない) | 充足 (コード) | `V6PlayfieldView.cs:38,118-124` が `NoteLayer` (uGUI "Notes" RectTransform) を提供し、**既存の `Assets/Game/Scripts/Gameplay/Notes.cs:110-129,158-165` が head/body の Image を `NoteLayer` に生成**して `V6LaneGeometry.NoteColor` / `LaneCount` を使う。`Notes.cs:181` がフレームごとに `V6PlayfieldView.Current` を参照 |
| 3 | 通常/ロング/同時押し/Miss/レーン別判定エフェクトが現行 Time/NoteIndex/Judge と一致 | 充足 (コード) | `Notes.cs:129` (HOLD の body Image)、`V6PlayfieldView.cs:155-165` (レーンフラッシュは `KeyBindings.IsLaneKeyDown(lane)` 駆動) |
| 4 | レーン描画/時間進行/リサイズの EditMode ロジックテスト、FHD/2K/Mac でフレーム切れ/ズレ/上下反転の画像比較 | 充足 (EditMode) / **未検証 (画像比較)** | `V6PlayfieldGeometryTests.cs` 存在。画像比較は実機必須 |
| 5 | 統合 Worker が GameScene へ一度で導入できるよう公開API/接続手順/所有オブジェクト/必要なアセットを PR に明記 | 充足 | `specs/` に接続手順あり |
| 6 | **8 レーンとノーツが実プレイで動き、判定と描画が合い、動画が背景として再生される** | **未充足** | `V6UiMode.Playfield = false` で描画自体が無効。加えて `GameScene.unity:2837` の `Lane` が `m_IsActive: 0` で、シーン側の構造も無い |

- **未充足の具体箇所**:
  1. `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` → `Playfield` を `true` にする (owner rollback の再承認が要る = オーナー判断待ち)。現状 `V6PlayfieldRuntime.cs:52` の `if (!V6UiMode.Playfield) return;` で **Canvas 自体が作られない**
  2. `Assets/Game/Scenes/GameScene.unity:2837` → `Lane` の `m_IsActive: 0` を戻し、コード側から active にする enabler を追加 (grep で enabler 0 件)。`Notes.cs:25,111` は `V6PlayfieldView.Current == null` のとき **3D 描画へフォールバック**する設計だが、フォールバック先の `Lane` が非 active なので **どちらの描画も出ない**
  3. `Assets/Game/Scripts/UI/V6/V6GameRuntime.cs:370-374` → `HideLegacyPlayfield()` が `Playfield` 有効時にのみ `HideByName("Lane")` を呼ぶ設計は legacy 排除として正しいが、**この状態では旧 v5 側のレーンも隠れない** (= 1 の再承認と 2 の enabler 追加が同時に必要)
- **未検証の区別**: 条件 4 の画像比較、条件 6 の実プレイは **実機必須**。**EditMode 緑は描画上の問題を検出しない** (フラグで無効化されている箇所は純論理的テストを通る)。
- **依存**: **#217 の子**。#182 (判定キュー) が判定同期の前提。→ #223 → #242。

### #217 — [P0/v6] Pencil確定UIをUnityに並列移植 — Game/Select最優先

- **区分**: **D (ブロック)**
- **実装/証拠**:
  - umbrella。子 Issue の実装は `Assets/Game/Scripts/UI/V6/**` に集約済み (Common/Game{Playfield,Hud,Loading,Pause}/History/Option/Ranking/Result/Select/Title)
  - `Assets/Game/Scripts/UI/V6/V6UiMode.cs` — 機能フラグの正本
  - `Assets/Game/Scripts/UI/V6/Common/V6UiProfile.cs:73-75` — 3 解像度プロファイル
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | P0 Game: 8レーン遠近法プレイフィールド・ノーツ・判定ライン、実動画背景、進捗/Grade/Score/Gauge/Combo/Lyrics/レーンキー・判定。静的デザイン貼付禁止 | **未充足** | #219 の 2 欠陥 (フラグ false / Lane 非 active) |
| 2 | P0 Select: 左560/中央536/右560、検索/フィルタ/Ready/戻る/該当なし、Cloud/ローカル実カタログ、songId/chartVersion | 充足 | #221 + #222 で充足 |
| 3 | 必須検証解像度 1920×1080 / 2560×1440 / 2560×1664 | 未検証 | Win 実機必須 (`docs/evidence/index.md` §1.7) |
| 4 | 旧 v5 / legacy / v6 の描画重複がない | 未充足 | #233 の歌詞二重描画 |
| 5 | ブランチ丸ごとマージ禁止、最新 main で作業 | 充足 (運用) | 本監査ブランチも main (`ab8c336`) 基準 |

- **未検証の区別**: 条件 3 は実機必須。**Pencil の座標読取は「デザインに clipping がない」証明であって「実装に重なりがない」証明ではない** (Issue #242 本文 21 行目も同旨を明記)。
- **依存**: 子の #219 #221 #222 #224 #233 #239 #240 #241 が未完了。**#233 と #219 が effective なボトルネック**。

---

## 3.2 Unity ゲームプレイ / 既存 UI クラスタ (#182 / #167 / #162 / #140 / #135 / #134 / #129 / #96 / #88)

### #182 — [Game/P1] 判定キューをレーン別・時刻順に再設計

- **区分**: **A (実装済み・証拠待ち)** — cc 実測確定
- **実装/証拠**:
  - `Assets/Game/Scripts/Gameplay/JudgeQueue.cs:21` — `public class JudgeQueue` (レーン分割 + 時刻順)
  - `Assets/Game/Scripts/Gameplay/Judge.cs` — `EnsureQueue` でキュー経由の判定に統一
  - commit: `ad20d21` / `6d9a4ae` / `f953eea` — **すべて `git merge-base --is-ancestor <sha> origin/main` = true**
    - `f953eea` "refactor(gameplay): HOLD の判定ルールを LongNote に集約し、テストと実挙動の分離を解消"
  - テスト: `Assets/Tests/Editor/JudgeQueueTests.cs`, `JudgeLogicTests.cs`, `PublishedChartJudgeRegressionTests.cs`, `LongNoteTests.cs`
  - 証跡: `docs/evidence/182-publish-to-judge-regression.md`
  - **PR #256 (open)** `w/w182-close` — 「判定の確定数 = noteCount とスコア API の整合を固定し、HOLD 中断の二重計数を直す」。**main には未マージ**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | Web で後ろ→前順にノーツ追加 or 既存ノーツを前へ移動して公開しても、見えるノーツを正しい時刻に判定できる | 充足 | `JudgeQueue.cs:21` + `Judge.cs` の `EnsureQueue` 結線 |
| 2 | 時刻順に並んだ譜面でも、他レーンの直前のノーツを見逃すと後続レーンの正常入力が無視されない | 充足 | `JudgeQueueTests.cs` (レーン分割キュー) |
| 3 | 判定合計が noteCount に一致する | 未充足 | PR #256 が未マージ。`68e1183` でテスト期待値は実挙動に合わせ済みだが、**noteCount 一致の固定は PR #256 側** |
| 4 | HOLD 中断の二重計数がない | 未充足 | 同上 (PR #256 の修正対象) |
| 5 | 実プレイでの通し確認 | 未検証 | 実 Win64 必須 |

- **未検証の区別**: 条件 5 は実機必須。**EditMode 緑は譜面順序の実挙動を保証しない**。代替 = `JudgeQueueTests.cs` 緑 + `docs/evidence/182-publish-to-judge-regression.md`。
- **依存**: #217 の子 #219 (判定と描画の同期) の前提。**PR #256 の統合で A の受入条件が完全に固定される**。

### #167 — [Game/P0] Unity全画面の描画だけのUIを排除し全ボタンを実動作へ接続

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `Assets/Tests/Editor/V5UiWiringTests.cs` — **8 テスト** (テスト名と行):
    - `ScreenCanvas_CarriesGraphicRaycaster` (`:20`)
    - `TitleCanvas_CarriesGraphicRaycaster_AndMenuButtonCallsRealHandler` (`:40`)
    - `OptionCanvas_CarriesGraphicRaycaster_AndBackButtonCallsRealHandler` (`:69`)
    - `ResultCanvas_CarriesGraphicRaycaster_AndRetryButtonCallsRealHandler` (`:91`)
    - `ReadyOptions_AreForwardedToGameHud` (`:137`)
    - `SelectReadyRow_WritesPlaySession` (`:199`)
    - `PauseDisplay_HasNoDeadRows` (`:246`)
    - `OptionSettings_SaveAndLoad_RoundTrips` (`:279`)
  - `docs/ui/issue-167-handler-audit.md` — F1〜F7 の欠陥と修正 (入力二重発火 / suspend 漏れ / F2 の二重割当 / Esc 同一フレーム裏渡し / ポーズとエラーダイアログ / NoteSpeed clamp 不一致 / カタログ更新停止)
  - `docs/ui/issue-167-ui-acceptance.md` — 実機受入手順 (手順のみ)
  - `Assets/Game/Scripts/UI/SceneInputGate.cs` — F4 の新規コンポーネント
  - commit: `36da73c` "fix(ui): v5 のdestroyをEditモードでも安全にし、代表キーの実バグを直す (#167)" (main ancestor), `5f11ce8`, `d4d758a`, `880aa56`
  - `specs/167-v5-ui-functional-wiring/tasks.md` = **0 完了 / 5 未完了** (Issue 本文の T1–T3 も未チェック)
- **受入条件**:

| # | 条件 (Issue 本文 T1–T3) | 判定 | 根拠 |
|---|---|---|---|
| T1 | FHD/2K/Mac の各解像度で実Windows/Unity画面を取得し、pointer hitbox / Raycaster / EventSystem / input mode / handler・state遷移の監査表を作る | 未充足 | 監査表 `docs/ui/issue-167-handler-audit.md` はあるが **コード読解による静的監査**。Issue 本文の「実Windows画面取得」は未実施 |
| T2 | Title/Select/Ready の全主要操作をクリック・キーボード両方で機能接続。設定保存/次画面反映を検証 | 充足 (コード) / 未検証 (実操作) | `TitleCanvas_...CallsRealHandler`, `SelectReadyRow_WritesPlaySession`, `ReadyOptions_AreForwardedToGameHud` |
| T3 | Game/Pause/Option/Result の全主要操作を実機で動作確認、Ranking/履歴/設定は実処理に接続し偽値を除く | 未充足 | `issue-167-handler-audit.md` の F1–F7 はコード修正まで。**実機動作確認は未実施** |
| 追加 | 「Owner実操作が合格条件」 | 未検証 | Issue 本文 80 行目。Windows 実機必須 |

- **未検証の区別**: **「実Windows/Unity画面を取得」と「Owner実操作」が受入条件そのもの**。`V5UiWiringTests.cs` 緑は **Canvas/Raycaster の静的構造とハンドラ接続を検証するだけにすぎず、クリック・キー操作の実挙動・画面遷移・描画は含まない**。代替 = 8 テスト緑 + 静的監査表 (F1–F7)。
- **依存**: Parent #162。→ #96 / #88 の前提。

### #162 — [P0/REBUILD] v0.1.13 のUI受け入れ撤回：全画面・全画面遷移・実操作を機能から再構築

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `docs/ui/issue-162-ui-contract.md` — **タスク2 の機能UI契約表**。6 シーン (`ProjectSettings/EditorBuildSettings.asset:9-24`) × 入力/ハンドラ/効果/フィードバック/根拠 を file:line 付きで F1–F18 として記録
  - `docs/ui/pr204-playtest-checkpoints.md` — 実プレイの受入チェックポイント (手順)
  - 全画面方針: `ProjectSettings/ProjectSettings.asset:119` `fullscreenMode: 1`, `:107` `resizableWindow: 0`, `defaultIsNativeResolution: 1`
  - テスト: `Assets/Game/Tests/FullscreenPolicyTests.cs` — `ProjectSettings_FullScreenMode_IsFullScreenWindow`, `ProjectSettings_DefaultIsNativeResolution_IsTrue`, `ProjectSettings_ResizableWindow_IsFalse`, `GameScripts_HaveNoScreenSetResolution`, `JsonMacScreen_Is2560x1664`, `JsonFhdScreen_Is1920x1080`, `JsonTwoKScreen_Is2560x1440`, `FhdProfile_MatchesJson`, `TwoKProfile_MatchesJson`, `MacProfile_MatchesJson`
  - テスト: `Assets/Tests/Editor/WindowBaselineTests.cs`, `V5UiTests.cs`, `V5GameHudTests.cs`, `TitleOptionV5Tests.cs`
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 全画面表示になる (PlayerSettings 設定だけでは不十分) | 充足 (コード) / 未検証 (実機) | `ProjectSettings.asset:107,119` + `FullscreenPolicyTests.cs` 4 テスト |
| 2 | 画面配置・操作可能領域が成立 | 未充足 | `docs/ui/issue-162-ui-contract.md` が A-1 (Ranking 未接続) / B-3 / C-2 (凡例と実挙動の不一致) 等を defect として記録 |
| 3 | 見た目だけで内部機能がない UI がない | 充足 (コード) | `V5UiWiringTests.cs` 8 テスト + `issue-167-handler-audit.md` の F1–F7 修正 |
| 4 | 旧 Canvas を `SetActive(false)` にして v5 動的 Canvas を表示する画面で、旧機能の表示/遷移が落とらない | 充足 | `Assets/Tests/Editor/GameUiExclusivityTests.cs` |
| 5 | 実機での受入 | 未検証 | `docs/ui/pr204-playtest-checkpoints.md` は手順のみ。Windows 実機必須 |

- **未検証の区別**: 条件 5 は実機必須。**契約表は「コード読解による静的監査」であり、自筆が「実プレイ証拠は無い」と明記** (`docs/ui/issue-162-ui-contract.md` 5 行目)。代替 = EditMode 緑 + 静的契約表。
- **依存**: #167 → #96 / #88 の上位。

### #140 — [Unity/Font] 日本語の □ (豆腐) を根絶: コード内文字列を文字セットに追加 + 動的フォールバックフォント

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `Assets/Editor/JapaneseFontAssetBuilder.cs:233` — `CollectCodeTexts()` (スクリプト内文字列を収集)
  - `Assets/Editor/JapaneseFontAssetBuilder.cs:249` — `EnsureDynamicFallback()` 定義 / `:104` — 呼び出し
  - `Assets/Editor/JapaneseFontAssetBuilder.cs:33` — `DynamicFallbackPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Dynamic SDF.asset"` (実在、6513 bytes)
  - テスト: `Assets/Tests/Editor/JapaneseFontCodeStringCoverageTests.cs` — `CodeStringLiterals_AreCoveredByMainAtlasOrDynamicFallback` (`:38`), `DynamicFallback_IsRegisteredOnTheMainFontAsset` (`:79`); `Assets/Tests/Editor/JapaneseFontCoverageTests.cs`
  - commit: `aa5e908` "test(#140): コード内文字列の字形カバレッジ EditMode テストを追加 / fix(#88): 全画面カバレッジ閾値を 0.99 に" (main ancestor), `8a50bab` (merge `w/w140fix`)
  - 証跡: `docs/evidence/140-font-code-strings.md`
  - **展示 patch line には未適用**: `git ls-tree -r --name-only b677f8f` に `CollectCodeTexts` / `EnsureDynamicFallback` は **0 件** (`JapaneseFontAssetBuilder.cs` 自体はあるが旧版)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | `Assets/Game/Scripts/**/*.cs` の日本語を文字セットに追加して再生成 | 充足 | `JapaneseFontAssetBuilder.cs:233 CollectCodeTexts()` + `aa5e908` のテスト |
| 2 | 動的 NotoSansJP をフォールバックとして追加し、足りない文字を実行時に生成 | 充足 | `JapaneseFontAssetBuilder.cs:249 EnsureDynamicFallback()` + `NotoSansJP-Dynamic SDF.asset` |
| 3 | フォールバックが主フォントアセットに登録されている | 充足 | `JapaneseFontCodeStringCoverageTests.cs:79` |
| 4 | コード内文字列の字形が主アトラス or 動的フォールバックで必ずカバーされる | 充足 (EditMode) | `JapaneseFontCodeStringCoverageTests.cs:38` |
| 5 | **trunk と展示 patch line の両方へ** | **未充足** | 展示 patch line `b677f8f` に `CollectCodeTexts` / `EnsureDynamicFallback` が **0 件** |
| 6 | 実機画面での豆腐 0 | 未検証 | `docs/evidence/win64-2026-11-06-font/` は存在するが、**本監査では実画像の中身を検査していない** → 観測項目として記録 |

- **未検証の区別**: 条件 4 は **EditMode 静的カバレッジ** であって **実描画の豆腐 0 ではない** (字形が存在してもアトラス配置の問題は別)。代替 = EditMode 緑 + `docs/evidence/140-font-code-strings.md`。
- **依存**: #88 の受入条件 3 (日本語フォント欠落 0) と連動。展示 patch line 側は #22/#250 の配布対象と同時対応。

### #135 — [Registration/P0] Web Studio: 新規楽曲の登録をブラウザだけで完結

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `web/pages/new-song.tsx`, `web/pages/new-song.server.ts`
  - `web/src/components/new-song/` — `new-song-flow.tsx` (`:16` `resolvePublishTransport` / `:216` `parseYouTubeId` / `:137` published state / `:30-31`「token はメモリのみ。API が応答し published データを読み戻すまで成功表示しない」), `song-info-step.tsx`, `chart-step.tsx`, `publish-step.tsx`, `publish-success.tsx`, `stepper.tsx`, `duration-probe.tsx`
  - `web/src/lib/new-song/` — `draft.ts`, `slug.ts` (重複チェック), `oembed.ts` (YouTube videoId / 長さ取得), `validation.ts`, `iframe-api.ts`, `fixture-api.ts`
  - テスト: `web/src/components/new-song/new-song-flow.test.tsx`, `web/src/lib/new-song/__tests__/{draft,slug,validation,oembed,fixture-api}.test.ts`
  - `specs/135-new-song/tasks.md` = **5 完了 / 0 未完了**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 新規 songId (slug) 入力と重複チェック | 充足 | `web/src/lib/new-song/slug.ts` + `__tests__/slug.test.ts` |
| 2 | YouTube URL 貼り付けから videoId / 長さ取得 | 充足 | `web/src/lib/new-song/oembed.ts` + `__tests__/oembed.test.ts`、`new-song-flow.tsx:216` |
| 3 | 譜面エディタとの往復 | 充足 | `new-song-flow.tsx` の `chart-step.tsx` 連携 (`web/src/components/chart-editor/**`) |
| 4 | Publish の検証付き成功表示 | 充足 | `new-song-flow.tsx:30-31` + `publish-success.tsx` |
| 5 | Unity カタログに反映されるまでの確認手順 | 充足 (手順) / 未検証 (実行) | `web/src/lib/publish/transport.ts:25` + 手順書。**反映の実測は未実施** |

- **未検証の区別**: 条件 5 の実行部は **実 API + Unity 実機の双方が必要**。**Vitest 緑は本番 API への publish 成功を意味しない** (`transport.ts:31-35` は fixture が明示許可されない限り例外を投げる設計)。代替 = Vitest 緑。
- **依存**: #61 (Web Studio 親仕様) の子、#73 (Publish Flow) の上に成り立つ。#3 の S3 ゲート。

### #134 — [Ranking/P0] Unity: リザルトでスコア登録 (名前入力) と曲別ランキング表示

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `Assets/Game/Scripts/UI/RankingClient.cs:113` (`POST /api/v1/scores`), `:143` `BuildScoreRequestJson`, `:240` `PostScore`, `:318` `UnityWebRequest(url,"POST")`
  - `Assets/Game/Scripts/UI/ResultRankingOverlay.cs:186` `GetLeaderboard`, `:307` `PostScore`
  - `Assets/Game/Scripts/UI/RankingPanel.cs`, `Assets/Game/Scripts/UI/V6/Result/V6ResultViewDriver.cs:23`
  - テスト: `Assets/Tests/Editor/RankingClientTests.cs` — 11 テスト (`ValidateNickname_TrimsAndAcceptsAsciiAndKana` `:26`, `BuildScoreRequestJson_ContainsSubmissionFields` `:50`, `ParseLeaderboard_ReadsEntries` `:100`, `Urls_FollowTheOpenApiContract` `:142`, `IsValidSongId_FollowsTheServerPattern` `:152`, `Nickname_RoundTripsThroughPlayerPrefs` `:165` ほか)
  - サーバ側: `web/routes/api/v1/scores.ts` — サーバが `computeUnityScore` で **再計算** し、一致しないクライアント値には 422
  - **展示 patch line には未移植**: `git ls-tree -r --name-only b677f8f` に `RankingClient.cs` / `ResultRankingOverlay.cs` は **0 件**
  - 関連ブランチ: `origin/134-patch` に `615db66` "feat(ranking): festival ranking client + scene-independent overlays (#134)" と `5067329` (main 未マージ)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | リザルトで『ランキング登録』(ニックネーム入力、Enter 登録 / Esc スキップ) | 充足 | `RankingClient.cs:205 SaveNickname` + `ResultRankingOverlay.cs:307` |
| 2 | その曲の Top10 + 自分の順位を表示 | 充足 | `ResultRankingOverlay.cs:186 GetLeaderboard` + `web/routes/api/v1/leaderboards/**` |
| 3 | シーン非依存のオーバーレイ (KeyConfigUI と同方式) | 充足 | `ResultRankingOverlay.cs` は独立 MonoBehaviour。`Assets/Tests/Editor/RankingPanelBackdropTests.cs` |
| 4 | オフライン/失敗でもゲームを止めない | 充足 | `RankingClientTests.cs` のパース失敗系 + `V6ResultLogic.ClassifySubmitResult` (201/429/422) |
| 5 | **trunk と v0.1.2 ベースの展示 patch line の両方に載せる** | **未充足** | 展示 patch line `b677f8f` に `RankingClient.cs` / `ResultRankingOverlay.cs` が **0 件** |
| 6 | 実 Win64 での送信 → 2 台目での表示 | 未検証 | 実機・2 クライアント必須 |

- **未検証の区別**: 条件 6 は実機必須。**EditMode 緑は HTTP 通信の結果・サーバの受入を保証しない** (`Urls_FollowTheOpenApiContract` は URL 書式の検証のみ)。代替 = 11 テスト緑 + サーバ側 `scores.ts` の実装読解。
- **依存**: #21 / #7 (S4) の Unity 側。展示 patch line 側は #22/#250 の配布と連動。

### #129 — [Dev/Mac] オーナー直接確認用の独立 macOS アプリを Desktop に置く

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `Assets/Editor/BuildScript.cs:50` — `public static void BuildStandaloneMacOS()` (patch line に macOS ビルド入口が無かったのを追加)
  - 配布物 (Git 外): `~/Desktop/FlashBeat-Mac/FlashBeat.app` + `起動.command` + `README.txt` + `FlashBeat-update.log` + `FlashBeat-backup-20261109132410.app` + `old/` (実在確認済)
  - `README.txt` の記載: バージョン `0.1.0+fffc399` / commit `fffc399` / Unity `6000.5.6f1` / アプリ内自動更新 (Windows 版と同じ)
  - `b677f8f` (展示 patch line の base) は `git merge-base --is-ancestor b677f8f origin/main` = **true**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | Mac に直接確認用の独立アプリが Desktop 配下にある | 充足 | `~/Desktop/FlashBeat-Mac/FlashBeat.app` (実在確認済) |
| 2 | patch line に macOS ビルド入口 (`BuildStandaloneMacOS`) を足す | 充足 | `Assets/Editor/BuildScript.cs:50` (main にある) |
| 3 | **v0.1.5 のソース (patch line `b677f8f`) からビルドする** | 未充足 | 配布物の README は `0.1.0+fffc399` = **main の最新**からビルド。指定ソース `b677f8f` ではない (main は b677f8f の後継で内容的には下回らないが、指定と別) |
| 4 | 展示と挙動を揃える (同一 SHA) | 未検証 | 展示 Windows 世代 3 は `b77b63c`、Mac は `fffc399`。**同一 SHA ではない** |

- **未検証の区別**: 条件 4 は実機比較が必要。**macOS Player の起動確認は Win64 の代用にならない** (`docs/evidence/index.md` §1.6)。
- **依存**: #22 (配布) と同じ「展示と同一 SHA」の要件に連動。

### #96 — [Unity/P1] Title/Result/Option/Opening の UI を 1024x768〜2560x1440 で崩れなくする

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `Assets/Game/Scripts/UI/V5/UISizeProfile.cs` — `UISizeProfile.Fhd()` / `.TwoK()` / `.Mac()` + `CanvasConfigurator.Configure(canvas)`
  - `Assets/Game/Scripts/UI/V6/Common/V6UiProfile.cs:73-75` — FHD 1920×1080 / 2K 2560×1440 / MacBook 2560×1664
  - テスト: `Assets/Game/Tests/FullscreenPolicyTests.cs` — `FhdProfile_MatchesJson`, `TwoKProfile_MatchesJson`, `MacProfile_MatchesJson`, `JsonMacScreen_Is2560x1664` 等 10 テスト
  - テスト: `Assets/Tests/Editor/TitleOptionV5Tests.cs`, `ResultV5ViewTests.cs`, `V5UiTests.cs`
  - 証跡 (実機 PNG の実解像度は `file` コマンドで実測):
    - `docs/evidence/win64-2026-11-06/*.png` — **7 枚すべて 2560×1440** (01-title / 02-select-no1 / 03-select-no2-manimani / 04-game-playing / 05-result / 06-retry-game-reloaded / 07-result-run2)
    - `docs/evidence/win64-2026-11-06-issue13/*.png` — 11 枚すべて **1296×759** (ウィンドウ枠込み。クライアント 1280×720)
    - `docs/evidence/233-hud-nre/*.png` — **2560×1664** (Mac 実機)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | Title / Result / Option / Opening が 1024×768〜2560×1440 で崩れない | 充足 (コード) | `UISizeProfile.cs` + `V6UiProfile.cs:73-75`。`FullscreenPolicyTests.cs` が json↔C# 一致を固定 |
| 2 | 実機スクリーンショット (01_title.png / 07_result.png / 08_retry.png) での確認 | 未充足 (解像度カバレッジ不足) | 2560×1440 と 1296×759 (=1280×720 ウィンドウ) の **2 解像度のみ**。**1024×768 と 1920×1080 の撮影が無い** |
| 3 | Select / Game は #88 が担当 | — | スコープ外 (§3.2 #88 参照) |

- **未検証の区別**: 条件 2 は **実機撮影** 必須。**プロファイル座標の EditMode 検証は実描画の見切れ・重なりを保証しない**。代替 = `FullscreenPolicyTests.cs` 緑 + 2560×1440 / 1296×759 の実 PNG。
- **依存**: #162 → #167 の下位。#88 と解像度方針を共有 (`design/ui-size-profiles.json`)。

### #88 — [Win/P0] 最新統合版が全画面にならず Select/Game の UI が崩れる (#52 回帰)

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `ProjectSettings/ProjectSettings.asset:119` — `fullscreenMode: 1` (= FullScreenWindow)。**Issue 本文 119 行目が指摘した `fullscreenMode: 2` (= Maximized Window) は解消済み**
  - `ProjectSettings/ProjectSettings.asset:107` — `resizableWindow: 0` (擬似全画面を禁止)
  - `defaultIsNativeResolution: 1`
  - テスト: `Assets/Game/Tests/FullscreenPolicyTests.cs` — `ProjectSettings_FullScreenMode_IsFullScreenWindow`, `ProjectSettings_ResizableWindow_IsFalse`, `ProjectSettings_DefaultIsNativeResolution_IsTrue`, `GameScripts_HaveNoScreenSetResolution` (Assets/Game/Scripts 配下に `Screen.SetResolution` が 0 件であることを検証)
  - commit: `86b3911` "test(settings): Issue #169 フルスクリーン方針と mac プロファイル契約の固定テスト" (main ancestor), `aa5e908` "fix(#88): 全画面カバレッジ閾値を 0.99 に"
  - 実機 run-summary (Git 内): `docs/evidence/win64-2026-11-06-release-gate/run-summary-exhibit-743c88d.json`, `run-summary-exhibit-s1.json` → いずれも `"resolution": "1280 x 720 (fullscreen=0)"`
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 起動引数なしで `displayCoverage ≥ 0.99` (`-RequireFullscreenCoverage` が PASS) | 未充足 | Git 内の run-summary 2 件は **`fullscreen=0`**。`displayCoverage` を持つ run-summary は main に存在しない。`docs/evidence/index.md` §1.5「未証明: … 実機 1280×720 fullscreen」 |
| 2 | 1920×1080 / 2560×1440 / 1024×768 / 1280×720 で Select・Game・Result が見切れず重ならない (実機スクリーンショット) | 未充足 | 2560×1440 (7 枚) と 1296×759 (11 枚) のみ。**1920×1080 と 1024×768 の撮影が無い** |
| 3 | 日本語フォント欠落 (□) 0 | 未充足 (patch line) | trunk は #140 で充足、**展示 patch line `b677f8f` は未適用** (§3.2 #140) |

- **未検証の区別**: 条件 1 は **実 Windows 実機の計測** 必須。**`FullscreenPolicyTests.cs` が緑でも、PlayerSettings の serialized 値と実機のウィンドウ挙動は別物** (Issue 本文 104 行目も「設定だけ変えて修正済みと言わない」と明記)。代替 = EditMode 緑 + `ProjectSettings.asset` の値読み取り。
- **依存**: #162 / #167 の下位。#250 の 11/14 展示の前提。

---

## 3.3 Web Studio / Infra / 検証 クラスタ (#118 / #61 / #60 / #75 / #74 / #73 / #71 / #68 / #98 / #56 / #59 / #58 / #30)

### #118 — [Update/P0] 更新は明示式: タイトル画面に『アップデートあり』表示、押したときだけ更新

- **区分**: **A (実装済み・証拠待ち)** — cc 実測確定
- **実装/証拠**:
  - `Assets/Game/Scripts/Update/UpdateFlowModel.cs:17-20` — 不変条件コメント「`BeginApply` は Ready からのみ遷移し、適用は必ずユーザー確認を経由する。自動適用の経路はこのクラスに存在しない (OnApplicationQuit から呼ぶ API を提供しない)」
  - `Assets/Game/Scripts/Update/UpdateFlowModel.cs:136` — `public void BeginApply()`
  - `Assets/Game/Scripts/Update/UpdateChecker.cs:224` — `public void RequestDownload()` / `:320` — `public void RequestApply()` / `:329` — `Model.BeginApply()` / `:658` — 「ここからだけが適用への入口 (OnApplicationQuit からの自動適用は存在しない)」
  - `Assets/Game/Scripts/UI/UpdateNoticeUI.cs:21` — `public class UpdateNoticeUI : MonoBehaviour`
  - `Assets/Game/Scripts/UI/UpdateNoticePresenter.cs:27` — `public static class UpdateNoticePresenter` (PR #255 で追加)
  - **実コードに `OnApplicationQuit` の自動適用は 0 件** (`grep -rn "OnApplicationQuit" Assets/Game/Scripts/` のヒットは `UpdateChecker.cs:22` と `UpdateChecker.cs:658` の **コメントのみ**)
  - テスト (PR #255 で追加): `Assets/Tests/Editor/UpdateCheckerEntryPointTests.cs`, `UpdateCheckerTests.cs`, `UpdateNoticePresenterTests.cs`, `UpdateSourceStructureTests.cs` (474 行), `UpdateFlowTests.cs`, `UpdateResultFileTests.cs`
  - 証跡: `docs/evidence/118-explicit-update.md` (227 行)
  - commit: `887cb69` "fix(v6): #118 独立レビュー指摘 5 件を解消する (PR #255)", `549a5bf` "test(update): #118 の更新フローを受入条件ごとに固定する (実装状況の確定)" — `git merge-base --is-ancestor ab8c336 origin/main` = true (PR #255 は main に merge 済み)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 起動時の自動チェックは残してよい (表示のみ) | 充足 | `UpdateChecker.cs` の `LatestTag` 判定 → `UpdateNoticeUI` が通知 |
| 2 | ダウンロードはユーザー操作 (F3 / クリック) で開始 | 充足 | `UpdateChecker.cs:224 RequestDownload()` が通知 UI からのみ呼ばれる |
| 3 | 適用はユーザー確認を経由 | 充足 | `UpdateChecker.cs:320 RequestApply()` → `:329 Model.BeginApply()`、`UpdateFlowModel.cs:136 BeginApply()` は Ready からのみ遷移 |
| 4 | `OnApplicationQuit` からの自動適用が存在しない | 充足 | `grep` で実コード 0 件 (コメント 2 件のみ)。`UpdateSourceStructureTests.cs` が構造を固定 |
| 5 | 展示中のゲームを勝手に止めない | 充足 (コード) / 未検証 (実機) | 適用は別操作。`UpdateChecker.cs:658` のコメント |
| 6 | prerelease を stable に自動適用しない | 充足 | `UpdateChecker.cs` の `LatestTag` 限定 + `db64a1a` "fix(ci): push ビルドの更新チャンネルを stable 固定" |
| 7 | ZIP 全体 + SHA-256 検証を維持 | 充足 | `UpdateResultFileTests.cs` / `UpdateCheckerTests.cs` |
| 8 | 実機での更新 E2E | 未検証 | `docs/evidence/update-e2e-2026-11-07/README.md` があるが **本監査では実挙動を確認していない**。実機更新は展示運用の制約あり |

- **未検証の区別**: 条件 8 は実機必須。**EditMode の構造テスト緑は「更新画面が出て、押さないと適用されない」ことを保証しない** (UI 遷移とプロセス起動は別)。代替 = 7 テストファイル緑 + `docs/evidence/118-explicit-update.md` のコードレビュー。
- **依存**: 単独。#250 の A ゲートに含まれる。

### #61 — [Web UI Product Spec] 実Studioの体験・情報構成・視覚品質・E2E受け入れ基準

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - 本 Issue は **製品仕様の正本**。Issue 本文 36 行目が「**#61 is the product specification / coordination Issue, not an 11th code worktree**」と明記
  - 子 Issue の実装状況: #60 (A), #68 (A), #71 (A), #73 (A), #74 (B), #75 (B), #135 (A) → 本 Issue の実装修約の 6/7 が実装済み
  - 追加 Issue: #163 / #164 / #165 / #166 は **closed** (`gh issue list --state open` に無い)
  - 契約: `contracts/schemas/chart.v1.schema.json` の `notes[].type` は **`[1,2]`** (HOLD 許容) に更新済み — Issue 本文 49 行目が指摘した `const:1` は解消
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | Gate A: Live GET songs、本番で fixture 無し | 充足 | `web/src/app/catalog.ts:28` 実 `fetch("/api/v1/catalog")` + `:32` 失敗時 throw |
| 2 | Gate A: 表示される導線はすべて実装済み画面を開く | 充足 (コード) | `web/src/app/topbar.test.tsx`, `catalog-panel.test.tsx` |
| 3 | Gate A: カタログ行選択で正しい song+chartVersion を開く、サンプルが本物に見えることなし | 充足 | `web/src/lib/song-session/load.ts:21-60` (exact version + identity 検証) |
| 4 | Gate A: 1 件の publish が persisted live chartVersion を確認し、invalid token/invalid chart/error で入力を失わない | 充足 | `web/src/lib/publish/transport.ts:25,31-35` + `web/src/lib/publish/__tests__/*.test.ts` |
| 5 | Gate B: 実アプリアイコン (text `FB` フォールバックでない) | 未充足 | #67 は closed だが `web/public/**` の実アセットは本監査未確認 → **観測不足** |
| 6 | Gate B: 意味的色/コントロール/リスト層が Pen v5 に一致、Teal 残存無し | 充足 | `web/panda.config.ts:76` `include: ["gray","blue","red","green","amber"]` — Teal 除外済み。`docs/design/web-token-mapping.md` §2 「Accent (`blue`, replaces Park `teal` `#12A594`)」 |
| 7 | Gate B: デスクトップ幅を実際の編集作業に使う (filler card や placeholder でない) | 未検証 | 実ブラウザでの目視確認が必要 |
| 8 | Gate C: 実ブラウザ 1440×900 / 1280×720 / 390×844 / 200% zoom、キーボード+ポインタ | **未充足** | 視覚ゲートの証跡は `specs/075-visual-gate/evidence/2026-11-08/index.json` に 3 viewport 分の撮影がある。ただし **`exitCode: 1`** = **ゲートが不合格**。200% zoom の証跡は無い |
| 9 | Gate C: 実ブラウザ画素画像 **と** 実入力 E2E の両方 (テスト成功だけでは NG) | 未充足 | #74 の実操作 E2E が未了。`specs/74-responsive/tasks.md` の 3 タスクが未チェック |
| 10 | 本番デプロイは単独 deploy owner の管理 | 充足 (運用) | `docs/operations/cloudflare-bootstrap.md` |

- **未検証の区別**: 条件 7–9 は **実ブラウザの画素画像 + 実入力** が必要。**Vitest 緑は「クリックが確実に届く」を保証しない** (Issue 本文 52 行目「テスト成功だけではNG」)。代替 = `web/src/**` 47 テストファイル緑 + `docs/evidence/web-acceptance/{1920x1080,2560x1440,2560x1664}` の撮影。
- **依存**: #60 → #68 → #71 → #73 → #74 → #75 の順。**#74 / #75 が未完了 = 本 Issue は閉じられない**。

### #60 — [Web/UI Foundation] Pen v5 の色・surface階層・タイポグラフィをPanda/Parkへ写像

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `web/src/theme/colors/gray.ts` — Pen `well #131313` → step 1, `bg #1B1B1B` → step 2, `base #2D2D2D` → step 6, `text-dim #8C8C8C` → step 10, `text-muted #B0B0B0` → step 11, `text #FFFFFF` → step 12
  - `web/src/theme/colors/blue.ts` — Pen `accent #3FA9F5` → `blue.9`, `accent-light #9AD3FA` → `blue.11` (`blue.ts` の semantic token 定義全体)
  - `web/src/theme/colors/red.ts`, `green.ts`, `amber.ts` — status palettes
  - `web/panda.config.ts:76` — `include: ["gray","blue","red","green","amber"]` (**Teal 除外**)
  - `web/src/theme/tokens/colors.ts`, `text-styles.ts`, `layer-styles.ts`, `conditions.ts`, `global-css.ts`, `keyframes.ts`, `animation-styles.ts`, `durations.ts`, `shadows.ts`, `z-index.ts`
  - マッピング表: `docs/design/web-token-mapping.md` (§1 Neutral ramp, §2 Accent, §3 Status palettes)
  - `specs/060-web-visual-foundation/tasks.md` = **0 完了 / 5 未完了** (Issue 本文 T1–T5 も未チェック)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | Pen の変数インベントリとコンポーネント層を検査し、primitive→semantic→component の明示マッピングを書く | 充足 | `docs/design/web-token-mapping.md` |
| 2 | 正規パレット/surface/raised-vs-recessed を `panda.config.ts` と `theme/**` に適用し、Park UI API を保つ | 充足 | `web/src/theme/colors/*.ts` + `web/panda.config.ts:76` |
| 3 | タイポグラフィ (Noto Sans JP + fallback)、テキストサイズ、コントラスト、focus/state token を写す | 充足 | `web/src/theme/text-styles.ts`, `tokens/colors.ts` |
| 4 | Web の type/build/tests を実行し、1440×900 と 1280×720 でコンポーネント例を確認 | 充足 (テスト) / 未検証 (目視) | `web` 47 テストファイル緑。**目視確認は本監査未実施** |
| 5 | before/after の画素証跡、実行 SHA、未解決差分を記録 | 未充足 | `docs/evidence/web-visual/{before,after,interaction}/` は存在するが **本監査では内容未検証**。`specs/060` のチェックも未済 |

- **未検証の区別**: 条件 4–5 の目視・画素部分は **実ブラウザ** 必須。**型検査とテスト緑は「見た目が Pen と一致する」ことを保証しない**。代替 = `web` テスト緑 + `docs/design/web-token-mapping.md` の数値対応表。
- **依存**: #61 の独立した foundation。#67 / #68 / #71 と並行可能。

### #75 — [Web/Visual Gate] 画像ベースのデザインレビュー・比較・本番E2Eをリリース条件にする

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `web/tests/visual/capture.mjs` — 実 Chromium (puppeteer-core) で 3 ルート (`/`, `/chart?songId=who&version=1`, `/publish`) × 3 viewport (1440×900 / 1280×720 / 390×844) を撮影
    - `:207-211` console error / page error の収集
    - `:262` `if (measured.horizontalScroll || consoleErrors.length > 0) failed = true;`
    - `:289-291` 出力 JSON `{ horizontalOverflow, consoleErrors, exitCode }`
    - `:303-304` `process.exitCode = failed ? 1 : 0` (0 = clean / 1 = gate fail / 2 = setup fail)
  - 証跡: `specs/075-visual-gate/evidence/2026-11-08/index.json` — `{"horizontalOverflow": ["home@390x844"], "consoleErrors": [], "exitCode": 1}` = **ゲートが FAIL**
  - `specs/075-visual-gate/evidence/2026-11-08/README.md`
  - `docs/evidence/index.md` §1.7 — 「視覚ゲート `npm run visual` = **exit 1 (FAIL)** — `home@390x844` の横スクロール溢出。… **新規の regressions ではなく未解決 issue** (#74 / #75 / #68 系)」
  - `specs/075-visual-gate/tasks.md` = **5 完了 / 0 未完了**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: 実表示をキャプチャする最小経路 (ダミー画像禁止) | 充足 | `capture.mjs` は実 Chromium を使う |
| 2 | T2: `/`, `/chart?songId=…&version=…`, `/publish` を固定 viewport で撮影 | 充足 | `index.json` の `routes` × `viewports` と各 `file` / `fullPageFile` / `bytes` |
| 3 | T3: design-skills / unslop の基準を実画像に適用し redline 記録 | 未充足 | `docs/evidence/web-visual/{before,after}/` の内容は本監査未検証。redline 記録の形跡なし |
| 4 | T4: 実API での 選曲→編集→公開→再読込の証拠 | 未充足 | 証跡 JSON は静的ルート撮影のみ。実 API 経路の証跡は無い |
| 5 | T5: 修正前後の差分・SHA・コマンド・未解決項目をまとめ、本番デプロイ可否を独立判定 | 未充足 | 判定結果 = **不合格** (`exitCode: 1`) |
| 6 | ゲート条件: 画像取得なし・単体テストのみなら FAIL | 充足 (ゲートは機能している) | `capture.mjs` の exit code 設計。**不合格だから FIXME ではない** |

- **未検証の区別**: 条件 3–5 は **実ブラウザ撮影 + 目視レビュー** が必要。**`capture.mjs` が走ること (exit 1 で正しいこと) は、Gateway が合格することではない**。代替 = `index.json` の機械的結果。
- **依存**: #60 / #67 / #68 / #69 / #70 / #71 / #72 / #73 / #74。**#74 の狭幅修正 (PR #257) が唯一の既知の失敗原因**。

### #74 — [Web/Responsive] 画面サイズ・ズーム・キーボードで操作不能なUIをなくす

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `web/src/app/drawer.tsx` — 純関数のフォーカストラップ `nextFocusTarget()` (`:44-50`) + `FOCUSABLE_SELECTOR` (`:26-34`)。ヘッダコメントで「Tab/Shift+Tab はパネル内で循環し、外へ出ない」「Escape で閉じる」「閉じると開いた要素にフォーカスを返す」と明記
  - テスト: `web/src/app/drawer.test.ts`
  - テスト: `web/src/components/chart-editor/*.test.tsx` (6 ファイル), `web/src/components/song-publisher/song-publisher.test.ts`
  - `specs/74-responsive/tasks.md` = **26 完了 / 3 未完了**。未完了 3 件はいずれも「**open (#75)**」すなわち視覚ゲート待ち:
    - `:58` Real browser tab order walk — **open (#75)**
    - `:65` Rendered check at 390px — **open (#75)**
    - `:74` Screenshots of the five viewports — **open (#75)**
  - **PR #257 (open)** `w/w74fix2` — 「#74 狭幅ビューポートの横スクロールを解消する」
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: 1440×900 / 1280×720 / 1024×768 / 390×844 / 200% zoom のストレス箇所を特定 | 充足 | `specs/74-responsive/` に記録 |
| 2 | T2: grid/sidebar/toolbar/inspector のレイアウト変形とスクロール所有を実装 | 充足 | `web/src/styles/app.css` + `web/src/app/**` |
| 3 | T3: Tab/Enter/Escape/focus restore、dialog・select・input・note canvas の操作テスト | 充足 (コード) | `web/src/app/drawer.test.ts` の `nextFocusTarget` 純関数テスト + `chart-editor` の 6 テスト |
| 4 | T4: loading/error/長い日本語タイトル状態で可視/クリック領域と対比を検証 | 充足 (コード) / 未検証 (実機) | `song-publisher.test.ts`, `song-thumbnail.test.tsx` |
| 5 | T5: 実ブラウザのスクリーンショットと操作 E2E を SHA 付きで提出 | **未充足** | `specs/74` 3 タスク未チェック。**狭幅 (390×844) の横スクロール溢れが実測で残っている** (`specs/075-visual-gate/evidence/2026-11-08/index.json` の `horizontalOverflow: ["home@390x844"]`) |
| 6 | Given-When-Then: 必須操作が到達可能で、意図しない overflow・ボタン重なり・focus 喪失がない | **未充足** | PR #257 が未マージ |

- **未検証の区別**: 条件 5–6 は **実ブラウザ E2E** 必須。**Vitest 緑は「実際の幅で到達可能か」を保証しない** — 実際 390×844 では溢れが実測されている。代替 = `drawer.test.ts` 緑 + `index.json` の機械的結果。
- **依存**: #69 / #70 / #72 / #73。**#75 の既知失敗の唯一の原因**。

### #73 — [Web/Publish Flow] モック成功禁止・既存曲メタデータ編集・実API検証

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `web/src/lib/publish/transport.ts:25` — `resolvePublishTransport()` / `:31-35` — fixture はテストで明示許可されない限り **throw**
  - `web/src/lib/publish/draft.ts` — `savePublishDraft(draft, target)` (target = "editor" のとき `/chart?draft=…` を返す) / `readPublishDraft(id, songId, version)` (uuid 検証 + song/version 一致 + chart import 検証)
  - `web/pages/publish.tsx:1` — `import { readPublishDraft } from "@/lib/publish/draft"` / `:140` — `setLocalChart(draftId ? readPublishDraft(draftId, songId, version) : null)`
  - `web/src/components/song-publisher/song-publisher.tsx:11,429` — `savePublishDraft` の呼び出し
  - テスト: `web/src/lib/publish/__tests__/{client,draft,mock,transport,validation}.test.ts`, `web/src/components/song-publisher/{song-metadata-form.test.tsx,song-publisher.test.ts}`
  - `specs/073-publish-flow/tasks.md` は `specs/` に存在しない (numbers 71/74 等の既に統合済み slice は tasks 番号が省略されている場合がある)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: production 同一 origin live を強制、fixture はテスト明示時のみ | 充足 | `transport.ts:25,31-35` + `__tests__/transport.test.ts` |
| 2 | T2: 既存曲/新曲ドラフトの metadata と chart session を正しく初期化 | 充足 | `publish.tsx:140` + `song-metadata-form.tsx` |
| 3 | T3: 編集→検証→確認Dialog→Publish の loading/error/unauthorized/double click 状態 | 充足 | `song-publisher.test.ts` + `publish-success.tsx` |
| 4 | T4: Publish 後 catalog と該当 version チャートを実APIで再取得し反映を確認 | 充足 (コード) / 未検証 (実行) | `web/src/lib/publish/client.ts` の再取得経路。**実 API 実行の証跡は本監査未確認** |
| 5 | T5: Live 実行時は試験用 songId で監督承認下の E2E、token 漏洩なし | 未検証 | 本 Issue では実施していない (実デプロイ禁止)。`new-song-flow.tsx:30-31` は token をメモリのみに保つ設計 |
| 6 | Given-When-Then: 既存曲 v2 編集 → Publish 成功 → server が返した新 version の実GET と一致し UI に表示される。API 失敗/401/422 では成功表示が絶対に出ず入力が消えない | 充足 (コード) | `__tests__/client.test.ts` + `publish-success.tsx` + `draft.test.ts:17-18` |

- **未検証の区別**: 条件 4 の実行部と 5 は **実 API** 必須。**Vitest 緑は本番 publish の成功を意味しない**。代替 = `web/src/lib/publish/__tests__/` 5 ファイル緑。
- **依存**: #71 (session), #72 (editorUX)。#98 は本 Issue の「#73 の残り」。

### #71 — [Web/Chart Session] Catalogから指定曲の正しい版の譜面を開く

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `web/src/lib/song-session/load.ts`
    - `:21` `export async function loadSongSession(...)`
    - `:28-32` `version` が `/^[1-9][0-9]*$/` で整数でなければ throw
    - `:36` catalog 取得失敗 → throw / `:39` 曲が catalog に無ければ throw
    - `:41` `GET /api/v1/charts/{songId}?version={version}`
    - `:45-46` 404 等 → 「Chart vN unavailable (…)。Return to Catalog or retry.」= **静かな空譜面にはしない**
    - `:53-54` `snapshot.songId !== songId || snapshot.chartVersion !== Number(version)` → 「Chart identity mismatch.」throw
    - `:56` 公開譜面のバリデーション失敗 → throw
  - `web/pages/chart.tsx` — `import { loadSongSession, type SongSession } from "@/lib/song-session/load"`、ローディング/エラーを分離
  - テスト: `web/src/lib/song-session/load.test.ts`
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: songId/chartVersion パラメータと不正値/404 の扱いを定義 | 充足 | `load.ts:28-32,45-46` |
| 2 | T2: exact version chart 読込と contract test | 充足 | `load.ts:41,53-56` + `load.test.ts` |
| 3 | T3: chart route に song title/YouTube/譜面を供給、loading/error を明確に分離 | 充足 | `web/pages/chart.tsx` の `SongSession` 分岐 |
| 4 | T4: 公開曲 2 件以上を選択して各譜面の名前・ノーツ数・version の一致を実証 | 充足 (コード) / 未検証 (実行) | テストは fixture。**実公開曲での実証は本監査未確認** |
| 5 | Given-When-Then: B の v2 を開き A/サンプルのデータが混ざらない。古い版が 404 ならエラーと復帰手段を出す | 充足 | `load.ts:39,45-46,53-54` |

- **未検証の区別**: 条件 4 の実証は **実 API** 必須。**Vitest 緑は本番カタログの実際のバージョンと一致することを保証しない**。代替 = `load.test.ts` 緑 + `contracts/openapi.v1.yaml` との突合。
- **依存**: #68 (catalog) が前提。#73 / #98 の前提。

### #68 — [Web/Catalog] 固定4曲を廃止し本番Catalog APIと実状態に接続

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `web/src/app/catalog.ts`
    - `:9` `export type CatalogMode = "loading" | "ready" | "empty" | "error"`
    - `:19` `export async function loadCatalog(mode: CatalogMode): Promise<SongSummary[]>`
    - `:21` / `:32` / `:57` / `:63` / `:79` — **失敗は必ず throw** (`"GET /api/v1/catalog failed"` 等)。4 曲 fixture フォールバックは無い
    - `:28` `fetch("/api/v1/catalog", { … })` — `Cache-Control: no-store`
  - テスト: `web/src/app/catalog-view.test.ts`, `web/src/app/catalog-panel.test.tsx`
  - `web/src/app/types.ts`
  - **注意**: Issue 本文 291 行目の指定パスは `web/src/app/__tests__/catalog*.test.ts` だが、実体は `web/src/app/catalog-view.test.ts` / `catalog-panel.test.tsx` (**`__tests__/` 配下ではない**)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: API client/response adapter と契約テスト | 充足 | `catalog.ts:19-63` + `catalog-view.test.ts` |
| 2 | T2: 更新時の race/abort、HTTP error、invalid payload のテスト | 充足 | `catalog.ts:32,57,63,79` の throw 経路 + `catalog-view.test.ts` |
| 3 | T3: 実API で catalogVersion・実件数・song fields を観測し単体結果の証跡 | 未検証 | **実API 観測の証跡は本監査未確認**。`docs/evidence/web-acceptance/` に撮影はあるが数値の記録は無し |
| 4 | T4: 呼び出し元向け live/fixture 明示 contract を #nav に引き渡す | 充足 | `CatalogMode` 型 (`:9`) が live/fixture を明示 |
| 5 | Given-When-Then: 見える件数・曲名・Version が実APIと一致。通信失敗では4曲の嘘の成功へフォールバックしない | 充足 (コード) | `catalog.ts:21,32,57` の throw |

- **未検証の区別**: 条件 3 は **実 API 観測** 必須。**Vitest 緑は本番の曲数・バージョンの一致を保証しない**。代替 = `catalog-view.test.ts` 緑 + `contracts/openapi.v1.yaml` の形。
- **依存**: 既存契約 `contracts/openapi.v1.yaml` と main。#71 の前提。

### #98 — [Web] 既存曲のメタデータ編集と Publish 画面の回復動線 (#73 の残り)

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - **publish 側 (Issue 本文 112 行目の主担当) は充足**:
    - `web/pages/publish.tsx:1` `import { readPublishDraft }` / `:140` `setLocalChart(draftId ? readPublishDraft(draftId, songId, version) : null)`
    - `web/src/lib/publish/draft.ts` — `readPublishDraft(id, songId, version)` は uuid 検証 + song/version 一致 + chart import 検証
    - `web/src/components/song-publisher/song-metadata-form.tsx` — title/artist/level/duration/youtubeId の編集フォーム
    - テスト: `web/src/lib/publish/__tests__/draft.test.ts` (`:13` save → `:17` 読戻し → `:18` 別 song で throw)、`web/src/components/song-publisher/song-metadata-form.test.tsx`
  - **chart 側の回復動線は未実装**:
    - `web/pages/chart.server.ts` — loader が返すのは `{ songId, version }` **のみ**。**`draft` パラメータを受け 取っていない** (`return { songId: params.get("songId"), version: params.get("version") };`)
    - `web/pages/chart.tsx:1` — `import { savePublishDraft } from "@/lib/publish/draft";` のみ。**`readPublishDraft` を import していない**
    - 結果: publish → chart の遷移は `savePublishDraft` が返す `/chart?draft=…` URL を使うが、`chart.server.ts` が `draft` を捨て、chart ページはドラフトを復元しない
    - `web/src/lib/publish/draft.ts` の `savePublishDraft` の戻り値は target "editor" で `/chart?draft=…` (`web/src/components/song-publisher/song-publisher.tsx:429`, `web/pages/chart.tsx:175`)
  - **Issue は「#73 の残り」と明記** → #73 は A 判定済だが、**#73 の受入条件「既存曲 v2 を編集 → Publish 後、実GET と一致」に対応する chart 側の復元動線は #98 の担当**
  - `specs/098-*` の tasks は `specs/` に存在しない (`ls specs` に 098 なし)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 曲選択済みなら title/artist/level/duration/youtubeId を既存値で初期化・編集できる | 充足 | `song-metadata-form.tsx` + `song-metadata-form.test.tsx` |
| 2 | 失敗時に入力を保てる | 充足 | `publish.tsx` のエラー経路。`__tests__/client.test.ts` |
| 3 | **Publish → chart への回復動線 (ドラフト復元)** | **未充足** | `web/pages/chart.server.ts` に `draft` パラメータなし、`web/pages/chart.tsx` は `readPublishDraft` を import しない |

- **未充足の具体箇所**:
  1. `web/pages/chart.server.ts:6` — `return { songId: params.get("songId"), version: params.get("version") };` → `draft` を持ち越す必要
  2. `web/pages/chart.tsx:1` — `readPublishDraft` の import と復元呼び出しが無い
  3. 現状 `chart.tsx:175` は `savePublishDraft` を呼ぶ側 (= 書き出す側) のみ
- **未検証の区別**: 条件 3 はコード欠落 (未充足) であり、テスト不足ではない。**Vitest 緑は「公開→編集の往復動線」を保証しない** — `draft.test.ts` は **ライブラリ単位**の往復しか検証していない。
- **依存**: #73 (publish) が前提。**#61 Gate A の「publish → 再読込」経路に直結**。

### #56 — [Infra] AIエージェント開発環境の再整備 (Agent Skills / root contract / worktree 入口 / delivery 設定)

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `mise.toml` — `[tasks.skills-list]` (`:14`), `[tasks.skills-bootstrap]` (`:17`), `[tasks.skills-update]` (`:20`), `[tasks."worktree:setup"]` (`:25`), `[tasks."worktree:teardown"]` (`:30`), `[tasks.specify-install]` (`:34`), `[tasks.specify-init]` (`:37`)。**`skills-check` タスクは存在しない**
  - `scripts/worktree/setup.sh`, `scripts/worktree/teardown.sh` — 実在
  - `docs/adr/ADR-0001-agent-environment.md`, `ADR-0012.md`, `ADR-0027.md` — 実在
  - `constitution/CONSTITUTION.md`, `organization/profiles/release-driven-solo.md` — 実在。`AGENTS.md` 末尾「Constitution / operating profile」が参照
  - `AGENTS.md` (11115 bytes) — `CLAUDE.md` と **145 行の差分**があるが、骨格は同一 (Project Overview / Build, Test, Run / Architecture / Conventions / Known gotchas)。**能力ごとの担当 Skill 表 (dispatcher) ではない**
  - `skills-lock.json` — **19 エントリ** (各 `source: rebuildup/project-init`, `skillPath: skills/<name>/SKILL.md`, `computedHash` 付き)
  - `.agents/skills/` と `.claude/skills/` — **それぞれ 9 個のみ** (speckit-analyze / checklist / clarify / constitution / converge / implement / plan / specify / tasks)。**lock の 19 個は vendored されていない**
  - `evals/policy-evaluation/` — **存在しない**
  - `.config/wt.toml` — **存在しない** (`.config/` ディレクトリ自体が無い)
  - `.github/workflows/` — `build-macos.yml`, `build-win64.yml`, `summaries/` のみ。**`agent-config.yml` は存在しない**
  - `scripts/agent-routing/` — **存在しない** (`scripts/` には `worktree/` のみ)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| A1 | **Given** fresh clone, **When** `mise install --locked` の後 `mise run skills-check` を実行, **Then** lock と vendored Skill の hash が一致する | **未充足** | `mise.toml` に **`skills-check` タスクが無い** = コマンドが失敗する。さらに vendored は 9 個 (speckit のみ) で lock 19 個と **不一致** → hash 検査は mismatch になる |
| A2 | **Given** `AGENTS.md` だけを読む agent, **When** 能力ごとの担当表をたどる, **Then** 各 single 能力の担当 Skill と manual Skill の path が分かる | **未充足** | `AGENTS.md` は CLAUDE.md の写しで、**能力→Skill path の表が無い**。root contract へのポインタ (`constitution/CONSTITUTION.md`, `organization/profiles/release-driven-solo.md`) のみ |
| A3 | **Given** 新しい worktree, **When** `mise run worktree:setup` を実行, **Then** `.worktree.env` と toolchain が用意され、Unity の `Library/` を worktree 間で共有しないことが分かる | 充足 | `mise.toml:25` → `scripts/worktree/setup.sh` が実在。`docs/adr/ADR-0001-agent-environment.md` に記載 |
| T1 | Skills を現行 revision へ reconcile し `--copy` で commit | **未充足** | vendored 9 個 vs lock 19 個 |
| T2 | `AGENTS.md` を dispatcher に再構成し `CLAUDE.md` を adapter にする | **未充足** | 上記 A2 |
| T3 | worktree lifecycle と agent-routing の入口を project-local に置く | 部分充足 | worktree は有 (`scripts/worktree/`)、**`scripts/agent-routing/` が無い** |
| T4 | vendored-skill hash 検査と README/ADR を追加 | **未充足** | hash 検査コマンドが無い (`skills-check`)。ADR は有 |
| T5 | delivery 設定 (merge method / main protection) を profile に合わせる | 未検証 | `organization/profiles/release-driven-solo.md` はあるが、**GitHub 側 main protection の状態は本監査未確認** |
| V3 | `bash -n scripts/worktree/*.sh` = 0 | 未検証 | 本監査は実行していない (実行可能だが未実施) |

- **未検証の区別**: T5 / V3 は設定・実行の確認事項で、**コード読解では判定できない**。代替 = `mise.toml` と `scripts/worktree/*.sh` の実在確認。
- **依存**: 全 Worker の前提。**ただし #56 の未完了が他 Issue の実装を直接ブロックしているわけではない** (worktree setup は動いている)。

### #59 — 検証: Issue #30 の各機能 (歌詞同期 / キーガイド / HOLD / HUD) の受入確認

- **区分**: **B (部分実装)**
- **実装/証拠** (受入対象の 4 機能は **すべて main に実装済み**):
  - 歌詞同期: `Assets/Game/Scripts/UI/LyricsTimeline.cs` — テスト `Assets/Tests/Editor/LyricsTimelineTests.cs`, `ChartLyricsTimingTests.cs`
  - キーガイド: `Assets/Game/Scripts/UI/KeyGuideUI.cs` — テスト `Assets/Tests/Editor/KeyGuideTests.cs`, `ShortcutKeyCapTests.cs`, `ActionKeyHintTests.cs`
  - HOLD: `Assets/Game/Scripts/Gameplay/LongNote.cs` — テスト `Assets/Tests/Editor/LongNoteTests.cs`。`f953eea` "HOLD の判定ルールを LongNote に集約"
  - HUD: `Assets/Game/Scripts/UI/V5/GameHudView.cs` — テスト `Assets/Tests/Editor/V5GameHudTests.cs`, `V6GameHudViewTests.cs`
  - **KEY GUIDE と HOLD の値域**: `Assets/Game/Scripts/Gameplay/KeyBindings.cs:23` — 「`Judge.LaneKeys` はこの配列へのエイリアスであり、判定 (Judge) と…」。`4c55d31` "fix(gameplay): レーンキー表を KeyBindings.DefaultKeys に一本化"
  - **受入記録の欠落**: `docs/evidence/` に `lyrics-sync-*` / `key-guide-*` / `long-note*` / `hud-ux*` のディレクトリは **存在しない** (`ls docs/evidence/` 実測)
- **受入条件**:

| # | 機能 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 歌詞同期: 歌詞が音に同期して表示・非表示できる | 充足 (実装) / **未検証** | `LyricsTimeline.cs` + `LyricsTimelineTests.cs` 緑。**受入判定記録とスクリーンショットが無い** |
| 2 | キーガイド: 8 レーンのキー配置が `Judge.LaneKeys` と一致して画面に出る | 充足 (実装) / **未検証** | `KeyGuideUI.cs` + `KeyGuideTests.cs`。`4c55d31` で単一正本化。**受入記録が無い** |
| 3 | HOLD: HOLD ノーツが保持・判定・表示できる | 充足 (実装) / **未検証** | `LongNote.cs` + `LongNoteTests.cs`。**受入記録が無い** |
| 4 | HUD/UX: 判定・コンボ・プログレスの導線が崩れていない | 充足 (実装) / **未検証** | `GameHudView.cs` + `V5GameHudTests.cs` / `V6GameHudViewTests.cs`。**受入記録が無い** |
| 5 | 完了条件: 機能ごとの受入判定 (OK / NG / 未検証) と根拠 | **未充足** | Issue 本文 189–192 行目の 4 チェックボックスが **未チェック**。受入判定表がどこにも無い |
| 6 | 完了条件: スクリーンショットを `docs/evidence/<feature>-<date>/` に保存 | **未充足** | 当該ディレクトリ群が存在しない |
| 7 | 完了条件: NG は別 issue として起票し修正は実装ワーカーへ差し戻す | 未充足 | 起票記録が無い |
| 8 | 制約: 本 issue では実装・修正を行わない / Windows 実機は触らない | 充足 (運用) | 本 Issue の性格 |

- **未検証の区別**: **本 Issue は純粋な受入確認 Issue であり、判定の主角は「実装が動いているか」ではなく「受入記録が残っているか」**。4 機能とも EditMode テストは緑だが、**Issue 本文 196 行目が求める「実機起動での画面と挙動の確認」が 4 機能とも未実施**。代替 = EditMode 緑のみ。
- **依存**: #30 の仕様確定。**#30 が E (判定不能) のため、HOLD の判定ルール (先頭/終端の 2 判定か連続保持 1 判定か) は受入基準が未確定** → #59 の OK/NG 判定も現状出せない。

### #58 — 検証: カタログ合成 (30 曲表示) と再生クロック修正の回帰確認

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - カタログ合成: `Assets/Game/Scripts/Songs/GManager.cs:88` — `public static SongData[] ActiveSongs => mergedSongs;` (固定カタログ + remote 上書き)。`:80` にコメント「ActiveSongs: the cloud snapshot only *updates* the songs it knows」
  - テスト: `Assets/Tests/Editor/CatalogMergeTests.cs`, `CloudCatalogTests.cs` (11 件), `SongCatalogRobustnessTests.cs`, `ChartDataIntegrityTests.cs`
  - 再生クロック: `Assets/Game/Scripts/Gameplay/MediaTimeline.cs` — テスト `Assets/Tests/Editor/PlaybackClockTests.cs`, `MediaTimelineTests.cs`
  - **現状のカタログ規模**: `GManager` の 7 並列配列は **29 エントリ** (`CLAUDE.md` の記載。`ChartDataIntegrityTests.cs` が 29 Entries を固定)、`GManager.cs:407` — `public static int totalSong => ActiveSongs.Length - 1;`。かつ `SongName[27]` / `[28]` は **両方 `ニセモノ`** で `GManager.UnplayableTitles` により **選曲不可**
  - 実機証跡: `docs/evidence/win64-2026-11-06/02-select-no1.png`, `03-select-no2-manimani.png` (2560×1440)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | カタログ合成 (固定 + remote 上書き) | 充足 | `GManager.cs:80-88` + `CatalogMergeTests.cs` |
| 2 | 再生クロック (曲は必ず終わる / Ready 時にクロックが 0 に戻る) | 充足 (コード) | `MediaTimeline.cs` + `PlaybackClockTests.cs` |
| 3 | 譜面整合性・カタログ契約の EditMode テスト | 充足 | `ChartDataIntegrityTests.cs` / `ScoreCatalogIntegrityTests.cs` |
| 4 | 完了条件: EditMode テスト結果 (成功/失敗件数) の記録 | 充足 | `docs/evidence/index.md` §1.7 に **1071/1071 成功** を記録 |
| 5 | 完了条件: 選曲画面のスクリーンショットで **30 曲全部**が見える | **未充足** | 現状カタログは 29 配列エントリ = **選曲可は 28**、さらに 2 件 (`ニセモノ`) は `UnplayableTitles` で不可。**「30 曲全部」の前提が現状と一致しない** → Issue の基準更新が必要 |
| 6 | 完了条件: 1 曲プレイの実測結果 (ノーツ判定・Result 到達・Retry・YouTube 映像/音声・A/V 同期) | 部分充足 | `docs/evidence/win64-2026-11-06-release-gate/README-gen3.md` (Run D) に **判定 76 → Result → R リトライ / 映像再生 / 音 (同梱 wav) / A/V 同期** の実測あり。ただし **ストリーム音声 (m4a) は `Failed to recognize video file format` で未証明**、`docs/evidence/index.md` §1.5 も同旨 |
| 7 | 完了条件: 不具合があれば別 issue として起票する | 未充足 | 起票記録が無い (#13 が結果として残る可能性はあるが明示的記録は無し) |

- **未検証の区別**: 条件 6 の **「YouTube ストリームの音声」** は未証明 (同梱 wav で代替)。**EditMode 緑と「音が鳴っている」観測は別のもの**。代替 = Run D の実ログ + `docs/evidence/index.md` §1.5。
- **依存**: #30 の仕様確定が #59 の前提。#59 と #58 は同じ論点である。**#58 の「30 曲」基準は `GManager` の現状 (29 / 選曲可 28 / 不可 2) に更新が必要**。

### #30 — [Product Spec] ゲーム体験の理想形：HOLD・歌詞同期/簡素化・全キー案内・任意曲チュートリアル・pen.dev デザイン

- **区分**: **E (判定不能)**
- **実装/証拠**:
  - Issue 本文 247 行目が **「これは実装 Issue ではなく、継続的な要件すり合わせ・デザインレビューの正本」** と自己宣言
  - Issue 本文 249 行目 — 「**状態:** Product intent は確定 / 具体的な操作・採点・データ形状は要相談 / 実装・動作未検証」
  - 仕様書: `docs/ui/lyrics-sync.md` (§2 歌詞 spec)
  - 一部の方向性は既に main に反映: HOLD 採用 (`contracts/schemas/chart.v1.schema.json` の `type: [1,2]`、`LongNote.cs`)、キー案内の単一正本化 (`KeyBindings.cs:23`, `4c55d31`)、歌詞同期 (`LyricsTimeline.cs`)
- **未確定事項 (Issue 本文が明示)**:

| # | 未確定ポイント | 何が決めれば決まるか |
|---|---|---|
| 1 | HOLD の判定は「先頭・終端の 2 判定」か「連続保持 1 判定」か。離すタイミングの許容幅 (`:116`) | オーナーの判定仕様。**点数計算とコンボ寄与に直結するため実装 Worker が決められない** (`:186` 「未合意の仕様を実装担当が勝手に確定しない」) |
| 2 | 途中でキーを離したときの判定、押し直しの可否、同レーン別キーへの持ち替え (`:117`) | 同上 |
| 3 | スコアと最大コンボへの寄与、混在時の 100 万点算出、既存ランキングとの区分 (`:118`) | 同上。**ランキング互換にも影響** (`:176`) |
| 4 | 既存 JSON `{type,num,block,LPB}` に拡張フィールドを足すか v2 にするか (`:119`) | 契約 versioning 方針。**Issue 119 行目「勝手にデータ形式を確定しない」** |
| 5 | 歌詞の最小データ形状とタイミング単位 (秒/ms) (`:131`, `:190`) | 契約 + 移行方針。既存 `lyrics:{startTime,mondai,endTime,furigana,romaji}` と `_text.json` の段階移行 |
| 6 | 歌詞の表示単位 (1 行/複数行)、同期補正、間奏中の表示、歌詞なし HUD レイアウト (`:133`) | デザイン + UX 決定 |
| 7 | ガイドの表示位置・開閉操作・JIS/US キーマップ・本番デフォルト (`:151`) | Pen デザインとの調整。`:189` の「確認したい設計判断」 |
| 8 | Pen デザインのファイル/フレームへの参照と優先順位 (`:192`) | オーナーの .pen 確定 |

- **受入条件 (Issue 本文 180–186 行目の運用ルール)**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 理想形・判断・未決事項を積み上げる仕様討議の場所として機能する | 充足 | Issue 本文 249 行目の状態宣言 |
| 2 | 決定した要件に受け入れ条件を付け、最小単位で Issue を切る | 充足 | 子 Issue の存在 (#164 / #165 / #166 ほか) |
| 3 | 1 Agent = 1 Issue = worktrunk worktree = Draft PR | 充足 | 各子 Issue が `specs/<NNN>/` を持つ |
| 4 | 優先順位は「展示の実機安定性 > 初見の操作理解 > 歌詞なし状態や同期の不具合 > ロングノーツ > 高度な演出」 | 充足 (運用) | 実装順が #233 → v6 → HOLD と整合 |
| 5 | 全ステップで Win64 で実際にプレイ可能なビルドを残す | 未検証 | `docs/evidence/index.md` §1.7「Windows 実機 … 未証明」 |
| 6 | **未合意の仕様を実装担当が勝手に確定しない** | 充足 (運用) | 本 Issue が未確定点を明示的に保持している |

- **未検証の区別**: 条件 5 は実機必須。**「HOLD 実装が main にある」ことは「HOLD の判定仕様が確定した」ことを意味しない** — 判定仕様は本文の 未確定 1–4 のとおり未決。
- **依存**: **#59 の OK/NG 判定が本 Issue に-blocked**。#164 / #165 は closed だが、判定仕様が固まったかの証跡は main にない。


## 3.4 展示ゲート / S0–S4 クラスタ (#3 / #4 / #5 / #6 / #7 / #12 / #13 / #17 / #19 / #21 / #22)

このクラスタは #3 を親とする **段階ゲート**。 Issue 本文自身が「**Gate issue, not a worker implementation worktree**」と宣言しており、実装単位は子 Issue (#12 / #13 / #17 / #19 / #21) が持つ。したがって親ゲートは **D (ブロック)** が基本で、判定の焦点は「**各ステージの証跡が実 Win64 で出ているか**」にある。

### #3 — 展示版: 5 API に限定した契約と段階的実装 (umbrella)

- **区分**: **D (ブロック)**
- **実装/証拠**:
  - 契約: `contracts/openapi.v1.yaml` — **8 path** (`/api/v1/catalog`, `/api/v1/charts/{songId}`, `/api/v1/admin/songs/{songId}`, `/api/v1/admin/songs`, `/api/v1/rankings`, `/api/v1/stats`, `/api/v1/scores`, `/api/v1/leaderboards/{songId}`)。Issue 本文は「5 endpoints」だが、**#151 のランキング・統計・アーカイブ追加で 8 に増加** (commit `13102ee` "merge(#151): ランキング + 統計ダッシュボード + 楽曲アーカイブを統合する" — main ancestor)。**元 Issue の「5 API 限定」は現状の契約と一致しない**
  - 譜面スキーマ: `contracts/schemas/chart.v1.schema.json` — `notes[].type` は `[1,2]` (HOLD 許容、commit `6be2e07` "feat(chart): HOLD のデータモデルと意味論検証を実装 (#164)")
  - 契約 bugfix 記録: Issue #3 コメント「実物の譜面 2 曲を凍結スキーマが拒否 → `maxBlock` の `const: 8` を `integer 1..8` に、`notes` の `minItems: 1` を解除」。commit `2a98c22` "fix(contract): accept every real chart of the Unity project" (**main ancestor**)、web 側コピー `web/src/lib/chart/chart.v1.schema.json` も同期、ドリフト検知テストあり
  - アーキテクチャ正本: `docs/architecture/festival-mvp.md:53-59` — S0–S4 の段階定義と停止条件。`:61` 「**Source changes are not evidence of a working build.**」
  - 段階表: `docs/evidence/index.md` §1 — **S0 proven (partial) / S0-FONT proven (partial) / S1 未証明 / S2 未証明 / S3 未証明 / S4 未証明**
  - D1 適用記録: `docs/operations/d1-migrations.md:59-121` — migration `20261106124714_heavy_harrier.sql` は**リモート D1 に適用済み**（`d1_migrations` id 1 / applied 2026-11-06 12:54:38）。`:206` — 第 2 migration `20261107042653_friendly_purple_man.sql` は **Remote D1 — not applied yet**
  - デプロイ手順: `docs/operations/cloudflare-bootstrap.md`
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 段階順と各 SHA の証跡を記録 | 充足 | `docs/evidence/index.md` §1 の 5 行 + `release/check/stage-manifest.json` (`updatedAt`, `fingerprint.algorithm`, 3 世代の `exhibitDeployment`) |
| 2 | 依存実装前に契約レビュー | 充足 | `contracts/openapi.v1.yaml` + Issue コメントの「インターフェース v1 凍結宣言 (2026-11-06)」 |
| 3 | 過不足ない設計 (Hono → **Void**、React、Vite、Ajv、D1) | 充足 | `web/package.json` — `void 1.6.2`, `vite-plus ^1.4.0`, `react ^19.2.4`, `@pandacss/dev 2.5.0`, `@ark-ui/react 5.40.0`, `konva ^10.9.1`, `lucide-react 1.70.0` |
| 4 | 実 Win テストまで full playback / build を主張しない | 充足 (規律) | `docs/architecture/festival-mvp.md:61`、`docs/evidence/index.md` の「未証明」表記 |
| 5 | **S0–S4 の各段階が Win64 証跡を持つ** | **未充足** | `docs/evidence/index.md` §1 — S1–S4 が全て未証明 |
| 6 | **契約が 5 API のまま** | **未充足 (意図的な逸脱)** | 8 path に増加。 Issue 本文の記述が main と一致しない |

- **未検証の区別**: 条件 5 は **実 Win64** 必須。**Cloudflare に API がデプロイされていることは、Win Unity がその API を読んで再生できることの証明にならない** (S2 / S3 が未証明)。代替 = `docs/evidence/index.md` の自己申告 (S0 partial のみ)。
- **依存**: #4 → #5 → #6 → #7 と S2/S3 の Web 側 (#12 → #19)。#22 が全ゲートの証跡を回収する。

### #4 — [Gate S0/S1] Windows 展示版: ベースラインと YouTube 再生完走

- **区分**: **D (ブロック)**
- **実装/証拠**:
  - S0 相当は **proven (partial)**: `docs/evidence/index.md` §1 — S0 の fingerprint `7ef323ad…`, 「chart/result/retry **proven**; YouTube stream + audio **未証明**」
  - S0-FONT も proven (partial): fingerprint `e07ccba2…`
  - S1 は **未証明**: 同表「S1 | 未証明 | build exists in the #13 worktree — fingerprint `b8c0acde…`」
  - 実機第 3 世代の記録: `docs/evidence/index.md` §1.5 — code state `b77b63c` (2026-11-07 00:01)、Title → Select → まにまに → 判定 76 → Result → `R` リトライ、起動 14.1 秒、**映像あり** (`VideoPlayer prepared=True`)、**音あり** (`rms=0.44 … peak=0.82`、ただし `audioMode=Bundled`)
  - 詳細: `docs/evidence/win64-2026-11-06-release-gate/README-gen3.md` (Run D)
  - プレイヤー log: `docs/evidence/win64-2026-11-06-release-gate/Player-exhibit-743c88d.redacted.log` / `-s1` / `-s2`
  - ランサマリ: `run-summary-exhibit-743c88d.json` — `"resolution": "1280 x 720 (fullscreen=0)"`
  - ゲート判定スクリプト: `release/check/verify-stage.ps1` (`-Stage S0` / `S1`)、`release/check/drive-gameplay.ps1`、`release/check/stage-manifest.json`
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | S0: Unity 6000.5.6f1 が Win64 で compile/build される | 充足 (証跡あり) | `docs/evidence/index.md` §1.5 の第 3 世代 (199 ファイル / 1 588 795 255 bytes、code state `b77b63c`) |
| 2 | S0: 既存 1 曲が ResultScene まで完走、tested artifact と commit SHA を保存 | 充足 | §1.5「判定 76 → Result → `R` リトライまで到達」+ `EXHIBIT-MANIFEST.txt` の世代・SHA |
| 3 | S1: yt-dlp + Deno で combined A/V ストリーミング | **未充足** | `docs/evidence/index.md` §1.5「音源は **同梱 wav** (`audioMode=Bundled`): ストリームの音声トラック（m4a）は `Failed to recognize video file format` で準備できず、**「YouTube ストリームの音声そのもの」は未証明**」 |
| 4 | S1: video-timeline 判定と retry | 充足 (部分) | 映像が `video[playing=True]` で判定 76 まで到達。**「映像の再生時刻に同期した判定」の測定値は未取得** |
| 5 | S1: Win64 artifact を保存 | 充足 | fingerprint `b8c0acde…` が §1 に記録されている |
| 6 | **S0 と S1 を別々に green にする** | **未充足** | §1 の S1 行が「未証明」 |
| 7 | S0 artifact 存在前に破壊的リファクタを始めない | 充足 (運用) | S0 artifact が §1.5 に実在 |
| 8 | **ソースだけの green を禁止** | 充足 (規律) | `docs/evidence/index.md` の冒頭の「A stage without a real Win64 play run is written as **未証明 (unproven)** — no green is claimed」 |

- **未検証の区別**: 条件 3 が核心。**映像が再生して判定が 76 まで進むことは、「YouTube の音声トラックが鳴っている」ことを保証しない** — 実測は同梱 wav の `rms` である。代替 = 追記は同梱 wav の数値のみ。
- **依存**: **Windows 実機 (展示 PC)** が所有者。コード側の実装は #13 が持つ。

### #5 — [Gate S2] Cloudflare API + Unity 動的カタログのデモ

- **区分**: **D (ブロック)**
- **実装/証拠**:
  - API 側: `web/routes/api/v1/catalog.ts` — `catalogVersion` = 公開済みスナップショット数、`Cache-Control: no-store`、アーカイブ済みは非掲載
  - PUT 公開: `web/routes/api/v1/admin/songs/[songId].ts:42` `export const PUT` → `:45` `adminTokenMatches(...)`、1 トランザクションで songs メタデータ + currentVersion + `chart_versions` 不変行追加 + catalogVersion bump、同一曲再 publish で chartVersion +1 (`:140` の同時 publish 対策)
  - D1: migration `20261106124714_heavy_harrier.sql` = **リモート適用済み** (`docs/operations/d1-migrations.md:121`)
  - Unity 側: `Assets/Game/Scripts/Songs/CloudCatalog.cs:89` `CatalogUrl(baseUrl)` / `:94` `ChartUrl(baseUrl, songId, chartVersion)` / `:110` `ParseCatalog(json)`
  - `Assets/Game/Scripts/UI/SelectSceneManager.cs:63` `StartCatalogRefresh()` / `:351` `StartCatalogRefresh` / `:360` `RefreshCatalogLoop` / `:370` `RefreshCatalogOnce` (`:374` `CloudCatalog.GetText(CloudCatalog.CatalogUrl(config.baseUrl), …)` / `:376` `ParseCatalog(body)`) / `:421-434` — 曲 ID + version で chart を取得
  - 設定: `Assets/Game/Resources/CloudCatalogConfig.json` = `{"enabled":true,"baseUrl":"https://flashbeat.rebuildup.dev","chartVersion":1,"refreshSeconds":8}` (**有効**・本番 URL 固定)
  - テスト: `Assets/Tests/Editor/CloudCatalogTests.cs`, `CatalogMergeTests.cs`, `SongCatalogRobustnessTests.cs`, `ScoreCatalogIntegrityTests.cs`, `SceneVideoCatalogOrderTests.cs`
  - 段階表: `docs/evidence/index.md` §1 — **S2 = 未証明** (「no deployed API, no client build」)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | 契約: `contracts/openapi.v1.yaml` (5 routes) + `contracts/schemas/chart.v1.schema.json` | 充足 (拡張伴随) | 8 path に増加 (ランキング等)。**Issue の「5 routes」は現状と不一致** |
| 2 | Backend: native Void + Vite+ + D1、secrets は Infisical | 充足 | `web/package.json` (`void 1.6.2`, `vite-plus`)、`web/env.ts:15` `FLASHBEAT_ADMIN_TOKEN: string()` (runtime secret のみ)、`docs/operations/cloudflare-bootstrap.md` |
| 3 | UI: Panda CSS + Park UI + Konva (Tailwind/shadcn なし) | 充足 | `web/package.json` に tailwind / shadcn **0 件**。`web/panda.config.ts:76` `include: ["gray","blue","red","green","amber"]`、`web/src/components/ui/` = 15 ファイル (すべて `@ark-ui/react` ベース) |
| 4 | Gate: Mac が既存有効 chart を HTTP PUT で公開 | 充足 (コード) / 未検証 (実行) | `admin/songs/[songId].ts:42` PUT。**本監査は PUT を実行していない**。`web/tests/production-api.test.ts:198` `describe("deployed publish API", { skip: !publishEnabled })` — スキップされる構造 |
| 5 | Gate: Win Unity が再ビルドなしでカタログ更新し新曲を再生 | **未充足** | §1 S2 = 未証明 |
| 6 | Gate: 実 yt-dlp ストリーム | 未充足 | #4 / #13 と同一のブロッカー |
| 7 | S1 の runnable build を保持 | 充足 | §1.5 の 3 世代が別フォルダで保持 |
| 8 | このゲートでランキングやエディタ UI を実装しない | 充足 (規律) | ランキングは #7 / #135 に分離 |

- **未検証の区別**: 条件 4–6 は **実デプロイ + Win 実機**。**`production-api.test.ts` が `skip` される限り、本番 API の到達性は緑ではない** — 緑に見える緑が危険。代替 = `docs/evidence/index.md` の「未証明」。
- **依存**: **Windows 実機** + **本番 PUT の承認** (Infisical token が Worker に投入済みかどうか)。#17 が実装側。

### #6 — [Gate S3] Panda CSS + Park UI Web Studio → Unity 公開デモ

- **区分**: **D (ブロック)**
- **実装/証拠**:
  - Web Studio の実装は §3.3 の #19 / #61 / #73 / #135 / #74 / #75 に記載。`web/src/components/new-song/**`, `web/src/components/song-publisher/**`, `web/src/components/chart-editor/**` (19 ファイル、Konva は `web/src/components/chart-editor/note-grid.tsx:1` `from "react-konva"`)
  - 発行経路: `web/pages/publish.tsx:140` → `web/src/lib/publish/client.ts:39` `PUT` / `web/routes/api/v1/admin/songs/[songId].ts:42`
  - E2E: `web/tests/e2e/studio-flow.spec.ts` (4 テスト: 「all screens share the studio shell」「publish shows the admin-token boundary before pressing it」「catalog → editor → publish → back to catalog」「forced catalog states render inside the shared shell」)、`visitor-flow.spec.ts` (viewport ごとの clean console)、`editor-note-flow.spec.ts`
  - 視覚: `web/tests/visual/capture.mjs` の 3 viewport に `1280×720` / `1440×900` を含む → Issue の「1280×720 と 1440×900 checked」に対応
  - 段階表: `docs/evidence/index.md` §1 — **S3 = 未証明** (「no published chart version」)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | UI stack 固定 (Void + Vite+ + React/TS + Panda + Park UI (Ark UI) + Konva + YouTube IFrame API、**Tailwind/shadcn 禁止**) | 充足 | `web/package.json` に tailwind/shadcn 0 件。`web/src/components/ui/*` は `@ark-ui/react` 由来。`web/src/components/chart-editor/youtube-preview.tsx` が IFrame API |
| 2 | Gate: Mac Web Studio が既存 chart JSON を import/edit し Cloudflare へ publish | 充足 (コード) / 未検証 (実接続) | `web/src/lib/new-song/**`, `web/src/components/new-song/new-song-flow.tsx`, `publish-dialog.tsx` |
| 3 | Gate: Win Unity が再ビルドなしで選択画面に出て ResultScene まで再生 | **未充足** | §1 S3 = 未証明 |
| 4 | 1280×720 と 1440×900 で確認 | 充足 (撮影あり) / 未検証 (目視) | `web/tests/visual/capture.mjs` の viewport 設定。ただし**ゲートは現在 exit 1** |
| 5 | UI feedback は小さな分離 Issue にする | 充足 (運用) | `docs/architecture/festival-mvp.md:67` 「New UI feedback can be handled by an unallocated UI iteration issue without blocking playable S2/S3」 |
| 6 | 依存: S2 の remote catalog が証明済み / 最後の Windows artifact を保持 | 未充足 | S2 が未証明 |

- **未検証の区別**: 条件 3 は **実 Win64**。**Web 側の publish テスト緑は、公開が D1 に到達し Win Unity がそれを読めることを保証しない**。代替 = `studio-flow.spec.ts` 緑 + §1 の「未証明」。
- **依存**: **#5 (S2) が未証明 = S3 の前提が未成立**。これが #6 を D にする直接理由。

### #7 — [Gate S4 optional] 展示用クラウドランキング

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `web/routes/api/v1/scores.ts`
    - `:10-17` 仕様コメント — 「client-sent `score` that disagrees is a **422**」、「duplicate identical submission (same songId + nickname + score) within **10 seconds** is a **429** with `Retry-After`」
    - `:115` `const score = computeUnityScore(request.perfect, request.great, request.bad, chart.noteCount);` → **サーバ側で再計算**
    - `:53` / `:74` / `:101` / `:111` / `:127` / `:141` — 各 422 の診断経路
  - `web/routes/api/v1/_score-model.ts:176` — `if (sum !== noteCount)` → 「perfect + great + bad + miss must equal the N notes of chart version M」 = **Issue の「check judgment count==noteCount」**
  - `web/routes/api/v1/_score-model.ts:182` — `maxCombo > noteCount` → 422
  - `web/routes/api/v1/_score-model.ts:213` — `maxScore = Math.fround(noteCount * 5)` (float32 精度の明示)
  - `web/routes/api/v1/leaderboards/[songId].ts:22` `const LEADERBOARD_SIZE = 10;` / `:51` `.limit(LEADERBOARD_SIZE)` = **top 10 per songId+chartVersion**
  - D1: migration `20261106124714_heavy_harrier.sql` に `scores` / `chart_versions` テーブル (リモート適用済み)
  - Unity 側: `Assets/Game/Scripts/UI/RankingClient.cs:113` (nickname 検証) / `:143` (POST body) / `:205` (GET) / `:240` / `:243` `PostScore(...)` / `:277` / `:318`; `Assets/Game/Scripts/UI/ResultRankingOverlay.cs:186` `GetLeaderboard` / `:307` `PostScore` / `:325` 再取得
  - v6 側: `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:864` `RankingClient.PostScore(...)` / `:1332` `GetLeaderboard(...)`; `V6/Select/Data/V6SelectDataSource.cs:466`
  - テスト: `web/tests/score-model.test.ts`, `web/tests/api.test.ts`, `Assets/Tests/Editor/RankingClientTests.cs` (11 テスト)
  - 段階表: `docs/evidence/index.md` §1 — **S4 = 未証明**、証跡欄は「not started — keep the current playable build」
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | D1 に score submission と songId+chartVersion ごとの top 10 | 充足 | `scores` テーブル + `leaderboards/[songId].ts:22,51` |
| 2 | サーバが score を再計算 | 充足 | `scores.ts:115` + `_score-model.ts:176,182,213` |
| 3 | judgment count == noteCount を検査 | 充足 | `_score-model.ts:176` |
| 4 | 単純な編集可能ニックネーム、アカウント無し、実アンチチート無し | 充足 | `RankingClient.cs:113` の ASCII/かな検証。`RankingClientTests.cs:26` `ValidateNickname_TrimsAndAcceptsAsciiAndKana` |
| 5 | ランキングを casual / unverified と表示 | 充足 (要文言確認) | `V6RankingView.cs` / `V6RankingLogic.cs:124` にオンラインランキング表示。**「casual/unverified」の文言ラベルは本監査で実文字列を確認していない** → 観測不足 |
| 6 | Gate: 実 Win64 play が result を送信する | 未充足 | §1 S4 = 未証明 |
| 7 | Gate: 2 番目のブラウザ/クライアントがそれを見る | 未充足 | 同上。**「2 台目のクライアント」の観測記録が無い** |
| 8 | 永続ローカルハイスコアを server の権威にしない | 充足 (コード) | `ScorePersistence` はローカルの保存であり、サーバの真値としない。`GManager.Hiscore` は PlayerPrefs |
| 9 | 時間がなければ S3 を維持 | 充足 (運用) | §1 S4 行「keep the current playable build」 |

- **未検証の区別**: 条件 6–7 は **実 Win64 + 2 クライアント**。**`score-model.test.ts` 緑は「サーバが 422 を返す」ことの証明であって、「実プレーの結果が実際に submit される」ことの証明ではない**。代替 = サーバ側ユニットテストのみ。
- **依存**: #21 (Unity 送信側) + #5 (S2 のデプロイ済み API) が前提。**Issue 自身が optional を明示**。

### #12 — [S2/Mac] Void + Vite+ React + Panda CSS/Park UI 最小アプリの足場

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `web/package.json`
    - `"dev": "vp dev"` / `"build": "vp build"` / `"preview": "vp preview"` / `"postinstall": "void prepare && panda codegen"`
    - `void 1.6.2` / `@void/react 1.6.2` / `vite: npm:@voidzero-dev/vite-plus-core@^1.4.0` / `vite-plus ^1.4.0` / `react ^19.2.4` / `@pandacss/dev 2.5.0` + `@pandacss/preset-base` + `@pandacss/preset-panda` + `@pandacss/vite` + `@pandacss/types` / `@park-ui/cli 1.0.1` / `@ark-ui/react 5.40.0` / `konva ^10.9.1` / `react-konva ^19.3.2` / `lucide-react 1.70.0`
    - **`tailwind` / `shadcn` は package.json に 0 件** (grep で確認)
  - `web/panda.config.ts` — `include` は 5 スケール (`gray`, `blue`, `red`, `green`, `amber`)。`web/src/theme/tokens/colors.ts`, `text-styles.ts`, `layer-styles.ts`, `conditions.ts`, `global-css.ts`, `keyframes.ts`, `animation-styles.ts`, `durations.ts`, `shadows.ts`, `z-index.ts`
  - `web/src/components/ui/` = 15 ファイル、`@ark-ui/react` の `ark` factory ベース (`group.tsx`, `tabs.tsx`, `field.tsx`, `select.tsx`, `dialog.tsx`, `button.tsx`, `input.tsx`, `toast.tsx`, `icon.tsx`, `icon-button.tsx`, `close-button.tsx`, `loader.tsx`, `spinner.tsx`, `span.tsx`, `absolute-center.tsx`)
  - `web/src/app/` = アプリシェル (`topbar.tsx` が `Portal` / `createListCollection` を使用、`drawer.tsx` が `FOCUSABLE_SELECTOR` と純関数 `nextFocusTarget`)
  - `web/src/theme/colors/{gray,blue,red,green,amber}.ts` + `docs/design/web-token-mapping.md`
  - `web/void.config.ts`, `web/void.lock.json`, `web/components.json` (Park UI 設定), `web/vite.config.ts`, `web/tsconfig.json`
  - テスト: `web/src/app/drawer.test.ts`, `web/src/app/catalog-view.test.ts`, `web/src/app/catalog-panel.test.tsx`, `web/src/app/topbar.test.tsx`, `web/src/components/ui/select.test.tsx` ほか **47 ファイル**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: 公式 Void scaffold (`--toolchain vite-plus --framework react`)、生成 agent ファイルの確認 | 充足 | `web/package.json` の `void 1.6.2` + `void.lock.json` + `web/AGENTS.md` |
| 2 | T2: Panda の公式 Vite plugin を React plugin の前、typed styled-system codegen、Void の実ソースディレクトリを include | 充足 | `web/vite.config.ts` + `web/panda.config.ts` (`include` 5 スケール) + `"codegen": "panda codegen"` |
| 3 | T3: `@park-ui/cli init` と prebuilt Button/Input/Field/Tabs/Dialog/Select/Toast | 充足 | `web/src/components/ui/` の 15 コンポーネント。**Park 由来であることが `web/components.json` で宣言されている** |
| 4 | T3: **独自 UI プリミティブを作らない** | 充足 | `web/src/components/ui/` は全て `ark` factory / `@ark-ui/react` の再エクスポート。**自作 primitive はありません** |
| 5 | T4: コンパクトな dark シェル、単一 accent、sidebar/top tools、1280×720 と 1440×900 対応、loading/empty/error | 充足 (コード) | `web/src/app/**` + `web/src/components/welcome/**` + `catalog.ts` の `CatalogMode` (loading/ready/empty/error) |
| 6 | T5: `vp check` / `vp build` + スクリーンショット証跡、**tailwind/shadcn が package に無い** | 充足 (証跡あり) | `web/tests/visual/capture.mjs` の撮影 + `docs/evidence/web-visual/{before,after,interaction}/`。tailwind/shadcn 0 件は grep で確認 |
| 7 | Acceptance: エディタが存在する前に **単独で起動・ビルドできる** | 充足 | `"dev": "vp dev"` / `"build": "vp build"` が `web/` 単体で完結 |
| 8 | Acceptance: 新しい backend / 自作 UI システムを作らない | 充足 | backend は `web/routes/api/v1/**` の既存のみ。UI は Park 由来 |

- **未検証の区別**: 条件 6 の「スクリーンショット証跡」は撮られしているが、**本監査では目視していない** (Issue が求める 1440×900 / 1280×720 の目視レビュー)。代替 = 視覚ゲートの機械的結果 + token 対応表。
- **依存**: #19 / #135 / #73 が本足場の上に載る。**#5 の子**。

### #13 — [S1/Win] yt-dlp+Deno→Unity VideoPlayer 1曲ストリーミング完走

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs`
    - `:92` `public static class YouTubeStreamResolver`
    - `:74-80` — 「**the media is downloaded to a local cache instead of streamed by URL**… Media Foundation (`MFCreateSourceReaderFromURL`) cannot send custom HTTP headers. Downloading the exact same bytes with yt-dlp and playing the local file sidesteps that limitation entirely.」
    - `:111` `ResolveYtDlpPath()` — PATH → mise install tree → Python install dirs → `Assets/StreamingAssets/yt-dlp/yt-dlp.exe` → Unix 3 箇所 (`:126/:135/:148-156/:156`)
    - `:166` `ResolveDenoPath()` / `:269` — ffmpeg 探索
    - `:67-91` — Invidious 経路を **採用しない** ことにした理由のコメント (「0/13 hosts return valid JSON today」)
  - `Assets/Game/Scripts/Gameplay/YouTubeMediaCache.cs:52` `public static class YouTubeMediaCache` / `:294` `YouTubeStreamResolver.DownloadAsync(...)` / `:91` `DownloadOverride` (テスト用フック)
  - `Assets/Game/Scripts/Gameplay/MediaCacheManager.cs:17` — `GetCachedVideoIds` / `:52` `GetCachedSongCount` / `:58` `GetCacheSizeBytes` / `:76` `Clear` / `:93` `ClearAll`
  - `Assets/Game/Scripts/Gameplay/MusicManager.cs`
    - `:43` `PrepareTimeout = 20f` / `:44` `FallbackStartTimeout = 30f` / `:48` `StallFailSeconds = 30f` — **「unconditional fixed 8-second wait」は無い**
    - `:260-263` + `:538-547` — `DisableInvidiousPlayer()` で Invidious 経路を削除
    - `:337-343` / `:362-368` — 結合 / 分割 (video-only + audio-only) の 2 経路
    - `:401` `audioMode = PlaybackRecovery.DecideAudioSource(combined, videoReady, audioReady, Music != null);`
    - `:168-169` — `Resources.LoadAsync<AudioClip>("Musics/" + songName)`
    - `:580-589` — direct URL probe
  - タイミング同期: `Assets/Game/Scripts/Gameplay/MediaTimeline.cs`, `Assets/Game/Scripts/Gameplay/PlaybackRecovery.cs`, `Assets/Game/Scripts/Gameplay/SongEndStateMachine.cs:78`
  - テスト: `Assets/Tests/Editor/PlaybackClockTests.cs`, `MediaTimelineTests.cs` ほか `Assets/Game/Tests/` の再生系テスト
  - 実機: `docs/evidence/win64-2026-11-06-release-gate/README-gen3.md` (Run D) — 映像 `prepared=True` / `video[playing=True]`、音 `rms=0.44…0.56 peak=0.82`、**`audioMode=Bundled`**、**m4a は `Failed to recognize video file format`**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: 実 yt-dlp/Deno probe、`-F` で combined A/V 形式を選択、**download-to-local fallback なし** | **未充足 (意図的な逸脱)** | 実装は **ローカルキャッシュへダウンロード**して再生する (`YouTubeStreamResolver.cs:74-80`, `YouTubeMediaCache.cs:294`)。Issue は「no download-to-local fallback」と明記しているが、Media Foundation の制約に対する**明示的な設計変更**として main に取り込まれている |
| 2 | T2: VideoPlayer Prepare→Play、音声出力、fail/retry UI、**固定 8 秒待ちの除去**、Invidious fallback 除去 | 充足 (コード) | `MusicManager.cs:43-48` (タイムアウト定義)、`:538-547` (Invidious 除去)、`:401` `PlaybackRecovery.DecideAudioSource` |
| 3 | T3: note 描画/判定を video 再生時刻に同期、buffering で pause、**stall 越しに `Time.time` を累積しない**、既存 note JSON 変換を維持 | 充足 (コード) / 未検証 (実機) | `MediaTimeline.cs` + `PlaybackClockTests.cs` / `MediaTimelineTests.cs`。**実機での判定同期誤差の測定値は未取得** |
| 4 | T4: Unity EditMode タイミングテスト + **Win64 実ストリーミング select→result→retry**、測定値 (起動遅延・判定同期誤差) の取得 | **部分充足** | EditMode テストは存在。実 Win64 の select→result→retry は §1.5 で到達済み。ただし**起動遅延 14.1 秒**の記録は `docs/evidence/index.md` §1.5 にあり、**判定同期誤差の数値は未記載** |
| 5 | T5: SHA と chart タグ付けした runnable Win build を保存 | 充足 | §1 S1 行 fingerprint `b8c0acde…`、§1.5 第 3 世代 code state `b77b63c` + `EXHIBIT-MANIFEST.txt` |
| 6 | No scope: 新 UI / auth / 編集ツール / オフライン / 新 note type | 充足 (規律) | 該当実装の追加なし |

- **未充足の具体箇所**: **T1 の「download-to-local fallback なし」**。これは Issue の陳腐化 (2026-11-06 時点の想定) であり、main の実装は **`googlevideo.com` の URL を Unity の `VideoPlayer` が読めない** (`WindowsVideoMedia error 0x80070005` / `MFCreateSourceReaderFromURL`) ため、**同一バイト列を yt-dlp で落してローカル再生する**方式に置き換えたもの。Issue #13 のコメント 2 (rebuildup 2026-11-06) がこの実測報告の出典。
- **未検証の区別**: **「映像が再生した」「判定が 76 まで進んだ」は「ストリームの音声トラックが鳴った」ことを保証しない**。実測の `rms` は **同梱 wav**。代替 = `docs/evidence/win64-2026-11-06-release-gate/Player-exhibit-*.redacted.log` の `audioMode=Bundled` 行。
- **依存**: #4 (S1 ゲート) の実装側。**この Issue を閉じるには、ストリーム音声 (m4a) の再生が実 Win64 で証明されること**が必要。

### #17 — [S2/Win] Unity の固定楽曲配列を Cloud Catalog 読込へ切替

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `Assets/Game/Scripts/Songs/CloudCatalog.cs`
    - `:18` `public static class CloudCatalog`
    - `:89` `public static string CatalogUrl(string baseUrl)`
    - `:94` `public static string ChartUrl(string baseUrl, string songId, int chartVersion)`
    - `:110` `public static SongData[] ParseCatalog(string json)`
  - `Assets/Game/Scripts/UI/SelectSceneManager.cs`
    - `:63` `StartCatalogRefresh()` (選曲画面入場時)
    - `:334-343` — 既存の refresh が動いていればスキップ / 完了でクリア
    - `:351` `StartCatalogRefresh` / `:357` `StartCoroutine(RefreshCatalogLoop(config))`
    - `:360` `RefreshCatalogLoop` / `:370` `RefreshCatalogOnce` / `:374` `CloudCatalog.GetText(CloudCatalog.CatalogUrl(config.baseUrl), (code, text) => …)` / `:376` `status == 200 ? CloudCatalog.ParseCatalog(body) : null`
    - `:400` `if (v6Data != null) v6Data.RefreshAsync();` — v6 選曲画面への反映
    - `:421-434` — `CloudCatalog.ChartUrl(config.baseUrl, song.remoteId, version)` で **exact version** の譜面を取得
  - `Assets/Game/Scripts/Songs/GManager.cs:88` — `public static SongData[] ActiveSongs => mergedSongs;` (:80 のコメント「the cloud snapshot only *updates* the songs it knows」) — **固定カタログが真値ではなく、cloud が上書きする構造** (Issue タスク 3 の「do not use Resources or parallel GManager arrays as remote catalog truth」を満たす)
  - 設定: `Assets/Game/Resources/CloudCatalogConfig.json` = `enabled: true`, `baseUrl: https://flashbeat.rebuildup.dev`, `refreshSeconds: 8`
  - テスト: `Assets/Tests/Editor/CloudCatalogTests.cs`, `CatalogMergeTests.cs`, `SongCatalogRobustnessTests.cs`, `ScoreCatalogIntegrityTests.cs`
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: 契約に fit した小さな typed UnityWebRequest GET クライアント、base URL を config 化、unavailable/empty/error で crash しない | 充足 | `CloudCatalog.cs:89,94` + `CloudCatalogConfig.json` + `SelectSceneManager.cs:374-376` (`status == 200 ? … : null`) |
| 2 | T2: 選曲画面入場時 + idle 中 5–10 秒周期で更新、remote の曲名/メタデータを既存 UI に表示、realtime socket 無し | 充足 (コード) | `SelectSceneManager.cs:63` (入場時) + `:360 RefreshCatalogLoop` + `CloudCatalogConfig.json` `refreshSeconds: 8` (**Issue 指定の 5–10 秒範囲内**) + socket コードなし |
| 3 | T3: (songId, chartVersion) で譜面を取得し完走まで snapshot を保持、**Resources / 並列配列を remote catalog の真値にしない** | 充足 (コード) | `SelectSceneManager.cs:421-434` (exact version) + `GManager.cs:88` `ActiveSongs` の merge 構造 |
| 4 | T4: 動的 Youtube VideoPlayer を 1 個使う（曲ごとに object を作らない） | 充足 | `MusicManager` は `songVideo` を 1 つ保持し、`YPlayer[GManager.songID]` を初期化時に一度だけ張る構造。`MusicManager.cs:542` の `YPlayer[GManager.songID]` 参照が単一 |
| 5 | T5: **Win64 E2E — PUT で新曲を公開 → 再ビルドなし → 選曲 → ストリーム → ノーツ/判定 → result → artifact + SHA** | **未充足** | `docs/evidence/index.md` §1 S2 = 未証明 |
| 6 | Gate: S2 が単独で demo でき、S1 の last-good build を保持 | **未充足** (S2 側) | §1.5 に 3 世代が保持されている点は充足 |

- **未検証の区別**: 条件 5 は **実 Win64 + 本番 PUT**。**`CloudCatalogTests.cs` 緑は「remote catalog を読める」ことの証明であって、「公開直後の新曲が再ビルドなしで出現して再生できる」ことの証明ではない**。代替 = ユニットテスト + `CloudCatalogConfig.json` の設定確認。
- **依存**: **#13 (S1) と #15/#16 (公開スキーマ)** が前提 (Issue 本文)。加えて **本番 API の到達性**。

### #19 — [S3/Mac] Park UI 楽曲メタデータ・Publish 画面を接続

- **区分**: **A (実装済み・証拠待ち)**
- **実装/証拠**:
  - `web/src/components/song-publisher/` — `song-publisher.tsx`, `song-metadata-form.tsx`, `publish-dialog.tsx`, `publish-result.tsx` ほか
    - `song-publisher.tsx:29` — 「#18, the request goes to `PUT /api/v1/admin/songs/{songId}`」
    - `song-publisher.tsx:290` `const [token, setToken] = useState("")` — **token は実行時入力**
    - `song-publisher.tsx:400` token を publish へ渡す / `:426` 依存 / `:569` ダイアログへ渡す
    - `publish-dialog.tsx:10-11` — 「the staff token of the operator. The token lives in the memory of the browser tab for this screen」/ `:117` `Field.Label` "Staff token" / `:144` token 未入力なら Publish ボタン無効
  - `web/src/lib/publish/client.ts:7` — 「The staff token travels as a bearer token in …」 / `:22-23` `token: string` = 「Staff token, entered by the operator at runtime」 / `:39` `fetch(...)`
  - `web/src/lib/publish/transport.ts:9-11` — 「`/api/v1/admin/songs/{songId}` of the host the studio was loaded from … the value is a public URL, the staff token is never part of …」
  - `web/env.ts:15` — `FLASHBEAT_ADMIN_TOKEN: string()` (**Worker runtime secret のみ**。Web バンドル・Unity バイナリ・コミットに出さない = Issue タスク 2 の「never bundled」)
  - `web/src/components/chart-editor/` — `chart-editor.tsx`, `note-grid.tsx` (Konva `react-konva`), `lyrics-editor.tsx`, `selection-panel.tsx`, `validation-panel.tsx`, `youtube-preview.tsx` = **Issue タスク 1 の「existing chart editor」**
  - `web/src/components/new-song/` — `new-song-flow.tsx:16` / `:30-31` / `:137` / `:216` (Issue #135 の別動線)
  - テスト: `web/src/lib/publish/__tests__/{client,draft,mock,transport,validation}.test.ts`, `web/src/components/song-publisher/{song-metadata-form.test.tsx,song-publisher.test.ts}`
  - 証跡: `docs/evidence/web-acceptance/{1920x1080,2560x1440,2560x1664}/` + `docs/evidence/web-visual/`
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: 最小の曲メタデータ入力 (title, artist, video ID, level, duration) + 既存 chart editor、validation が可視 | 充足 | `song-metadata-form.tsx` + `song-metadata-form.test.tsx` + `chart-editor/validation-panel.tsx` |
| 2 | T2: 1 つの Publish ボタン → typed fetch で `PUT /api/v1/admin/songs/{songId}`、**token は operator の runtime entry から (bundle には入れない)**、success/error 状態 | 充足 | `client.ts:39` + `publish-dialog.tsx:117,144` + `env.ts:15` |
| 3 | T3: 1280×720 と 1440×900 で preview/test、**横溢れなし**、密度とタイポグラフィの一致 | **未充足** | **視覚ゲートが `exitCode: 1`**、`horizontalOverflow: ["home@390x844"]` (`specs/075-visual-gate/evidence/2026-11-08/index.json`)。1280×720 / 1440×900 の 2 viewport 自体は撮影済みだが、横溢れは 390 幅で残存 |
| 4 | T4: Mac ブラウザ publish → Windows Unity のカタログ更新と full streamed play、**S3 runnable build とログを保存** | 未充足 (実装側) | **publish → Win Unity の再生は #6 (Gate S3) の責務**。本 Issue は Web 側のみ。ログ保存の記録は §1 S3 = 未証明 |
| 5 | Gate: **Unity 再ビルドなしで Web UI が再生可能な公開 chart を作る** | 充足 (Web 側) / 未検証 (Win 側) | publish 経路は実装済み。**再ビルドなし再生は #6 で未証明** |
| 6 | No scope: private cloud drafts / auth UI / 余計な styling infrastructure | 充足 (規律) | 認証 UI は無い。token は 1 入力欄のみ |

- **未検証の区別**: 条件 3 は **実ブラウザ撮影 + 目視**。**Vitest 緑は「1280×720 で横溢れしない」ことを保証しない** — 実測で溢れがある。代替 = 視覚ゲートの機械的結果。
- **依存**: #12 (足場) / #16 (公開 API) が前提。**#6 が本 Issue の Win 側ゲート**。

### #21 — [S4/Win optional] Unity リザルトをオンライン送信しランキング表示

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - `Assets/Game/Scripts/UI/RankingClient.cs`
    - `:113` — nickname 検証 (ASCII + かな)
    - `:143` — POST body 構築
    - `:205` — GET leaderboard
    - `:240` — endpoint 組み立て
    - `:243` `public static IEnumerator PostScore(string songId, int chartVersion, string nickname, …)`
    - `:277` `public static IEnumerator GetLeaderboard(string songId, int chartVersion, Action<Result> onDone)`
    - `:318` — レスポンス解釈
  - `Assets/Game/Scripts/UI/ResultRankingOverlay.cs:18` `public class ResultRankingOverlay : MonoBehaviour` / `:186` `GetLeaderboard` / `:307` `PostScore` / `:325` 送信後の再取得
  - v6: `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:864` `RankingClient.PostScore(song.remoteId, chartVersion, nickname, …)` / `:1332` `GetLeaderboard(remoteId, chartVersion, r => { result = r; })`
  - v6 選曲: `Assets/Game/Scripts/UI/V6/Select/Data/V6SelectDataSource.cs:466` `GetLeaderboard(remoteId, chartVersion, …)`
  - v5 選曲: `Assets/Game/Scripts/UI/V5/SelectView.cs:1703` `GetLeaderboard(song.remoteId, song.chartVersion, …)`
  - ローカル保存: `Assets/Game/Scripts/Persistence/ScorePersistence.cs` + `Assets/Game/Scripts/Persistence/SaveLoadManager.cs`
  - テスト: `Assets/Tests/Editor/RankingClientTests.cs` (11 テスト: `:26` `ValidateNickname_TrimsAndAcceptsAsciiAndKana`, `:50` `BuildScoreRequestJson_ContainsSubmissionFields`, `:100` `ParseLeaderboard_ReadsEntries`, `:142` `Urls_FollowTheOpenApiContract`, `:152` `IsValidSongId_FollowsTheServerPattern`, `:165` `Nickname_RoundTripsThroughPlayerPrefs`)
  - 契約整合: `RankingClientTests.cs:142` `Urls_FollowTheOpenApiContract` = `contracts/openapi.v1.yaml` との一致を固定
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: 譜面完走後に nickname と P/G/B/M で 1 回 POST、songId と chartVersion は実行中の run から固定、**end での missed notes 修正** | 充足 (コード) / 未検証 (実機) | `V6ResultView.cs:864` / `ResultRankingOverlay.cs:307` / `RankingClient.cs:143`。**end 時の missed notes 修正の実機挙動は未確認** |
| 2 | T2: **結果はネットワーク遅延に関わらず即表示**、非同期で top10 GET。**ローカルハイスコアを server の権威にしない** | 充足 (コード) | `V6ResultView.cs` は result 表示と送信を分離 (`V6ResultLogic.cs:224` 「送信中 (RankingClient.PostScore の応答待ち)」) + `:1332` 非同期取得 |
| 3 | T3: submitting / error / leaderboard-empty の状態を既存 uGUI/TMP で表示、UI framework 変更なし | 充足 | `V6ResultLogic.cs:224` + `V6ResultView.cs` の状態表示。`Assets/Game/Scripts/UI/V5/UISizeProfile.cs` 体系を維持 |
| 4 | T4: **End-to-end streamed play → submit → 2 台目のデバイスで表示**、artifact 保存 | **未充足** | §1 S4 = 未証明 |
| 5 | Gate: 実 run 後にスコアボードが見える、S3 build は fallback のまま | **未充足** (実機) | 同上 |
| 6 | No scope: login / anti-cheat | 充足 (規律) | 該当実装なし |

- **未検証の区別**: 条件 4 は **実 Win64 + 2 台目**。**`RankingClientTests.cs` 緑は「JSON が正しい」ことの証明であって、「実プレーの結果が実際に POST され、2 台目に表示される」ことの証明ではない**。代替 = クライアントのユニットテスト + サーバ側 `score-model.test.ts`。
- **依存**: #17 (catalog) と #7 (サーバ側 API) が前提。**optional**。

### #22 — [Release/Win] 各ステップ Win64 展示版 smoke / パッチ配布ゲート

- **区分**: **B (部分実装)**
- **実装/証拠**:
  - 証跡インデックス: `docs/evidence/index.md` — §1 ステージ表 / §1.5 展示フォルダ 3 世代 / §1.6 Mac 検証 / §1.7 統合後再検証
  - 機械可読な双子: `release/check/stage-manifest.json` — `fingerprint.algorithm` (SHA-256 over `<fileSha256>  <relative/path>`) / `artifactRoots` / `exhibitDeployment` (activeGeneration 3、3 世代の path / content / sourceBuild / buildResult)
  - 検証スクリプト: `release/check/verify-stage.ps1` (`-Stage S0` 等) / `drive-gameplay.ps1` / `redact-log.ps1` / `README.md`
  - 承認チェックリスト: `docs/operations/release-approval-checklist.md` — 「**1 項目でも確認できていないものは「未証明」と書き、green を作りません**」/ A. 前提と権限 / B. 成果物チェック / … の複数セクション
  - runbook: `docs/operations/festival-runbook.md` — §1 展示フォルダ / **§4 1280×720 スモークチェックリスト (10 項目のチェックボックス)** / 「**起動は `FlashBeat.exe` の直接起動だけ**」「ランチャー / `Play.cmd` / `Update.cmd` / 起動時自動更新 / ロールバックコマンドは **存在しません**」
  - 補助: `docs/operations/exhibit-runbook.md`, `docs/operations/exhibit-handoff-2026-11-08.md`, `docs/operations/release-gate.md`, `docs/operations/update-patch.md` (#36), `docs/operations/three-resolution-fullscreen-verification.md`
  - 段階表 §1: **S0 proven (partial) / S0-FONT proven (partial) / S1 未証明 / S2 未証明 / S3 未証明 / S4 未証明**
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | T1: S0–S3 (S4 任意) ごとに**実 Win64 play 証跡と正確 SHA** — select → YouTube ストリーム → notes/result → retry、ログ/スクリーンショット + CF URL/chartVersion | **未充足** | §1 の S1–S4 が未証明。**S0 のみ partial** |
| 2 | T2: 1280×720 での高速 UX smoke (日本語グリフ、入力マッピング、音画同期、ネットワークエラー状態)、**source チェックと実ゲーム証跡を区別** | **未充足 (チェックリスト未実施)** | `festival-runbook.md` §4 の 10 項目は **チェックボックス未記入**。`:99` 自身が「実機 1280×720 パネルでの fullscreen は **未検証**」と記載。`:189` は「ウィンドウモードで `FlashBeat.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0`」を代替手順として提示 |
| 3 | T3: 証明済み Win64 artifact を保存し短い展示 runbook を維持 | 充足 | `stage-manifest.json` の 3 世代 + `festival-runbook.md` + `exhibit-runbook.md` |
| 4 | T4: パッチは **テスト済みの新リリースを承認**し、**ゲームを閉じて 1 回適用 (#36) してから `FlashBeat.exe` を直接起動**。展示中に強制更新しない、**ロールバック経路を足さない**、壊れた更新は別パッチで出版 | 充足 (運用) | `festival-runbook.md` 冒頭 + `docs/operations/update-patch.md` の「**`OnApplicationQuit` からの自動適用は存在しません** (issue #118 で廃止)」 |
| 5 | T5: 承認済み PR のみを release に merge、**本番 main と Cloudflare を独立に承認**、backend API v1 互換 | 充足 (運用) | `release-approval-checklist.md` の A 節「片方の承認を他方の承認に読み替えない」 |
| 6 | Acceptance: 各ゲートに**文書化された直接起動可能な `FlashBeat.exe`** が存在 | **部分充足** | §1.5 に第 3 世代の直接起動構成が実在。**S1–S3 の直接起動可能ビルドは未証明** |
| 7 | Acceptance: **未検証の後続パッチが現在の展示ビルドを置き換えない** | 充足 (運用) | `festival-runbook.md`「展示中は **読み取り専用**」+ `update-patch.md`「1 世代だけ残します」 |
| 8 | Acceptance: ランチャー無し、ユーザー向けロールバック無し | 充足 | `festival-runbook.md` 冒頭 + `update-patch.md` |

- **未検証の区別**: 条件 1–2 が核心。**`release-approval-checklist.md` が存在して緑でも、チェックボックスが未記入なら gate は通っていない** — 文書自身の原則どおり「未証明」。代替 = `docs/evidence/index.md` の自己申告 (S0 partial のみ)。
- **依存**: **全 S ゲート**。且つ #22 は **PR readiness を確認するのみで merge しない** (チェックリスト A 節) = 統合は cc 側。


## 4. 推奨する次の実装単位 (≤10 件)

cc 指定の優先順位 (**P0 動作基盤 → v6 UI 完全統合 → ゲーム機能完成 → Web Studio → 残件**) に沿って並べた。各単位は **1 Worker が 1 PR で完了できる粒度**、かつ **1 件を直ちに検証できる**ことを基準にしている。

### 優先順位サマリ

| # | 単位 | 対象 Issue | 優先度 | 想定工数 | 完了判定 (DoD) |
|---|---|---|---|---|---|
| U1 | v6 プレイフィールドの再有効化 + `Lane` 構造の復活 | #219, #233(B) | **P0** | 中 | 実 Win64 で 8 レーンとノーツが見え、判定が同期する |
| U2 | ゲーム確認窓 (0.86s → 1.6s) と旧歌詞二重描画の解消 | #233(B) | **P0** | 小 | 旧 v5 歌詞 Canvas が GameScene に出ない |
| U3 | v6 Option「データ初期化」の保存範囲統一 | #241 | P0 | 小 | `ClearSaveData` が `ScorePersistence.ResetAll()` を呼ぶ |
| U4 | Web の publish → chart ドラフト復元動線 | #98 | P0 (Web) | 小 | `/chart?draft=…` を開くと編集内容が復元される |
| U5 | 視覚ゲートの緑化 (狭幅横溢れ) | #74, #75, #61 | P0 (Web) | 中 | `npm run visual` が exit 0 |
| U6 | 展示 patch line `b677f8f` への #140 / #134 移植 | #140, #134 | P0 (展示) | 中 | 展示ビルドで豆腐 0 かつリザルト登録が動く |
| U7 | Win64 実機解像度スイープ (1920×1080 / 1280×720 / 1024×768) | #88, #96 | P0 | 中 | `displayCoverage ≥ 0.99` の実測値が記録に残る |
| U8 | ストリーム音声 (m4a) の再生と `audioS.Play()` の実証 | #13, #4 | P0 (S1) | 中〜大 | 展示ビルドでストリーム音声が鳴る |
| U9 | `skills-check` と agent 環境の欠落ファイル | #56 | 残件 | 小 | `mise run skills-check` が lock と一致する |
| U10 | 受入記録の整備 (#59 / #58 / #22 のチェックリスト実測) | #59, #58, #22 | 残件 | 中 | OK/NG/未検証の判定表とスクリーンショットが残る |

---

### U1 — v6 プレイフィールドの再有効化 + `Lane` 構造の復活 (#219, #233)

- **なぜ最優先か**: #233 の実機表示が壊れている直接原因で、#217 / #223 / #242 / #250 のすべてがここで塞がれている。cc は「8 レーンが見えない」が描画失敗ではないと実測済みだが、**`V6UiMode.Playfield = false` により v6 プレイフィールド自体が描画されない**状態は未解決。
- **対象**:
  - `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` — `public static bool Playfield = false;` → 有効化 (または v6 実行時に限り true にする profile 追加)
  - `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldRuntime.cs:52` — `if (!FlashBeat.UI.V6.V6UiMode.Playfield) return;` のガードを実装修正に合わせる
  - `Assets/Game/Scripts/UI/V6/V6GameRuntime.cs:370-374` — `HideByName("Lane")` が `Playfield` 有効時に旧 v5 構造を隠すか残すかを決定 (どちらでもよいが、採る方を明示すること)
  - `Assets/Game/Scenes/GameScene.unity:2837` — `m_Name: Lane` の `m_IsActive: 0` (**enabler が 0 件**)。**有効化するコードパスを 1 つ追加** (例: `GameSceneManager` の `Start()` で `GameObject.Find("Lane").SetActive(true)`)。ただし **v6 有効時は v5 経路と二重描画になるため排他が必要** (`Assets/Game/Scripts/UI/V5/GameUiExclusivity.cs` の既存パターンに倣う)
  - `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldView.cs:38` `NoteLayer` / `:88-115` lanes/separators/judgeLine / `:155-165` lane flash — 既存の投影ロジックは完備
- **注意**: `Assets/Game/Scripts/Gameplay/Notes.cs:105-129,158-165` は既に `V6PlayfieldView.Current` 経由で head/body Image を生成する。**再有効化で破綻しないか**が要点 (`Notes.cs:25` に 3D フォールバックの分岐コメントがある)。
- **DoD**: (1) `V6UiMode.Playfield` 有効状態で GameScene に 8 レーン・判定ライン・ノーツが出ることを **EditMode テスト** (`Assets/Game/Tests/V6PlayfieldGeometryTests.cs` の拡張) で固定、(2) **実 Win64** で 1 曲再生しノーツと判定の同期を目視確認、(3) 旧 v5 `Lane` の二重描画が無いことをスクリーンショットで確認。
- **依存**: なし (他 Issue より先行)。**#233 の残り 2 件の前提**。

### U2 — ゲーム確認窓の拡張と旧歌詞二重描画の解消 (#233)

- **対象**:
  - 確認窓 0.86 秒 → 設計意図 1.6 秒。**本監査では 0.86 s の定義位置を file:line で特定できなかった** → Worker がまず `grep -rn "0.86\|ConfirmWindow\|confirm" Assets/Game/Scripts/` で実体を特定すること (候補: `V6GameHudView` / `GameHudRuntime` / `Judge` の Miss 判定)
  - 旧 v5 歌詞二重描画: v5 の歌詞表示 (`LyricsOverlay` 相当) と v6 の歌詞表示の両方が active になっている箇所。`Assets/Game/Scripts/UI/V5/GameUiExclusivity.cs` と同じ排他パターンで v5 側を無効化する
- **DoD**: 実 Win64 で (1) 1 曲再生し、後半にノーツの無い区間 (**Blank interval**) でもレーン/判定ラインが判定直前まで確認できること、(2) 歌詞が二重表示されないこと の 2 点のスクリーンショット。
- **依存**: **U1 と同じ PR 内、または直後の PR**。#233 の B 判定を A に上げる最後の 2 項目。

### U3 — v6 Option「データ初期化」の保存範囲統一 (#241)

- **対象**: `Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs:1044-1053` の `ClearSaveData()`:
  ```csharp
  PlayerPrefs.DeleteKey("Hiscore");
  PlayerPrefs.DeleteKey(FlashBeat.UI.V5.OptionView.KeyBindingsPrefsKey);
  PlayerPrefs.Save();
  Array.Clear(GManager.Hiscore, 0, GManager.Hiscore.Length);
  KeyBindings.ResetToDefaults();
  ```
  → **`ScorePersistence.ResetAll()` (`Assets/Game/Scripts/Persistence/ScorePersistence.cs:276`) を呼んでいない**。`:280-281` は `ScoreStore.StorageKey` と `LegacyStorageKey` を削除するが、この経路の呼び出し元は `Assets/Game/Scripts/Persistence/SaveLoadManager.cs:54 ResetHiscore()` だけ (`Assets/Game/Scenes/OptionScene.unity:11105 m_MethodName: ResetHiscore`)。**`Assets/Game/Scripts/UI/**` 配下から `ResetAll()` の呼び出しは 0 件**
- **影響**: v6 のオプション画面から「データを初期化」を押しても、**新スコア保存 (`FlashBeat.ScoreStore`) が残る**。旧 `Hiscore` だけ消える = 実装と UI の説明が食い違う
- **DoD**: `ClearSaveData` が `ScorePersistence.ResetAll()` を呼び、EditMode テスト (`Assets/Tests/Editor/` 配下に追加) で「保存済みスコア・Hiscore・キーマップ・キーバインドの 4 種がすべて初期化される」を固定。**他タブ (音量 / 曲速度 / タイミング / フラッシュ) の保存範囲は変えない** (Issue の主要受入条件)
- **依存**: なし

### U4 — Web の publish → chart ドラフト復元動線 (#98)

- **対象**:
  - `web/pages/chart.server.ts:6` — loader が `{ songId, version }` のみ返す → **`draft` パラメータを受け取る** (`params.get("draft")`)
  - `web/pages/chart.tsx:1` — `import { savePublishDraft }` のみ → **`readPublishDraft` も import** して、draft があれば `readPublishDraft(draftId, songId, version)` で復元
  - 参照: `web/pages/publish.tsx:1,:140` が既に `readPublishDraft(draftId, songId, version)` を使っている — **publish 側は完成、chart 側だけが欠落**
  - `web/src/lib/publish/draft.ts` の既存 API をそのまま使う (uuid 検証 + song/version 一致 + chart import 検証つき)
- **注意**: `specs/098-*` の tasks は `specs/` に存在しないが、Issue 本文のチェックはpublish 側 2 項目 + chart 側 1 項目。**chart 側 1 項目が未充足**
- **DoD**: `web/src/lib/publish/__tests__/draft.test.ts` を拡張し、「publish → `/chart?draft=…` → 開くと同じ draft が復元される」を**ページレベル**で 1 テスト追加 (`web/tests/e2e/studio-flow.spec.ts` の "catalog → editor → publish → back to catalog" が近い)
- **依存**: なし (#73 の publish 側は A 判定済)

### U5 — 視覚ゲートの緑化 (狭幅横溢れ) (#74, #75, #61)

- **対象**: `specs/075-visual-gate/evidence/2026-11-08/index.json` → `{"horizontalOverflow": ["home@390x844"], "consoleErrors": [], "exitCode": 1}`。**PR #257 (`w/w74fix2`) が未マージ**
- **手順**: (1) PR #257 をレビューして main に取り込む、(2) `web/tests/visual/capture.mjs` を再実行し `exitCode: 0` を確認、(3) `specs/075-visual-gate/evidence/` に新日付の証跡を追加、(4) `specs/74-responsive/tasks.md` の残り 3 タスク (「Real browser tab order walk」「Rendered check at 390px」「Screenshots of the five viewports」) をチェック
- **DoD**: `npm run visual` の exit code が 0、かつ `web/tests/e2e/visitor-flow.spec.ts` が全 viewport で clean console
- **依存**: **#61 Gate C の前提**。#61 が閉じられるにはこれが緑

### U6 — 展示 patch line `b677f8f` への #140 / #134 移植 (#140, #134)

- **対象**: 展示用ビルドは **patch line `b677f8f`** から作られている (`docs/evidence/index.md` §1.5 の第 3 世代 code state は `b77b63c`)。しかし:
  - `git ls-tree -r --name-only b677f8f` に `CollectCodeTexts` / `EnsureDynamicFallback` は **0 件** → **#140 の動的フォールバックフォントが展示ビルドに無い** (豆腐 0 の保証失效)
  - 同様に #134 のランキング送信 (`RankingClient`) が展示ビルドに無い可能性
- **手順**: (1) `b677f8f` と `main` の差分をレビューし、#140 / #134 を移植 (または展示 line を main に追いつかせる判断)、(2) 展示ビルドを再生成し `docs/evidence/index.md` §1.5 の世代表を更新
- **DoD**: 展示第 4 世代の `EXHIBIT-MANIFEST.txt` に新 SHA が載り、豆腐 0 とリザルト登録が**そのビルドで**確認できる
- **依存**: **Windows 実機**。**注意**: 展示 line と main をどちらに寄せるかは cc / オーナーの判断 (issue 本文は main 基準)

### U7 — Win64 実機解像度スイープ (#88, #96)

- **対象**: `docs/evidence/win64-2026-11-06-release-gate/run-summary-exhibit-*.json` → `"resolution": "1280 x 720 (fullscreen=0)"`。**`displayCoverage` キーは main のどこにも存在しない**
- **手順**: (1) `PlayerSettings` は `ProjectSettings/ProjectSettings.asset:119 fullscreenMode: 1` / `:107 resizableWindow: 0` / `:48-49 1920×1080` で、**設定値は正しい**。問題は**実機での実効値**。(2) `Assets/Game/Scripts/UI/V5/FullscreenDiagnostics` 相当の実効値取得 (`Screen.width/height/fullScreenMode/dpi`) を Player.log に記録させ、1920×1080 / 1280×720 / 1024×768 の 3 解像度で採取 (3) `displayCoverage` (描画領域が実ディスプレイを覆う割合) を run-summary に記録
- **禁止**: `docs/operations/exhibit-handoff-2026-11-08.md` §1 が明文で **「ウィンドウのドラッグ / リサイズで擬似全画面を再現することは禁止」** と書いている。**リサイズしたスクリーンショットは証跡にならない**
- **既存**: `Assets/Game/Tests/FullscreenPolicyTests.cs` (10 テスト) は設定値の固定のみ。**実測値の固定ではない**
- **DoD**: 3 解像度それぞれの「実効解像度 + displayCoverage ≥ 0.99」の記録が `docs/evidence/` に残り、`docs/ui/` と `docs/operations/three-resolution-fullscreen-verification.md` の記述と一致
- **依存**: **Windows 実機** (展示 PC は展示中。具体 Passive PC が必要)

### U8 — ストリーム音声 (m4a) の再生実証 (#13, #4)

- **対象**: 展示第 3 世代の `Player.log` は **`audioMode=Bundled`** — 鳴っているのは**同梱 wav**。**ストリームの音声トラック (m4a) は `Failed to recognize video file format` で準備できない**
- **前提**: `Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs:74-80` のコメントが既に答えを出している — 「Media Foundation (`MFCreateSourceReaderFromURL`) はカスタム HTTP ヘッダを送れない。yt-dlp で同じバイト列をローカルにダウンロードして再生すればこの制限を回避できる」。つまり **映像は成功、音声トラックだけが失敗**している
- **候補手段 (Worker が実測で選ぶ)**: (1) 結合 format (video+audio) の itag を優先選択して単一ファイルにする (`-F` の出力を解析する箇所は `YouTubeStreamResolver.cs:34` `One line of yt-dlp -F output`)、(2) 音声のみ m4a を別 yt-dlp で取得して `AudioSource` に渡す (`MusicManager.cs:362-368` の split 経路は既に存在する)、(3) ffmpeg で結合してから再生
- **DoD**: 実 Win64 で `Player.log` に **`audioMode=Stream` (または同値)** が記録され、**ストリーム音声の `rms` 値が同梱 wav ではなくストリーム由来である**ことを示すログ/数値が残る
- **依存**: **Windows 実機**。**注意**: この Issue が解けるまで #4 (Gate S1) は green にならない

### U9 — `skills-check` と agent 環境の欠落ファイル (#56)

- **対象** (本監査で実測した欠落):
  | 期待 | 現状 |
  |---|---|
  | `mise run skills-check` | **`mise.toml` にタスクが存在しない** (`[tasks.skills-list]` `:14`, `skills-bootstrap` `:17`, `skills-update` `:20` のみ) |
  | lock 19 エントリ vs vendored 9 個 | `.claude/skills/` + `.agents/skills/` は **speckit 9 個のみ**。lock 側 (`skills-lock.json`) は 19 エントリ → **hash 不一致になる** |
  | `evals/policy-evaluation/` | **ディレクトリが存在しない** |
  | `.config/wt.toml` | **`.config/` ディレクトリ自体が存在しない** |
  | `.github/workflows/agent-config.yml` | **存在しない** (`.github/workflows/` は `build-macos.yml`, `build-win64.yml`, `summaries/` のみ) |
  | `scripts/agent-routing/` | **存在しない** (`scripts/` には `worktree/` のみ) |
  | `AGENTS.md` の能力→Skill ディスパッチ表 | **無い** (`AGENTS.md` は `CLAUDE.md` の写しで 145 行の差)。root contract へのポインタ (`constitution/CONSTITUTION.md`, `organization/profiles/release-driven-solo.md`) のみ |
- **DoD**: `mise install --locked && mise run skills-check` が **exit 0** で、lock と vendored Skill の hash が一致することが CI で固定される
- **依存**: なし。**ただし #56 の未完了は他 Issue の実装を直接ブロックしていない** (worktree setup は動いている) ため優先度は P2

### U10 — 受入記録の整備 (#59 / #58 / #22)

- **対象**: 本監査で特定した「**実装は揃っているのに受入記録が無い**」案件。
  - **#59**: 4 機能 (歌詞同期 / キーガイド / HOLD / HUD) とも実装と EditMode テストは揃っている (`LyricsTimeline.cs` + `LyricsTimelineTests.cs`, `KeyGuideUI.cs` + `KeyGuideTests.cs`, `LongNote.cs` + `LongNoteTests.cs`, `GameHudView.cs` + `V5GameHudTests.cs`) が、**`docs/evidence/` に `lyrics-sync-*` / `key-guide-*` / `long-note*` / `hud-ux*` ディレクトリが 1 つも無い**。Issue 本文 189–192 行目の 4 チェックボックスも未チェック
  - **#58**: 「**30 曲全部**」の基準が現状と不一致 (`GManager` の 7 並列配列は 29 エントリ、`totalSong = ActiveSongs.Length - 1` = 28、`ニセモノ` 2 件は `UnplayableTitles` で選曲不可)。**Issue の基準自体の更新**が必要
  - **#22**: `docs/operations/release-approval-checklist.md` のチェックボックスが**全項目未記入**。`docs/operations/festival-runbook.md` §4 の 1280×720 スモーク 10 項目も未記入
- **DoD**: 各 Issue に「機能ごとの OK / NG / 未検証 の判定表 + 根拠ファイル:行 + スクリーンショットパス」が残り、NG は起票記録がある
- **注意**: #59 の OK/NG 判定は **#30 の HOLD 判定仕様が未確定 (E 判定)** のため、**HOLD 1 項目だけは判定を出せない**。残りは先行して記録してよい
- **依存**: #30 の仕様確定待ちは #59 の HOLD 行のみ

---

## 5. 監査できなかったこと (Why not audited)

以下は **本監査 worktree からは確認できなかった**。推測で埋めていない。

| 項目 | 理由 | 代替 |
|---|---|---|
| Windows 実機 (展示 PC) での実挙動 | 監査 worktree は macOS。展示 PC への起動・入力・kill は禁止 (`feedback_no_exhibit_machine_automation`) | `docs/evidence/**` の既存証跡のみ |
| Win64 CI の Unity ライセンス | `docs/operations/unity-ci-license.md` — CI は Secret 未設定で停止 | Issue #249 の状態を引き継ぐ |
| `production-api.test.ts` の実デプロイ到達性 | テストが `skip: !publishEnabled` で **緑でも到達性は証明にならない** | `docs/operations/d1-migrations.md` の適用記録 |
| 確認窓 0.86 秒の定義位置 | `grep` で一意な定義箇所を特定できなかった | U2 の Worker が `grep -rn "0.86\|ConfirmWindow"` で特定する |
| `GameScene.unity` の `Lane` を active にする enabler | コード内に **0 件**。存在しないものは file:line を示せない | `GameScene.unity:2837 m_IsActive: 0` の事実のみ |
| 「casual / unverified」の UI ラベル文言 | 該当文字列の実ファイル位置を確認できなかった | `V6RankingView.cs` の表示構造のみ |
| 実機 1280×720 ディスプレイでの fullscreen | 展示 PC に 1280×720 パネルが**無い** (`festival-runbook.md:189`) | ウィンドウモードでの代替手順のみ |
| `specs/060` / `specs/075` / `specs/074` などの証跡画像の**内容** | ピクセル画像の目視レビューは本監査の範囲外 | ファイル存在と機械的結果 (`index.json`) のみ |
| GitHub 側 main protection / branch protection の状態 | GitHub API の権限・設定確認の範囲外 | `docs/operations/release-approval-checklist.md` の運用記述のみ |

---

## 6. 監査方法の再掲 (§0 の要約)

- **判定の根拠順序**: (1) `git merge-base --is-ancestor <sha> origin/main` で main 到達を確認した実コード、(2) `main` 上の file:line、(3) `main` 上のテスト名、(4) `specs/*/tasks.md` のチェックボックス、(5) `docs/evidence/**` の実物。
- **使っていないもの**: Issue の Closed/Open 状態、Worker の自己申告、Issue 本文のチェックボックス **単独では判定根拠にしていない** (チェックボックスが `[x]` でも §3 の判定が B になる例が #118 / #56 / #75 / #60 にある)。
- **3 値判定**: 「充足」は**コードが存在する**こと、「未検証」は**実機/実サービスの観測が要る**ことを意味し、両者を混用しない。**EditMode 緑 / Vitest 緑は「充足」の根拠にはなるが「未検証」を「充足」に変えることはできない**。
- **secret 値**: 本ドキュメントには GitHub Actions secret や Infisical の実値を一切記載していない。`web/env.ts` への言及は **変数名のみ**。
- **コード変更**: 本監査は **読み取り専用**。`docs/audit/open-issues-audit.md` 以外のファイルは一切変更していない。
