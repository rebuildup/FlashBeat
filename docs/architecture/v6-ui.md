# FlashBeat v6 UI (Pencil 移植) — 実装メモ

Issue #217 (親) / #218–#223 (子) の成果。ゲーム画面・選曲画面・タイトル画面・
リザルト画面・設定画面を Pencil の v6 デザイン
(`feature/pencil-ui` @ `19f08b31a3cf269bd0921543d6bc4f8ee3b6b876`) へ移植した。

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
| 履歴 / ランキング | `Assets/Game/Scripts/UI/V6/History/`, `V6/Ranking/` | #239 |
| プレイフィールド (8レーン遠近法) | `Assets/Game/Scripts/UI/V6/Game/Playfield/` | #219 |
| ゲーム HUD (3解像度) | `Assets/Game/Scripts/UI/V6/Game/Hud/` | #220 |
| ポーズ | `Assets/Game/Scripts/UI/V6/Game/Pause/` | #224 |
| ローディング | `Assets/Game/Scripts/UI/V6/Game/Loading/` | #224 |
| タイトル | `Assets/Game/Scripts/UI/V6/Title/` | #239 |
| リザルト (+ 登録モーダル) | `Assets/Game/Scripts/UI/V6/Result/` | #240 |
| 設定 | `Assets/Game/Scripts/UI/V6/Option/` | #241 |
| 起動・切替 (統合) | `Assets/Game/Scripts/UI/V6/V6UiMode.cs`, `V6GameRuntime.cs` | #223 |

## v6 と旧 UI の切替

`Assets/Game/Scripts/UI/V6/V6UiMode.cs`:

```csharp
V6UiMode.GameHud = true;      // ゲーム画面: v6 の HUD + プレイフィールド
V6UiMode.SelectScreen = true; // 選曲画面: v6 の 3 カラム
V6UiMode.Playfield = ...;     // v6 の遠近法プレイフィールド
V6UiMode.VideoDim  = ...;     // 動画を減光 (design の Video Dim)
V6UiMode.VerdictRow = ...;    // レーン別判定行
```

- `false` にすると旧 (v5) の UI に Fallback する (未移植画面用の退避先)。
- v6 有効時、旧 3D プレイフィールド (`Lane` / `JudgeLine` / `LaneLight*`) と旧 HUD (`GameHudView`) は
  作られない。二重描画で崩れるため。
- 一部の画面は `V6UiMode` ではなく **自分自身の公開フラグ** で切替する
  (例: `V6ResultView.Enabled`)。統合担当が `V6UiMode` と連動させる。

### design に無い機能を理由に原有機能を削らない

レーンフラッシュ (キー入力時にレーンが光る) は **元からあった機能** であり、
design ファイルに描かれていないことを理由に削除してはならない。
design は「UI から読み取れる機能」として実装する基準であり、
既存のゲーム進行に関わる機能を design 解釈で消さない。

## 描画の積層 (ゲーム画面)

```
1. 動画 (VideoPlayer が描く)
2. Video Dim #00000080   → Canvas "V6GameDimCanvas"
3. プレイフィールド (レーン線 / ノーツ / 判定ライン) → Canvas "V6GamePlayfieldCanvas"
4. HUD (グレード / スコア / コンボ / キーガイド / 歌詞) → Canvas "V6GameHudCanvas"
```

HUD 内の "Video Dim" は無効化されている (順序が崩れるため)。減光は 2 番の Canvas が出す。

## データの流れ (実データから表示)

| 表示 | データ源 |
|---|---|
| グレード / スコア / コンボ / 判定 | `GManager.score` / `combo` / `perfect` / `great` / `bad` / `miss` |
| ゲージ / 進捗バー | `GManager.score` と `GManager.step_time` (メディア時刻) |
| レーン別判定行 | `Judge` が Instantiate した判定プレハブを走査してレーンと表示を対応付け |
| キーガイド (8連) | `KeyBindings.GetKeys(lane)`。`KeyBindings.Changed` を購読するのでキー設定に即追従 |
| 歌詞 | `LyricsTimeline.Build(...)` + `GManager.step_time` (動画と同じタイムライン) |
| 曲リスト / 選択中の曲 | `V6SelectDataSource` (GManager の実カタログ + CloudCatalog + 実譜面 JSON) |
| ベストスコア | `GManager.BestScoreOf(songId)` (stable songId + 譜面版, Issue #189) |
| ランキング | `RankingClient` (オンライン。取得失敗時は空リスト + エラー表示) |
| リザルト | `GManager` のプレイセッション実値 (スコア / コンボ / 判定数) |
| ジャケット | `Assets/Game/Resources/V6/select/jacket/` (Pencil 設計の正式画像 26 枚。無い曲だけ空枠) |

固定データ (样本曲名・"988,513" 等の仮スコア) は表示しない。

## design からの意図的な差異

- **歌詞枠の位置**: design (`HcP8N`) は画面中央に置くが、v6 プレイフィールドが画面中央を
  使うため中央寄りではなく上端寄りに置く (進捗バーの直下)。寸法・書体は design どおり
  (1120×220 / r8 / Furigana fs22 / Lyrics fs44 / Romaji fs20 / 縦 gap 6 / padding [16,24])。
- **判定行の列位置**: design の判定サンプル (Uc9v6: x=623/1155) はキーガイドの列より
  1 レーン左にずれており、キー入力との対応が読めない。レーンと判定の対応を読めることを
  優先し、実装はキーガイドの列 (`V6HudMetrics.KeyColumnCenter`) に揃えている。

## 検証

- EditMode テスト: `Assets/Game/Tests/V6*Tests.cs` (純ロジック) と `Assets/Tests/Editor/`。
- 実機 (macOS ビルド) での通し確認:

  ```bash
  # ビルド + 配置 (レビュー用)
  script/deploy-mac-desktop.sh integration/v6-followup

  # ビルドのみ
  Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneMacOS
  # v6 の 選曲→Ready→ゲーム を実データで検証 (スクリーンショット付き)
  open -n Builds/macOS/FlashBeat.app --args -autotest v6 -autotest-out /tmp/v6-verify
  ```

  検証スクリプト: `Assets/Game/Scripts/Common/AutoTestV6.cs`
- 画面キャプチャ (シーン単位): `-autoshot <dir> -autoshot-scenes SelectScene,GameScene`

### ゲートは独立

コンパイル / EditMode テスト / ビルド / ゲーム動作 / デザイン受入は **別のゲート**。
テストが通っても実機受入が通ったことにはならず、実機が未検証なら未検証と報告する。
