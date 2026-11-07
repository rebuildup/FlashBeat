# Plan: #118

## 構成
- `Assets/Game/Scripts/Update/UpdateFlowModel.cs`: 純粋な状態機械 (Unity API なし)。遷移・不変条件をここに集約。
- `Assets/Game/Scripts/Update/UpdateChecker.cs`: IO (UnityWebRequest / staging / プロセス起動) を担う MonoBehaviour。起動時は確認のみ。`RequestDownload()` / `RequestApply()` がユーザー起点の唯一の入口。OnApplicationQuit の自動適用を削除。
- `Assets/Game/Scripts/UI/UpdateNoticeUI.cs`: コード構築 overlay。TitleScene のみ表示、F3/F4/クリック/× 処理、進捗表示。
- `Assets/Tests/Editor/UpdateFlowTests.cs`: UpdateFlowModel の遷移・不変条件 (自動適用なし・dismiss・失敗復帰) の EditMode テスト。IO は Model に含まれないためネットワーク不要。
- 既存 UpdateCheckerTests (ParseLatestRelease / UpdateVersion / UpdateStaging / UpdateChannel) は変更なしで通ることを確認。

## リスク
- Unity を実行できないためコンパイル検証不可 → 全変更行を再読し、API 名 (CanvasScaler.ScaleMode.ScaleWithScreenSize 等) と asmdef 参照を目視確認する。
- 新 .cs の .meta は手作り GUID で生成する。

## タスク (tasks.md 参照)
1. UpdateFlowModel + テスト
2. UpdateChecker 改修 (確認のみ化 / RequestDownload / RequestApply / OnApplicationQuit 削除)
3. UpdateNoticeUI 新規
4. docs 更新
5. patch ライン (118-patch) への同名義適用
