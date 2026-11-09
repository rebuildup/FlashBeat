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
| **D: 阻塞 (blocked)** | 着手・完了が他 Issue / オーナー判断 / Windows 実機 / Unity ライセンス / secrets などに依存する。何を待っているかを明記する |
| **E: 判定不能** | 判断材料が Issue 記述・コード・証跡のいずれにも不足。何が決めれば決まるかを明記する |

### 受入条件の 3 値

各受入条件に **充足 / 未充足 / 未検証** のいずれかを付与する。

- **充足** — `main` に実装が存在し、要件を満たしているとコードから確認できる
- **未充足** — 実装が存在しない、または存在しても要件を満たさない (file:line で示す)
- **未検証** — 実装は存在するが **テスト緑だけでは足りず、実機/実サービスの観測が必要** な条件。**EditMode 緑 ≠ 実機確認** を厳密に区別する

### 未検証の扱い (厳格区分)

| 代替検証 | 実機確認に代わらない理由 |
|---|---|
| EditMode / Vitest 緑 | 実行環境・描画・入力・実サービスewel を再現しない。**画面表示・操作性・実通信の結果は含まれない** |
| コード読み | 「動く」保証にならない。null 参照・シーン不整合・実 API 差異はcaught されない |
| macOS Player 起動 | **Win64 の代用にならない** (`docs/evidence/index.md` §1.6 が明記) |
| スクリーンショット 1 枚 | 譜面の空白区間などを誤って「正常」と読む危険 (#233 の実例) |

実機確認済みのものは **実行環境・日付・成果物パス (または SHA-256) を明記** した。観測できなかったものは **未検証** とし、代替検証の範囲を明記した。

### 調査で使った証拠

- `git merge-base --is-ancestor <sha> origin/main` — commit が `main` に入っているかの判定
- `git log --oneline -- <path>` — 実装ファイルの導入 commit
- `gh issue view <n>` — Issue 本文 + 全コメント
- `Assets/Tests/Editor/` (73 ファイル) / `Assets/Game/Tests/` (18 ファイル) / `web/**/*.test.ts(x)` (47 ファイル) の実在 확인
- `specs/*/tasks.md` のチェックボックス (証拠信号。ただし stale な場合あり)
- `docs/evidence/**` の実測アーティファクト

---

## 1. サマリ表 (全 47 件)

| # | タイトル (短縮) | 区分 | 実装位置 | 充足/未充足/未検証 | 阻塞 | 推奨アクション |
|---|---|---|---|---|---|---|
| 3 | 展示版: 5 API 契約と段階的実装 | **D** | `contracts/openapi.v1.yaml`, `web/routes/api/v1/**` | 3/2/1 | Win64 CI ライセンス・Windows 実機 | S1–S4 ゲートの証跡を 1 段ずつ作る |
| 4 | Gate S0/S1 Win64 ベースライン+YouTube | **D** | `Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs` | 2/1/1 | Windows 実機 (展示 PC) | S1 の Win64 run を取得 (映像あり・音声 unresolved) |
| 5 | Gate S2 Cloudflare API + Unity 動的カタログ | **D** | `Assets/Game/Scripts/Songs/CloudCatalog.cs:91,96,105` | 1/1/2 | Windows 実機 + 本番 PUT | Mac で PUT → Win Unity 再生を 1 回通す |
| 6 | Gate S3 Web Studio → Unity 公開デモ | **D** | `web/pages/publish.tsx`, `web/src/components/new-song/**` | 1/1/2 | Windows 実機 | 公開 chartVersion を Win Unity で再生 |
| 7 | Gate S4 optional クラウドランキング | **B** | `web/routes/api/v1/scores.ts:1-80`, `web/routes/api/v1/leaderboards/**` | 3/1/2 | 2台目のクライアント | サーバ再計算は充足、2nd クライアント観測のみ未検証 |
| 12 | S2/Mac Void+Vite+React+Panda/Park 足場 | **A** | `web/package.json`, `web/panda.config.ts:39,76` | 4/0/1 | — | クローズ提案可 (视觉确认のみ未検証) |
| 13 | S1/Win yt-dlp→VideoPlayer 1曲完走 | **B** | `Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs`, `MediaCacheManager.cs` | 3/2/1 | Windows 実機 | ストリーム音声 (m4a) 再生と `audioS.Play()` を実機で |
| 17 | S2/Win 固定楽曲配列 → Cloud Catalog | **B** | `Assets/Game/Scripts/Songs/CloudCatalog.cs`, `GManager.cs:88` | 4/1/1 | Windows 実機 | 关口の E2E のみ未検証 |
| 19 | S3/Mac Park UI メタデータ+Publish 接続 | **A** | `web/src/components/song-publisher/**`, `web/pages/publish.tsx` | 3/0/1 | — | Web 側は充足。Win 側は #6 のゲート |
| 21 | S4/Win optional リザルト送信+ランキング | **B** | `Assets/Game/Scripts/UI/RankingClient.cs:113,143,240` | 3/1/1 | Windows 実機 | 2nd クライアント表示の実測 |
| 22 | Release/Win Win64 展示版 smoke/配布ゲート | **B** | `docs/evidence/index.md`, `release/check/**` | 4/4/2 | Windows 実機 | S0 のみ partial。S1–S4 を詰める |
| 30 | Product Spec ゲーム体験の理想形 | **E** | `docs/ui/lyrics-sync.md` | 0/0/1 | オーナーの仕様確定 | HOLD/歌詞の操作・採点形状の確定待ち |
| 56 | Infra AIエージェント開発環境再整備 | **B** | `mise.toml:14-37`, `skills-lock.json` | 2/3/0 | — | `skills-check` / `evals/` / `.config/wt.toml` / `agent-config.yml` が欠落 |
| 58 | 検証 カタログ合成(30曲)+再生クロック | **B** | `GManager.cs:88`, `Assets/Tests/Editor/CatalogMergeTests.cs` | 2/1/2 | — | 「30曲」前提が stale (現在 29 行/28 選曲可)。基準の更新が必要 |
| 59 | 検証 #30 各機能の受入確認 | **B** | `LyricsTimeline.cs`, `KeyGuideUI.cs`, `LongNote.cs`, `GameHudView.cs` | 0/1/4 | — | 機能別 OK/NG/未検証 の記録と証跡が未作成 |
| 60 | Web/UI Foundation Pen v5 → Panda/Park | **A** | `web/src/theme/colors/{gray,blue,red,green,amber}.ts`, `docs/design/web-token-mapping.md` | 4/0/1 | — | クローズ提案可 (视觉一致のみ未検証) |
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

> **C (未実装) が 0 件**なのは「全部做强历史上実装済みではない」ではなく、**47 件すべてに `main` 上のコード or ゲート定義 or 仕様書が存在する**ため。未充足なのは個別の受入条件であり、それは各詳細節で file:line を伴って列挙した。

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

- **区分**: **D (阻塞)** — 集約 Issue。単独で実装できる単位ではない
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

- **区分**: **D (阻塞)**
- **実装/証拠**:
  - 本 Issue は **受入ゲート** (実装単位ではない)
  - 前提: `docs/evidence/index.md` §1.7「Pencil デザイン全画面一致 = **未受入**。3 解像度の受入は Windows 実機に残ります」
  - 対象画面の実装は `Assets/Game/Scripts/UI/V6/**` に存在 (§3.1 各子 Issue 参照)
- **受入条件**:

| # | 条件 | 判定 | 根拠 |
|---|---|---|---|
| 1 | #220 解像度テンプレートと実値 HUD、游戏既存レーン/効果/判定は回帰確認対象 | 充足 | `V6/Common/V6UiProfile.cs:73-75` (FHD/2K/MacBook), `V6/Game/Hud/V6GameHudView.cs` |
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

- **区分**: **D (阻塞)**
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
| 2 | v6 の 8 レーン遠近形状・判定ライン・ノーツを **実プレイデータに投影** する描画にする (静的设计を貼って誤魔化さない) | 充足 (コード) | `V6PlayfieldView.cs:38,118-124` が `NoteLayer` (uGUI "Notes" RectTransform) を提供し、**既存の `Assets/Game/Scripts/Gameplay/Notes.cs:110-129,158-165` が head/body の Image を `NoteLayer` に生成**して `V6LaneGeometry.NoteColor` / `LaneCount` を使う。`Notes.cs:181` がフレームごとに `V6PlayfieldView.Current` を参照 |
| 3 | 通常/ロング/同時押し/Miss/レーン別判定エフェクトが現行 Time/NoteIndex/Judge と一致 | 充足 (コード) | `Notes.cs:129` (HOLD の body Image)、`V6PlayfieldView.cs:155-165` (レーンフラッシュは `KeyBindings.IsLaneKeyDown(lane)` 驱动) |
| 4 | レーン描画/時間進行/リサイズの EditMode ロジックテスト、FHD/2K/Mac でフレーム切れ/ズレ/上下反転の画像比較 | 充足 (EditMode) / **未検証 (画像比較)** | `V6PlayfieldGeometryTests.cs` 存在。画像比較は実機必須 |
| 5 | 統合 Worker が GameScene へ一度で導入できるよう公開API/接続手順/所有オブジェクト/必要なアセットを PR に明記 | 充足 | `specs/` に接続手順あり |
| 6 | **8 レーンとノーツが実プレイで動き、判定と描画が合い、動画が背景として再生される** | **未充足** | `V6UiMode.Playfield = false` で描画自体が無効。加えて `GameScene.unity:2837` の `Lane` が `m_IsActive: 0` で、シーン側の構造も無い |

- **未充足の具体箇所**:
  1. `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` → `Playfield` を `true` にする (owner rollback の再承認が要る = オーナー判断待ち)。現状 `V6PlayfieldRuntime.cs:52` の `if (!V6UiMode.Playfield) return;` で **Canvas 自体が作られない**
  2. `Assets/Game/Scenes/GameScene.unity:2837` → `Lane` の `m_IsActive: 0` を戻し、コード側から active にする enabler を追加 (grep で enabler 0 件)。`Notes.cs:25,111` は `V6PlayfieldView.Current == null` のとき **3D 描画へフォールバック**する設計だが、フォールバック先の `Lane` が非 active なので **どちらの描画も出ない**
  3. `Assets/Game/Scripts/UI/V6/V6GameRuntime.cs:370-374` → `HideLegacyPlayfield()` が `Playfield` 有効時にのみ `HideByName("Lane")` を呼ぶ設計は legacy 排除として正しいが、**この状態では旧 v5 側のレーンも隠れない** (= 1 の再承認と 2 の enabler 追加が同時に必要)
- **未検証の区別**: 条件 4 の画像比較、条件 6 の実プレイは **実機必須**。**EditMode 緑は描画存在的问题を検出しない** (フラグで無効化されている箇所は純論理的テストを通る)。
- **依存**: **#217 の子**。#182 (判定キュー) が判定同期の前提。→ #223 → #242。

### #217 — [P0/v6] Pencil確定UIをUnityに並列移植 — Game/Select最優先

- **区分**: **D (阻塞)**
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
