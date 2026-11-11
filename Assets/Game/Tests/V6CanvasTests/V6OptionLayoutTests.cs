using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Option;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// v6 Option 画面 (design B6If6Z パッチ) のレイアウト収まり検証 (純ロジック)。
    ///
    /// 正本: docs/design/v6/flashbeat-v6-nodes.json の
    /// - Option · v6 (55605) 配下の 06 Play · v6 (B6If6Z) / 06 Display (EzLfb) /
    ///   06 Sound (z7joSy) / 06 Data (DQHHp) の descendants パッチ
    /// - 契約 docs/design/v6/flashbeat-v6-contract.md §4 (共通フレーム / ゾーン)
    ///
    /// すべての要素が Main ゾーン (24,96) 1680×780 / フレーム 1728×972 に収まることを
    /// V6Layout.FitsIn で検証する (実機でのはみ出し — 旧実装のタブ右寄り・KeyCap フレーム外 —
    /// の再発防止)。座標は design 空間 (左上原点・y_down)。
    /// </summary>
    public class V6OptionLayoutTests
    {
        private static readonly Vector2 MainPos = new Vector2(24f, 96f);
        private static readonly Vector2 MainSize = new Vector2(1680f, 780f);
        private static readonly Vector2 FrameSize = new Vector2(1728f, 972f);

        // --- タブ (design C0piZz: メイン (438,0) 824x48、タブ 200x48 gap8) ---

        [Test]
        public void Tabs_FillDesignStrip_AndFitInMain()
        {
            for (int i = 0; i < V6OptionLogic.TabCount; i++)
            {
                var topLeft = V6OptionLogic.TabTopLeft(i);
                var size = new Vector2(V6OptionLogic.TabW, V6OptionLogic.TabsH);
                Assert.IsTrue(V6Layout.FitsIn(MainPos, MainSize, MainPos + topLeft, size),
                    $"タブ {i} が Main ゾーン外 (design C0piZz)");
                // design 値そのもの (パッチ C0piZz の子の並び 200+8 間隔)
                Assert.AreEqual(438f + (200f + 8f) * i, topLeft.x, 0.0001f);
                Assert.AreEqual(0f, topLeft.y, 0.0001f);
            }

            // タブ帯の右端 = 438 + 4*200 + 3*8 = 1262 ≤ 1680
            Assert.LessOrEqual(V6OptionLogic.TabsX + V6OptionLogic.TabsW, MainSize.x);
        }

        [Test]
        public void Tabs_DoNotOverlapPanels()
        {
            // タブ帯 y: 0..48 / 設定・プレビュー y: 68..756 — 縦に分離
            Assert.LessOrEqual(V6OptionLogic.TabsY + V6OptionLogic.TabsH, V6OptionLogic.SetY);
            Assert.LessOrEqual(V6OptionLogic.TabsY + V6OptionLogic.TabsH, V6OptionLogic.PrevY);
        }

        // --- 設定パネル (design crVgG: メイン (304,68) 512x688、行 496x56) ---

        [Test]
        public void SettingsPanel_AndRows_FitInMain()
        {
            var panelPos = MainPos + new Vector2(V6OptionLogic.SetX, V6OptionLogic.SetY);
            var panelSize = new Vector2(V6OptionLogic.SetW, V6OptionLogic.SetH);
            Assert.IsTrue(V6Layout.FitsIn(MainPos, MainSize, panelPos, panelSize),
                "設定パネルが Main ゾーン外");

            // 行が最も多いタブ (Display 9 行, 詰め行間 60) でも最終行がパネルに収まること
            int maxRows = 9;
            float step = V6OptionLogic.RowStep(maxRows);
            float lastBottom = V6OptionLogic.SettingsRowFirstY + step * (maxRows - 1)
                + V6OptionLogic.SettingsRowH;
            Assert.LessOrEqual(lastBottom, V6OptionLogic.SetH);
        }

        [Test]
        public void SettingRow_Inners_FitInRow()
        {
            // 行の内側 (design t5iVf 等): padding [0,12] — Label x12 w200 / Value 右端 x380 w120 /
            // Prev (392,8) 40x40 / Next (444,8) 40x40 → 右端 484 ≤ 496
            var rowPos = MainPos + new Vector2(V6OptionLogic.SetX + V6OptionLogic.SetRowX,
                V6OptionLogic.SetY + V6OptionLogic.SettingsRowFirstY);
            var rowSize = new Vector2(V6OptionLogic.SetRowW, V6OptionLogic.SettingsRowH);

            Assert.IsTrue(V6Layout.FitsIn(rowPos, rowSize, rowPos + new Vector2(12f, 0f),
                new Vector2(200f, V6OptionLogic.SettingsRowH)), "Label が行外");
            Assert.IsTrue(V6Layout.FitsIn(rowPos, rowSize, rowPos + new Vector2(260f, 0f),
                new Vector2(120f, V6OptionLogic.SettingsRowH)), "Value が行外");
            Assert.IsTrue(V6Layout.FitsIn(rowPos, rowSize, rowPos + new Vector2(392f, 8f),
                new Vector2(40f, 40f)), "Prev (chevron-left) が行外");
            Assert.IsTrue(V6Layout.FitsIn(rowPos, rowSize, rowPos + new Vector2(444f, 8f),
                new Vector2(40f, 40f)), "Next (chevron-right) が行外");
        }

        // --- プレビュー (design cqQMc: メイン (864,68) 512x688) ---

        [Test]
        public void PreviewPanel_GamePreview_AndKeyGrid_FitInMain()
        {
            var panelPos = MainPos + new Vector2(V6OptionLogic.PrevX, V6OptionLogic.PrevY);
            var panelSize = new Vector2(V6OptionLogic.PrevW, V6OptionLogic.PrevH);
            Assert.IsTrue(V6Layout.FitsIn(MainPos, MainSize, panelPos, panelSize),
                "プレビューパネルが Main ゾーン外");

            var gamePos = panelPos + new Vector2(V6OptionLogic.GamePrevX, V6OptionLogic.GamePrevY);
            var gameSize = new Vector2(V6OptionLogic.GamePrevW, V6OptionLogic.GamePrevH);
            Assert.IsTrue(V6Layout.FitsIn(panelPos, panelSize, gamePos, gameSize),
                "Game Preview がパネル外");

            // JIS キー一覧 (design Key 1..Key _): 各段 y356/400/444/488、34x40 step36。
            // 最長の 1 段目 (13 個, x0=8) の右端 = 8 + 12*36 + 34 = 474 ≤ 512
            for (int r = 0; r < V6OptionLogic.JisGridKeys.Length; r++)
            {
                float right = V6OptionLogic.GridRowX[r]
                    + (V6OptionLogic.JisGridKeys[r].Length - 1) * V6OptionLogic.GridKeyStepX
                    + V6OptionLogic.GridKeyW;
                float bottom = V6OptionLogic.GridRowY[r] + V6OptionLogic.GridKeyH;
                Assert.LessOrEqual(right, V6OptionLogic.PrevW, $"キー段 {r} がパネル外 (右)");
                Assert.LessOrEqual(bottom, V6OptionLogic.PrevH, $"キー段 {r} がパネル外 (下)");
            }

            // 説明ブロック (design Divider y544 / Desc Title y564 / Desc y614)
            Assert.IsTrue(V6Layout.FitsIn(panelPos, panelSize,
                panelPos + new Vector2(24f, V6OptionLogic.DescDividerY), new Vector2(464f, 1f)),
                "Divider がパネル外");
            Assert.IsTrue(V6Layout.FitsIn(panelPos, panelSize,
                panelPos + new Vector2(24f, V6OptionLogic.DescTitleY), new Vector2(464f, 40f)),
                "Desc Title がパネル外");
            Assert.IsTrue(V6Layout.FitsIn(panelPos, panelSize,
                panelPos + new Vector2(24f, V6OptionLogic.DescY),
                new Vector2(V6OptionLogic.DescW, V6OptionLogic.DescH)),
                "Desc がパネル外");
        }

        // --- フッター キー凡例 (design パッチ QQ4jM → r4IJx: (0,30) gap24 5 項目) ---

        [Test]
        public void FooterLegend_FitsInFooterZone()
        {
            // KeyCap 幅 = ceil(LabelAdvanceEm(key) * fs22) + padding 8*2、Label 幅 60、項内 gap8。
            string[][] items =
            {
                new[] { "↑↓", "選択" }, new[] { "←→", "変更" }, new[] { "Tab", "タブ切替" },
                new[] { "Esc", "戻る" }, new[] { "Enter", "決定" },
            };
            float total = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                float keyW = Mathf.Ceil(V6Ui.LabelAdvanceEm(items[i][0]) * V6Controls.KeyCapFontSize)
                    + 8f * 2f;
                // Label 幅は TMP 実測で詰める (design 実測: 選択/変更/戻る/決定 45、
                // タブ切替 89)。ここでは上界として概算 (全角 1em) + 2px を使う。
                float labelW = Mathf.Ceil(V6Ui.LabelAdvanceEm(items[i][1]) * 22f) + 2f;
                float itemW = keyW + 8f + labelW;
                // 各項目が凡例ゾーン (0,30) 840x32 に収まる (design r4IJx 左下ゾーン幅 840)
                Assert.LessOrEqual(itemW, 840f, $"凡例 {i} がゾーン幅外");
                total += itemW;
            }

            total += V6OptionLogic.LegendGap * (items.Length - 1);
            Assert.LessOrEqual(total, 840f, "キー凡例 5 項目がフッター左ゾーンに収まらない");
        }

        // --- フレーム全体 ---

        [Test]
        public void MainZone_FitsInFrame()
        {
            Assert.IsTrue(V6Layout.FitsIn(Vector2.zero, FrameSize, MainPos, MainSize),
                "Main ゾーンが共通フレーム (1728x972) 外");
        }
    }
}
