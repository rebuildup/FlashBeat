---
description: "FlashBeat lean tasks — Issue 241"
---

# Tasks: v6 Option 4 タブの整合 (Issue #241)

**Issue**: #241 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** 4 タブの設定項目と既存 API・値域の対応付け (Play / Display / Sound / Data)。
  — **done when**: 各設定項目が「表示値 / 実 API / 値域 / 保存 or セッション寿命 / 反映先」と
  対 file:line で対応付けられていること
  — **実測**: `Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs:826` (NoteSpeed) / `:832`
  (NoteTiming) / `:843` (Key Layout・キー設定) / `:869` (`GManager.Flash`) / `:881` (`GManager.FlashT`) /
  `:887` (`GManager.FlashBG`) / `:1019-1028` (Main/Effect/BGM 音量) / `:1044-1047`
  (cache size) / `:1050-1053` (cache clear)。
  値域 clamp は `OptionSceneManager.AdjustNoteSpeed` / `AdjustNoteTiming` が既存で持つ
  (`OptionSettings.NoteSpeedMin/Max` / `NoteTimingMin/Max`)

- [x] **T2** 設定の寿命の整理 (永続 vs セッション)。
  — **done when**: 永続する設定とセッション設定が明示され、復元経路が記録されていること
  — **実測**: 永続 = `OptionSettings.Save()` (`V6OptionView.cs:986,994,1002,1009,1016,1023`)、
  定義は `Assets/Game/Scripts/UI/OptionSceneManager.cs:29` / `Save()` は `:53`。
  セッション = PlaySession の Key Guide / Lyrics。
  キー設定は `KeyBindings` / `KeyActions` の JSON (`FlashBeat-keyconfig.json` + PlayerPrefs ミラー)。
  **全設定を一律に再起動後まで保存する新要件は撤回** (Issue 本文)

- [x] **T3** Data: cache size / cache clear が既存 `MediaCacheManager` を使っていることの確認。
  — **done when**: 使用箇所が file:line 付きで記録されていること
  — **実測**: `V6OptionView.cs:1044-1047` (`MediaCacheManager.FormatSize` /
  `GetCacheSizeBytes`)、`:1050-1053` (`MediaCacheManager.ClearAll()`)。
  テスト `Assets/Tests/Editor/MediaCacheManagerTests.cs`

- [x] **T4** `ClearSaveData` が **安定キー `FlashBeat.ScoreStore` も消す** ことの確認
  (Issue 本文が名指しした旧欠陥の修正済み確認)。
  — **done when**: 消去経路が file:line 付きで記録され、復活しないことを固定するテストがあること
  — **実測**: `V6OptionView.cs:1068-1078` が `FlashBeat.Persistence.ScorePersistence.ResetAll()`
  を呼ぶ (`Assets/Game/Scripts/Persistence/ScorePersistence.cs:276`、`:280-281` で
  `ScoreStore.StorageKey` = `"FlashBeat.ScoreStore"` と `LegacyStorageKey` = `"Hiscore"` を削除)。
  `Array.Clear(GManager.Hiscore)` + `KeyBindings.ResetToDefaults()` + `KeyActions.ResetToDefaults()`。
  テスト `Assets/Tests/Editor/V6OptionWiringTests.cs:181`
  (`ClearSaveData_ErasesTheStableScoreStore_SoTheScoreDoesNotRevive` — 再起動相当の
  `ScorePersistence.Unload()` 後・旧形式からの移行後も復活しないことまで固定)、
  `:208` (`ClearSaveData_ResetsTheLaneKeysAndTheMenuActions`)。

- [x] **T5** 確認 / キャンセルの実装と、見た目だけのトグルを新設していないことの確認。
  — **done when**: 確認ダイアログの実装位置が記録され、撤回済み 4 行が新設されていないこと
  — **実測**: `V6OptionView.cs:1090` 付近 `OpenClearConfirm` / `CancelClearConfirm` /
  `ConfirmClearSaveData` (結果表示 `"消去しました"`)。テスト
  `Assets/Tests/Editor/V6OptionWiringTests.cs:347`
  (`TheClearSaveDataDialog_CancelsWithoutChangingAnything_AndClearsWithTheMouse`)。
  `V6OptionView.cs` に **Live Rank / Hit Timing / Judge Text / Progress Bar の新しい設定項目は無い**
  (Issue 本文で撤回済みの追加要件のため、見た目だけのトグルは作らない)

- [x] **T6** 譜面 JSON と設定 JSON の Resources 混在バグの修正 (PR #264 で完了)。
  — **done when**: 設定 JSON の保存先が Resources 以外であることが確認できること
  — **実測**: PR #264 で `OptionSceneManager.SettingsDirectory()` → `UserSettings/song-settings/`、
  ファイル名 `.setting.json` へ。起票記録は `docs/evidence/59-feature-acceptance.md` §3。
  ガードは `Assets/Tests/Editor/ResourceChartUniquenessTests.cs`

- [ ] **T7** 実機での 4 タブ操作 (マウス / キー) と値変更の実動作確認。
  — **done when**: 4 タブすべてがマウスとキーの両方で操作でき、
  値変更が実動作 (音量 / Flash / NoteSpeed) に反映され、記録が残ること
  — **macOS 実機 (実ディスプレイ全画面 2560×1664) で実行可能**。
  **Windows 実機の代替ではない**
  — **Windows 実機 (展示 PC) では cc は実行しない。オーナー実施**

- [ ] **T8** Data タブの cache size 表示と cache clear の実機確認。
  — **done when**: cache size が実測値を表示し、cache clear が機能した記録があること
  — **macOS 実機 (実ディスプレイ全画面) で実行可能**

- [ ] **T9** Clear save data の確認 → キャンセル (変化なし) → 実行 → **再起動後もスコアが復活しない** ことの
  実機確認。
  — **done when**: キャンセルで何も変わらないこと、実行後に再起動してスコアが復活しないことの
  記録があること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**
  (再起動サイクルは macOS でも可能だが、**Windows 実機での確認を優先**)

- [ ] **T10** FHD / 2K / Mac の 3 解像度での目視 (文字・値・説明・操作範囲)。
  重なり・欠け・押せない hitbox が無いこと。
  — **done when**: 3 条件の実画像と判定記録が `docs/evidence/241-v6-option-4tabs/` に残ること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  **窓リサイズによる擬似全画面は証拠にならない**

- [ ] **T11** 撤回済み 4 行 (Live Rank / Hit Timing / Judge Text / Progress Bar) を
  「意図未確定のデザイン差分」として記録する。
  — **done when**: 4 行が「未確定・必須化しない」と記録され、
  **見た目だけのトグルとして実装されていない** こと
  — **実装はしない** (Issue 本文の撤回事項)

- [ ] **T12** 判定表 (10 個の受入条件 × OK / NG / 未検証) を作り PR で報告する。
  — **done when**: 判定が OK / NG / 未検証 のいずれかで記録され、
  **未検証が「合格」に書き換えられていない** こと
  — **Issue コメントの投稿は cc が行わない** (PR を作成して cc に報告する)
