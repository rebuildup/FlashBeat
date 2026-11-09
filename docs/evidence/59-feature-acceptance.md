# Issue #59 — #30 各機能 (歌詞同期 / キーガイド / HOLD / HUD) の受入確認記録

- **対象 Issue**: #59 (検証: Issue #30 各機能の受入確認)
- **基準点**: `origin/main` = 7355f7e (2026-11-10 / PR #264・#265 マージ直後)
- **実行環境**: macOS / Unity 6000.5.6f1 / EditMode テスト (`-runTests -testPlatform editmode`)
- **テスト結果**: **1181 / 1181 成功 / 失敗 0** (`/tmp/testresults-241b.xml` 相当、PR #264 のブランチで実測)

> **判定の厳格さについて**
> 本記録は「EditMode 緑 = 実機合格」ではない。**Windows 実機 (展示 PC) は未操作**のため、
> 画面表示・入力・実通信を含む条件は **未検証** のまま残す。
> macOS の実機スクリーンショットは「その環境で何が映ったか」の記録であり、
> Win64 の代替ではない (`docs/evidence/index.md` §1.6 の扱いに従う)。

---

## 1. 判定サマリ

| # | 機能 | 判定 (コード・テスト) | 判定 (実機) | 主な証跡 |
|---|---|---|---|---|
| 1 | 歌詞の動画時刻への同期 | **充足** | **未検証** (Win64) / macOS では表示を確認 | `Assets/Game/Scripts/UI/LyricsTimeline.cs`、テスト 18+1+6 件、スクリーンショット `game-06-dense-2560x1664.png` |
| 2 | 全キー案内 (キーガイド) | **充足** | **未検証** (Win64) / macOS では下部のキー列を確認 | `Assets/Game/Scripts/UI/KeyGuideUI.cs`、テスト 11+3 件、スクリーンショット同上 |
| 3 | HOLD (ロングノーツ) | **充足** (仕様凍結済み) | **未検証** (実機での保持・離し・中断は未実施) | `Assets/Game/Scripts/Gameplay/LongNote.cs`、テスト 23+2 件、`specs/164-long-note/spec.md` |
| 4 | HUD (スコア / コンボ / 判定ゲージ) | **充足** | **未検証** (Win64) / macOS では表示を確認 | `Assets/Game/Scripts/UI/V5/GameHudView.cs` / `V6/Game/Hud/V6GameHudView.cs`、テスト 多数、スクリーンショット同上 |

**未充足は 0 件。** 残るのは **実機 (Windows) 観察が必要な条件だけ**。

---

## 2. 機能別の記録

### 2.1 歌詞同期 (Issue #30 §2)

| 項目 | 内容 |
|---|---|
| 実装 | `Assets/Game/Scripts/UI/LyricsTimeline.cs` — 歌詞行の開始・終了時刻から「いま表示する行」を決める純ロジック。`Assets/Game/Scripts/UI/V6/Game/Hud/V6GameHudLogic.cs` の `LyricsTimeline_Build` が HUD へ渡す |
| 時刻軸 | 動画 (`VideoPlayer.time`) のタイムラインを参照 (`Assets/Game/Scripts/Gameplay/MediaTimeline.cs`)。`Time.deltaTime` 累積から独立 |
| テスト | `Assets/Tests/Editor/LyricsTimelineTests.cs` **18 件** (行の開始/終了時刻、終端未設定時の既定 duration、間奏の区間に線は出ない、furigana の有無など)<br>`Assets/Tests/Editor/ChartLyricsTimingTests.cs` **1 件** (全同梱譜面の開始<終了)<br>`Assets/Tests/Editor/EditDataSerializerLyricsTests.cs` **6 件** (Web 公開 → Unity 読み込みの歌詞往復) |
| 実機 (macOS) | 2560×1664 実ディスプレイ全画面で **歌詞「ああ」とローマ字行「ala」が表示**されていることを確認 (`docs/evidence/233-playfield/screenshots/game-06-dense-2560x1664.png`)。旧 v5 歌詞との二重表示はない (同 PR #263 の `GraphicVisibility` / `GameUiExclusivity` で検査を固定)<br>**注意**: このスクリーンショットと `docs/evidence/233-playfield/` 自体は PR #263 で main に入る予定 (本 PR と同時に参照可能になる) |
| 判定 | **充足** (コード・テスト) / **未検証** (Win64 実機での同期確認) |

**残存**: Windows 実機で「歌詞が動画と同期している」ことを人の目で確認する (#242 の全画面受入に含む)。

### 2.2 キーガイド (全キー案内) (Issue #30 §3)

| 項目 | 内容 |
|---|---|
| 実装 | `Assets/Game/Scripts/UI/KeyGuideUI.cs` — 8 レーンのラベルとキーを **判定と同一の定義** から生成 (`Assets/Game/Scripts/Gameplay/KeyBindings.cs` が単一正本) |
| テスト | `Assets/Tests/Editor/KeyGuideTests.cs` **11 件** (8 レーンであること、レーン間にキーの重複がないこと、判定と UI が同じ表を読むこと、記号キー表示名、日本語フォントを使うこと)<br>`Assets/Tests/Editor/KeyBindingsTrunkTests.cs` **3 件** (割当変更でガイド表示が追従すること) |
| 実機 (macOS) | 同スクリーンショットの最下部に **A / S / D / F / J / K / L** のキー列が表示**されていることを確認 (スクリーンショットは PR #263 で main に入る) |
| 判定 | **充足** (コード・テスト) / **未検証** (Win64 実機) |

**残存**: Windows 実機でのキーガイド表示 (JIS 配列の実キーとの一致) の確認。

### 2.3 HOLD (ロングノーツ) (Issue #30 §1 / #164 で凍結)

| 項目 | 内容 |
|---|---|
| 仕様 | `specs/164-long-note/spec.md` に**凍結済み** (先頭 1 判定 + 終端で離す / 離す許容幅 ±0.15s / 中断で即 BAD・押し直し不可 / タップと同一配点 / `type=2` + `endNum`)。**Issue #164 は CLOSE** |
| 実装 | `Assets/Game/Scripts/Gameplay/LongNote.cs` (判定の純ロジック) / `Assets/Game/Scripts/Gameplay/JudgeQueue.cs` (`BeginHold` / `UpdateHold` / `HoldResult`) / `Assets/Game/Scripts/Gameplay/Judge.cs` (`DowngradeHoldToBad`) |
| テスト | `Assets/Tests/Editor/LongNoteTests.cs` **23 件** (開始/終了時刻、長さが拍に追従する、終端での離しが成功，稍微早い/遅い離しは失敗，中断は救済されない、最大スコアが note 数×5 のまま、既存 tap 譜面の最終 note 時刻が変わらない，等等)<br>`Assets/Tests/Editor/JudgeQueueTests.cs` の HOLD 2 件 (`Hold_ExpiryBoundary_MatchesLongNoteRule` / `Hold_RuntimeResult_MatchesLongNoteRules`) |
| 実機 | **未実施**。macOS / Win64 どちらでも「HOLD を押し続けて終端で離す」「途中で離すと BAD」の実演奏確認はしていない |
| 判定 | **充足** (仕様・実装・テスト) / **未検証** (実機演奏) |

**残存**: 実機での HOLD 演奏 2 ケース (成功 / 中断) の確認。**Issue #30 の D5 (`scoringVersion`) のみ未決**だが、HOLD はタップと同点なので記録方式への影響は小さい (`docs/ui/issue-30-spec-decisions.md` §3 D5)。

### 2.4 HUD (Issue #30 §4)

| 項目 | 内容 |
|---|---|
| 実装 | `Assets/Game/Scripts/UI/V5/GameHudView.cs` (v5 HUD: スコア / コンボ / 判定ゲージ / ランク) と `Assets/Game/Scripts/UI/V6/Game/Hud/V6GameHudView.cs` (v6 HUD) |
| テスト | `Assets/Tests/Editor/V5GameHudTests.cs` (スコア区切り / コンボボーナス / ゲージ幅 / ランク記号 SS〜C など 14 件)<br>`Assets/Tests/Editor/V6GameHudViewTests.cs`、`Assets/Game/Tests/V6GameHudLogicTests.cs` (レイアウト・ゲージ・歌詞ビルド・正解ラベル)、`Assets/Game/Tests/V6CanvasTests/V6GameHudContractTests.cs` (デザイン契約)、`V6HudResolutionProfileTests.cs` (解像度別プロファイル)、`Assets/Tests/Editor/HudPlayStateTests.cs` **9 件** (準備中 / 失敗 / 再生開始のメッセージ切替) |
| 実機 (macOS) | 同スクリーンショットで **ランク C / SCORE 0 / COMBO 0 / 判定ゲージ**、および判定Miss の表示を確認 |
| 判定 | **充足** (コード・テスト) / **未検証** (Win64 実機) |

**残存**: Windows 実機での HUD の可読性・重なり (#96 / #242)。

---

## 3. 発見した不具合の起票記録 (#59 の完了条件)

本記録の作成中に **1 件の実バグを発見**し、Issue #241 として起票・修正・PR #264 で main へ取り込んだ。

| 発見 | 起票 | 状態 |
|---|---|---|
| 譜ごとの offset 設定を `Assets/Resources/<曲名>.json` に書き出していたため、**設定 JSON が譜面 JSON と区別できず `Resources.Load(曲名)` が不定**になっていた (譜面の重複検査も編集のたびに壊れていた) | #241 | **修正済み** (PR #264 / `OptionSceneManager.SettingsDirectory()` を `UserSettings/song-settings/` へ、ファイル名を `.setting.json` へ) |

**既知の未解決 (起票済み・本 Issue の外)**:

| 内容 | 起票 | 状態 |
|---|---|---|
| Windows Media Foundation が `googlevideo.com` の URL を読めない (`WindowsVideoMedia error 0x80070005`)。映像・音声がストリーム由来で鳴らない | #4 / #13 | **未解決** (Windows 実機が必要。展示 PC は未操作) |

---

## 4. 総合判定

- **未充足の受入条件は 0 件**。4 機能すべてに実装・テスト・仕様がある。
- **残るのは実機観測だけ** (Windows 実機での 4 機能の目視確認)。
  macOS 実機 (2560×1664 全画面) では **歌詞・キーガイド・HUD・プレイフィールドの 4 つすべてが映っている**ことをスクリーンショットで確認済み。
- **HOLD だけは macOS / Win64 どちらでも実機演奏を確認していない**ため、
  #59 の HOLD 行は「充足 (コード・テスト) / 未検証 (実機演奏)」の 2 値表記のままとする。

**Issue #59 を Close する条件**: Windows 実機 (展示 PC) での 4 機能の確認が完了し、
そのスクリーンショットと Player.log が `docs/evidence/` に残ること。
**Windows 実機は展示 PC にあるため、実行はオーナーの許可が前提** (cc は自発的に触らない)。
