# Plan: 123 — v5 Result / Result-Register

## アプローチ

ランタイムコードビルド (Issue #106 基盤の上)。シーン YAML は編集しない。

- `ResultView` (新規, `FlashBeat.UI.V5`) が自前 Canvas (`CanvasConfigurator.Configure`,
  sortingOrder 500) を作り、レガシー `Canvas` を `SetActive(false)` で隠す。
  既存 SceneManager の公開メソッド (`Retry`, `SelectScene`) をコールバックで再利用。
- 寸法: `UISizeProfile.Current` のフィールド (Margin/Fs*/Gap*) + デザイン実測値は
  `S(px) = px * FsM/30` でプロファイル倍率に合わせて縮小 (2k=1.33, mac=0.87, fhd*0.67=0.67)。
  全ノードはエッジ相対アンカー。TMP は省略なし (auto-size は使わず固定 fs + ellipsis)。
- 画像: ジャケット帯は既存 `FetchThumbnail` の YouTube サムネテクスチャを View に渡す
  (RawImage)。9-slice は `V5Ui.RoundedSprite`。design/assets の画像は本画面では不要。
- 登録: ローカルのみ。登録名は PlayerPrefs `V5PlayerName` (A-Z0-9, max 8)。
  ハイスコア本体は既存経路 (`SaveLoadManager` が ResultScene Start で PlayerPrefs 保存済み)。
- 入力: View の `Update` で処理し、`ResultSceneManager.Update` は View がある間入力をskip
  (二重処理防止)。モーダル中はショートカットをモーダル側に切替。

## リスク

- フォントアトラスに無い字形 (例: 済) は使わない。使用字: ランキングに登録/未登録/決定/曲別/スコア。
- アイコン (trophy/cloud/user) はフォントに無いため文字ラベルで代替。
- Unity Editor 不可のためコンパイル検証は Supervisor の mac-verify に依存 (目視リレビューで担保)。
