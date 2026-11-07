# Issue #167: v5 全画面の実動作 証跡取得手順書

**対象**: Issue #167 (Draft PR #177, base `release-0-1-11`)
**目的**: Win64 ビルドで実機操作し「押した結果に実動作がある」ことを画面と操作ログで残す。
**実行者**: Supervisor (Unity Editor / 実機 Windows は排他資源のため Worker は実行しない)

---

## 0. 事前

```bash
# 1) ブランチ取得
git fetch origin && git checkout 167 && git pull

# 2) EditMode テスト (V5UiWiringTests を含む)
Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml

# 3) Win64 ビルド
Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneWindows64
# 出力: Builds/Windows/FlashBeat.exe
```

**合格条件**: EditMode が全件 pass、ビルドが成功。

---

## 1. 操作ログ表 (押した結果の記録)

各行: 「画面 → 操作 → 期待される実動作 → 証跡」。スクリーンショットは操作の **直前** と **直後** の 2 枚。

### 1-1. Title

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| T1 | 起動 | Opening → (6 秒後) Title へ自動遷移 | shot `title-01-title.png` |
| T2 | **マウスで** `GameStart` 行をクリック | Select シーンへ遷移 (クリックが効く = GraphicRaycaster が載っている) | shot `title-02-gamestart-click.png` |
| T3 | タイトルで `↑` / `↓` | 強調行が移動し、効果音が鳴る | shot `title-03-keynav.png` |
| T4 | `Enter` / `Space` | 強調中の行 (GameStart) が実行され Select へ | shot `title-04-enter.png` |
| T5 | `Option` 行をクリック | Option シーンへ | shot `title-05-option.png` |
| T6 | `Exit` 行をクリック | ゲーム終了 (Editor なら再生停止) | shot `title-06-exit.png` |
| T7 | Ranking 行 | **出ない**こと (Ranking 実装が未接続のあいだ描かない契約) | shot `title-07-no-ranking.png` |

### 1-2. Select

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| S1 | `←` (Back) を**クリック** | Title へ戻る | shot `select-01-back.png` |
| S2 | 曲行を**クリック** | 選択中の曲・BEST・グレードが更新される | shot `select-02-songrow.png` |
| S3 | `↑`/`↓` / ホイール | 選択行が移動する | shot `select-03-keynav.png` |
| S4 | 右下 `Ready` を**クリック** | Ready パネル (このプレイの設定) が開く | shot `select-04-ready.png` |
| S5 | Ready の `Key Guide` 行 `→` を**クリック** | 値が `Compact` → `Full` に変わる (**この後 F5 で Game へ**) | shot `select-05-keyguide-full.png` |
| S6 | Ready の `Lyrics` 行 `→` を**クリック** | 値が `Auto` → `Off` に変わる (**この後 F5 で Game へ**) | shot `select-06-lyrics-off.png` |
| S7 | Ready の `NoteSpeed` 行 `→` を**クリック** | ノーツ速度の値が上がる (1..50 にクランプ) | shot `select-07-notespeed.png` |
| S8 | 右下 `Start` を**クリック** | Game シーンへ遷移 | shot `select-08-start.png` |
| S9 | `F2` | 履歴 (プレイ済み曲) 一覧 | shot `select-09-history.png` |
| S10 | `F3` | Ranking 画面。Local に「この端末の記録」、Online は **cloud 曲だけ** 出る | shot `select-10-ranking.png` |
| S11 | `Tab` | 検索 (タイプ) モード | shot `select-11-typing.png` |

