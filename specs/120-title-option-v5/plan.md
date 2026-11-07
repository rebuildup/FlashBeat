# Plan: 120 — v5 Title / Opening / Option

## 方針

- sibling 実装 `ResultView` (origin/123) と同じパターン: 静的 `Build()` ファクトリが
  新規 Canvas (ScreenSpaceOverlay, `CanvasConfigurator.Configure`) を作り、
  View MonoBehaviour が Update で入力を処理して既存 Manager の public メソッドを
  コールバック呼び出し。レガシー Canvas は呼び出し側 (Manager.Start) が非アクティブ化。
- 寸法は fhd 実測値 (pen CLI) を `S(px) = px * (UISizeProfile.Current.FsM / 30f)` で
  プロファイル倍率に合わせる。色は `V5Tokens`。
- 画像は `Resources.Load<Texture2D>("V5/Common/...")` + `Sprite.Create`。
  ボタン/行の背景は画像を伸縮せず `V5Ui.RoundedSprite` (9-slice) を使用。
- 共有デコは `V5Decor` に集約 (他ワーカーが `V5/Common` を参照する場合の経路)。

## リスク

- lucide アイコン (雲・ギア・chevron 等) はフォントアトラスに無い → アイコンは省略し
  テキストで代替 (報告事項)。
- `KeyConfigUI.Open()` は未マージ (origin/109 は `Toggle()` のみ) →
  リフレクションで Open() を優先、無ければ Toggle()。コンパイルは常に通る。
- NotoSansJP SDF に無い字形 (例: 済) は使わない。使用する日本語は
  既存 UI でも使われている語彙に限定。

## 順序

1. 資産コピー (V5/Common) + V5Decor
2. TitleView + TitleSceneManager 統合
3. OptionView + OptionSceneManager 統合
4. OpeningView + OpeningSceneManager 統合
5. テスト + 全ファイル再読チェック (コンパイル目視) + コミット/Push/PR
