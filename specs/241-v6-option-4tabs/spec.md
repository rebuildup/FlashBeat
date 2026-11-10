---
description: "FlashBeat lean spec — Issue 241"
---

# Spec: 既存設定の意味・保存範囲を保って v6 Option の 4 タブ UI を整合させる

**Issue**: #241 | **Branch**: `w/spec-gameui` | **Date**: 2026-11-10 | **Status**: Draft

## Input

`[P0/v6 Option] 既存設定の意味・保存範囲を保って4タブのUIを整合`

## Objective

v6 Option の **Play / Display / Sound / Data** の 4 タブを、**既存設定の意味と寿命 (保存範囲) を
壊さずに** 読みやすく操作できるようにし、**表示値・実動作・保存先の接続漏れを全部埋める**。

**PR #264 で実装は実質完了している。** 残る受入条件は **実機での目視 (3 解像度) と
確認/キャンセルの実操作** だけである。

本 spec の価値は、**「設定の意味と寿命の契約」を 1 枚に固定し、
残りが実機目視であることを明記すること** である。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 設定モデル | **既存 API と値域をそのまま使う**。`OptionSettings` (永続) + `GManager` (ランタイム値) +
  `KeyBindings` / `KeyActions` (JSON 保存) + `MediaCacheManager` (キャッシュ) の 4 系統。**新しい並行 SettingsPresenter や保存経路は新設しない** (Issue 本文) |
| 設定の寿命 | **全設定を再起動後まで保存する一律の新要件は撤回** (Issue 本文のスコープ訂正)。`OptionSettings` で永続化するものは保存・復元を確認し、**PlaySession の Key Guide / Lyrics は既存のセッション設定**として扱う |
| 消去範囲 | `ClearSaveData` の対象は **案内している既存データだけ**: ベストスコア (`ScorePersistence` / `FlashBeat.ScoreStore`) と キー設定 (`KeyBindings` / `KeyActions`)。**新しい保存データ種別を作らない** |
| 撤回された追加要件 | **Live Rank / Hit Timing / Judge Text / Progress Bar** の 4 行は現行設定モデルに対応がない。**見た目だけのトグルを作らない**。**意図未確定のデザイン差分として記録**し、ユーザーの範囲合意なく新設しない (Issue 本文) |
| 共有箇所の直列化 | Pause との共有箇所は **#224 と直列化** する。両方の Worker が同時に触らない |
| 参照デザイン | pencil frozen `19f08b31` — Play `B6If6Z` / Display `EzLfb` / Sound `z7joSy` / Data `DQHHp` / 共通フレーム `rw7zx`。**差分の発見を新機能開発の承認として扱わない** (Issue 本文) |
| 実機 | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。macOS 実機 (実ディスプレイ全画面 2560×1664) は **Win64 の代替にならない** |
| 証跡 | `docs/evidence/241-v6-option-4tabs/` に集める (新規)。`docs/evidence/index.md` /
  `docs/audit/open-issues-audit.md` は **他 Worker 所有** なので触らない |

## 非目標 (Out of scope)

