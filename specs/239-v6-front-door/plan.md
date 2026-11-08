# plan — 239 v6 front door

## 変更対象 (所有パス内)

| ファイル | 変更 |
| --- | --- |
| `Assets/Game/Scripts/UI/V6/Title/V6TitleLinks.cs` | `EntryScreen` / `PendingEntry` (Title → Select の入口要求) を追加 |
| `Assets/Game/Scripts/UI/V6/Title/V6TitleLogic.cs` | タブ KeyCap と poll キーの単一ソース (`HistoryTabKey` / `RankingTabKey` + KeyCode) |
| `Assets/Game/Scripts/UI/V6/Title/V6TitleView.cs` | タブ KeyCap を単一ソースから取る |
| `Assets/Game/Scripts/UI/TitleSceneManager.cs` | V6Links 設定/解除 (OnDestroy)、H / R キー poll、pending 設定して Select へ遷移 |
| `Assets/Game/Scripts/UI/SelectSceneManager.cs` | pending entry の消費、`OnV6OpenSettings` を全モード有効化 (History/Ranking 入力部分のみ) |
| `Assets/Game/Scripts/UI/V6/History/V6HistoryLogic.cs` | `PlaysLabel` → `RecordedCountLabel` (記録曲数) |
| `Assets/Game/Scripts/V6/History/V6HistoryView.cs` | ラベル修正・順位/HOLD セル削除・曲ランキング空状態分岐 |
| `Assets/Game/Scripts/UI/V6/Ranking/V6RankingLogic.cs` | `ShowOnlineSegment` / `SongRankingEmptyMessage` / `PlayersEmptyMessage` |
| `Assets/Game/Scripts/UI/V6/Ranking/V6RankingView.cs` | 凡例修正、Online セグメント条件表示、PLAYERS の死んだ矢印削除 |

読み取りのみ: `V6SelectDataSource.cs` (`HasError` / `StatusMessage` / `IsLoading` / `RankingFor`)、`Assets/Game/Scripts/UI/V6/Common/**`。

## テスト

- `Assets/Tests/Editor/V6HistoryLogicTests.cs` — PlaysLabel のテストを RecordedCountLabel に差し替え (誤った文言を固定していたため)。
- `Assets/Tests/Editor/V6RankingLogicTests.cs` — `ShowOnlineSegment` / `SongRankingEmptyMessage` / `PlayersEmptyMessage` を追加。
- `Assets/Game/Tests/V6TitleRuntimeTests.cs` — タブキーの cap 表記と poll キーの一致 (単一ソースの不変条件)。

既存テスト: レイアウト系 (V6HistoryRankingLayoutTests / V6TitleLayoutTests) は座標・定数のテストで、本変更の対象外の値のみを固定しているため変更しない。

## 検証

- `script/compile-check.sh --project /Users/rebuit/work/... --assembly flashbeat` (Editor 起動は禁止)。
- EditMode テスト実行は Supervisor が統合時に実施 (実機プレイ検証は不可 → evidence に「未検証」と明記)。

## リスク

- `V6TitleLinks` の静的 Action はシーン遷移で持ち越されるため、TitleSceneManager の OnDestroy で解除する (dangling delegate 防止)。
- Select 画面の L/R 凡例は #221 所有のビューにしか出せない → evidence に残課題として記録。
