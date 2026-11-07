# Spec: 109-key-config — カスタムキー設定

## 背景 / Problem

判定 (`Judge.CheckAll`) は `Input.GetKeyDown(KeyCode)` をレーン固定のキーテーブル (`Judge.LaneKeys`) で照会するため、大西配列など QWERTY でない物理配列の PC では正しく入力できず、展示不可。Refs #41 (v0.1.3 パッチ要求)。

## Goal

- レーンごとのキー割り当てをユーザーが設定・保存できる。
- 非 QWERTY 配列の PC でもゲームが遊べる。
- 変更は v0.1.2 のソース (`4b0685d`) にも cherry-pick 可能な形にする。

## Design

### KeyBindings (新規, `Assets/Game/Scripts/Gameplay/KeyBindings.cs`)

- 静的クラス = キー割り当ての正本。8 レーン x 可変長 `KeyCode[]`。
- 既定値は CLAUDE.md の正本キーマップと同一 (v0.1.2 では `Judge.LaneKeys` が private のため独自に保持)。
- API: `GetKeys(lane)`, `Current`, `SetLane(lane, keys)`, `ToggleKey(lane, key)`, `ResetToDefaults()`, `Save()`, `Load()`, `IsReserved(key)`, `LaneCount`, `Changed` イベント。
- 不変条件:
  - 1 キーは最大 1 レーンにのみ属する (割り当てると移動)。
  - `ToggleKey` / 移動によってレーンが空になる変更は拒否。
  - `SetLane(lane, 空)` だけは編集中の一時状態として空を許す。`Save()` は空レーンがあると保存しない (false)。
  - 予約キー (Esc / F1-F15 / Tab / Return / KeypadEnter / Space / 矢印 / Backspace / マウス / None) は割り当て不可。
- 保存: JSON (`{"lanes":["Alpha1,Q,A,Z", ...]}`) を PlayerPrefs キー `FlashBeat.KeyBindings` に保存。保存先は `IStorage` を注入可能 (単体テスト用)。
- 読み込み: 保存データが無ければ `StreamingAssets/keybindings.json` (運用者上書き用) を読む。不明キー / 壊れデータ / レーン数不一致はレーン単位 (全体破損時は全体) で既定値にフォールバック。例外は決して外に出さない。

### Judge (既存, 1 行のみ)

`KeyCode[] keys = LaneKeys[targetLane];` → `KeyCode[] keys = KeyBindings.GetKeys(targetLane);`
`Judge.LaneKeys` は trunk では正本の既定値として残す (既存テストが参照)。

### KeyConfigUI (新規, `Assets/Game/Scripts/UI/KeyConfigUI.cs`)

- コードのみで生成するオーバーレイ (シーンファイルには触れない)。`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + DontDestroyOnLoad で全シーン有効。
- F2 で開閉 (どのシーンでも)。閉じているときは入力を一切奪わない。
- 操作 (すべてレイアウト非依存キー): ↑↓ = レーン選択, Enter = listen モード (押したキーをそのレーンにトグル割り当て), Esc = listen 終了 / 閉じる, Backspace = レーンのキーを消す (空レーンでは保存不可), R = 初期化 (2 回押しで確認)。
- 閉じるときに保存。空レーンがある場合は保存せずメッセージ表示で閉じない。
- 動作確認用の 1 行表示「押したキー: X → レーン N」。
- Canvas: ScreenSpaceOverlay, CanvasScaler ScaleWithScreenSize 1920x1080, match 0.5 → 1024x768〜2560x1440 で崩れない。
- フォント: シーン内の TMP フォント (Noto/YuGoth を優先) を流用。日本語ラベル (キー設定 / 押したキー / 初期化)。
- 「F2: キー設定」ヒントは TitleScene / SelectScene のみ表示。
- KeyGuideUI に依存しない (v0.1.2 にもそのまま載る)。表示名関数は独自に持つ。

### KeyGuideUI (trunk のみ変更)

- 表示元を `Judge.LaneKeys` → `KeyBindings` に変更し、`KeyBindings.Changed` で自動更新。`BuildLaneLabel(lane)` のシグネチャは維持 (既存テストが参照)。

## Non-goals

- シーンファイル (.unity) の変更なし。
- Select/Title 各マネージャ、MusicManager には触れない。
- Unity Editor の起動・実機検証は Supervisor が行う。

## Manual verification checklist (大西配列 PC)

1. タイトル画面で F2 → キー設定パネルが開く。
2. ↑↓ でレーンを選択、Enter → 大西配列で「A」の位置にある物理キーを押す → レーン表示が更新される。
3. 画面下の「押したキー: X → レーン N」で意図したレーンに割り当たったか確認。
4. Esc で閉じる (保存) → ゲームを開始し、割り当てたキーでノーツが取れるか。
5. ゲーム中に F2 → 開くこと、閉じた後に Space / Enter がゲームに効くことを確認。
6. F2 → R ×2 で初期化 → 既定 (QWERTY 相当) に戻る。
7. Backspace でレーンを空にして Esc → 「保存できません」メッセージが出て閉じない。
8. ゲーム再起動 → 設定が残っている (PlayerPrefs)。
9. 1024x768 ウィンドウでパネルがはみ出さない。
