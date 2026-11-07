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
| 平行光源 (全シーン / `DirectionalLight.prefab`) | `Light.shadows = None` |
| 全 Renderer (全シーン / 全プレハブ) | `shadowCastingMode = Off` / `receiveShadows = false` |
| 8 つのプレイフィールド素材 | `Standard` → `FlashBeat/FlatUnlit` (新規シェーダー) |
| 動画 quad (VideoPlayer を持つ MeshRenderer) | 新規 `VideoPicture.mat` |

`FlashBeat/FlatUnlit` (`Assets/Game/Art/Shaders/FlatUnlit.shader`) は
`_MainTex` と `_Color` を持つ unlit シェーダーで、ブレンド状態と ZWrite は
マテリアル側のプロパティで決まる。`LaneFlash` / `BGFlash` が実行時に
`material.color` を操作する挙動もそのまま有効。

- 不透明 (Note / JudgeLine / Background / Particle): queue Geometry, ZWrite On
- 半透明 (Lane / LaneLight / BlackSemiTransparent / GameBackground): queue 3000, ZWrite Off
- 動画 (`VideoPicture.mat`): queue Geometry, ZWrite On。`VideoPlayer` が
  `targetMaterialProperty` (`_MainTex`) に書き込む動画テクスチャをそのまま表示する

## 検証

`Assets/Editor/FlatRenderCheck.cs` を batchmode で実行すると、

1. 全シーン / 全プレハブに残った shadow 設定 (light / caster / receiver) を監査し、
2. GameScene のカメラで **ピクセル値** を読む (unlit なら materialize した色が
   そのまま出るはず。50% 灰色 = 128、白 = 255)、
3. カメラ画像と probe のスクリーンショットを PNG で書き出す。

が一度に確認できる。
