# Evidence — Issue #58: カタログ合成 (選曲可能な曲が全部選べる) と再生クロックの回帰確認

**実施日**: 2026-11-09 / 2026-11-10
**実施環境**: **Mac** (Unity 6000.5.6f1 / macOS スタンドアロン / 実ディスプレイ 2560×1664 全画面)
**Windows 展示機には一切アクセスしていない** (指示どおり)。

---

## 1. 何を実測したか

Issue #58 の手順 2〜3 のうち、**Mac で実機実行できる部分** を実測した。

| 手順 | 状態 | 備考 |
|---|---|---|
| 1. EditMode テストを batchmode で実行して結果を記録 | **別 Evidence** (`docs/evidence/index.md` を参照) / 本 PR は実機証跡 | 本 PR は実機証跡に限定 |
| 2. 選曲画面でカタログ登録の全件が表示・選択できる | **実施 (2 曲)** | 選曲画面 → Ready → ゲーム画面 → 曲終了 → Result まで通した |
| 3. 1 曲フルプレイ (判定 / Result 到達 / Retry) | **実施 (2 曲)** | 下記「実測結果」 |
| 3. YouTube 映像/音声 / A/V 同期 | **未検証** | 下記「未検証」 |

走らせたハーネスは **内蔵の `AutoTestV6SongEnd`** (`Assets/Game/Scripts/Common/AutoTestV6SongEnd.cs`)。
このハーネスは「すぐ終わる曲」を既定で選ぶため、カタログ末尾の曲を選ぶために
`-autotest-song` を素通しする **ラッパ** を追加した
(`script/mac-select-evidence.sh`)。

---

## 2. 実行方法 (再現手順)

```
# 実ディスプレイ全画面 (2560x1664) で起動する。窓リサイズによる擬似全画面は不可。
script/mac-select-evidence.sh \
  --out docs/evidence/58-select-screen/run-a-twitterland \
  --song ツイッターランド
```

Player.log の `[FullscreenDiagnostics]` 行で **実効値** を確認する。

```
[FullscreenDiagnostics] event=startup, screen=2560x1664, currentResolution=2560x1664@60.00Hz,
  fullScreen=True, fullScreenMode=FullScreenWindow, dpi=299, safeArea=0,0,2560x1664, ...
```

ラッパは自分の子プロセスだけを kill する。**ユーザーが手元で起動した
FlashBeat には触らない。**

---

## 3. 実測結果

### 3.1 曲 A — ツイッターランド (`run-a-twitterland/`)

`autotest-v6songend-result.txt`:

```
[AutoTestV6SongEnd] RESULT pass=31 fail=0
song=ツイッターランド
songEndState=Completed
forcedByDeadline=False
scene=ResultScene
perfect=129 great=0 bad=0 miss=1
judged=130
settled=130
chartNotes=130 runtimeNotes=130
score=992307 combo=129 maxCombo=129 comboPeak=129
rankingMode=Online
judgedMatchesChart=True
```

### 3.2 曲 B — 一龠 (`run-b-ichikan/`)

```
[AutoTestV6SongEnd] RESULT pass=31 fail=0
song=一龠
songEndState=Completed
forcedByDeadline=False
scene=ResultScene
perfect=180 great=1 bad=0 miss=10
judged=191
settled=191
chartNotes=191 runtimeNotes=191
score=945549 combo=89 maxCombo=91 comboPeak=90
rankingMode=Online
judgedMatchesChart=True
```

### 3.3 判定 Reading (読むべき点)

| 項目 | 意味 |
|---|---|
| `pass=31 fail=0` | 内蔵ハーネスの検査が **全項目成功** |
| `songEndState=Completed` | **期限で強制終了していない**。曲的自然終了まで到達した |
| `forcedByDeadline=False` | 〃 (同上。ハーネスの期限で打ち切られていない) |
| `scene=ResultScene` | 曲終了後に **Result 画面へ遷移した** (再現クロックの回帰なし) |
| `judged == settled == chartNotes == runtimeNotes` | **判定数が譜面ノート数と一致**。取りこぼし・重複判定なし |
| `judgedMatchesChart=True` | 上記の機械的照合 |

### 3.4 スクリーンショット

各 run に 4 枚 (Git LFS 管理):

| ファイル | 内容 |
|---|---|
| `autotest-v6songend-01-01-select.png` | 選曲画面 (カタログから曲を選んでいる状態) |
| `autotest-v6songend-02-02-game.png` | ゲーム画面 (開始直後) |
| `autotest-v6songend-03-03-gameplay.png` | ゲーム画面 (ノーツが流れている状態) |
| `autotest-v6songend-04-04-result.png` | Result 画面 (曲終了 · 遷移済み) |

`Player.log` も各 run に同梱。

---

## 4. 判定の限界 (正直に)

1. **選曲可能な 26 曲すべてを 1 曲ずつ完走したわけではない。**
   カタログ末尾の **2 曲** を完走した。残りの 24 曲について
   **曲ごとに完走した証拠はない**。
2. **YouTube 映像の視認と A/V 同期は未検証。**
   今回のハーネスは Space だけを送り、判定は `Miss` / `Perfect` の数で示す。
   「映像が見えるか」「音と映像が合っているか」は別項目で実測が必要。
   (映像プレーンの視認は #233 の側で macOS 実機撮影済み:
   `docs/evidence/233-playfield/README.md`)
3. **Windows 実機では未検証。** Media Foundation の URL 読み込み失敗 (#13) が
   あるため、macOS で通ったことは Windows の挙動を保証しない。
4. **日報の `Miss` は自動操作の結果** (ノーツを意図的に打っていない) であり、
   判定ロジックの不具合ではない。`judged == chartNotes` が一致している点が要点。
5. **Rebuild / Retry の実測は今回の run では行っていない。**

---

## 5. 証跡

| ファイル | 内容 |
|---|---|
| `run-a-twitterland/` | ツイッターランドの証跡 (log / 4 枚 / result txt) |
| `run-b-ichikan/` | 一龠の証跡 (log / 4 枚 / result txt) |
| `script/mac-select-evidence.sh` | 実行に使ったラッパ (リポジトリ側) |

ビルドの基点: `w/w58-macos-evidence` の作業ツリー (基点 commit `9e3c63e`)。
本 PR は `origin/main` をマージ済み。
