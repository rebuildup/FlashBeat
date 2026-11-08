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

---

## 追補 (2026-11-08, #142/#146/#148 残タスクの統合実装)

KeyActions 導入後に残った「表示の固定文字列」と「迂回入力」を正本に寄せる。

### 要件 (追補)

1. **表示の正本化**: V5 ビューの凡例・キーキャップの固定文字列
   (`"Esc"` / `"Space"` / `"R"` / `"BS"`) を廃止し、`ActionKeyHint.Primary/All`
   経由で現在の割当から生成する。対象:
   SelectView (5 箇所の Esc 凡例)、OptionView (Esc 凡例)、PauseMenuView
   (Resume/Retire/Retry キャップ + Esc 凡例)、ResultView (R/Space 凡例・Skip キャップ)、
   GameErrorView (Esc/R キャップ)、GameReadyView (Space/Esc キャップ)。
   - 対応する操作は KeyActions の定義に従う (Esc→Back, Space→Confirm/Play/Start,
     R→Retry, BS→MenuRetire など)。
   - 生入力が Enter/KeypadEnter のままの箇所 (Result の Name/Save、Select の
     Enter→Ready 遷移) は #146 T3 の決定に従い固定表示を維持する。
2. **入力の正本化**: メニュー操作の `Input.GetKeyDown` 直接読みを `KeyActions.IsDown`
   に統一する (TitleSceneManager の先例に従い、既定外の併用キーは生読みを残す)。
   - SelectView (Typing/Ready/History/Ranking 各状態)、OptionView、PauseMenuView、
     ResultView、GameErrorView、SelectSceneManager (V6 History/Ranking)、
     ResultRankingOverlay。
   - レーンキー (KeyBindings) の読み取りは触らない。
   - KeyActions に存在しない固定ショートカット (F2 設定トグル、F3/F4 更新通知、
     F5 再取得、Tab/WASD/S/D/L/K/R モード切替・ナビ) は対象外。
   - 名前入力モーダルでは Space が文字入力になるため Confirm (既定 Space) を読まない。
3. **テスト** (`Assets/Tests/Editor/MenuKeyActionWiringTests.cs`):
   - ActionKeyHint が割当変更に追従する (Back/Confirm/Play を変えると表示が変わる)。
   - 割当が空の操作でも「—」を表示して壊れない。
   - V5 ビューの凡例/キーキャップ API 呼び出し行に固定キーリテラルが残っていないことの
     ソース走査。
   - 対象ファイルが Esc/Space を直接読んでいないことのソース走査。

### 挙動不変条件

既定割当からの変更は禁止 (ユーザーが設定を変えない限り全画面の挙動は今日と同一)。
既定キーが生読みに含まれる場合は必ず KeyActions 経由へ置換し、
既定外の併用キー (Enter 等) だけ生読みを残す。
