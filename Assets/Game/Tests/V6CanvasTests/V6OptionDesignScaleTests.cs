using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Option;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// v6 画面ごとのスケール方式の統一 (純ロジック)。
    ///
    /// v6 の全画面は design 上の共通フレーム `Frame/Shared 1728x972` を ref 共有している
    /// (契約 docs/design/v6/flashbeat-v6-contract.md §2 のノード表)。
    /// 同じフレームの描画倍率 (design px → 実 px) が画面ごとに違うと、
    /// Select / History / Ranking / Result / Title から Option へ連続して遷移したときに
    /// 視覚的に破綻する。
    ///
    /// 実測で Option だけ縦長画面を高さ基準 + 1 クランプで縮小していたため
    /// (2560x1664 で Option 0.584 に対し他画面 1.4815)、
    /// Option も他画面と同じ min(W/1728, H/972) に統一した。
    ///
    /// ゲーム HUD (min(W/1920, H/1080)) は HUD 専用設計値 (契約 §8) なので本テストの対象外。
    /// </summary>
    public class V6OptionDesignScaleTests
    {
        private static readonly int[][] RealResolutions =
        {
            new[] { 1920, 1080 },   // FHD
            new[] { 2560, 1440 },   // 2K
            new[] { 2560, 1664 },   // MacBook Air M5
            new[] { 1920, 1200 },   // 16:10
            new[] { 1728, 972 },    // design フレーム自体
            new[] { 1280, 720 },    // 小さめ
        };

        [Test]
        public void OptionScale_EqualsSelectScale_AtRealResolutions()
        {
            for (int i = 0; i < RealResolutions.Length; i++)
            {
                int w = RealResolutions[i][0];
                int h = RealResolutions[i][1];
                Assert.AreEqual(V6SelectViewLogic.DesignScale(w, h), V6OptionLogic.DesignScale(w, h), 0.0001f,
                    $"{w}x{h}: Option のスケールが Select と一致しない");
            }
        }

        [Test]
        public void OptionScale_UsesSharedFrameFormula_AtRealResolutions()
        {
            // v6 共通フレーム (1728x972) の式 = min(W/1728, H/972)。
            // History / Ranking / Pause / Loading は V6SelectViewLogic.DesignScale を、
            // Result / Title は同式を自前で持つため、ここでは式そのものを固定する。
            for (int i = 0; i < RealResolutions.Length; i++)
            {
                int w = RealResolutions[i][0];
                int h = RealResolutions[i][1];
                float expected = Mathf.Min((float)w / 1728f, (float)h / 972f);
                Assert.AreEqual(expected, V6OptionLogic.DesignScale(w, h), 0.0001f,
                    $"{w}x{h}: Option が共通フレームの式から外れている");
            }
        }

        [Test]
        public void OptionScale_UsesSharedFrameFormula()
        {
            // 共通式 = min(W/1728, H/972) (アップスケール可)。
            Assert.AreEqual(1f, V6OptionLogic.DesignScale(1728, 972), 0.0001f);
            Assert.AreEqual(1920f / 1728f, V6OptionLogic.DesignScale(1920, 1080), 0.0001f);
            Assert.AreEqual(2560f / 1728f, V6OptionLogic.DesignScale(2560, 1440), 0.0001f);
            Assert.AreEqual(2560f / 1728f, V6OptionLogic.DesignScale(2560, 1664), 0.0001f);
            // 異常系は 1 にフォールバック
            Assert.AreEqual(1f, V6OptionLogic.DesignScale(0, 0), 0.0001f);
        }

        [Test]
        public void SharedFrameSize_IsSameForOptionAndSelect()
        {
            Assert.AreEqual(V6SelectViewLogic.DesignW, V6OptionLogic.DesignW, 0.0001f);
            Assert.AreEqual(V6SelectViewLogic.DesignH, V6OptionLogic.DesignH, 0.0001f);
        }
    }
}
