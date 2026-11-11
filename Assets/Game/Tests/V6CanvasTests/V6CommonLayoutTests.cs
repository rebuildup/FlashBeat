using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Common.Tests
{
    /// <summary>
    /// v6 共通部品ライブラリの純ロジック (design 空間 1728×972) 検証。
    /// 「左右カラムが design 幅 1728 からはみ出さない」「Main ゾーン内の要素が
    /// 24..1704 に収まる」「契約の寸法 (ボタン高 56 / KeyCap 高 36 / 角丸 / fs) が
    /// トークンとして正しく写されている」ことを EditMode で保証する。
    /// </summary>
    public sealed class V6CommonLayoutTests
    {
        private static readonly Vector2 DesignOrigin = new Vector2(0f, 0f);
        private static readonly Vector2 DesignSize = new Vector2(V6Frame.DesignW, V6Frame.DesignH);
        private static readonly Vector2 MainOrigin = V6Frame.MainPos;
        private static readonly Vector2 MainSize = V6Frame.MainSize;

        // --- 座標変換 (design 左上原点 y_down → RectTransform 左下原点 y_up) ---

        [Test]
        public void ToLocal_FlipsYOnly()
        {
            var local = V6Layout.ToLocal(new Vector2(24f, 96f));
            Assert.AreEqual(24f, local.x);
            Assert.AreEqual(-96f, local.y);
        }

        [Test]
        public void ToLocal_OriginStaysAtParentTopLeft()
        {
            var local = V6Layout.ToLocal(Vector2.zero);
            Assert.AreEqual(0f, local.x);
            Assert.AreEqual(0f, local.y);
        }

        // --- フレーム ゾーン (契約 §4 の座標そのもの) ---

        [Test]
        public void FrameZones_MatchContractCoordinates()
        {
            Assert.AreEqual(new Vector2(24f, 0f), V6Frame.HeaderPos);
            Assert.AreEqual(new Vector2(1680f, 96f), V6Frame.HeaderSize);
            Assert.AreEqual(new Vector2(24f, 96f), V6Frame.MainPos);
            Assert.AreEqual(new Vector2(1680f, 780f), V6Frame.MainSize);
            Assert.AreEqual(new Vector2(24f, 876f), V6Frame.FooterPos);
            Assert.AreEqual(new Vector2(1680f, 96f), V6Frame.FooterSize);
            Assert.AreEqual(new Vector2(0f, 0f), V6Frame.ModalPos);
            Assert.AreEqual(new Vector2(1728f, 972f), V6Frame.ModalSize);
        }

        [Test]
        public void FrameZones_StayInsideDesignFrame()
        {
            Assert.IsTrue(V6Layout.FitsIn(DesignOrigin, DesignSize, V6Frame.HeaderPos, V6Frame.HeaderSize));
            Assert.IsTrue(V6Layout.FitsIn(DesignOrigin, DesignSize, V6Frame.MainPos, V6Frame.MainSize));
            Assert.IsTrue(V6Layout.FitsIn(DesignOrigin, DesignSize, V6Frame.FooterPos, V6Frame.FooterSize));
            Assert.IsTrue(V6Layout.FitsIn(DesignOrigin, DesignSize, V6Frame.ModalPos, V6Frame.ModalSize));
        }

        [Test]
        public void MainZone_SpansContractXRange()
        {
            // 契約: Main ゾーン内の要素は x 24..1704 に収まる。
            Assert.AreEqual(24f, MainOrigin.x);
            Assert.AreEqual(1704f, MainOrigin.x + MainSize.x);
        }

        // --- ヘッダー戻るボタン (design zUcsi/zCQBY) と Button 内 KeyCap ---

        [Test]
        public void BackButton_MatchesDesignInternals()
        {
            // 56×56 の中に arrow-left 24 @(16,3.5) と Key fs16 @(15,29.5) 26×23。
            // 中央寄せで積むとアイコンが y17..39 に下がり Key (y30..) と重なる。
            Assert.AreEqual(56f, V6Controls.BackButtonSize);
            Assert.AreEqual(24f, V6Controls.BackButtonIconSize);
            Assert.AreEqual(new Vector2(16f, 3.5f), V6Controls.BackButtonIconPos);
            Assert.AreEqual(new Vector2(15f, 29.5f), V6Controls.BackButtonKeyPos);
            Assert.AreEqual(new Vector2(26f, 23f), V6Controls.BackButtonKeySize);
            Assert.AreEqual(16f, V6Controls.BackButtonKeyFontSize);
            // 上下の余白は等しく (3.5)、アイコンと Key の間は 2。
            Assert.LessOrEqual(V6Controls.BackButtonIconPos.y + V6Controls.BackButtonIconSize,
                V6Controls.BackButtonKeyPos.y);
            Assert.AreEqual(V6Controls.BackButtonIconPos.y,
                V6Controls.BackButtonSize - (V6Controls.BackButtonKeyPos.y + V6Controls.BackButtonKeySize.y),
                0.001f, "上下の余白が等しくない (アイコンが上へ寄る)");
        }

        [Test]
        public void ButtonKeyCap_UsesDesignMetrics()
        {
            // Button Primary/Secondary の Cap は高さ 32・padding [0,8] (単体 KeyCap は 36 / [0,10])。
            Assert.AreEqual(32f, V6Controls.ButtonKeyCapHeight);
            Assert.AreEqual(8f, V6Controls.ButtonKeyCapPaddingX);
            // design の Cap 幅は [送り幅 + 16] で作る (E=29 / F3=41 / Space=78 相当)。
            // Key は高さ 32 でも fs22 のまま (正本 Prefab/Button Primary の Cap 実測値)。
            // 高さ比 (22/36) で 19 に縮めてはいけない。
            foreach (var key in new[] { "E", "F3", "Space" })
            {
                float capW = Mathf.Ceil(V6Ui.LabelAdvanceEm(key) * V6Controls.ButtonKeyFontSize)
                    + V6Controls.ButtonKeyCapPaddingX * 2f;
                Assert.AreEqual(22f, V6Controls.KeyFontSize(capW, key, V6Controls.ButtonKeyCapPaddingX), key);
            }

            // 幅が極端に狭いときだけ幅フィットで縮む (折り返し防止)。
            Assert.Less(V6Controls.KeyFontSize(20f, "Space", V6Controls.ButtonKeyCapPaddingX), 22f);
        }

        // --- 右寄せ / 中央 / 下寄せ (右カラムはみ出し防止) ---

        [Test]
        public void PlaceRight_RightColumnStaysInsideDesignWidth()
        {
            // Main ゾーン右端 (1704) に右寄せした 560 幅の右カラム。
            var size = new Vector2(560f, 780f);
            var topLeft = V6Layout.RightTopLeft(MainOrigin.x + MainSize.x, MainOrigin.y, size);

            Assert.AreEqual(1144f, topLeft.x);
            Assert.IsTrue(V6Layout.FitsIn(DesignOrigin, DesignSize, topLeft, size),
                "right column must stay inside the 1728 design width");
        }

        [Test]
        public void PlaceRight_RowsWithContractWidths_StayInsideMainZone()
        {
            // 契約 §5 の行幅 (Song 504 / Rank 496) を Main 右端に右寄せしても収まる。
            var songTopLeft = V6Layout.RightTopLeft(1704f, 96f, new Vector2(V6Lists.SongRowW, V6Lists.SongRowH));
            var rankTopLeft = V6Layout.RightTopLeft(1704f, 96f, new Vector2(V6Lists.RankRowW, V6Lists.RankRowH));
            Assert.IsTrue(V6Layout.FitsIn(MainOrigin, MainSize, songTopLeft, new Vector2(V6Lists.SongRowW, V6Lists.SongRowH)));
            Assert.IsTrue(V6Layout.FitsIn(MainOrigin, MainSize, rankTopLeft, new Vector2(V6Lists.RankRowW, V6Lists.RankRowH)));
        }

        [Test]
        public void PlaceCenter_KeepsElementCentered()
        {
            var size = new Vector2(512f, 88f);
            var topLeft = V6Layout.CenterTopLeft(V6Frame.DesignW * 0.5f, 668f, size);
            Assert.AreEqual(864f - 256f, topLeft.x, 0.001f);
            Assert.AreEqual(668f, topLeft.y);
        }

        [Test]
        public void PlaceBottom_KeepsFooterInsideDesignHeight()
        {
            var size = new Vector2(273f, 56f);
            var topLeft = V6Layout.BottomTopLeft(567f, V6Frame.FooterPos.y + V6Frame.FooterSize.y, size);
            Assert.AreEqual(972f - 56f, topLeft.y, 0.001f);
            Assert.IsTrue(V6Layout.FitsIn(DesignOrigin, DesignSize, topLeft, size));
        }

        // --- 積みオフセット (リスト行間隔: Select 76 / Ranking 52) ---

        [Test]
        public void VerticalOffsets_SelectRowPitch76()
        {
            // 行高 72 + gap 4 = 76 間隔 (Select の一覧スクロール)。
            var offsets = V6Layout.VerticalOffsets(4f, 72f, 72f, 72f, 72f);
            Assert.AreEqual(new[] { 0f, 76f, 152f, 228f }, offsets);
        }

        [Test]
        public void VerticalOffsets_RankingRowPitch64()
        {
            // design (S1TZD Score·k = (8,60+64k) 496×56): 行高 56 + gap 8 = 64 間隔。
            var offsets = V6Layout.VerticalOffsets(8f, 56f, 56f, 56f, 56f);
            Assert.AreEqual(new[] { 0f, 64f, 128f, 192f }, offsets);
        }

        [Test]
        public void HorizontalOffsets_StacksWithGap_MatchSongRowDesignX()
        {
            // design mcCOI (padding [0,12] gap 12): Level (12) / Thumb (68) /
            // Texts (176) / Grade (436)。順序は design の並び (Level 左) どおり。
            var offsets = V6Layout.HorizontalOffsets(12f, 44f, 96f, 248f, 56f);
            Assert.AreEqual(new[] { 12f, 68f, 176f, 436f },
                new[]
                {
                    offsets[0] + V6Lists.RowPaddingX,
                    offsets[1] + V6Lists.RowPaddingX,
                    offsets[2] + V6Lists.RowPaddingX,
                    offsets[3] + V6Lists.RowPaddingX,
                });
            // 合計 + padding 12×2 = 504 (SongRow 幅)。
            Assert.AreEqual(504f, 12f + 44f + 12f + 96f + 12f + 248f + 12f + 56f + 12f);
        }

        // --- 行内レイアウト (design 実測座標で右端が行幅を超えない) ---

        [Test]
        public void SongRow_InternalElements_StayInsideRow()
        {
            // design mcCOI: Level (12,14) 44×44 / Thumb (68,9) 96×54 /
            // Texts (176,2) 248×69 / Grade (436,18) 56×36。
            Assert.LessOrEqual(V6Lists.SongLevelX + V6Lists.SongLevelSize, V6Lists.SongThumbX,
                "Level must not overlap Thumb");
            Assert.LessOrEqual(V6Lists.SongThumbX + V6Lists.SongThumbW, V6Lists.SongTextsX,
                "Thumb must not overlap Texts");
            Assert.LessOrEqual(V6Lists.SongTextsX + V6Lists.SongTextsW, V6Lists.SongGradeX,
                "Texts must not overlap Grade");
            Assert.LessOrEqual(V6Lists.SongGradeX + V6Lists.SongGradeW, V6Lists.SongRowW,
                "Grade must stay inside the row");
            // Texts (Title + gap2 + Composer) が行高 72 に収まる。
            Assert.LessOrEqual(V6Lists.SongTextsY + V6Lists.SongTextsH, V6Lists.SongRowH,
                "Texts must stay inside the row height");
        }

        [Test]
        public void RankRow_InternalElements_StayInsideRow()
        {
            // design Hbxqf: Pos (12) 36 / Name (60) 160 / Value (232) 180 右寄せ / Grade (424) 56。
            Assert.LessOrEqual(V6Lists.RankPosX + V6Lists.RankPosSize, V6Lists.RankNameX,
                "Pos must not overlap Name");
            Assert.LessOrEqual(V6Lists.RankNameX + V6Lists.RankNameW, V6Lists.RankValueX,
                "Name must not overlap Value");
            Assert.LessOrEqual(V6Lists.RankValueX + V6Lists.RankValueW, V6Lists.RankGradeX,
                "Value must not overlap Grade");
            Assert.LessOrEqual(V6Lists.RankGradeX + V6Lists.RankGradeW, V6Lists.RankRowW,
                "Grade must stay inside the row");
        }

        [Test]
        public void PlayerRow_InternalElements_StayInsideRow()
        {
            // design tcgRX: Pos (12) 36 / Name (60) 160 / Value (232) 180 / Plays (424) 56。
            Assert.LessOrEqual(V6Lists.RankPosX + V6Lists.RankPosSize, V6Lists.RankNameX,
                "Pos must not overlap Name");
            Assert.LessOrEqual(V6Lists.RankNameX + V6Lists.RankNameW, V6Lists.RankValueX,
                "Name must not overlap Value");
            Assert.LessOrEqual(V6Lists.RankValueX + V6Lists.RankValueW, V6Lists.PlayerPlaysX,
                "Value must not overlap Plays");
            Assert.LessOrEqual(V6Lists.PlayerPlaysX + V6Lists.PlayerPlaysW, V6Lists.RankRowW,
                "Plays must stay inside the row");
        }

        // --- 契約値の写し (fs / 角丸 / 高さ) ---

        [Test]
        public void Tokens_MatchContractConstants()
        {
            Assert.AreEqual(4f, V6Tokens.RadiusSmall);
            Assert.AreEqual(8f, V6Tokens.RadiusMedium);
            Assert.AreEqual(16f, V6Tokens.RadiusLarge);
            Assert.AreEqual(56f, V6Tokens.ButtonHeight);
            Assert.AreEqual(36f, V6Tokens.KeyCapHeight);
        }

        [Test]
        public void Tokens_MatchContractColors()
        {
            Assert.AreEqual(new Color(1f, 1f, 1f), V6Tokens.Text);
            AssertColor(new Color(0x1B / 255f, 0x1B / 255f, 0x1B / 255f, 1f), V6Tokens.Key1);
            AssertColor(new Color(0x3A / 255f, 0x3A / 255f, 0x3A / 255f, 1f), V6Tokens.Key4);
            AssertColor(new Color(0x52 / 255f, 0x52 / 255f, 0x52 / 255f, 1f), V6Tokens.Key6);
            AssertColor(new Color(0x3F / 255f, 0xA9 / 255f, 0xF5 / 255f, 1f), V6Tokens.Accent);
            AssertColor(new Color(0f, 0f, 0f, 0x99 / 255f), V6Tokens.Scrim);
            AssertColor(new Color(0xED / 255f, 0xED / 255f, 0xED / 255f, 0x33 / 255f), V6Tokens.Border);
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1f / 255f, "r");
            Assert.AreEqual(expected.g, actual.g, 1f / 255f, "g");
            Assert.AreEqual(expected.b, actual.b, 1f / 255f, "b");
            Assert.AreEqual(expected.a, actual.a, 1f / 255f, "a");
        }

        [Test]
        public void Controls_UseContractFontSizes()
        {
            Assert.AreEqual(30f, V6Controls.ButtonFontSize);
            Assert.AreEqual(22f, V6Controls.KeyCapFontSize);
            Assert.AreEqual(22f, V6Controls.TagFontSize);
            Assert.AreEqual(32f, V6Controls.ButtonIconSize);
        }

        // --- テキスト幅の事前見積り (実行レイアウト前に確定しない preferredWidth の代替) ---

        [Test]
        public void EstimateTextWidth_ScalesWithFontSize()
        {
            float at22 = V6Layout.EstimateTextWidth("Space", 22f);
            float at44 = V6Layout.EstimateTextWidth("Space", 44f);
            Assert.AreEqual(at22 * 2f, at44, 0.001f);
            Assert.Greater(at22, 0f);
        }

        [Test]
        public void EstimateTextWidth_FullWidthCharsAreWiderThanAscii()
        {
            string ascii = "ab";
            string fullWidth = "ああ";
            Assert.Greater(
                V6Layout.EstimateTextWidth(fullWidth, 22f),
                V6Layout.EstimateTextWidth(ascii, 22f));
        }

        [Test]
        public void KeyCapFontSize_FitsContractRatioAndClampsToWidth()
        {
            // 高さ比基準: 36 × 22/36 = 22。
            Assert.AreEqual(22f, V6Ui.KeyCapFontSize(104f, 36f, "Space"));
            // 幅が狭い場合は幅フィットで縮む (利用幅 = 幅 - padding×2)。
            float narrow = V6Ui.KeyCapFontSize(40f, 36f, "Space");
            Assert.Less(narrow, 22f);
            Assert.GreaterOrEqual(narrow, 1f);
        }

        // --- 選択中の曲パネル Controls 行 (契約 §7) ---

        [Test]
        public void PanelControls_StayInsideContractPanel()
        {
            float[] offsets = V6Panel.ControlsOffsets();
            float total = V6Panel.SkipSize + V6Panel.ControlsGap + V6Panel.PlaySize + V6Panel.ControlsGap + V6Panel.SkipSize;

            Assert.GreaterOrEqual(offsets[0], 0f);
            Assert.LessOrEqual(offsets[2] + V6Panel.SkipSize, V6Panel.ControlsW);
            Assert.AreEqual((V6Panel.ControlsW - total) * 0.5f, offsets[0], 0.001f);
            // Controls 行 (512×88) はパネル (536×780) 内に収まる。
            Assert.LessOrEqual(12f + V6Panel.ControlsW, V6Panel.PanelW);
            Assert.LessOrEqual(668f + V6Panel.ControlsH, V6Panel.PanelH);
        }
    }
}