### 1-3. Game

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| G1 | 選曲から開始 | Ready パネル (Space で開始) | shot `game-01-ready.png` |
| G2 | `Space` / Ready パネルをクリック | 再生開始 (HUD が出る) | shot `game-02-playing.png` |
| G3 | S5 で `Full` を選んだ場合 | 下端のキーガイドに **レーンの全キー** (例 `1 Q A Z`) が出る | shot `game-03-keyguide-full.png` |
| G4 | S5 で `Off` を選んだ場合 | 下端のキーガイドが **出ない** | shot `game-04-keyguide-off.png` |
| G5 | S6 で `Off` を選んだ場合 | 歌詞パネルが **出ない** | shot `game-05-lyrics-off.png` |
| G6 | S6 で `Auto` を選んだ場合 | 歌詞が歌の進行に合わせて出る | shot `game-06-lyrics-auto.png` |
| G7 | 右下ポーズボタンを**クリック** | v5 ポーズメニューが開く (レガシー MenuPanel は開かない) | shot `game-07-pause.png` |
| G8 | ポーズの `Display` タブ | `Key Guide` / `Lyrics` の **2 行だけ** ある (押しても変化しない行が無い) | shot `game-08-pause-display.png` |
| G9 | ポーズ `Display` → `Key Guide` 行をクリック | HUD のキーガイドが即時に変わる | shot `game-09-keyguide-live.png` |
| G10 | ポーズ `Display` → `Lyrics` 行をクリック | 歌詞の表示が即時に変わる | shot `game-10-lyrics-live.png` |
| G11 | ポーズ `Setting` タブの `MainVolume` `←` | 音量が下がり、値と実際の音量が一致する | shot `game-11-volume.png` |
| G12 | ポーズ `Ranking` タブ | 「この端末の記録 (ローカル)」と明記されている | shot `game-12-pause-ranking.png` |
| G13 | ポーズ `Keys` タブ | 現在のキー割り当て (レーン色つき) | shot `game-13-pause-keys.png` |
| G14 | ポーズの `Retry` / `Retain` を**クリック** | GameScene 再開 / Select へ遷移 | shot `game-14-retry.png` |
| G15 | ポーズ状態で `Esc` | ポーズが閉じる (レガシー HUD が二重に出ない) | shot `game-15-esc.png` |

### 1-4. Result

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| R1 | 曲終了 | Result シーンへ (順位 / スコア / 判定内訳 / HISCORE) | shot `result-01-result.png` |
| R2 | `Retry` を**クリック** | Game へ戻りリトライ (スコアが 0 に戻る) | shot `result-02-retry.png` |
| R3 | `OK` を**クリック** | Select へ戻る | shot `result-03-ok.png` |
| R4 | `Name` を**クリック** | 「この端末の名前」モーダル (ローカル限定) | shot `result-04-name-modal.png` |
| R5 | モーダルで名前を入力して `Save` | HISCORE 行の名前が保存される (**オンライン登録ではない**) | shot `result-05-name-saved.png` |
| R6 |  その後 Select へ戻り ranking を確認 | 保存された名前とスコアが反映されている | shot `result-06-name-persisted.png` |

### 1-5. Option

| # | 操作 | 期待される実動作 | 証跡 |
|---|---|---|---|
| O1 | Title → `Option` | Option シーンへ | shot `option-01.png` |
| O2 | `Main Volume` `←` / `→` を**クリック** | 音量が変わり、即座に音が変わる | shot `option-02-volume.png` |
| O3 | `NoteSpeed` `←` | 速度が下がる (1 以下にならない) | shot `option-03-speed.png` |
| O4 | `NoteTiming` `←` / `→` | 判定幅が変わる | shot `option-04-timing.png` |
| O5 | `Key Layout` 行 | 実際の割り当てが `標準` / `カスタム` で表示される | shot `option-05-keylayout.png` |
| O6 | `Data` タブ → `Clear save data` → もう一度 Enter | ハイスコアとキー設定が消える | shot `option-06-clear.png` |
| O7 | `Back` を**クリック** | Title へ戻る | shot `option-07-back.png` |
| O8 | **アプリを再起動**して `Option` を開く | 音量 / ノーツ速度 / 判定幅が保存された値で表示されている (復元) | shot `option-08-persisted.png` |

---

## 2. スクリーンショット取得位置

- 解像度: 1920x1080 (ウィンドウモード可)。
- 取得タイミング: 操作の **直前** と **直後**。HUD は先頭フレームで入らないことがあるため、
  操作後に 0.5 秒待ってから撮る。
- 保存先: `docs/ui/issue-167-evidence/` (本 Issue の PR に添付)。
- 特に重要な 3 枚:
  1. `title-02-gamestart-click.png` — **クリックが効く**ことの証跡 (本 Issue の中心)。
  2. `game-03-keyguide-full.png` / `game-04-keyguide-off.png` — Ready の設定が Game に届く証跡。
  3. `option-08-persisted.png` — 設定が保存・復元される証跡。

## 3. 異常時の切り分け

| 症状 | 確認先 |
|---|---|
| クリックが反応しない | 対象 Canvas に `GraphicRaycaster` があるか (v5 Canvas は `V5GameUi.NewScreenCanvas` が付与) |
| クリックが二重に反応 | `EventSystem` が 2 個ある可能性 → シーン監査 (T4) を確認 |
| Ready の設定が Game に反映されない | `PlaySession` の値と `GameHudView.BuildKeyGuide` / `GameSceneManager.ApplyTextPanelVisibility` の参照 |
| 音量が反映されない | `OptionSceneManager.ApplyVolumes` と `AudioListener.volume` |
| ポーズとレガシーが二重に出る | `GameSceneManager.HasV5PauseMenu` の判定 |
