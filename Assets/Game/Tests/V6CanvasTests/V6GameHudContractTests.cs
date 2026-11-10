using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Game.Hud;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// v6 Game HUD の契約 (docs/design/v6/flashbeat-v6-contract.md §8 / §12) の
    /// 純ロジック検証。ランタイムの uGUI 生成は含まない。
    /// </summary>
    public class V6GameHudContractTests
    {
        // --- 契約 §8: KeyGuide 格子 (KeyGuide と判定行は同じ格子) ---

        [Test]
        public void LaneGrid_Fhd_MatchesContract()
        {
            V6HudMetrics.LaneGridDesign(1920, 1080, out float first, out float spacing);
            // 契約: KeyGuide x = 310, 487, ... (KeyCap 56 幅 → 中心は +28)
            Assert.AreEqual(338f, first, 0.5f);
            Assert.AreEqual(177.42857f, spacing, 0.01f);
            // 最終列中心: 310 + 28 + 7 × 177.43 = 1580
            Assert.AreEqual(1580f, first + 7 * spacing, 1f);
        }

        [Test]
        public void LaneGrid_TwoK_MatchesContract()
        {
            V6HudMetrics.LaneGridDesign(2560, 1440, out float first, out float spacing);
            // 契約: KeyGuide x = 529, 733, ..., 1954 (KeyCap 75 幅 → 中心は +37.5)
            Assert.AreEqual(566.5f, first, 0.5f);
            Assert.AreEqual(203.57143f, spacing, 0.01f);
            Assert.AreEqual(1991.5f, first + 7 * spacing, 1f);
        }

        [Test]
        public void LaneGrid_Mac_MatchesContract()
        {
            V6HudMetrics.LaneGridDesign(2560, 1664, out float first, out float spacing);
            // 契約: KeyGuide x = 464, 686, ..., 2019 (KeyCap 75 幅 → 中心は +37.5)
            Assert.AreEqual(501.5f, first, 0.5f);
            Assert.AreEqual(222.14285f, spacing, 0.01f);
            Assert.AreEqual(2056.5f, first + 7 * spacing, 1f);
        }

        [Test]
        public void KeyGuideTemplateX_ConvertsDesignPxToTemplate()
        {
            // design px を等比倍率で割ったテンプレート座標になり、
            // 描画時 (× 4/3) に契約どおりの design px に戻る。
            Assert.AreEqual(566.5f / (4f / 3f), V6HudMetrics.KeyGuideTemplateX(2560, 1440, 0), 0.01f);
            Assert.AreEqual(
                (566.5f + 7 * 203.57143f) / (4f / 3f),
                V6HudMetrics.KeyGuideTemplateX(2560, 1440, 7), 0.01f);
            // FHD はテンプレート = design px (scale 1)
            Assert.AreEqual(338f, V6HudMetrics.KeyGuideTemplateX(1920, 1080, 0), 0.5f);
        }

        // --- 契約 §8 ⚠: レーン中心への追従 (レガシー 3D プレイフィールド時の投影) ---

        [Test]
        public void ScreenXToTemplateX_DividesByFrameScale()
        {
            // かつて hud root 自身の localScale (=1) で割っており、
            // 2K / Mac で 1.333 倍ズレていた。親フレームの等比倍率で割るのが正しい。
            Assert.AreEqual(500f, V6HudLogic.ScreenXToTemplateX(500f, 1f), 1e-4f);
            Assert.AreEqual(375f, V6HudLogic.ScreenXToTemplateX(500f, 4f / 3f), 1e-4f);
            // 未初期化 (scale 0) は等倍として扱う
            Assert.AreEqual(500f, V6HudLogic.ScreenXToTemplateX(500f, 0f), 1e-4f);
        }

        // --- 契約 §8: 縦位置 (中央 / 下端アンカーが作る design px) ---

        [Test]
        public void AnchoredTopDesignY_MatchesContract()
        {
            // Combo 上端: FHD 430 / 2K 573 / Mac 685
            Assert.AreEqual(430f, V6HudMetrics.MiddleAnchoredTopDesignY(1920, 1080, 110f), 0.5f);
            Assert.AreEqual(573f, V6HudMetrics.MiddleAnchoredTopDesignY(2560, 1440, 110f), 1.5f);
            Assert.AreEqual(685f, V6HudMetrics.MiddleAnchoredTopDesignY(2560, 1664, 110f), 1.5f);
            // Combo Status 上端: FHD 460 / 2K 613 / Mac 725
            Assert.AreEqual(460f, V6HudMetrics.MiddleAnchoredTopDesignY(1920, 1080, 80f), 0.5f);
            Assert.AreEqual(613f, V6HudMetrics.MiddleAnchoredTopDesignY(2560, 1440, 80f), 1.5f);
            Assert.AreEqual(725f, V6HudMetrics.MiddleAnchoredTopDesignY(2560, 1664, 80f), 1.5f);
            // KeyGuide 上端: FHD 1011 / 2K 1347 / Mac 1571
            Assert.AreEqual(1011f, V6HudMetrics.BottomAnchoredTopDesignY(1920, 1080, 33f, 36f), 0.5f);
            Assert.AreEqual(1347f, V6HudMetrics.BottomAnchoredTopDesignY(2560, 1440, 33f, 36f), 2f);
            Assert.AreEqual(1571f, V6HudMetrics.BottomAnchoredTopDesignY(2560, 1664, 33f, 36f), 2f);
        }

        // --- 契約 §12: Combo Bonus block は廃止 (issue-162 B-4 再照合) ---
        // 「+ COMBO BONUS」「次のボーナス」は実在しない加点の表示 (近似値 / 仮の式)
        // だったため block ごと削除した。この座標は Combo Status が単独で使う。

        // --- 契約 §8 / §12: 強調下地色 #3A3220 (+ セグメント / FC チップ) ---

        [Test]
        public void AccentBase_MatchesContractColor()
        {
            // AccentBase は Color 型なので Color32 へ変換して比較する
            // (Color32 と Color は別の型で、AreEqual(object, object) では一致しない)。
            Assert.AreEqual(new Color32(0x3A, 0x32, 0x20, 0xFF), (Color32)V6HudLogic.AccentBase);
        }
    }
}
