using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Playfield;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// 固定解像度プロファイル (契約 §8) + contain-fit スケール (issue #748) の
    /// 純ロジック検証。3 プロファイルのネイティブ解像度では design px = 実 px
    /// (scale = 1)。それ以外の解像度では最も近いプロファイルの design 値に
    /// 画面へ収まる一様スケールを掛ける。
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
            // 共有寸法は 2K と同じ (契約 §8)。歌詞スロットは高さ合わせ写像なので
            // 2K (y=370) と Mac (y=428) で縦位置だけ変わる。
            Assert.AreEqual(370, m2k.LyricY);
            Assert.AreEqual(428, mMac.LyricY);
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
        public void ForResolution_UnknownResolution_UsesNearestProfileContainFit()
        {
            // 1920×1200: 幅が FHD (1920) に最も近い。横は等倍・縦は余るので
            // contain スケールは 1 (design 値そのまま)。
            var m = V6HudMetrics.ForResolution(1920f, 1200f);
            Assert.AreEqual("fhd", m.Name);
            Assert.AreEqual(1f, m.Scale, 1e-4f);
            Assert.AreEqual(95, m.GradeFs);
            Assert.AreEqual(600, m.GaugeW);

            // 1280×720: FHD が最近。contain スケール 2/3 で design 値が縮む
            // (以前は等倍のまま → 巨大な UI になっていた)。
            m = V6HudMetrics.ForResolution(1280f, 720f);
            Assert.AreEqual("fhd", m.Name);
            Assert.AreEqual(2f / 3f, m.Scale, 1e-4f);
            Assert.AreEqual(Mathf.RoundToInt(600f * 2f / 3f), m.GaugeW);

            // 3840×2160: 幅同点 (2K/Mac) → アスペクト比 16:9 で 2K。
            // contain スケール 3/2 で 2K design が 4K いっぱいに広がる
            // (以前は 2K のまま → 画面の約 2/3 しか占めなかった)。
            m = V6HudMetrics.ForResolution(3840f, 2160f);
            Assert.AreEqual("2k", m.Name);
            Assert.AreEqual(1.5f, m.Scale, 1e-4f);
            Assert.AreEqual(Mathf.RoundToInt(240f * 1.5f), m.ComboValueFs);
        }

        // --- issue #748: 解像度マトリクス (720p / 1080p / 1440p / 4K / UW / 縦長) ---

        [Test]
        public void ForResolution_ResolutionMatrix_ScalesToFitScreen()
        {
            // (w, h, 期待プロファイル, 期待 contain スケール)。
            // スケールは「選択プロファイルの design 寸法を画面に収める係数」
            // = min(w / profileW, h / profileH)。定数ではなく関係で検証する。
            var table = new (float w, float h, string name)[]
            {
                (1280f, 720f, "fhd"),     // 720p
                (1920f, 1080f, "fhd"),    // 1080p (ネイティブ)
                (2560f, 1440f, "2k"),     // 1440p (ネイティブ)
                (3840f, 2160f, "2k"),     // 4K
                (3440f, 1440f, "2k"),     // ウルトラワイド 21:9
                (1080f, 1920f, "fhd"),    // 縦長 (タテ画面)
                (2560f, 1080f, "2k"),     // ウルトラワイド寄りの横長
            };

            foreach (var (w, h, name) in table)
            {
                var m = V6HudMetrics.ForResolution(w, h);
                Assert.AreEqual(name, m.Name, $"{w}×{h}");
                Assert.AreEqual(V6HudMetrics.ContainScaleFor(w, h), m.Scale, 1e-4f,
                    $"{w}×{h}: Scale は contain-fit と一致すること");

                // スケール後の主要寸法は「design 値 × scale」に一致し、
                // 少なくとも一辺が画面いっぱいまで広がる (contain の定義)。
                float designW = name == "fhd" ? 1920f : 2560f;
                float designH = name == "fhd" ? 1080f : (name == "mac" ? 1664f : 1440f);
                float expectedScale = Mathf.Min(w / designW, h / designH);
                Assert.AreEqual(expectedScale, m.Scale, 1e-3f, $"{w}×{h}");
            }
        }

        [Test]
        public void ForResolution_4K_CoversFullScreen()
        {
            // 4K で HUD が画面の約 2/3 に留まらないこと (issue #748 の再現条件)。
            // contain-fit なので、スケール適用後の「デザイン空間」は
            // 常に画面全体を覆う: 画面幅 ÷ scale = プロファイルの design 幅。
            var m = V6HudMetrics.ForResolution(3840f, 2160f);
            Assert.AreEqual(1.5f, m.Scale, 1e-4f);
            Assert.AreEqual(3840f / m.Scale, 2560f, 0.5f); // 2K design 幅いっぱい
            Assert.AreEqual(2160f / m.Scale, 1440f, 0.5f); // 2K design 高さいっぱい
        }

        [Test]
        public void ForResolution_UltrawideAndTall_ScalePolicy()
        {
            // ウルトラワイド 21:9 (3440×1440): 高さが 2K design と一致するので
            // contain スケールは 1。横に多少の余白が残るが、HUD は全て表示され
            // アスペクト比は保たれる (縦を削る cover は Top Shade / 判定行を
            // 切るため採用しない)。
            var m = V6HudMetrics.ForResolution(3440f, 1440f);
            Assert.AreEqual("2k", m.Name);
            Assert.AreEqual(1f, m.Scale, 1e-4f);
            Assert.AreEqual(1440f / m.Scale, 1440f, 0.5f); // 高さいっぱい

            // さらに横長 (2560×1080): 高さ基準で縮小 (scale < 1)。
            // HUD が画面からはみ出さない。
            m = V6HudMetrics.ForResolution(2560f, 1080f);
            Assert.AreEqual("2k", m.Name);
            Assert.Less(m.Scale, 1f);
            Assert.AreEqual(1080f / m.Scale, 1440f, 0.5f);

            // 縦長 (1080×1920): 高さ基準で縮小。HUD がはみ出さない。
            m = V6HudMetrics.ForResolution(1080f, 1920f);
            Assert.AreEqual("fhd", m.Name);
            Assert.Less(m.Scale, 1f);
            Assert.AreEqual(1920f / m.Scale, 1080f, 0.5f);
        }

        [Test]
        public void MatchesResolution_DetectsScaleChange()
        {
            // プロファイル名が同じでもスケールが違えば「別の解像度」
            // (リビルドが必要)。例: 3840×2160 と 2560×1440 は共に 2K だが
            // scale が 1.5 / 1.0 で異なる。
            Assert.IsTrue(V6HudMetrics.ForResolution(3840f, 2160f).MatchesResolution(3840f, 2160f));
            Assert.IsFalse(V6HudMetrics.ForResolution(2560f, 1440f).MatchesResolution(3840f, 2160f));
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

        // --- 正本 Uc9v6 T3U7u6 / HcP8N W1ppF: メニューと開始案内の寸法 ---

        [Test]
        public void MenuButton_MatchesDesignInternals()
        {
            // T3U7u6 180×48: [ラベル fs22][gap 10][KeyCap 55×32 (padding [0,10] の自動幅)]。
            var fhd = V6HudMetrics.Fhd(1f);
            Assert.AreEqual(180, fhd.MenuW);
            Assert.AreEqual(48, fhd.MenuH);
            Assert.AreEqual(22, fhd.MenuFs);
            Assert.AreEqual(32, fhd.MenuCapH);
            Assert.AreEqual(10, fhd.MenuGap);

            // 2K/Mac は 240×64 / fs30。KeyCap の自動高は fs の行高に比例 (22→32, 30→44)。
            var m2k = V6HudMetrics.TwoK();
            Assert.AreEqual(240, m2k.MenuW);
            Assert.AreEqual(64, m2k.MenuH);
            Assert.AreEqual(30, m2k.MenuFs);
            Assert.AreEqual(44, m2k.MenuCapH);
            Assert.AreEqual(10, m2k.MenuGap);
        }

        [Test]
        public void ReadyPrompt_MatchesDesignStartPrompt()
        {
            // HcP8N の Start Prompt (W1ppF): KeyCap 36 高 / fs22 / padding [0,10] と
            // 「to start」の行 (gap 10)。「Space to Start」の一文は design に無い。
            Assert.AreEqual(36f, V6HudLogic.ReadyPromptCapHeight);
            Assert.AreEqual(22f, V6HudLogic.ReadyPromptFontSize);
            Assert.AreEqual(10f, V6HudLogic.ReadyPromptGap);
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
