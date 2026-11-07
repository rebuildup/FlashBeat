# Spec: #142 メニュー操作キーの一元化 (KeyActions)

カスタムキー設定がレーンしかcoverageしていなかった。Title (Space/O/E)・Result (Space/R)・
Game ポーズ (R/E/T/BS/Esc)・Select (Space/Esc/矢印)・MusicManager (Space)・Typing (Space) が
各 SceneManager の `Input.GetKeyDown(KeyCode.X)` インライン読みのまま。
これらを 1 つの操作キー表 (KeyActions) に集約し、全画面が参照・UI から変更・保存可能にする。

## 要件

1. `Assets/Game/Scripts/Gameplay/KeyActions.cs` (静的):
   - enum `KeyAction` (Confirm/Back/Retry/Option/Exit/MenuRetry/MenuEnd/MenuRetire/MenuSwitch/Start/Play/NavPrev/NavNext)
   - 既定表は今日の挙動 exactly (Start のみ Space+Return+KeypadEnter へ拡張)
   - `IsDown(action)` は `KeyBindings.GetKeyDownOrInjected` 経由 (-autotest 注入対応)
   - Get/Set/Reset/Save/Load (PlayerPrefs JSON "FlashBeat.KeyActions"、不正データは既定値)
   - `Changed` イベント
2. Inline 読み替換: 上記全ファイルの該当 `Input.GetKeyDown` を `KeyActions.IsDown` へ
   (romaji タイピング・マウス・EventSystem・F3/F4 は対象外)
3. 割当規則: 演奏中に使う操作 (Play/Back) は予約キーのみ。それ以外は未使用キー自由。
   レーン割当中のキーは拒否 (メッセージ)。
4. KeyConfigUI に第 2 ページ『メニュー操作』 (←→ / PageUp/PageDown / クリックタブで切替)。
5. 画面のキー名ヘルプ (Title の push space/push o/push e、Typing の Space to Start) は現在の割当を表示。
6. Tests: EditMode NUnit (KeyActionsTests) + AutoTest keyconfig 拡張 (第 2 ページ操作 + Title 反映)。

Refs #142 #109 #41
