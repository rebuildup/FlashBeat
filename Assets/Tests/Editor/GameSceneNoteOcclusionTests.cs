using System.Collections.Generic;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #735: GameScene のワールド空間オブジェクトがノーツを暗くしない
    /// ことを、シーン実体のジオメトリで固定する回帰テスト。
    ///
    /// 背景: ノーツ (NotePlaneY = 0.55 の薄いタイル) は不透明・unlit で
    /// 白く描かれるが、その「上」に乗る半透明の暗い板があると
    /// (SrcAlpha ブレンドで黒を重ねると) ノーツは暗くなる。
    /// 既知の減光板は "kuro" (BlackSemiTransparent.mat, 黒 α0.3, queue 3000)
    /// で、映像スクリーン面と同じ傾斜平面上にあり、ノーツ面より常に奥
    /// (画面下側) にあるため深度テストでノーツの上には乗らない。
    ///
    /// この不変条件は「暗い半透明レンダラのバウンディングボックスが
    /// ノーツの占める領域と交差しない」ことで保証できる。
    /// ここでは GameScene を additive で開き、有効なレンダラのうち
    /// 「queue ≥ 3000 (透明) かつ暗い色 (輝度 &lt; 0.5) かつ α &gt; 0」の
    /// ものがノーツ領域と交差しないことを検査する。
    /// 白い半透明 (LaneLight の点灯、レーンガイド) は明るくする方向なので
    /// 対象外。Lane (暗い板) はシーンで非アクティブなので対象外。
    /// </summary>
    public class GameSceneNoteOcclusionTests
    {
        private const string ScenePath = "Assets/Game/Scenes/GameScene.unity";

        /// <summary>「暗い半透明」とみなす色の輝度上限 (sRGB 0..1)。</summary>
        private const float DarkLuminance = 0.5f;

        /// <summary>透明パスとみなすレンダーキューの下限。</summary>
        private const int TransparentQueue = (int)RenderQueue.Transparent;

        [Test]
        public void DarkTransparentRenderers_DoNotIntersectNoteVolume()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                // ノーツが占める領域: y = NotePlaneY の薄いタイルが
                // z ∈ [0, VisibleDepthZ]・全レーン分を流れる。
                // レーン x 範囲は Judge.LaneWorldX の両端 ± ノーツ半幅で覆う。
                var noteVolume = new Bounds(
                    new Vector3(0f, MediaTimeline.NotePlaneY, MediaTimeline.VisibleDepthZ * 0.5f),
                    new Vector3(12f, 0.08f, MediaTimeline.VisibleDepthZ + 2f));

                var offenders = new List<string>();
                int checkedCount = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!renderer.gameObject.activeInHierarchy) continue;
                        if (!IsDarkTransparent(renderer.sharedMaterial)) continue;

                        checkedCount++;
                        if (renderer.bounds.Intersects(noteVolume))
                        {
                            offenders.Add(
                                $"{renderer.name} (mat={renderer.sharedMaterial.name}, " +
                                $"bounds={renderer.bounds}, pos={renderer.transform.position})");
                        }
                    }
                }

                Assert.Greater(checkedCount, 0,
                    "暗い半透明レンダラが 1 つも見つからない — 監査自体が空振りしている " +
                    "(kuro quad が消えたか、判定条件が壊れている)");
                Assert.IsEmpty(offenders,
                    "ノーツ領域と交差する暗い半透明レンダラがある — " +
                    "ノーツを暗くする遮蔽板になりうる:\n" + string.Join("\n", offenders));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// 「ノーツを暗くしうる半透明板」かどうか:
        /// 透明キュー (≥3000) で、色が暗く (輝度 &lt; 0.5)、アルファが乗っている。
        /// 白い半透明 (発光系) は除外する。
        /// </summary>
        private static bool IsDarkTransparent(Material material)
        {
            if (material == null) return false;
            if (material.renderQueue < TransparentQueue) return false;
            if (!material.HasProperty("_Color")) return false;

            Color c = material.color;
            if (c.a <= 0f) return false;

            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return lum < DarkLuminance;
        }
    }
}
