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

<!-- ここから Issue 別詳細節 -->
