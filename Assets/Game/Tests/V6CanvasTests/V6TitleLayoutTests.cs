using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Title;
using FlashBeat.Update;

namespace FlashBeat.UI.V6.Title.Tests
{
    /// <summary>
    /// v6 タイトル画面 (QJtTv "01 Title · v6") の純ロジック検証。
    ///
    /// - フレームは 1728×972 固定で 1:1 (等倍) 中央配置 (引き伸ばしはしない):
    ///   origin = ((w−1728)/2, (h−972)/2) 切り捨て。
    ///   正本は design POJwG "Resolution Check · Title" の実配置
    ///   (FHD (96,54) / 2K (416,234) / Mac (416,346))。
    /// - Title 固有ノードの design 配置 (各ゾーン ローカルの design px) を
    ///   V6TitleLogic の design テーブルとして固定する。
    /// </summary>
    public sealed class V6TitleLayoutTests
    {
        // --- フレーム origin (1:1 固定中央配置) ---

        [Test]
        public void FrameOrigin_Fhd_MatchesDesignPlacement()
        {
            Assert.AreEqual(new Vector2(96f, 54f), V6TitleLogic.FrameOrigin(1920, 1080));
        }

        [Test]
        public void FrameOrigin_2k_And_Mac_MatchDesignPlacement()
        {
            Assert.AreEqual(new Vector2(416f, 234f), V6TitleLogic.FrameOrigin(2560, 1440));
            Assert.AreEqual(new Vector2(416f, 346f), V6TitleLogic.FrameOrigin(2560, 1664));
        }

        [Test]
        public void FrameOrigin_FloorsOddResolutions()
        {
            // (1921−1728)/2 = 96.5 → 96、(1081−972)/2 = 54.5 → 54 (design の定義: 切り捨て)。
            Assert.AreEqual(new Vector2(96f, 54f), V6TitleLogic.FrameOrigin(1921, 1081));
            Assert.AreEqual(new Vector2(416f, 346f), V6TitleLogic.FrameOrigin(2561, 1665));
        }

        [Test]
        public void FrameOrigin_InvalidSize_ReturnsZero()
        {
            Assert.AreEqual(Vector2.zero, V6TitleLogic.FrameOrigin(0, 1080));
            Assert.AreEqual(Vector2.zero, V6TitleLogic.FrameOrigin(1920, -1));
        }

        [Test]
        public void FrameOrigin_FrameFitsScreen()
        {
            // 3 解像度ともフレームが origin + サイズで画面内に収まる。
            Assert.LessOrEqual(V6TitleLogic.FrameOrigin(1920, 1080).x + V6TitleLogic.FrameW, 1920f);
            Assert.LessOrEqual(V6TitleLogic.FrameOrigin(2560, 1440).y + V6TitleLogic.FrameH, 1440f);
            Assert.LessOrEqual(V6TitleLogic.FrameOrigin(2560, 1664).y + V6TitleLogic.FrameH, 1664f);
        }

        // --- Title 固有ノードの design 配置 (QJtTv 実測値) ---

        [Test]
        public void HeaderNodes_MatchDesignCoordinates()
        {
            // CtA3V: Exit 162×56 (Header ローカル y20、正本は fill なし)。
            // 塗りは内部の Icon Button (Z6C0w) 56×56 $k4 r8 だけで、その中に
            // log-out 32 @(12,12)、KeyCap (68,12) 高さ 32、ラベル (109,6.5)。
            Assert.AreEqual(new Vector2(0f, 20f), V6TitleLogic.BackButtonPos);
            Assert.AreEqual(new Vector2(162f, 56f), V6TitleLogic.BackButtonSize);
            Assert.AreEqual(new Vector2(0f, 0f), V6TitleLogic.BackIconButtonPos);
            Assert.AreEqual(56f, V6TitleLogic.BackIconButtonSize);
            Assert.AreEqual(new Vector2(12f, 12f), V6TitleLogic.BackIconPos);
            Assert.AreEqual(32f, V6TitleLogic.BackIconSize);
            Assert.AreEqual(new Vector2(68f, 12f), V6TitleLogic.BackKeyCapPos);
            Assert.AreEqual(new Vector2(109f, 6.5f), V6TitleLogic.BackLabelPos);
            // aqq3q/ci867: タイトル文字 (72,20) 185×56 (正本では enabled: false)。
            Assert.AreEqual(new Vector2(72f, 20f), V6TitleLogic.HeaderTitlePos);
            Assert.AreEqual(new Vector2(185f, 56f), V6TitleLogic.HeaderTitleSize);
            // D7gbLV 更新ボタン: Header ローカル (1021,20) 355×56 (UKhaE 840,20 の中の 181,0)。
            Assert.AreEqual(new Vector2(1021f, 20f), V6TitleLogic.UpdateButtonPos);
            Assert.AreEqual(new Vector2(355f, 56f), V6TitleLogic.UpdateButtonSize);
            // L6w6G バージョン: Header ローカル (1400,32) 280×32、
            // 現行版 (93,0) / 矢印 (162,2) / 新版 (190,0)。
            Assert.AreEqual(new Vector2(1400f, 32f), V6TitleLogic.VersionZonePos);
            Assert.AreEqual(new Vector2(280f, 32f), V6TitleLogic.VersionZoneSize);
            Assert.AreEqual(new Vector2(93f, 0f), V6TitleLogic.VersionCurrentPos);
            Assert.AreEqual(new Vector2(162f, 2f), V6TitleLogic.VersionArrowPos);
            Assert.AreEqual(new Vector2(190f, 0f), V6TitleLogic.VersionNewPos);
        }

