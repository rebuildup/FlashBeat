---
description: "FlashBeat lean implementation plan — Issue 241"
---

# Plan: v6 Option 4 タブの整合 — Issue #241

**Issue**: #241 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

```
[ 実装 (main 済み・変更しない) ]

Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs
  Play  :826 NoteSpeed / :832 NoteTiming / :843 Key Layout・キー設定
          :970 AdjustNoteSpeed → OptionSceneManager.AdjustNoteSpeed (clamp + 保存)
          :976 AdjustNoteTiming → 保存 + offset 書込
  Display :869 GManager.Flash / :881 FlashT / :887 FlashBG (CycleFlashSpeed)
            :1008-1023 トグル + OptionSettings.Save()
  Sound  :1019-1028 AudioListener.volume = GManager.mainVolume ほか
  Data   :1044-1047 MediaCacheManager.FormatSize / GetCacheSizeBytes
          :1050-1053 MediaCacheManager.ClearAll()
          :1068-1078 ClearSaveData()
              ├─ ScorePersistence.ResetAll()   ← 安定キー FlashBeat.ScoreStore も消す
              ├─ Array.Clear(GManager.Hiscore)
              ├─ KeyBindings.ResetToDefaults()
              └─ KeyActions.ResetToDefaults()
          :1090+ OpenClearConfirm / CancelClearConfirm / ConfirmClearSaveData
  → 永続化: OptionSettings.Save() (:986,994,1002,1009,1016,1023)
  → OptionSettings 定義: OptionSceneManager.cs:29 / Save(): :53

[ 既存 API をそのまま使う (新設しない) ]
  OptionSettings (PlayerPrefs "FlashBeat.*")  … 永続
  GManager (Flash / FlashT / FlashBG / mainVolume …) … ランタイム値
  KeyBindings / KeyActions (JSON "FlashBeat-keyconfig.json" + PlayerPrefs ミラー)
  MediaCacheManager … キャッシュ

[ 残作業 = 実機目視 ]
  ① 4 タブのマウス操作 (各タブの全行)
  ② 4 タブのキー操作
  ③ 値変更 → 実動作 (音量 / Flash / NoteSpeed) → 再起動後の復元
  ④ cache size 表示 / cache clear
  ⑤ Clear save data → 確認 → キャンセル (変化なし) → 実行 → 再起動後もスコアが復活しない
  ⑥ FHD / 2K / Mac の 3 解像度で重なり・欠け・hitbox
  ⑦ 撤回済み 4 行 (Live Rank / Hit Timing / Judge Text / Progress Bar) の
     「意図未確定のデザイン差分」としての記録
```

## 実装順序

1. **既存実装とテストの確認 (変更なし)**
   - `V6OptionView.cs` の 4 タブ行と `OptionSettings.Save()` の呼び出し群を読む。
   - `Assets/Game/Tests/V6OptionLogicTests.cs` と `Assets/Tests/Editor/V6OptionWiringTests.cs`
     (`:181` / `:208` / `:347`) が緑であることを確認する。
2. **撤回済み 4 行の記録 (実装なし)**
   - Live Rank / Hit Timing / Judge Text / Progress Bar が **現行設定モデルに対応しない** ことを
     spec に記録する (Issue 本文の撤回)。
   - **見た目だけのトグルは作らない。**
3. **Pause との共有箇所の直列化確認**
   - Pause 側の共有箇所が `#224` の Worker と競合しないかを確認し、
     同時に触るファイルがあれば調整する。
4. **実機での操作確認 (macOS で可能な範囲 → Windows はオーナー実施)**
   - macOS 実機 (実ディスプレイ 2560×1664 全画面) で 4 タブのマウス / キー操作、
     値変更、音量即時反映、cache size 表示、Clear save data の確認 → キャンセル を観測する。
   - **macOS の観測は Win64 の代替にならない** (`docs/evidence/index.md` §1.6 / §1.8)。
5. **Windows 実機 (オーナー実施)**
   - FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664 の 3 条件で 4 タブを操作し、
     重なり・欠け・押せない hitbox がないことを確認する。
   - 手順は `docs/operations/win64-candidate-acceptance-2026-11-10.md`。
6. **消去の再起動サイクル確認 (Windows 実機)**
   - Clear save data を実行 → 再起動 → スコアが復活しないこと。
   - キー設定も既定へ戻っていること。
7. **判定表**
   - 10 個の受入条件を OK / NG / 未検証 で記録する。**未検証を「合格」に書き換えない**。

## ブロッカー

| ブロッカー | 内容 | 対処 |
|---|---|---|
| **Windows 実機 (展示 PC)** | 3 解像度の目視・消去の再起動サイクル・hitbox 確認は実機必須。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** | 手順書と候補 SHA を渡し、判定を「未検証」で固定 |
| **Pause との共有** | Pause 側の共有箇所は `#224` と直列化 | 同時改変しない。競合が見つかった場合は統合 owner と調整 |
| **撤回済み 4 行** | Live Rank / Hit Timing / Judge Text / Progress Bar | **新機能として必須化しない**。見た目だけのトグルを作らない |
| **`docs/evidence/index.md` の編集** | 他 Worker 所有 | 触らない |
| **再起動サイクル** | 消去後にスコアが復活しないことは PlayerPrefs 単位ではテスト済みだが、**実機での再起動は未観測** | 実機で 1 回通す (オーナー実施) |

## テスト戦略

| 種別 | 内容 | 備考 |
|---|---|---|
| EditMode (既存のみ) | `Assets/Game/Tests/V6OptionLogicTests.cs`、`Assets/Tests/Editor/V6OptionWiringTests.cs` (`:181` スコア消去 / `:208` キー設定リセット / `:347` 確認ダイアログのキャンセルと消去) | **新規テストは書かない** (実装は PR #264 で完了) |
| 実機 (macOS) | 4 タブのマウス / キー操作、音量即時反映、cache 表示、Clear save data の確認 → キャンセル | **実ディスプレイ全画面**。Win64 の代替にはしない |
| 実機 (Windows) | 3 解像度の目視 / hitbox / 消去の再起動サイクル | **オーナー実施** |
| 回帰 | 既存 Option (v5) の挙動が変わっていないこと | 既存 API を再利用しているので Changed なし。差分があれば PR #264 の再確認 |
