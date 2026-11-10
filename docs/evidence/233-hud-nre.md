# v6 HUD が毎フレーム NullReferenceException で止まっていた件 (Issue #233)

対象:
- `Assets/Game/Scripts/UI/V6/Game/Hud/V6GameHudView.cs`
- `Assets/Tests/Editor/V6GameHudViewTests.cs`

## 発見の経路

PR #249 を `main` に merge した直後、**実機 (macOS プレイヤー) の自動テストハーネス**
(`-autotest v6`, 2560×1664 実ディスプレイ全画面) を走らせたところ、
`[AutoTestV6] RESULT pass=20 fail=1` とともに **Player.log に NullReferenceException が 1240 件** 記録されていた。

```
NullReferenceException: Object reference not set to an instance of an object
  at FlashBeat.UI.V6.Game.Hud.V6GameHudView.Refresh () [0x00233] in <f72009f6...>:0
  at FlashBeat.UI.V6.V6GameRuntime.Update () [0x0003d] in <f72009f6...>:0
```

IL2CPP のスタックトレースに行番号が無いため、
一度ビルドに **診断ログ (`[HUDDIAG]`) を一時的に差し込んで** どの参照が null かを特定した:

```
[HUDDIAG] m=ok gradeText=ok scoreText=ok comboText=ok lyricText=ok
  lyricFuriganaText=ok lyricRomajiText=ok lyricBox=ok progressTrack=ok progressFill=ok
  gaugeFill=ok gaugeBest=ok comboFill=ok bonusRow=ok bonusValueText=ok
  comboStatusBlock=ok comboStatusRow=ok fullComboChip=ok
  nextBonusText=ok nextBonusReward=NULL keyGuideRow=ok
```

## 原因

`V6GameHudView.BuildBonusRow` は **「値」側の Text だけを返す** ヘルパーだった。

```csharp
bonusValueText = BuildBonusRow(bonusRow, 0f, "+ COMBO BONUS", "+0");
nextBonusText  = BuildBonusRow(bonusRow, m.BonusRowH, "次のボーナス 300", "+0");
```

`BuildBonusRow` の中身は「ラベル Text を作る → 値 Text を作る → **値 Text を返す**」なので、

- 1 行目: `bonusValueText` = 値 Text ✅ (意図どおり)
- 2 行目: `nextBonusText` = **値 Text** (のはずが **ラベル** を指すべき変数) ❌
  かつ `nextBonusReward` = 値 Text の参照が **代入されないまま null** ❌

`Refresh()` は毎フレーム

```csharp
nextBonusText.text = "次のボーナス " + milestone;
nextBonusReward.text = "+" + ...;   // ← ここで NullReferenceException
```

で落ちていた。

### 影響 (重大)

`Refresh()` はこの地点で中断するため、**以降の更新が一切走っていなかった**:

- 進捗バー (progress fill)
- **歌詞** (UpdateLyrics) — 歌詞枠が真っ黒のまま発生しうる
- レーン別判定行 (UpdateLaneVerdicts)
- レーン格子 / キーキャップ位置 (UpdateLaneGrids)

つまり「画面が壊れて見える」現象の一部は、この NRE が原因だった。
実機のスクリーンショットでも、修正前は歌詞が空欄・キーキャップが未配置だった。

## 修正

- `BuildBonusRow` が **ラベルと値の両方を `out` で返す** ようにした。
- `Rebuild()` の破棄対象に `nextBonusText` / `nextBonusReward` を追加。
- `V6GameHudViewTests` (EditMode) を追加。
  HUD を **実構築して** `Refresh()` を呼ぶので、参照が欠落した瞬間に落ちる。

## 検証

| 検証 | 結果 |
|---|---|
| Unity EditMode スイート (Unity 6000.5.6f1 / macOS batchmode) | **1073 / 1073 成功** (追加 2 件を含む) |
| macOS スタンドアロンビルド | 成功 |
| 実機 (macOS プレイヤー, 2560×1664 実ディスプレイ全画面, `-autotest v6`) | **NullReferenceException 1240 件 → 0 件**。自動テストは `pass=20 fail=1` |
| 残.fail=1 | `no legacy graphics are visible` — 旧 `LoadingOverlay` が v6 中も可視。別タスクで修正中 |
| スクリーンショット | `before-fix-gameplay-2560x1664.png` / `after-fix-gameplay-2560x1664.png` |

修正後スクリーンショットでは **歌詞 (本文 + romaji) が出る** ようになり、
キーキャップ行もレーン位置へ揃っている (修正前は歌詞枠が空)。

## 未検証 / 残課題

- **Windows 実機では未検証** (展示 PC は無操作。GitHub Actions もライセンス未設定で未実行)。
- **8 レーン / 判定ライン / ノーツが見えない件は未解決**。上記の NRE とは別件で、
  動画 (3D レンダラ) と 3D プレイフィールドの描画順が疑われる (別 PR で調査中)。
- 画面下部に歌詞が二重に出ている (3D 側?) 点は未調査。
