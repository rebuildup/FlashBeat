# Plan: 121 — Select v5

## 構成

- `SelectView` (MonoBehaviour, namespace FlashBeat.UI.V5) が自前 Canvas
  (`CanvasConfigurator.Configure`, Constant Pixel Size x1) 上に全 UI を構築。
  `SelectView.Create(SelectSceneManager)` で生成し、レガシー `Canvas` は非表示化。
- 寸法: fhd 実ピクセル基準のデザイン値を `u = profile.Margin / 48f` 倍して使用
  (2k=4/3, mac=3/4, 小画面=Scale)。フォント/行は既存 profile 値 (FsM 等) を優先。
- SelectSceneManager はデータと遷移の所有者: カタログ更新コルーチン、
  `GameScene()` (リモート譜面込み) 、`TitleScene()`、`playSound()`。
  Update は `view.HandleInput()` に一本化。
- コールバック: view → manager (`Play`, `ToTitle`, `Sound`)。

## リスク

- LFS 画像は実体なし → ジャケット実物はデザイン asset から実曲一致分のみ
  同梱、残りは YouTube サムネ or 無地タイル。
- `▷` は NotoSansJP SDF charset に無い → ボタンラベルは文字のみ。
- 履歴の検証は mac-verify (Supervisor) 依存。本環境では Unity compile 不可 →
  構文/参照を手 re-read と pure-logic テストで担保。