        [Test]
        public void HeaderRight_DoesNotDrawNonDesignZones()
        {
            // 正本 QJtTv のヘッダー右は zPAF8 だけで、中身は [更新操作] [バージョン] の 2 つ。
            // 曲情報 / カタログ状態 / 自己ベストのゾーンは設計に存在しない。
            // (以前の実装は (1080,0) / (1380,0) に独自ゾーンを描いていた)
            Assert.IsFalse(V6TitleLogic.ShowUpdateButton(UpdateFlowPhase.Idle));
            Assert.IsTrue(V6TitleLogic.ShowUpdateButton(UpdateFlowPhase.Available));
            Assert.IsTrue(V6TitleLogic.ShowNewVersion(UpdateFlowPhase.Available, "1.2.0"));
            Assert.IsFalse(V6TitleLogic.ShowNewVersion(UpdateFlowPhase.Available, ""));
        }

        [Test]
        public void MainNodes_MatchDesignCoordinates()
        {
            // CnRJj: ロゴ Main ローカル (0,180) 1680×332 (assets/13.png contain)。
            Assert.AreEqual(new Vector2(0f, 180f), V6TitleLogic.LogoPos);
            Assert.AreEqual(new Vector2(1680f, 332f), V6TitleLogic.LogoSize);
            // FHcxk: タブ列 (280,552) 1120×48、タブ 1 枚 280×48。
            Assert.AreEqual(new Vector2(280f, 552f), V6TitleLogic.TabsPos);
            Assert.AreEqual(new Vector2(1120f, 48f), V6TitleLogic.TabsSize);
            Assert.AreEqual(new Vector2(280f, 48f), V6TitleLogic.TabSize);
        }

        [Test]
        public void FooterNodes_MatchDesignCoordinates()
        {
            // QQ4jM: キー凡例 Footer ローカル (0,30)、gap16。
            Assert.AreEqual(new Vector2(0f, 30f), V6TitleLogic.LegendPos);
            Assert.AreEqual(16f, V6TitleLogic.LegendGap);
            // T8HuQS: 補助操作 (1198,20) 197×56 / 主操作 (1407,20) 273×56。
            Assert.AreEqual(new Vector2(1198f, 20f), V6TitleLogic.FooterAuxPos);
            Assert.AreEqual(new Vector2(197f, 56f), V6TitleLogic.FooterAuxSize);
            Assert.AreEqual(new Vector2(1407f, 20f), V6TitleLogic.FooterPrimaryPos);
            Assert.AreEqual(new Vector2(273f, 56f), V6TitleLogic.FooterPrimarySize);
        }

        [Test]
        public void FooterButtons_StayInsideDesignFrame()
        {
            // フッターの子は design 幅 1728 内に収まる (右寄せのはみ出し防止)。
            Assert.LessOrEqual(V6TitleLogic.FooterPrimaryPos.x + V6TitleLogic.FooterPrimarySize.x, 1728f);
            Assert.LessOrEqual(V6TitleLogic.FooterAuxPos.x + V6TitleLogic.FooterAuxSize.x, 1728f);
        }

        [Test]
        public void TitleVisibility_MatchesDesign()
        {
            // 正本 QJtTv の表示状態: タブ (FHcxk) / 凡例 (QQ4jM) は表示、
            // フッター右下の操作ボタン (T8HuQS) と タイトル文字 (ci867) は enabled: false。
            Assert.IsTrue(V6UiMode.TitleTabs);
            Assert.IsTrue(V6UiMode.TitleLegend);
            Assert.IsFalse(V6UiMode.TitleFooterActions);
            Assert.IsFalse(V6UiMode.TitleHeaderTitle);
        }

        [Test]
        public void Tabs_SpanDesignMainZone()
        {
            // タブ列 (280,552) 1120×48 は Main ゾーン (24,96) 1680×780 内に収まる。
            Assert.IsTrue(V6TitleLogic.TabsPos.x >= 24f);
            Assert.LessOrEqual(V6TitleLogic.TabsPos.x + V6TitleLogic.TabsSize.x, 24f + 1680f);
            Assert.LessOrEqual(V6TitleLogic.TabsPos.y + V6TitleLogic.TabsSize.y, 96f + 780f);
        }
    }
}
