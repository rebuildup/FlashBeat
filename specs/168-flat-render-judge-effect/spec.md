# spec: プレイフィールドのフラット描画化と判定エフェクトのレーン修正 (#168)

## 目的

展示ライン (`main`) で報告されている 2 つの表示不具合を直す。

1. **プレイ中の動画とノーツがくすむ** — Standard シェーダー (ライティングあり) と平行光源の組み合わせが原因。
   - 動画 quad の法線が平行光源 (euler 50,-30,0) の**裏側**を向いており直接光に当たらないため、
     環境光 (約 0.21) だけで描画され灰色に潰れている。
   - ノーツはレーンライトの帯 (y = 7.67) が落とす影で横縞の暗部ができている。
2. **判定エフェクト (PERFECT/GREAT/BAD/MISS) が常にレーン 0 に表示される** —
   `Judge.message()` が `notesManager.LaneNum[0]` (最初の未判定ノート) の位置を無条件に使っているため、
   同一タイムスタンプの 2 ノーツを同時叩いたときに**踏んだレーンと異なるレーン**にエフェクトが出る。

## 対処

| 対象 | 変更 |
|---|---|
| 平行光源 (全シーン / `DirectionalLight.prefab`) | `Light.shadows = None` |
| 全 Renderer (全シーン / 全プレハブ) | `shadowCastingMode = Off` / `receiveShadows = false` |
| 8 つのプレイフィールド素材 | `Standard` → `FlashBeat/FlatUnlit` (新規シェーダー) |
| 動画 quad (VideoPlayer を持つ MeshRenderer) | 新規 `VideoPicture.mat` (`_MainTex` を `targetMaterialProperty` に書く) |
| `Judge.message()` | 判定したノートのレーン (`LaneNum[noteIndex]`) に表示。Miss は先頭ノートへフォールバック |

`FlashBeat/FlatUnlit` は unlit シェーダーで、色はマテリアルの定義色がそのまま描画される (フラット)。
`LaneFlash` / `BGFlash` が実行時に `material.color` を操作する挙動もそのまま有効。

- 不透明 (Note / JudgeLine / Background / Particle / VideoPicture): queue Geometry, ZWrite On
- 半透明 (Lane / LaneLight / BlackSemiTransparent / GameBackground): queue 3000, ZWrite Off

## 検証

- `Assets/Editor/FlatRenderCheck.cs` (batchmode): 全シーン/全プレハブに残った shadow 設定を監査し、
  GameScene のカメラでピクセル値を読む (unlit なら 50% 灰色 = 128)。
- `Assets/Tests/Editor/JudgeEffectPlacementTests.cs`: 同時押しで 2 つの異なるレーンへ出ること、
  Miss のフォールバック、範囲外インデックスで例外が出ないこと。

## 完了条件

- Unity EditMode テスト green
- Win64 ビルド + ZIP + SHA-256 を GitHub Release として公開 ( Issue #22 のリリースゲートに従う )