- 新規設定項目 (Live Rank / Hit Timing / Judge Text / Progress Bar) の新設。
- 新しい保存経路・新しい ScoreStore 種別の新設。
- 既存描画・レーン効果のアルゴリズム変更 (Display タブ)。
- 既存 API の再設計。
- Pause 側の共有箇所の同時改変 (=#224 と直列化)。
- `docs/evidence/index.md` / `docs/audit/open-issues-audit.md` の編集 (他 Worker 所有)。

## Acceptance criteria

1. **Play** — NoteSpeed / NoteTiming / Key Layout / キー設定を **既存 API と値域** に従って操作でき、
   表示値・実動作・保存先が一致している。
2. **Display** — 既存の Flash / FlashTiming / FlashSpeed / Key Guide / Lyrics が接続され、
   **既存の描画・レーン効果のアルゴリズムは変更されていない**。
3. **Sound** — Main / Effect / BGM の既存音量へ **即時反映** される。
4. **設定の寿命** — `OptionSettings` で永続化する設定は保存・復元され、
   **Key Guide / Lyrics はセッション設定** として扱われている。
5. **Data** — cache size / cache clear が **既存 `MediaCacheManager`** を使い、
   Clear save data は **案内している既存データ (ベストスコア + キー設定) だけ** を消し、
   **消去後にスコアが復活しない**。
6. **確認 / キャンセル** — 消去の確認モーダルのキャンセルで何も変化せず、
   結果表示が実際の状況と一致する。
7. **操作性** — 4 タブをマウス / キーの両方で操作でき、
   **FHD / 2K / Mac の 3 解像度で重なり・欠け・押せない hitbox がない**。
8. **未確定の追加機能を必須化していない** — Live Rank / Hit Timing / Judge Text / Progress Bar を
   **見た目だけのトグルとして新設していない**。
9. **未解決点を隠して完成扱いにしていない** — 視覚上の未解決点は記録として残っている。
10. 受入判定が OK / NG / 未検証 の 3 値で記録され、**未検証が「合格」に書き換えられていない**。

## 現在の実装状況 (2026-11-10 時点)

**充足済み (コード・テスト)**

| 受入条件 | 判定 | 根拠 (file:line) |
|---|---|---|
| 1. Play: NoteSpeed / NoteTiming / Key Layout / キー設定を既存APIと値域で操作 | 充足 | `Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs:826` (NoteSpeed 行)、`:832` (NoteTiming 行)、`:970` (`AdjustNoteSpeed` → `OptionSceneManager.AdjustNoteSpeed` = clamp + 保存)、`:976` (`AdjustNoteTiming` → 保存 + offset 書込)。`OptionSettings.NoteSpeedMin/Max` / `NoteTimingMin/Max` で clamp。キー設定は `:843` 付近の `KeyBindings.ResetToDefaults()` / `IsDefaultKeyLayout()` |
| 2. Display: Flash / FlashTiming / FlashSpeed を既存値のまま接続、既存アルゴリズムは変更しない | 充足 | `V6OptionView.cs:869` (`GManager.Flash` の On/Off)、`:881` (`GManager.FlashT`)、`:887` (`GManager.FlashBG` = `V6OptionLogic.CycleFlashSpeed`)、`:1008-1023` (トグル + `OptionSettings.Save()`) |
| 3. Sound: Main / Effect / BGM へ即時反映 | 充足 | `V6OptionView.cs:1019-1028` 付近 (`AudioListener.volume = GManager.mainVolume` ほか) |
| 4. 設定の寿命: `OptionSettings` 永続分は保存/復元、Key Guide/Lyrics はセッション設定 | 充足 | `OptionSettings.Save()` の呼び出し群 (`V6OptionView.cs:986,994,1002,1009,1016,1023`)。`OptionSettings` 定義は `Assets/Game/Scripts/UI/OptionSceneManager.cs:29`、`Save()` は `:53` |
| 5a. Data: cache size / cache clear は既存 `MediaCacheManager` を使う | 充足 | `V6OptionView.cs:1044-1047` (`MediaCacheManager.FormatSize` / `GetCacheSizeBytes`)、`:1050-1053` (`MediaCacheManager.ClearAll()`) |
| 5b. Clear save data が **安定キー `FlashBeat.ScoreStore` も消す** (旧来の欠陥の修正) | 充足 | `V6OptionView.cs:1068-1078` `ClearSaveData()` が `FlashBeat.Persistence.ScorePersistence.ResetAll()` を呼ぶ。`Assets/Game/Scripts/Persistence/ScorePersistence.cs:276` (`ResetAll`)、`:280-281` (`ScoreStore.StorageKey` と `LegacyStorageKey` を削除)。**旧 `Hiscore` 配列も `Array.Clear` し、`KeyBindings` / `KeyActions` も既定へ戻す** |
| 6a. 確認 / キャンセルの実装 | 充足 (コード) | `V6OptionView.cs:1090` 付近 `OpenClearConfirm` / `:1096` 付近 `CancelClearConfirm` / `:1103` 付近 `ConfirmClearSaveData` (結果表示 `"消去しました"`)。テスト `Assets/Tests/Editor/V6OptionWiringTests.cs:347` (`TheClearSaveDataDialog_CancelsWithoutChangingAnything_AndClearsWithTheMouse`) |
| 6b. 消去後にスコアが復活しないことのテスト | 充足 | `Assets/Tests/Editor/V6OptionWiringTests.cs:181` (`ClearSaveData_ErasesTheStableScoreStore_SoTheScoreDoesNotRevive` — 再起動相当の `ScorePersistence.Unload()` 後も、旧形式からの移行でも復活しないことまで固定)、`:208` (`ClearSaveData_ResetsTheLaneKeysAndTheMenuActions`) |
| 8. 未確定の追加機能を必須化していない | 充足 | `V6OptionView.cs` に Live Rank / Hit Timing / Judge Text / Progress Bar の **新しい設定項目は存在しない**。撤回済みの追加要件は Issue 本文で明示的に撤回されている |
| 譜面/設定 JSON の Resources 混在バグ (付随発見) | 充足 | PR #264 で `OptionSceneManager.SettingsDirectory()` → `UserSettings/song-settings/`、ファイル名 `.setting.json` へ。`docs/evidence/59-feature-acceptance.md` §3 に起票記録 |
| EditMode テスト | 充足 | `Assets/Game/Tests/V6OptionLogicTests.cs`、`Assets/Tests/Editor/V6OptionWiringTests.cs` |

**未充足**

| 受入条件 | 判定 | 未充足の理由 |
|---|---|---|
| 5c. 消去の **実機** 確認 (再起動後もスコアが復活しないこと) | **未充足 (実機必須)** | テストは PlayerPrefs を直接操作している。**実機での再起動サイクルは未観測**。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** |
| 6c. 確認 / キャンセルと結果表示の一致 (実表示) | **未充足 (実機必須)** | モーダルの実表示・実クリック・実キー操作は Win 実機必須。**EditMode 緑は表示を保証しない** |
| 7. 4 タブの実機操作 (マウス / キー) と 3 解像度での重なり・欠け・hitbox | **未充足 (Windows 実機必須)** | FHD / 2K / Mac の実画像比較の記録が無い。**オーナー実施**。macOS は実ディスプレイ 2560×1664 のみ |
| 9. 視覚上の未解決点の記録 | **未充足** | 撤回済み 4 行 (Live Rank / Hit Timing / Judge Text / Progress Bar) の扱いを「意図未確定のデザイン差分として記録」する文書が本 spec 以外に無い |
| Issue 本文が名指しした旧欠陥の現況 | **充足 (PR #264 で修正済み)** | `docs/audit/open-issues-audit.md` の #241 節は **PR #264 マージ前の記述**。現状は `V6OptionView.cs:1068` が `ScorePersistence.ResetAll()` を呼ぶため **「未充足」は解消済み**。同監査のサマリ表も「PR #264 merged (`ClearSaveData` が `ScorePersistence.ResetAll()` を呼ぶ)」と更新済み |
