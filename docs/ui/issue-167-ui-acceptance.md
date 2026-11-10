# Issue #167 残受入: Windows 実機一巡 操作手順書 (v6 UI 既定)

**対象**: Issue #167 ([Game/P0] Unity 全画面の描画だけのUIを排除し全ボタンを実動作へ接続) / 親 #162
**基点**: `origin/main` = `a2fefe1` (作業ブランチ `w/w167-residual`)。旧記述
(Draft PR #177 / base `release-0-1-11` / `git checkout 167`) は廃棄済み — PR の base は
削除されたブランチにつき無効で、現行は main 起点の作業ブランチで検証する。
**目的**: Windows 実機で Title → Select → Ready → Game → Pause → Result → Retry を
一巡し、「押した結果に実動作がある」ことを画面と操作ログで残す。

**実行者**: オーナー (Windows 実機の操作受入)。**Windows 実機の操作受入は未実施** (2026-11-10 時点)。
cc (コードワーカー) は実機・展示 PC を一切操作しない。実機受入はこの節 (§4) のオーナーの責務である。

---

## 0. 前提: v6 UI が既定

実機で見えるのは **v6 の Title / Select / HUD / Pause / Result** である。v5 の同等画面は
生成されない (v5 Pause は `V6GameRuntime` が毎フレーム非表示化する)。手順はすべて v6 基準で書く。

| フラグ | 既定値 | 根拠 |
|---|---|---|
| `V6UiMode.GameHud` | `true` | `Assets/Game/Scripts/UI/V6/V6UiMode.cs:15` |
| `V6UiMode.SelectScreen` | `true` | `Assets/Game/Scripts/UI/V6/V6UiMode.cs:18` |
| `V6TitleView.Enabled` | `true` | `Assets/Game/Scripts/UI/V6/Title/V6TitleView.cs:50` |
| `V6PauseView.Enabled` | `true` | `Assets/Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs:39` |
| `V6ResultView.Enabled` | `true` | `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:40` |

補足 (v6 既定で動かない旧経路):

- v5 の HUD / Pause / Select / Title / Result は生成されない。GameScene の v5 Pause
  (`PauseMenuView`) は `V6GameRuntime.Update` が毎フレーム `SetActive(false)` する
  (`Assets/Game/Scripts/UI/V6/V6GameRuntime.cs:105-113`)。
- 旧ランキングオーバーレイ (`ResultRankingOverlay` / F5 ボード) は v6 Result 有効時に停止する
  (`Assets/Game/Scripts/UI/ResultRankingOverlay.cs:100-103`)。登録モーダルは v6 側が持つ。
- Game のローディング表示は `GameLoadingRoute.Current = V6` で `V6LoadingView` が単独表示
  (`Assets/Game/Scripts/UI/LoadingOverlay.cs:23-36`)。Ready プロンプト (Space で開始) だけは
  v5 の `GameReadyView` が Ready 専用表示として使う (`Assets/Game/Scripts/UI/V5/GameHudRuntime.cs:93-95`)。

## 1. 解像度要件 (証拠の有効性)

- 有効: **FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664 の実ディスプレイ全画面**。
- 無効: ウィンドウモードでのリサイズ等で「サイズだけ真似た」撮影は **証拠にならない**。
  UI は実解像度基準でレイアウトする (`SelectView.DesignScale`, `UISizeProfile`) ので、
  実ディスプレイの全画面で撮ること。

## 2. 一巡操作ログ表 (押した結果の記録)

各行: 「操作 → 期待される実動作 → 証跡」。スクリーンショットは操作の **直前** と **直後** の 2 枚。
保存先: `docs/ui/issue-167-evidence/`。

### 2-1. Title (v6)

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| T1 | 起動 | Opening → Title (v6) へ自動遷移。中段のタブ行 / 左下の凡例行は無い (`V6UiMode.TitleTabs/Legend = false`) | shot `title-01.png` |
| T2 | フッター右下 **START** を**クリック** | Select (v6) へ遷移 (クリックが実 Button に届く) | shot `title-02-start-click.png` |
| T3 | `↑` / `↓` + `Space` | 強調行が移動し、START と同じ経路で Select へ | shot `title-03-key.png` |
| T4 | フッターの **設定** を**クリック** (`O` キーでも可) | Option (v6) へ | shot `title-04-option.png` |

### 2-2. Select (v6) → Ready

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| S1 | 曲行を**クリック** | 選択中の曲が変わり、BEST / グレードが更新される | shot `select-01-songrow.png` |
| S2 | `↑`/`↓` / ホイール | 選択行が移動する | shot `select-02-keynav.png` |
| S3 | `O` キー / フッターの**設定** | 右カラムが「このプレイの設定」に切り替わり、**Key Guide / Lyrics / NoteSpeed の 3 行**が出る | shot `select-03-settings.png` |
| S4 | `Key Guide` 行を**クリック** | `Compact → Full → Off` と切り替わる (ゲーム中のキーガイド表示に実反映) | shot `select-04-keyguide.png` |
| S5 | `Lyrics` 行を**クリック** | `Auto ↔ Off` が切り替わる (ゲーム中の歌詞表示に実反映) | shot `select-05-lyrics.png` |
| S6 | `NoteSpeed` 行 `←`/`→` | 速度が 1..50 にクランプされて変わる | shot `select-06-notespeed.png` |
| S7 | フッター右下 **Ready** を**クリック** (`Enter` / `Space` でも可) | 選択が確定し **Game へ遷移する**。Game へ入るのはこの Ready (または Enter / Space) であり、F5 (Ranking) ではない | shot `select-07-ready.png` |
| S8 | `F3` | Ranking 画面。**Local セグメントのみ**で、非 cloud 曲には **Online セグメントが出ない** | shot `select-08-ranking.png` |

> 誤記の修正 (旧手順書 S5 / S6): 「この後 F5 で Game へ」は **誤り**。F5 は Result 系のランキング
> ボードであり、Select から Game へは **Ready ボタン / `Enter` / `Space`** (S7) である。

### 2-3. Game (v6 HUD)

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| G1 | 選曲から開始 | Ready プロンプト (**Space で開始**) が出る。曲準備中は `V6LoadingView` が単独で出る | shot `game-01-ready.png` |
| G2 | `Space` | 再生開始。v6 HUD (スコア / コンボ / レーン) が出る | shot `game-02-playing.png` |
| G3 | S4 で `Full` を選んだ場合 | 下端のキーガイドにレーンの全キーが出る | shot `game-03-keyguide-full.png` |
| G4 | S4 で `Off` を選んだ場合 | キーガイドが出ない | shot `game-04-keyguide-off.png` |
| G5 | S5 で `Off` を選んだ場合 | 歌詞パネルが出ない | shot `game-05-lyrics-off.png` |
| G6 | 演奏中にキーを叩く | 判定 (Perfect / Great / Bad / Miss) が実カウントされ、スコアに反映する | shot `game-06-judge.png` |

### 2-4. Pause (v6)

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| P1 | `Esc` または HUD のメニューボタンを**クリック** | **v6 ポーズ** (Play / Display / Sound / Data タブ) が開く。v5 の PauseMenuView は開かない (毎フレーム非表示化) | shot `pause-01.png` |
| P2 | `Display` タブ | `Key Guide` / `Lyrics` の **実動作する行だけ** ある (押しても変化しない行が無い) | shot `pause-02-display.png` |
| P3 | `Sound` タブの `MainVolume` `←` | 値が下がり、実際の音量が変わる | shot `pause-03-volume.png` |
| P4 | `Data` タブ | 「この曲のベスト」「記録済みの曲」等の **この端末の実データ** が出る (ローカル限定の確認箇所、§3) | shot `pause-04-data.png` |
| P5 | ポーズ中に `Esc` | ポーズが閉じ、演奏が続く (レガシー MenuPanel が二重に出ない) | shot `pause-05-esc.png` |

### 2-5. Result (v6) → Retry

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| R1 | 曲終了 (またはポーズからリタイア) | Result (v6) へ。スコア / 判定内訳 / HISCORE が実データで出る | shot `result-01.png` |
| R2 | フッターの **Register** を**クリック** (`Enter`) | ローカル固定曲では **「この端末の名前」** モーダルが開く (オンライン登録の語彙は出ない — `V6ResultView.cs:916-931`) | shot `result-02-register.png` |
| R3 | 名前を入力して保存 | この端末 (PlayerPrefs) に名前が保存され、次回も復元される | shot `result-03-saved.png` |
| R4 | フッターの **Retry** を**クリック** (`R`) | Game へ戻り、スコア / コンボ / 判定カウンタが 0 に戻る | shot `result-04-retry.png` |
| R5 | Retry 後に曲を終了 | 新記録が HISCORE に反映している (ベストスコア更新は Awake で確定 — `ResultSceneManager.ApplyBestScore`) | shot `result-05-hiscore.png` |
| R6 | **OK** を**クリック** (`Space` / `Esc`) | Select (v6) へ戻る | shot `result-06-ok.png` |

## 3. 「ローカル限定」表記の確認箇所 (R5)

| 画面 | 確認内容 | 根拠 |
|---|---|---|
| Select Ranking (v6) | 非 cloud 曲では **Online セグメントが出ない** (Local のみ) | `V6RankingView.cs:611-620` |
| Select Ranking (v6) | プレイヤー別集計はオンライン API が無いため空状態であることが分かる | `V6RankingView.cs:27` |
| Pause (v6) `Data` タブ | 「この曲のベスト」等はこの端末の実データ (ローカル) | `V6PauseView.cs:475-497` |
| Result (v6) `Register` | ローカル曲ではモーダル見出しが「この端末の名前」(オンライン語彙が出ない) | `V6ResultView.cs:916-931` |

## 4. Windows 実機の操作受入 (未実施 — オーナー実施節)

- **状態**: 未実施 (2026-11-10)。本節はオーナーが実機で埋める。
- 手順: §2 の表を Win64 ビルド (`Builds/Windows/FlashBeat.exe`) の全画面で上から順に実行し、
  各行の「期待される実動作」どおりになったかを証跡 shot とともに記録する。
- 合格条件: §2 の全行が期待どおり + §3 のローカル限定表記が 4 箇所とも確認できること。
- 異常時: スクリーンショットと操作 (押したキー / クリック対象) を記録して Issue #167 にコメントする。
  切り分けの入口: クリックが効かない → 対象 Canvas の `GraphicRaycaster`
  (`V5GameUi.NewScreenCanvas`) / ポーズが二重に出る → `V6GameRuntime` の v5 Pause 非表示化
  (`V6GameRuntime.cs:105-113`) / 設定が反映されない → `PlaySession` / `OptionSettings.Save()`。

---

本手順書は Issue #167 の残受入 (Windows 実機の一巡) のためのものであり、
EditMode テストの緑は実機合格の代用ではない。
