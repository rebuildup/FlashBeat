# Plan: #142 KeyActions

- KeyBindings.cs の構造 (IStorage / PlayerPrefs JSON / Changed / 注入フック) を踏襲する。
- 既定表は現行コードの各 `Input.GetKeyDown` 呼び出しから機械的に採る。Start のみ
  Space+Return+KeypadEnter (タスク指示)。
- 検証規則は Set() に集約: in-game 操作 (Play/Back) は `KeyBindings.IsReserved` のみ、
  それ以外は `IsLaneBound` のみ拒否。既定値は検証を通らない (R/E/T は既定でレーン重複) ので直接投入。
- KeyConfigUI は page (0/1) を軸に既存の行 UI を一般化 (MoveSelection/SwitchPage/ToggleActionKey)。
- ヘルプ表示は実行時に TMP テキストを走査して差し替える (シーン YAML は触らない)。

リスク:
- Unity が起動できないためコンパイル検証は Supervisor の Mac ビルドに依存。
  全変更行を再読して目視検査する。
- 予約キーの中で UI 内移動に使う Esc/矢印/Tab/PageUp/PageDown は listen 対象から除外
  (Space/Return/KeypadEnter/BS/F キーは割当可能)。
