# #224 差し替え / 切り戻し手順 (後方互換)

Issue #224 の受入条件 5「P0 owner へ後方互換性のある差替手順と PR を提出」に対応する。
v6 の Pause / Loading を入れても、**v5 の画面と挙動は消していない**。切り替えは
コード上のフラグ 2 つだけで、シーン / プレハブ / アセットの編集は要らない。

## 1. 所有者を決める場所 (単一の判定点)

| 画面 | 判定 | 既定 | 判定を読む側 |
|---|---|---|---|
| Pause | `V6UiMode.GameHud && V6PauseView.Enabled` | `true` | `GameSceneManager.cs:308-312`, `V6GameRuntime.cs:78,110-111`, `V6PauseRuntime.cs:52`, `V6GameHudView.cs:569`, `GameHudRuntime.cs:162` (`GameUiExclusivity.PauseEnabled`) |
| Loading (GameScene の曲準備) | `GameLoadingScreen.ForGameScene(V6UiMode.GameHud)` | `true` → v6 | `LoadingOverlay.cs:24-36` (`LoadingOverlay.Current` / `UsesLegacyOverlay`)、`MusicManager` / `NotesManager` は `UsesLegacyOverlay` のときだけ旧表示を出す |
| 時間・音声の停止 (PauseGate) | **判定なし (常時有効)** | — | `MusicManager` / `Judge` / `Notes` の `Update` |

`V6UiMode.GameHud` が `true` のときは `V6GameRuntime` が `V6GameUi` を 1 つだけ作り、
その中で Pause と Loading の Canvas を構築する (`V6GameRuntime.cs:74-92, 306-316`)。
旧 UI 側は毎フレーム非表示にされる (`V6GameRuntime.HideLegacyUi`) ので、**二重表示は起きない**。

## 2. 差し替え (v5 → v6)

既定で v6 が有効。追加作業は無い。

```
V6UiMode.GameHud = true;      // Assets/Game/Scripts/UI/V6/V6UiMode.cs
V6PauseView.Enabled = true;   // Assets/Game/Scripts/UI/V6/Game/Pause/V6PauseView.cs:39
```

## 3. 切り戻し (v6 → v5)

Pause だけ / Loading ごと、の 2 通り。**どちらも 1 行の変更で戻る。**

| 戻したい範囲 | 変更 | 結果 |
|---|---|---|
| v6 Pause だけ戻す | `V6PauseView.Enabled = false` | Esc / メニューボタンは v5 の `PauseMenuView` が受ける。**PauseGate は共通なので、時間と音声が止まる挙動は v6 と同じ** |
| Game 画面全体 (Pause + Loading + HUD) を戻す | `V6UiMode.GameHud = false` | `V6GameRuntime` が作られない。v5 の `GameHudRuntime` / `PauseMenuView` / `LoadingOverlay` / `MediaLoadingOverlay` がそのまま動く。PauseGate は引き続き有効 |

切り戻しで壊れないことの根拠:

- `PauseGate` (`Assets/Game/Scripts/Gameplay/PauseGate.cs`) は v5 / v6 の両方が
  `Enter()` / `Exit()` を呼ぶ共通部品で、どちらのモードでも `MusicManager` / `Judge` / `Notes` が
  `IsPaused` を見る。**UI を戻しても「ポーズで時計と音声が止まる」は残る**。
- v5 経路は `MediaLoadingOverlay` / `LoadingOverlay` / `PauseMenuView` として main に残置。
  削除も無効化もしていない (`GameUiExclusivity.PauseEnabled` に現在値を渡すだけ)。
- シーン (`GameScene.unity`) とプレハブは編集していない。フラグは実行時の静的フィールドのみ。

## 4. 提出済みの PR

| コミット / PR | 内容 |
|---|---|
| `ddc25eb` | PauseGate 新設 + v5 / v6 双方の Pause からの呼び出し + キー凡例の真実化 |
| `5275fcd` | v6 Pause / Loading の固定フレーム配置 |
| `e53fd24` → `76394d5` | Loading の実進捗・ジャケット・再試行・動画表示 |
| `168789a` | ローディング映像のテクスチャ実接続 |
| PR #309 | 統合時の EditMode 実測 (1322 / 1322 passed) を `tasks.md` に記録 |

## 5. この手順で言えないこと

- **実機での見た目・操作感**(受入条件 4 の画像比較 / 6 の実 Win64 操作)は未検証。
  本ドキュメントは「後方互換に切り替えられること」の手順であって、実機合格の証拠ではない。
- フラグは `PlayerPrefs` 化していない (実行時の静的フィールド)。展示中に切り替えるには
  ビルドし直す。設定画面から選ばせる要件は本 Issue に無い。
