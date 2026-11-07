# tasks: プレイフィールドのフラット描画化と判定エフェクトのレーン修正 (#168)

## 描画側 (`feature/flat-rendering`)

- [x] 1. `Assets/Game/Art/Shaders/FlatUnlit.shader` を新規追加 (unlit / `_MainTex` + `_Color` /
      ブレンド・ZWrite・Cull はマテリアル側プロパティ)
- [x] 2. `Assets/Game/Art/Materials/VideoPicture.mat` を新規追加 (FlatUnlit / `_MainTex` を書く先)
- [x] 3. 8 つのプレイフィールド素材の `m_Shader` を FlatUnlit に差し替え
      (`_Color` / `_MainTex` は原本と同一、不透明は queue 2000、半透明は queue 3000 + ZWrite Off)
- [x] 4. 動画 quad (VideoPlayer を持つ MeshRenderer) 29 個を `VideoPicture.mat` へ付け替え、
      `VideoPlayer.targetMaterialProperty` を `_MainTex` に設定
- [x] 5. 全シーン 8 + Legacy 1 / 全プレハブ 17 で `Light.m_Shadows.m_Type: 2 → 0`、
      `MeshRenderer.m_CastShadows/m_ReceiveShadows: 1 → 0`
- [x] 6. `Assets/Editor/FlatRenderCheck.cs` (batchmode 監査 + ピクセル計測) を追加
- [x] 7. `docs/architecture/flat-rendering.md` を追加

## 判定側 (`feature/judge-effect-lane`)

- [x] 8. `Judge.message(judge, noteIndex)` + 公開静的 `Judge.EffectPosition(laneNum, noteIndex)`
- [x] 9. Perfect / Great / Bad は判定ノートの index を渡す (削除前にレーンを読む)、
      Miss は index 0 にフォールバック
- [x] 10. `Assets/Tests/Editor/JudgeEffectPlacementTests.cs` を追加 (4 テスト)

## 統合と検証 (supervisor)

- [x] 11. 2 つの worker branch を `--no-ff` 統合 (`a107364`, `d72c148`)
- [x] 12. EditMode テスト green を確認 (407/407、ベースライン 403/403)
- [x] 13. `FlatRenderCheck` 再実行: `audit: lights=0 casters=0 receivers=0`、
      動画マテリアル `RGBA(0.502,…)` (50% 灰色 = 128)、ノーツ `RGBA(1.000,…)`
- [x] 14. before/after のカメラ画像を比較 (`origin/main` = くすんだ beige → 本線 = 白)
- [x] 15. `AGENTS.md` にフラット描画の規約を追記
- [x] 16. specs/168 の spec / plan / tasks を追加
- [x] 17. push + Draft PR + main へ統合
- [x] 18. Win64 ビルド (`FLASHBEAT_VERSION_BASE=0.1.14`)
- [x] 19. パッケージ (ZIP + SHA-256、`-MediaPolicy song-free`) と GitHub Release 公開
- [x] 20. `docs/operations/update-release.md` の現行版更新 + `bundleVersion` stamp
- [x] 21. Issue #168 を検証証跡付きでクローズ
