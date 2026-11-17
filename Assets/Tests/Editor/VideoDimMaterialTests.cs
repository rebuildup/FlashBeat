using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #575: GameScene の "kuro" quad が使う減光マテリアル
    /// (BlackSemiTransparent.mat) のアルファを固定するテスト。
    ///
    /// このマテリアルは動画全面を覆う黒板で、v5 / v6 共通の基礎減光。
    /// 従来は α≈0.55 で動画がかなり暗く、映像の明瞭さを損なっていた。
    /// α を下げてもノーツ / HUD の視認性は上部シェード・レーン線・
    /// フォント縁取り等の既存要素が担保するため、板自体は薄く保つ。
    /// </summary>
    public class VideoDimMaterialTests
    {
        private const string MaterialPath =
            "Assets/Game/Art/Materials/BlackSemiTransparent.mat";

        [Test]
        public void BlackDimMaterial_StaysClearlyThinnerThanTheLegacyHalfBlack()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.NotNull(mat, MaterialPath + " が見つからない");

            // 旧値 0.549 (ほぼ半分の黒) から十分に薄いことを保証する。
            // 上限 0.35: v6 では HUD 側の Video Dim (α≈0.5) がさらに乗るため、
            // ここを上げると動画の実効減光が再び強くなりすぎる。
            Assert.Greater(mat.color.a, 0f, "減光が無いと動画が白飛びして HUD が読めない");
            Assert.LessOrEqual(mat.color.a, 0.35f,
                "kuro quad の α が 0.35 を超えると動画の減光が強すぎる (Issue #575)");
        }

        [Test]
        public void BlackDimMaterial_IsStillTransparentBlack()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.NotNull(mat, MaterialPath + " が見つからない");

            // 色味は黒のまま (減光専用)。アルファだけを調整する。
            Assert.AreEqual(0f, mat.color.r, 0.001f);
            Assert.AreEqual(0f, mat.color.g, 0.001f);
            Assert.AreEqual(0f, mat.color.b, 0.001f);
        }
    }
}
