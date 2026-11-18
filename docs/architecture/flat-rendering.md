# フラットレンダリング (flat rendering)

## 何が問題だったか

プレイ中の動画とノーツが「くすんで」いた。原因は 3D オブジェクトの影ではなく、
Standard シェーダー (ライティングあり) と平行光源の組み合わせだった。

- **動画**: 絵柄は camera に向いた少し傾いた quad 上に載っているが、その法線は
  平行光源 (euler 50, -30, 0) の**裏側**を向いていた。画面に見えている面に直接光が
  当たらないので、環境光 (約 0.21) だけで描画され、灰色に潰れていた。
  UI のアルバム画像 (RawImage) は影響を受けないため、暗い動画との差がはっきり見えた。
- **ノーツ**: レーンライトの帯 (y = 7.67 の薄い板 8 本) が平行光源の影を落とし、
  ノーツの上に横縞の暗部を作っていた。加えて Standard の陰影で白色が灰色寄りになっていた。

## 対処

プレイフィールドを「光の影響を受けない素材」に統一し、影を全面的に止める。
色はそのマテリアルが定義する色がそのまま描画される (フラット) になる。

| 対象 | 変更 |
|---|---|
| 平行光源 (全シーン) | `Light.shadows = None` |
| 全 Renderer (全シーン / 全プレハブ) | `shadowCastingMode = Off` / `receiveShadows = false` |
| 8 つのプレイフィールド素材 | `Standard` → `FlashBeat/FlatUnlit` (新規シェーダー) |
| 動画 quad (VideoPlayer を持つ MeshRenderer) | 新規 `VideoPicture.mat` |

`GameBackground.mat` (黒・不透明扱いの半透明マテリアル) はどこからも
参照されていなかったため削除した (Issue #608)。有効化されると
queue 3000 でノーツより後に描かれ、プレイフィールドを黒く沈める
危険な素材だった。

`FlashBeat/FlatUnlit` (`Assets/Game/Art/Shaders/FlatUnlit.shader`) は
`_MainTex` と `_Color` を持つ unlit シェーダーで、ブレンド状態と ZWrite は
マテリアル側のプロパティで決まる。`LaneFlash` / `BGFlash` が実行時に
`material.color` を操作する挙動もそのまま有効。

- 不透明 (Note / JudgeLine / Background / Particle / VideoPicture): queue Geometry (2000), ZWrite On
- 半透明 (Lane / LaneLight / BlackSemiTransparent / 実行時ガイド線): queue 3000, ZWrite Off
- 動画 (`VideoPicture.mat`): queue Geometry, ZWrite On。`VideoPlayer` が
  `targetMaterialProperty` (`_MainTex`) に書き込む動画テクスチャをそのまま表示する

### 描画順 (Issue #608 で明文化)

ノーツがレーン・ガイド・フラッシュ・動画の「上」に常に描かれることを、
キューと深度の 2 段で保証する。

1. **不透明パス (queue 2000)**: 動画 quad・ノーツ・判定線が ZWrite On で
   先に描かれ、深度バッファに自分の位置を書く。
2. **透過パス (queue 3000)**: レーン・レーンライトのフラッシュ・
   ガイド線・動画減光 (`kuro`) が ZWrite Off・深度テストありで後から
   描かれる。ノーツより奥にあるフラグメントは深度テストで捨てられるので、
   ノーツの上に重なって減光することはない。逆にノーツより手前にある
   透過物だけがノーツに重なる (現状そのような配置は無い)。
3. 透過同士はカメラからの距離で奥→手前にソートされる
   (遠い動画減光 → 近いレーンフラッシュの順)。

設定画面の Game Preview (`V6OptionPreviewRig`) も同じ規則に従う:
ノーツは本編と同じ `Note.prefab` / `Note.mat` (queue 2000 不透明)、
ガイド線・判定線は `PlayfieldLaneGuides.CreateGuideMaterial()`
(queue 3000 透過) を使う。flat rendering ではライティングを使わない
ため、プレビューリグは Directional Light を持たない (Issue #608 で削除)。

### uGUI overlay は深度保証の対象外 (Issue #675)

上の深度保証は 3D レンダラ (カメラの描画) の中の話であり、
Screen Space Overlay の Canvas はその**後**に必ず全面へ描かれる。
深度バッファを通らないので、overlay 側の半透明 Image は
ノーツやプレイフィールドの上にそのまま重なる。

実害があったのが HUD の **Top Shade** (`V6HudChrome.BuildTopShade`)。
画面上端から約 260 px (FHD) の黒グラデーション (上端 α≈0.65) が
アプローチ中のノーツを暗くし、「ノーツが明るい白に見えない」
状態になっていた (マテリアル・シェーダー・色空間は無実 —
`FlatRenderCheck` がノーツ白 ≥0.95 を assert 済み)。

対処: シェードのテクスチャに**レーン帯の抜き**を焼き込む
(`V6TopShadeHole`)。抜きの形はノーツが実際に通る領域
(レーン境界が作る帯 + 余白) で、3D プレイフィールドでは
GameScene カメラでレーン面との交点を各行解いて投影し、
v6 プレイフィールド / カメラ不在時は `V6LaneGeometry` の
設計座標写像で近似する。シェード本体 (HUD 文字の可読性のための
上部の暗がり) は帯の外側で従来どおり残る。

**今後 uGUI overlay に「プレイフィールドより奥に見せたい」
半透明要素を足すときは、この隙間に注意すること。** overlay は
深度で負けられないので、(a) 形を抜く (今回の方式)、(b) overlay
側でプレイフィールドをなぞった位置に描かない、のどちらかを取る。

## 検証

`Assets/Editor/FlatRenderCheck.cs` を batchmode で実行すると、

1. 全シーン / 全プレハブに残った shadow 設定 (light / caster / receiver) を監査し、
2. GameScene のカメラで **ピクセル値** を読む (unlit なら materialize した色が
   そのまま出るはず。50% 灰色 = 128、白 = 255)、
3. カメラ画像と probe のスクリーンショットを PNG で書き出す。

が一度に確認できる。
