# FlashBeat v6 UI (Pencil 移植) — 実装メモ

Issue #217 (親) / #218–#223 (子) の成果。ゲーム画面と選曲画面を
Pencil の v6 デザイン (`feature/pencil-ui` @ `19f08b31a3cf269bd0921543d6bc4f8ee3b6b876`) へ移植した。

## 参照

- デザイン契約 (正本の抽出): [`docs/design/v6/flashbeat-v6-contract.md`](../design/v6/flashbeat-v6-contract.md)
- 機械可読なノード仕様: [`docs/design/v6/flashbeat-v6-nodes.json`](../design/v6/flashbeat-v6-nodes.json)
- 参照スクリーンショット: `docs/design/v6/screens/*.png` (Pencil からの書き出し)

## コード配置

| 領域 | 場所 | 所有者 Issue |
|---|---|---|
| 選曲画面のデータ契約 (interface) | `Assets/Game/Scripts/UI/V6/Select/IV6SelectDataSource.cs` | #222 |
| 選曲画面の実データ | `Assets/Game/Scripts/UI/V6/Select/Data/` | #222 |
| 選曲画面の 3 カラム View | `Assets/Game/Scripts/UI/V6/Select/View/` | #221 |
| プレイフィールド (8レーン遠近法) | `Assets/Game/Scripts/UI/V6/Game/Playfield/` | #219 |
| ゲーム HUD (3解像度) | `Assets/Game/Scripts/UI/V6/Game/Hud/` | #220 |
| 起動・切替 (統合) | `Assets/Game/Scripts/UI/V6/V6UiMode.cs`, `V6GameRuntime.cs` | #223 |

## v6 と旧 UI の切替

`Assets/Game/Scripts/UI/V6/V6UiMode.cs`:

```csharp
V6UiMode.GameHud = true;      // ゲーム画面: v6 の HUD + プレイフィールド
V6UiMode.SelectScreen = true; // 選曲画面: v6 の 3 カラム
```

- `false` にすると旧 (v5) の UI にFallback する (未移植画面用の退避先)。
- v6 有効時、旧 3D プレイフィールド (`Lane` / `JudgeLine` / `LaneLight*`) と旧 HUD (`GameHudView`) は
  作られない。二重描画で崩れるため。
- 準備中 / 失敗 / ポーズの画面は未移植 (P1) ので旧実装のまま使う。

## 描画の積層 (ゲーム画面)

```
1. 動画 (VideoPlayer が描く)
2. Video Dim #00000080   → Canvas "V6GameDimCanvas"  (sorting 300)
3. プレイフィールド (レーン線 / ノーツ / 判定ライン) → Canvas "V6GamePlayfieldCanvas" (sorting 400)
4. HUD (グレード / スコア / コンボ / キーガイド / 歌詞) → Canvas "V6GameHudCanvas" (sorting 500)
```

HUD 内の "Video Dim" は無効化されている (順序が崩れるため)。減光は 2 番の Canvas が出す。

## データの流れ (実データから表示)

| 表示 | データ源 |
|---|---|
| グレード / スコア / コンボ / 判定 | `GManager.score` / `combo` / `perfect` / `great` / `bad` / `miss` |
| ゲージ / 進捗バー | `GManager.score` と `GManager.step_time` (メディア時刻) |
| レーン別判定行 | `Judge` が Instantiate した判定プレハブを走査してレーンと表示を対応付け (3D のは非表示) |
| キーガイド (8連) | `KeyBindings.GetKeys(lane)`。`KeyBindings.Changed` を購読するのでキー設定に即追従 |
| 歌詞 | `LyricsTimeline.Build(...)` + `GManager.step_time` (動画と同じタイムライン) |
| 曲リスト / 選択中の曲 | `V6SelectDataSource` (GManager の実カタログ + CloudCatalog + 実譜面 JSON) |
| ベストスコア | `GManager.BestScoreOf(songId)` (stable songId + 譜面版, Issue #189) |
| ランキング | `RankingClient` (オンライン。取得失敗時は空リスト + エラー表示) |
| ジャケット | `Assets/Game/Resources/V6/select/jacket/` (Pencil 設計の正式画像 26 枚。無い曲だけ空枠) |

固定データ (样本曲名・"988,513" 等の仮スコア) は表示しない。

## 検証

- EditMode テスト: `Assets/Game/Tests/V6SelectViewLogicTests.cs`,
  `V6SelectDataLogicTests.cs`, `V6GameHudLogicTests.cs`
- 実機 (macOS ビルド) での通し確認:

  ```bash
  # ビルド
  Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneMacOS
  # v6 の 選曲→Ready→ゲーム を実データで検証 (スクリーンショット付き)
  open -n Builds/macOS/FlashBeat.app --args -autotest v6 -autotest-out /tmp/v6-verify
  ```

  検証スクリプト: `Assets/Game/Scripts/Common/AutoTestV6.cs`
- 画面キャプチャ (シーン単位): `-autoshot <dir> -autoshot-scenes SelectScene,GameScene`

## 未移植 (P1 以降)

- ポーズ / 曲ローディング (Issue #224) — 旧 UI のまま
- Title / Result / Option の全面移植 — 旧 UI のまま
- v6 の設定パネル (選曲画面の設定) — 未実装 (`onOpenSettings` は警告ログのみ)
