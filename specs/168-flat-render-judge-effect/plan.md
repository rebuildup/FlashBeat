# plan: プレイフィールドのフラット描画化と判定エフェクトのレーン修正 (#168)

## アプローチ

作業量とファイル所有が完全に独立した 2 領域に分ける。どちらも **一-worker-一-worktree** で
並列実装し、supervisor が検証して統合する。

| 領域 | branch | worktree | 担当ファイル |
|---|---|---|---|
| 描画 (フラット化) | `feature/flat-rendering` | `FlashBeat.flat` | `Assets/Game/Art/**`, `Assets/Game/Prefabs/**`, `Assets/Game/Scenes/**`, `Assets/Editor/FlatRenderCheck.cs` |
| 判定 (レーン修正) | `feature/judge-effect-lane` | `FlashBeat.judge` | `Assets/Game/Scripts/Gameplay/Judge.cs`, `Assets/Tests/Editor/JudgeEffectPlacementTests.cs` |

どちらも `origin/main` (`5067a1e`) から起こす。

## 描画側の方針

1. `FlashBeat/FlatUnlit` シェーダーを新規追加 (`Lighting Off` / `_MainTex` + `_Color` /
   ブレンド・ZWrite・Cull はマテリアル側の `[Enum]` プロパティで決まる)。
2. 8 つのプレイフィールド素材 (Background / BlackSemiTransparent / GameBackground /
   JudgeLine / Lane / LaneLight / Note / Particle) の `m_Shader` を差し替え。
   **`_Color` と `_MainTex` の値は 1 バイトも変えない** (元の Standard と同じ見た目を保つため)。
3. `VideoPicture.mat` を新規追加し、`VideoPlayer` を持つ GameObject の MeshRenderer が
   これを参照するようにする。`VideoPlayer.targetMaterialProperty` は `_MainTex` を指す。
   (`renderMode = 3` = Material の 29 quad のみ。`renderMode = 2` = External の
   `loadingmovie` は対象外。)
4. 全シーン / 全プレハブで `Light.m_Shadows.m_Type: 2 → 0`、
   `MeshRenderer.m_CastShadows: 1 → 0` / `m_ReceiveShadows: 1 → 0`。
   **`Assets/Game/Scenes/Legacy/tttt.unity` も含め 10 灯すべて**を対象にする。
5. 検証は `FlatRenderCheck` (batchmode) の**ピクセル計測**で行う
   (スクリーンショットの見栄えではなく数値で判定する)。

## 判定側の方針

1. `Judge.message()` に `noteIndex` 引数を追加し、`EffectPosition(laneNum, noteIndex)` を公開静的メソッドに切り出す。
2. Perfect / Great / Bad は判定したノートの index (`numOffset`) を渡し、
   **削除 (`deleteData(numOffset)`) より前**に `LaneNum[numOffset]` を読む。
3. Miss は判定ノートが無いので先頭 (index 0) にフォールバック。
4. レーン座標はノーツと同じ映射 (`block - 3.5`) を使う。

## 統合

1. 2 つの worker branch を `feature/flat-render-judge-effect` に `--no-ff` 統合する。
2. 統合後に EditMode テストを 1 回走らせ、407/407 green を確認する
   (ベースライン `origin/main` は 403/403)。
3. `FlatRenderCheck` を統合ツリーで再実行し、audit / ピクセル計測が green であることを確認する。

## リスク

- Unity が:mats と `ProjectSettings.asset` をエディタ起動時に書き換える (BOM 消失・`m_AllowLocking` 追加)。
  → コミット前に `git checkout` で戻す。BOM は `head -c3 | xxd -p` で `efbbbf` を確認。
- `_Color` の比較で `_EmissionColor` を誤って拾う (素材が `_EmissionColor` を先に持つため)。
  → `^\s+- _Color:` で限定して比較する。
