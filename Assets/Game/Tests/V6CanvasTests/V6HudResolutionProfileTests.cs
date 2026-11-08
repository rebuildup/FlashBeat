using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Playfield;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// 固定解像度プロファイル (契約 §8) の純ロジック検証。
    /// Owner 指示 2026-11-08: HUD は解像度ごとの固有フレームを
    /// design px = 実ピクセルで使う (等比拡大 / 引き伸ばしは一切しない)。
    /// </summary>
    public class V6HudResolutionProfileTests
    {
        // --- 契約 §8: 3 プロファイルの実ピクセル選択 ---

        [Test]
        public void ForResolution_KnownResolutions_ReturnProfileAsIs()
        {
            Assert.AreEqual(1f, V6HudMetrics.ForResolution(1920f, 1080f).Scale, 1e-4f);
            Assert.AreEqual(1f, V6HudMetrics.ForResolution(2560f, 1440f).Scale, 1e-4f);
            Assert.AreEqual(1f, V6HudMetrics.ForResolution(2560f, 1664f).Scale, 1e-4f);

            // 契約 §8 の design 値をそのまま保持する (等比拡大値に合わせない):
            // 2K: Grade fs125 / Menu (2256,64) / Combo (64,573) / Status (2016,613)
            var m2k = V6HudMetrics.ForResolution(2560f, 1440f);
            Assert.AreEqual(125, m2k.GradeFs);
            Assert.AreEqual(240, m2k.MenuW);   // Menu (2256,64) 240×64
            Assert.AreEqual(64, m2k.MenuH);
            Assert.AreEqual(573, m2k.ComboY);
            Assert.AreEqual(613, m2k.RightColY);
            // Mac: Combo (64,685) / Status (2016,725)
            var mMac = V6HudMetrics.ForResolution(2560f, 1664f);
            Assert.AreEqual(685, mMac.ComboY);
            Assert.AreEqual(725, mMac.RightColY);
            // 共有寸法は 2K と同じ (契約 §8)。歌詞枠 (契約 §10) の縦位置も同じ
            Assert.AreEqual(m2k.LyricY, mMac.LyricY);
            Assert.AreEqual(m2k.LyricFs, mMac.LyricFs);
            Assert.AreEqual(m2k.GaugeFillMax, mMac.GaugeFillMax);
        }

        // --- 契約 §8: 未知解像度の選択規則 (幅が最も近い → 同点はアスペクト比 → 高解像度) ---

        [Test]
        public void ForResolution_ResolutionTable_MatchesContract()
        {
            var table = new (float w, float h, string name)[]
            {
                (1920f, 1080f, "fhd"),
                (2560f, 1440f, "2k"),
                (2560f, 1664f, "mac"),
                (1920f, 1200f, "fhd"),
                (1280f, 720f, "fhd"),
                (3840f, 2160f, "2k"),
            };
            for (int i = 0; i < table.Length; i++)
            {
                Assert.AreEqual(table[i].name, V6HudMetrics.ForResolution(table[i].w, table[i].h).Name,
                    $"{table[i].w}×{table[i].h}");
            }
        }

        [Test]
        public void ForResolution_UnknownResolution_UsesNearestProfileAsIs()
        {
            // 1920×1200: 幅が FHD (1920) に最も近い。FHD 値を そのまま (縮尺を変えない)。
            var m = V6HudMetrics.ForResolution(1920f, 1200f);
            Assert.AreEqual("fhd", m.Name);
            Assert.AreEqual(95, m.GradeFs);
            Assert.AreEqual(600, m.GaugeW);
            // 1280×720: FHD が最近 (2K より近い)
            m = V6HudMetrics.ForResolution(1280f, 720f);
            Assert.AreEqual("fhd", m.Name);
            // 3840×2160: 幅同点 (2K/Mac) → アスペクト比 16:9 で 2K
            m = V6HudMetrics.ForResolution(3840f, 2160f);
            Assert.AreEqual("2k", m.Name);
            Assert.AreEqual(240, m.ComboValueFs);
        }

        [Test]
        public void ForResolution_DegenerateSize_FallsBackToFhd()
        {
            Assert.AreEqual("fhd", V6HudMetrics.ForResolution(0f, 1080f).Name);
            Assert.AreEqual("fhd", V6HudMetrics.ForResolution(1920f, 0f).Name);
        }

        [Test]
        public void MatchesResolution_DetectsProfileBoundary()
        {
            Assert.IsTrue(V6HudMetrics.TwoK().MatchesResolution(2560f, 1440f));
            Assert.IsFalse(V6HudMetrics.TwoK().MatchesResolution(2560f, 1664f));
            Assert.IsFalse(V6HudMetrics.Fhd(1f).MatchesResolution(2560f, 1440f));
        }

        // --- 契約 §6: キーガイド / 判定行はレーン中心に一致する ---

        [Test]
        public void LaneCenterScreenX_MatchesPlayfieldLaneCentres()
        {
            // レーン中心線 (V6LaneGeometry.TryLanePoint, y=792) の実画面 x と
            // 同じ写像 (DesignToScreen) なので必ず一致する。
            // FHD 1920×1080: スケール 1080/972、中央寄せオフセット 0
            float s = 1080f / 972f;
            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                V6LaneGeometry.TryLanePoint(lane, 0f, out var design, out _);
                float expected = design.x * s;
                Assert.AreEqual(expected, V6HudMetrics.LaneCenterScreenX(lane, 1920f, 1080f), 0.5f);
            }

            // Mac 2560×1664: スケール 1664/972、オフセット (2560 - 1728×s)/2
            s = 1664f / 972f;
            float offsetX = (2560f - 1728f * s) * 0.5f;
            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                V6LaneGeometry.TryLanePoint(lane, 0f, out var design, out _);
                float expected = design.x * s + offsetX;
                Assert.AreEqual(expected, V6HudMetrics.LaneCenterScreenX(lane, 2560f, 1664f), 0.5f);
                // 画面内に収まる (右カラムがみ出す問題と同じ失敗をしない)
                Assert.GreaterOrEqual(expected, 0f);
                Assert.LessOrEqual(expected, 2560f);
            }
        }

        [Test]
        public void LaneCenterScreenX_LaneCentresMatchLaneWidths()
        {
            // FHD: 判定線上のレーン幅 150×s。列間隔 = レーン幅 (中心線同士)。
            float s = 1080f / 972f;
            Assert.AreEqual(
                V6HudMetrics.LaneCenterScreenX(1, 1920f, 1080f) - V6HudMetrics.LaneCenterScreenX(0, 1920f, 1080f),
                150f * s, 0.5f);
        }
    }
}
