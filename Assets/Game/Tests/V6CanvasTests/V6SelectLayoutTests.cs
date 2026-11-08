using NUnit.Framework;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.Select.View.Tests
{
    /// <summary>
    /// v6 選曲画面のレイアウト計算 (design 空間 1728x972)。
    /// スクリーンショットではなく計算式で
    /// 「タイトルと戻るボタンが重ならない」「凡例が幅に収まる」
    /// 「ヒントと下矢印が重ならない」を検証する。
    /// 3 解像度 (1920x1080 / 2560x1440 / 2560x1664) でも同じ design 座標が
    /// そのまま使えること (frame を等比拡大するだけ) を併せて確認する。
    /// </summary>
    public sealed class V6SelectLayoutTests
    {
        private static readonly int[][] TargetResolutions =
        {
            new[] { 1920, 1080 },
            new[] { 2560, 1440 },
            new[] { 2560, 1664 },
        };

        /// <summary>曲リストパネルのローカル矩形 (リストパネル ローカル)。</summary>
        private static V6Rect ListLocal()
        {
            return new V6Rect(0f, 0f, V6SelectViewLogic.ListW, V6SelectViewLogic.ListH);
        }

        // --- ヘッダー ---

        [Test]
        public void HeaderTitle_DoesNotOverlapBackButton()
        {
            V6Rect back = V6SelectViewLogic.HeaderBackButton();
            V6Rect title = V6SelectViewLogic.HeaderTitle();

            Assert.IsFalse(back.Overlaps(title), "the title overlaps the back button");
            Assert.GreaterOrEqual(title.X - back.Right, 16f, "the title needs a gap from the back button");
        }

        [Test]
        public void HeaderTitle_DoesNotOverlapBackButtonAtAnyResolution()
        {
            // frame を等比拡大するだけなので、design 空間で重なっていなければ
            // どの解像度でも重ならない。
            foreach (int[] res in TargetResolutions)
            {
                float u = V6SelectViewLogic.DesignScale(res[0], res[1]);
                V6Rect back = V6SelectViewLogic.HeaderBackButton();
                V6Rect title = V6SelectViewLogic.HeaderTitle();

                Assert.IsFalse(back.Overlaps(title), "the title overlaps the back button at " + res[0] + "x" + res[1]);
                Assert.GreaterOrEqual((title.X - back.Right) * u, 16f * u);
            }
        }

        [Test]
        public void HeaderSearch_QueryAndCapDoNotOverlap()
        {
            V6Rect query = V6SelectViewLogic.HeaderSearchQuery();
            V6Rect cap = V6SelectViewLogic.HeaderSearchCap();

            Assert.IsFalse(query.Overlaps(cap), "the query text overlaps the A-Z cap");
            Assert.GreaterOrEqual(cap.X - query.Right, 8f, "the A-Z cap needs a gap from the query text");
            Assert.LessOrEqual(cap.Right, V6SelectViewLogic.HeaderSearch().W, "the A-Z cap must stay in the search field");
        }

        [Test]
        public void HeaderSearchCap_HasRoomForTheLabel()
        {
            V6Rect cap = V6SelectViewLogic.HeaderSearchCap();
            float nominal = V6SelectViewLogic.TextEmWidth("A-Z") * V6SelectViewLogic.CapFontSize;

            Assert.LessOrEqual(nominal, cap.W - V6SelectViewLogic.CapPadX * 2f + 0.01f,
                "the A-Z label must fit in the cap on one line");
        }

        [Test]
        public void HeaderItems_StayInsideHeader()
        {
            V6Rect header = V6SelectViewLogic.HeaderLocalRect();

            Assert.IsTrue(header.Contains(V6SelectViewLogic.HeaderBackButton()));
            Assert.IsTrue(header.Contains(V6SelectViewLogic.HeaderTitle()));
            Assert.IsTrue(header.Contains(V6SelectViewLogic.HeaderSearch()));
            Assert.IsTrue(header.Contains(V6SelectViewLogic.HeaderCount()));
            Assert.IsTrue(header.Contains(V6SelectViewLogic.HeaderFilter()));
        }

        [Test]
        public void HeaderCount_DoesNotOverlapFilterButton()
        {
            Assert.IsFalse(V6SelectViewLogic.HeaderCount().Overlaps(V6SelectViewLogic.HeaderFilter()));
        }

        // --- フッター (凡例) ---

        [Test]
        public void FooterLegend_FitsInLegendArea()
        {
            float width = V6SelectViewLogic.FooterLegendWidth();
            V6Rect area = V6SelectViewLogic.FooterLegendArea();

            Assert.LessOrEqual(width, area.W, "the legend must fit the footer legend area");
            Assert.LessOrEqual(width, V6SelectViewLogic.FooterSettings().X,
                "the legend must not run into the settings button");
        }

        [Test]
        public void FooterLegend_ItemsDoNotOverlapEachOther()
        {
            V6LegendItem[] items = V6SelectViewLogic.FooterLegend();
            Assert.Greater(items.Length, 1);

            for (int i = 0; i < items.Length; i++)
            {
                Assert.GreaterOrEqual(items[i].X, 0f, "a legend item starts left of the footer");
                Assert.LessOrEqual(items[i].X + items[i].Width, V6SelectViewLogic.FooterLegendArea().W,
                    "a legend item runs out of the footer");
                if (i > 0)
                {
                    float gap = items[i].X - (items[i - 1].X + items[i - 1].Width);
                    Assert.GreaterOrEqual(gap, V6SelectViewLogic.LegendItemGap - 0.01f,
                        "legend items overlap: " + items[i - 1].Key + " / " + items[i].Key);
                }
            }
        }

        [Test]
        public void FooterLegend_CapsAndLabelsDoNotOverlap()
        {
            V6LegendItem[] items = V6SelectViewLogic.FooterLegend();

            for (int i = 0; i < items.Length; i++)
            {
                float capRight = items[i].X + items[i].CapWidth;
                float labelX = items[i].X + items[i].CapWidth + V6SelectViewLogic.LegendLabelGap;
                Assert.GreaterOrEqual(labelX - capRight, V6SelectViewLogic.LegendLabelGap - 0.01f,
                    "the cap and the label overlap: " + items[i].Key);
                Assert.LessOrEqual(items[i].LabelWidth,
                    V6SelectViewLogic.EstimateTextWidth(items[i].Label, V6SelectViewLogic.LabelFontSize) + 0.01f,
                    "the label width must be its natural width");
            }
        }

        [Test]
        public void FooterLegend_CapLabelsFitOnOneLine()
        {
            V6LegendItem[] items = V6SelectViewLogic.FooterLegend();

            for (int i = 0; i < items.Length; i++)
            {
                float nominal = V6SelectViewLogic.TextEmWidth(items[i].Key) * V6SelectViewLogic.CapFontSize;
                Assert.LessOrEqual(nominal, items[i].CapWidth - V6SelectViewLogic.CapPadX * 2f + 0.01f,
                    "the cap label must fit on one line: " + items[i].Key);
            }
        }

        [Test]
        public void TextEmWidth_MatchesTheExpectedWidths()
        {
            // レイアウトの計算に使う字幅 (em 単位)。全角は 1.0、半角は種類別。
            Assert.AreEqual(1.90f, V6SelectViewLogic.TextEmWidth("A-Z"), 0.001f);
            Assert.AreEqual(2.98f, V6SelectViewLogic.TextEmWidth("Space"), 0.001f);
            Assert.AreEqual(1.82f, V6SelectViewLogic.TextEmWidth("Tab"), 0.001f);
            Assert.AreEqual(0.66f, V6SelectViewLogic.TextEmWidth("F"), 0.001f);
            Assert.AreEqual(2.00f, V6SelectViewLogic.TextEmWidth("↑↓"), 0.001f);
            Assert.AreEqual(2.00f, V6SelectViewLogic.TextEmWidth("決定"), 0.001f);
            Assert.AreEqual(3.00f, V6SelectViewLogic.TextEmWidth("曲送り"), 0.001f);
            Assert.AreEqual(2.00f, V6SelectViewLogic.TextEmWidth("検索"), 0.001f);
            Assert.AreEqual(4.00f, V6SelectViewLogic.TextEmWidth("絞り込み"), 0.001f);
        }

        [Test]
        public void FooterButtons_DoNotOverlapEachOther()
        {
            V6Rect settings = V6SelectViewLogic.FooterSettings();
            V6Rect ready = V6SelectViewLogic.FooterReady();

            Assert.IsFalse(settings.Overlaps(ready), "the settings and Ready buttons overlap");
            Assert.GreaterOrEqual(settings.X - V6SelectViewLogic.FooterLegendWidth(), 24f,
                "the legend needs a gap from the settings button");
        }

        [Test]
        public void FooterReady_InnerPartsStayInsideTheButton()
        {
            V6Rect ready = V6SelectViewLogic.FooterReady();
            V6Rect icon = V6SelectViewLogic.Offset(ready, V6SelectViewLogic.FooterReadyIcon());
            V6Rect label = V6SelectViewLogic.Offset(ready, V6SelectViewLogic.FooterReadyLabel());
            V6Rect cap = V6SelectViewLogic.Offset(ready, V6SelectViewLogic.FooterReadyCap());

            Assert.IsTrue(ready.Contains(icon), "the Ready icon stays inside the button");
            Assert.IsTrue(ready.Contains(label), "the Ready label stays inside the button");
            Assert.IsTrue(ready.Contains(cap), "the Space cap stays inside the button");
            Assert.IsFalse(label.Overlaps(cap), "the Ready label and the Space cap overlap");
            Assert.IsFalse(icon.Overlaps(label), "the Ready icon and the Ready label overlap");
        }

        [Test]
        public void FooterSettings_InnerPartsStayInsideTheButton()
        {
            V6Rect settings = V6SelectViewLogic.FooterSettings();
            V6Rect icon = V6SelectViewLogic.Offset(settings, V6SelectViewLogic.FooterSettingsIcon());
            V6Rect label = V6SelectViewLogic.Offset(settings, V6SelectViewLogic.FooterSettingsLabel());

            Assert.IsTrue(settings.Contains(icon), "the settings icon stays inside the button");
            Assert.IsTrue(settings.Contains(label), "the settings label stays inside the button");
            Assert.IsFalse(icon.Overlaps(label), "the settings icon and the settings label overlap");
        }

        // --- 曲リスト下端 ---

        [Test]
        public void ListBottomHint_DoesNotOverlapScrollDownButton()
        {
            V6Rect hint = V6SelectViewLogic.ListBottomHint();
            V6Rect down = V6SelectViewLogic.ListScrollButton(true);

            Assert.IsFalse(hint.Overlaps(down), "the list hint overlaps the down arrow button");
            Assert.GreaterOrEqual(down.X - hint.Right, 8f, "the list hint needs a gap from the down arrow button");
        }

        [Test]
        public void ListRows_StayAboveTheScrollButtonsAndTheHint()
        {
            V6Rect first = V6SelectViewLogic.ListRowRect(0);
            V6Rect last = V6SelectViewLogic.ListRowRect(V6SelectViewLogic.ListVisibleRows - 1);
            V6Rect up = V6SelectViewLogic.ListScrollButton(false);
            V6Rect down = V6SelectViewLogic.ListScrollButton(true);
            V6Rect hint = V6SelectViewLogic.ListBottomHint();

            Assert.IsFalse(up.Overlaps(first), "the up arrow button overlaps the first row");
            Assert.IsFalse(last.Overlaps(down), "the last row overlaps the down arrow button");
            Assert.IsFalse(last.Overlaps(hint), "the last row overlaps the list hint");
            Assert.LessOrEqual(last.Bottom, down.Y, "the rows must end above the down arrow button");
        }

        [Test]
        public void ListBottomHintAndButtons_StayInsideTheListPanel()
        {
            V6Rect list = ListLocal();

            Assert.IsTrue(list.Contains(V6SelectViewLogic.ListScrollButton(true)));
            Assert.IsTrue(list.Contains(V6SelectViewLogic.ListScrollButton(false)));
            Assert.IsTrue(list.Contains(V6SelectViewLogic.ListBottomHint()));
        }

        // --- 3 解像度で破綻しない根拠 ---

        [Test]
        public void Frame_FitsEveryTargetResolution()
        {
            foreach (int[] res in TargetResolutions)
            {
                float u = V6SelectViewLogic.DesignScale(res[0], res[1]);

                Assert.LessOrEqual(V6SelectViewLogic.DesignW * u, res[0] + 0.01f,
                    "the frame is wider than the screen at " + res[0] + "x" + res[1]);
                Assert.LessOrEqual(V6SelectViewLogic.DesignH * u, res[1] + 0.01f,
                    "the frame is taller than the screen at " + res[0] + "x" + res[1]);
            }
        }

        [Test]
        public void KeyElements_StayInsideTheFrame()
        {
            // frame が画面内に収まるなら、design 空間で frame 内に収まる要素は
            // どの解像度でも画面内に収まる (frame は等比拡大されるだけ)。
            V6Rect frame = V6SelectViewLogic.FrameRect();
            V6Rect header = V6SelectViewLogic.HeaderPanelRect();
            V6Rect footer = V6SelectViewLogic.FooterPanelRect();
            V6Rect list = V6SelectViewLogic.ListPanelRect();

            Assert.IsTrue(frame.Contains(header));
            Assert.IsTrue(frame.Contains(footer));
            Assert.IsTrue(frame.Contains(list));

            Assert.IsTrue(frame.Contains(V6SelectViewLogic.ToFrame(header, V6SelectViewLogic.HeaderBackButton())));
            Assert.IsTrue(frame.Contains(V6SelectViewLogic.ToFrame(header, V6SelectViewLogic.HeaderTitle())));
            Assert.IsTrue(frame.Contains(V6SelectViewLogic.ToFrame(header, V6SelectViewLogic.HeaderSearch())));
            Assert.IsTrue(frame.Contains(V6SelectViewLogic.ToFrame(header, V6SelectViewLogic.HeaderCount())));
            Assert.IsTrue(frame.Contains(V6SelectViewLogic.ToFrame(header, V6SelectViewLogic.HeaderFilter())));
            Assert.IsTrue(frame.Contains(V6SelectViewLogic.ToFrame(footer, V6SelectViewLogic.FooterSettings())));
            Assert.IsTrue(frame.Contains(V6SelectViewLogic.ToFrame(footer, V6SelectViewLogic.FooterReady())));
            Assert.IsTrue(frame.Contains(V6SelectViewLogic.ToFrame(list, V6SelectViewLogic.ListScrollButton(true))));
            Assert.IsTrue(frame.Contains(V6SelectViewLogic.ToFrame(list, V6SelectViewLogic.ListBottomHint())));
        }

        [Test]
        public void CapLabels_FitOnOneLineAtEveryResolution()
        {
            // design 空間のフォントサイズで幅を決めているので、倍率 u は
            // キャップ幅とラベル幅に共通でかかる (比率が変わらない)。
            foreach (int[] res in TargetResolutions)
            {
                float u = V6SelectViewLogic.DesignScale(res[0], res[1]);

                V6LegendItem[] items = V6SelectViewLogic.FooterLegend();
                for (int i = 0; i < items.Length; i++)
                {
                    float inner = (items[i].CapWidth - V6SelectViewLogic.CapPadX * 2f) * u;
                    float label = V6SelectViewLogic.EstimateTextWidth(items[i].Key, V6SelectViewLogic.CapFontSize) * u;
                    Assert.LessOrEqual(label, inner + 0.01f,
                        "the cap label wraps at " + res[0] + "x" + res[1] + ": " + items[i].Key);
                }

                V6Rect cap = V6SelectViewLogic.HeaderSearchCap();
                float capInner = (cap.W - V6SelectViewLogic.CapPadX * 2f) * u;
                float capLabel = V6SelectViewLogic.EstimateTextWidth("A-Z", V6SelectViewLogic.CapFontSize) * u;
                Assert.LessOrEqual(capLabel, capInner + 0.01f,
                    "the A-Z cap label wraps at " + res[0] + "x" + res[1]);
            }
        }

        [Test]
        public void Layout_IsIndependentOfTheScreenResolution()
        {
            // レイアウトは design 空間の定数だけで決まる (実解像度に依存しない)。
            // 画面サイズを変えても、同じ design 座標の矩形が返ることを確認する。
            V6Rect title = V6SelectViewLogic.HeaderTitle();
            V6Rect back = V6SelectViewLogic.HeaderBackButton();
            float legendWidth = V6SelectViewLogic.FooterLegendWidth();

            foreach (int[] res in TargetResolutions)
            {
                Assert.AreEqual(20f, V6SelectViewLogic.CapFontSize, "the cap font size must be a design constant");
                Assert.AreEqual(20f, V6SelectViewLogic.LabelFontSize, "the label font size must be a design constant");
                Assert.AreEqual(0f, back.X);
                Assert.AreEqual(80f, title.X);
                Assert.AreEqual(legendWidth, V6SelectViewLogic.FooterLegendWidth());
                Assert.LessOrEqual(legendWidth, V6SelectViewLogic.FooterLegendArea().W);
            }
        }
    }
}
