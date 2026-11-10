# Evidence — Issue #278: HOLD (ロングノーツ) の実機 3 段階判定

**実施日**: 2026-11-10
**実施環境**: **Mac** (Unity 6000.5.6f1 / macOS スタンドアロン / 実ディスプレイ 2560×1664 全画面)
**Windows 展示機には一切アクセスしていない** (指示どおり)。

---

## 1. 何をしたか

Issue #278 の対応案 **(A) 検証用譜面を用意する** (カタログには登録しない) を採用し、
HOLD を含む **検証譜面 (タップ 4 + HOLD 3)** を macOS 実機プレイヤーに差し込んで
HOLD の **開始 → 保持 → 終端** の 3 段階を観測した。

- 差し替えの正本: `Assets/Game/Scripts/Common/HoldProbeChart.cs`
  (封筒つきの譜面 JSON。`GManager` の 7 並列配列にも `Assets/Game/Resources/` にも
  何も追加していない = カタログは増えない)
- 差し込み口: `Assets/Game/Scripts/Songs/PlaySessionSnapshot.cs` の
  `ChartJsonOverride` (テスト用シーム)
- ハーネス: `Assets/Game/Scripts/Common/AutoTestV6Hold.cs`
  (プレイヤー引数 `-autotest v6hold`)
- 起動スクリプト: `script/mac-holdprobe-harness.sh`

## 2. 実行方法 (再現手順)

```
# 実ディスプレイ全画面 (2560x1664)。窓リサイズによる擬似全画面は不可。
script/mac-holdprobe-harness.sh <app path> <out dir>
```

起動は実行ファイル直叩きで **自分の子プロセスだけ** を kill する
(ユーザーが手元で起動した FlashBeat には触らない)。

## 3. 実測結果 (run-a / 合格)

`run-a/autotest-v6hold-result.txt`:

```
[AutoTestV6Hold] RESULT pass=41 fail=0
song=ツイッターランド
scene=ResultScene
chartNotes=7 runtimeNotes=7 taps=4 holds=3
holdInstances=3 maxHoldSeconds=5.00
holdObserved=3 holdEnded=3 holdBodiesVisible=3
holdClear=3 holdBroken=0 missLog=0 holdEndEvents=clear@13.10,clear@24.10,clear@35.10
perfect=7 great=0 bad=0 miss=0
judged=7
settled=7
maxScore=35 score=1000000 combo=7 maxCombo=7 comboPeak=7 hitsSent=7
```

### 3.1 判定の読み方

| 項目 | 意味 |
|---|---|
| `pass=41 fail=0` | ハーネスの検査が **全項目成功** |
| `runtimeNotes=7 / taps=4 / holds=3` | **差し替え譜面**が実機で再生された (商品譜面ではない) |
| `holdInstances=3 / maxHoldSeconds=5.00` | HOLD の **実体**が 3 個あり、胴体が 5 秒分伸びている |
| `holdObserved=3` | HOLD の **開始** 3 回を観測 |
| `holdBodiesVisible=3` | 保持中に **胴体が描画されていた** (レンダラーが見え、長さが伸びている) |
| `holdClear=3 / holdBroken=0` | **終端** 3 回すべてが「離して成功」= 保持が最後まで保たれた |
| `holdEndEvents=clear@13.10,clear@24.10,clear@35.10` | 終端の判定時刻 (譜面の終端 13.05 / 24.05 / 35.05 + 判定幅内の余裕) |
| `judged=7 == settled=7 == chartNotes=7` | 取りこぼし・重複判定なし |
| `maxScore=35` (= 7 x 5) / `score=1000000` | スコアの天井が **HOLD 入り譜面**に従っている (タップのみの譜面とは違う) |
| `combo=7 / maxCombo=7` | コンボが上がり切っている (タップだけなら 4 止まり) |
| `perfect=7 great=0 bad=0 miss=0` | 全ノーツが Perfect |

### 3.2 スクリーンショット (run-a)

| ファイル | 内容 |
|---|---|
| `autotest-v6hold-01-01-select.png` | 選曲画面 |
| `autotest-v6hold-02-02-game.png` | ゲーム画面 (準備完了) |
| `autotest-v6hold-03-03-gameplay.png` | ゲーム画面 (再生中) |
| `autotest-v6hold-04-04-hold-body.png` | **保持中の HOLD** — 左端レーンに白い帯 (胴体) が伸びている / `Perfect` 表示 |
| `autotest-v6hold-05-06-result.png` | Result 画面 (曲終了 · 遷移済み) |

`Player.log` / `hold-schedule.txt` / 結果 txt も run-a に同梱。

## 4. 負の対照 (control) — 失敗しなかったら緑谈不上

修正前の run (run-b-before-fix/) は **同じハーネスで fail=2** だった。

```
[AutoTestV6Hold] RESULT pass=39 fail=2
chartNotes=17 runtimeNotes=7 taps=14 holds=3
holdClear=0 holdBroken=3 holdEndEvents=broken@8.03,broken@20.00,broken@32.00
perfect=4 great=0 bad=3 miss=0
```

これが示した実害:

1. **差し替え譜面が負けて商品譜面が再生されていた** (差し替えは `chartJson == null` の
   ときだけ効く実装だった。原子番はリモート取得済みの譜面を渡す)。
2. **HOLD が 3 個とも Broken** — 保持フェーズが再現できず、HOLD の胴体が
   最後まで保たれていなかった (`taps=14` は番兵 10 個を数えていた)。

同じハーネスで **修正前は落ち、修正後は緑** になるので、
「緑だから正しい」とは言えない。判定ロジック自体の検証は
EditMode (`HoldProbeChartTests` / `HoldProbeHarnessTests`) でも固定している。

## 5. 判定の限界 (正直に)

1. **キー押下は「注入シーム」で再現している。**
   HOLD の保持フェーズは `Input.GetKey` (実キー) を見る。macOS の Unity legacy Input は
   CGEvent の合成キーイベントを `Input.GetKey` として **見ない** ので (実測: run-b で
   OS レベルのキー押下が反映されなかった)、保持状態は既存の注入シームを拡張して
   模擬している (`KeyBindings.InjectKeyHold`)。
   **判定経路 (Judge → JudgeQueue → LongNote) 自体は実機のもの**だが、
   「本物のキーボードを押して HOLD が保たれる」ことは **未検証**。
2. **Windows 実機では未検証。** 受入条件 3 のとおり Windows 側の受入として残す。
3. 対象の曲 (ツイッターランド) の **映像・音声** は YouTube 由来で、
   今回観測したのは HOLD の判定と描画であって A/V 同期ではない。

## 6. 証跡

| ファイル | 内容 |
|---|---|
| `run-a/` | 合格 run のログ / 結果 / スケジュール / スクリーンショット 5 枚 |
| `run-b-before-fix/` | 修正前 run の結果とログ抜粋 (負の対照) |
| `script/mac-holdprobe-harness.sh` | 起動スクリプト (リポジトリ側) |
| `Assets/Game/Scripts/Common/AutoTestV6Hold.cs` | ハーネス本体 |
| `Assets/Game/Scripts/Common/HoldProbeChart.cs` | 差し替え譜面 |
| `Assets/Tests/Editor/HoldProbeChartTests.cs` / `HoldProbeHarnessTests.cs` | EditMode テスト |
