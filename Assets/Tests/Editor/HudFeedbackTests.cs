using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Playfield;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// 2026-11-12 のオーナー指摘に対応する表示ロジックのテスト。
    ///
    /// - 判定は判定線の位置に出現し、上へ移動して読み取り時間を置き、
    ///   最後は下へ落ちながら消える (Issue #731 / design §10.3。
    ///   旧 #671 の「上方から落下して静止位置へ着地」は廃止)。
    /// - コンボもスコアと同じ時間割でポップする。
    /// - 歌詞スロットは文字に合わせて大きさが決まる (箱だけ大きくしない)。
    /// </summary>
    public class HudFeedbackTests
    {
        // --- 判定ポップアップ (Issue #731 / design §10.3: 上昇 + 保持 + 下方退場) ---

        /// <summary>
        /// 境界・中間時刻での軌道。判定確定 t=0 を原点に、
        /// 判定線上に出現 → EaseOutCubic で頂点まで上昇 → 頂点で保持 →
        /// EaseInCubic で加速しながら判定線を割って下へ落下 + フェード。
        /// </summary>
        [Test]
        public void VerdictPose_RisesFromJudgeLine_Holds_FallsBelow()
        {
            var m = V6HudMetrics.Fhd(1f);
            float peak = V6HudLogic.VerdictPeakGapAboveLine;
            float exitDrop = V6HudLogic.VerdictExitDropPx(m);

            // t=0: 判定線の位置 (オフセット 0) に即時出現、不透明。
            var p0 = V6HudLogic.VerdictPoseAt(0f, m);
            Assert.AreEqual(0f, p0.OffsetPx, 0.001f,
                "判定ポップは判定線の位置から始まる");
            Assert.AreEqual(1f, p0.Alpha, 0.001f);

            // 上昇フェーズ: オフセットは 0 → peak へ単調増加 (下から上へ動く)。
            float prev = 0f;
            for (int i = 1; i <= 10; i++)
            {
                float t = V6HudLogic.VerdictRiseSeconds * i / 10f;
                float off = V6HudLogic.VerdictPoseAt(t, m).OffsetPx;
                Assert.Greater(off, prev, $"上昇フェーズは単調に上へ進む (t={t:F3})");
                Assert.LessOrEqual(off, peak + 0.001f, "頂点を超えない");
                prev = off;
            }

            // 上昇の終端と保持フェーズ: 頂点 (peak) に留まる。
            Assert.AreEqual(peak,
                V6HudLogic.VerdictPoseAt(V6HudLogic.VerdictRiseSeconds, m).OffsetPx, 0.001f);
            Assert.AreEqual(peak,
                V6HudLogic.VerdictPoseAt(
                    V6HudLogic.VerdictRiseSeconds + V6HudLogic.VerdictHoldSeconds * 0.5f, m)
                    .OffsetPx, 0.001f,
                "保持フェーズは頂点で静止する");

            // 下方退場フェーズ: peak から単調減少し、終端は判定線より下 (負)。
            float fallStart = V6HudLogic.VerdictRiseSeconds + V6HudLogic.VerdictHoldSeconds;
            prev = peak;
            for (int i = 1; i <= 10; i++)
            {
                float t = fallStart + V6HudLogic.VerdictFallSeconds * i / 10f;
                float off = V6HudLogic.VerdictPoseAt(t, m).OffsetPx;
                Assert.Less(off, prev, $"退場フェーズは単調に下へ進む (t={t:F3})");
                prev = off;
            }

            var pEnd = V6HudLogic.VerdictPoseAt(V6HudLogic.VerdictTotalSeconds, m);
            Assert.AreEqual(-exitDrop, pEnd.OffsetPx, 0.001f,
                "退場の終端は判定線より下 (exitDrop 分)");
            Assert.Less(pEnd.OffsetPx, 0f,
                "下方退場は判定線を割って下へ落ちる");
            Assert.AreEqual(0f, pEnd.Alpha, 0.001f, "寿命末では消えている");

            // 総寿命を過ぎても透明のまま (オフセットは終端に留まる)。
            var pAfter = V6HudLogic.VerdictPoseAt(V6HudLogic.VerdictTotalSeconds + 1f, m);
            Assert.AreEqual(0f, pAfter.Alpha, 0.001f);

            // EaseIn の加速: 退場の中間点ではまだ頂点に近く、
            // 終盤に大きく進む (線形なら中間で travel の半分を進むが、
            // EaseInCubic(0.5)=0.125 なので進行は 1/8 に留まる)。
            float midFall = V6HudLogic.VerdictPoseAt(fallStart + V6HudLogic.VerdictFallSeconds * 0.5f, m)
                .OffsetPx;
            float travel = peak + exitDrop;
            float expectedMid = peak - travel * 0.125f;
            Assert.AreEqual(expectedMid, midFall, 0.01f,
                "退場は EaseInCubic (中間点の進行は 12.5%)");
        }

        /// <summary>
        /// 反例ガード: 旧軌道 (Issue #671「静止位置の真上から落下して着地 →
        /// 静止 → フェード」) が復活しないことを拒否する。
        /// 旧軌道の特徴は「出現時に既に上空 (オフセット正大) にいて、
        /// 時間とともにオフセットが減少し 0 (静止位置) で止まる」こと。
        /// 新軌道は t=0 で判定線上 (0) から始まり、序盤は上へ増加する。
        /// </summary>
        [Test]
        public void VerdictPose_RejectsOldSpawnHighFallToRestPath()
        {
            var m = V6HudMetrics.Fhd(1f);

            // 旧実装は出現時点で既に「静止位置より上」(正の大きなオフセット)
            // にいた。新契約では判定線上 (0) が出発点。
            Assert.AreEqual(0f, V6HudLogic.VerdictPoseAt(0f, m).OffsetPx, 0.001f,
                "判定ポップが判定線より上の空中から出現してはいけない (旧軌道)");
            Assert.AreEqual(0f, V6HudLogic.VerdictPoseAt(-0.5f, m).OffsetPx, 0.001f,
                "判定前の負の経過も判定線上として扱う");

            // 旧軌道は序盤にオフセットが減少した (上→下への落下)。
            // 新軌道は序盤に増加する (判定線→上への上昇)。
            float early1 = V6HudLogic.VerdictPoseAt(0.03f, m).OffsetPx;
            float early2 = V6HudLogic.VerdictPoseAt(0.09f, m).OffsetPx;
            Assert.Greater(early2, early1,
                "序盤は判定線から上へ移動する (旧軌道の落下ではない)");

            // 旧軌道では寿命の終盤もオフセットは 0 (静止位置) のまま
            // フェードだけが起きた。新軌道は判定線より下 (負) まで落ちる。
            float late = V6HudLogic.VerdictPoseAt(
                V6HudLogic.VerdictTotalSeconds - 0.02f, m).OffsetPx;
            Assert.Less(late, 0f,
                "終盤は判定線を割って下へ落ちている (旧軌道の静止保持ではない)");

            // 旧軌道の「静止位置 + 落下距離」の出現高さ (peak + VerdictH 相当の
            // 上空出発) を新軌道が取らないことも確認する。
            float oldAppearOffset = V6HudLogic.VerdictPeakGapAboveLine
                + V6HudLogic.VerdictExitDropPx(m);
            for (int i = 0; i <= 20; i++)
            {
                float t = V6HudLogic.VerdictTotalSeconds * i / 20f;
                float off = V6HudLogic.VerdictPoseAt(t, m).OffsetPx;
                Assert.IsFalse(System.Math.Abs(off - oldAppearOffset) < 0.5f,
                    $"旧軌道の出現高さ ({oldAppearOffset:F0}px 上) を通らない (t={t:F2}, off={off:F1})");
            }
        }

        [Test]
        public void VerdictAlpha_IsOpaqueUntilExitFade()
        {
            var m = V6HudMetrics.Fhd(1f);

            // 上昇・保持フェーズは常に不透明 (フェードインしない)。
            Assert.AreEqual(1f, V6HudLogic.VerdictPoseAt(0f, m).Alpha, 0.001f);
            Assert.AreEqual(1f,
                V6HudLogic.VerdictPoseAt(
                    V6HudLogic.VerdictRiseSeconds + V6HudLogic.VerdictHoldSeconds, m)
                    .Alpha, 0.001f,
                "下方退場の開始時点でもまだ不透明");

            // 退場フェーズは 1→0 に線形で下がる。
            float fallMid = V6HudLogic.VerdictPoseAt(
                V6HudLogic.VerdictRiseSeconds + V6HudLogic.VerdictHoldSeconds
                    + V6HudLogic.VerdictFallSeconds * 0.5f, m).Alpha;
            Assert.AreEqual(0.5f, fallMid, 0.001f);

            // 寿命末とそれ以降は透明。
            Assert.AreEqual(0f,
                V6HudLogic.VerdictPoseAt(V6HudLogic.VerdictTotalSeconds, m).Alpha, 0.001f);
            Assert.AreEqual(0f,
                V6HudLogic.VerdictPoseAt(V6HudLogic.VerdictTotalSeconds + 0.5f, m).Alpha,
                0.001f);
        }

        // --- ポップ・パンチ (Issue #434) ---

        [Test]
        public void PunchScalePopsUpAndSettles()
        {
            Assert.AreEqual(1f, V6HudLogic.PunchScale(-1f, 1.1f, 0.05f, 0.2f), 0.001f);
            Assert.Greater(V6HudLogic.PunchScale(0.02f, 1.1f, 0.05f, 0.2f), 1f, "立ち上がりで拡大する");
            Assert.AreEqual(1f, V6HudLogic.PunchScale(0.2f, 1.1f, 0.05f, 0.2f), 0.001f,
                "全体時間を過ぎたら等倍へ戻る");
            Assert.AreEqual(1f, V6HudLogic.PunchScale(1f, 1.1f, 0.05f, 0.2f), 0.001f);
        }

        [Test]
        public void ApproachConvergesToTarget()
        {
            float v = 0f;
            for (int i = 0; i < 120; i++)
            {
                v = V6HudLogic.Approach(v, 100f, 1f / 60f, 10f);
            }

            Assert.AreEqual(100f, v, 0.5f, "指数 tween は目標へ収束する");
        }


        // --- 桁別ポップ (Issue #445) ---

        [Test]
        public void DigitPopIsShortAndSettles()
        {
            // オーナー指摘「効果が出てから消えるまでが無駄に長い」への対応で、
            // 桁ポップは 0.25 秒以内に必ず終わる。
            Assert.LessOrEqual(V6HudLogic.DigitPopTotal, 0.25f);
            Assert.Less(V6HudLogic.DigitPopAttack, V6HudLogic.DigitPopTotal);
            Assert.AreEqual(1f, V6HudLogic.PunchScale(V6HudLogic.DigitPopTotal,
                V6HudLogic.DigitPopPeak, V6HudLogic.DigitPopAttack, V6HudLogic.DigitPopTotal), 0.001f);
            Assert.Greater(V6HudLogic.PunchScale(0.02f,
                V6HudLogic.DigitPopPeak, V6HudLogic.DigitPopAttack, V6HudLogic.DigitPopTotal), 1f,
                "立ち上がりで拡大する");
        }

        [Test]
        public void VerdictLifeIsLongEnoughToRead()
        {
            // Issue #731: 上昇・保持・下方退場の 3 フェーズ合計が総寿命。
            // 保持フェーズは読み取りに必要な 0.4 秒以上を確保する。
            Assert.GreaterOrEqual(V6HudLogic.VerdictTotalSeconds, 0.5f);
            Assert.LessOrEqual(V6HudLogic.VerdictTotalSeconds, 1.0f + 0.0001f);
            Assert.AreEqual(
                V6HudLogic.VerdictRiseSeconds + V6HudLogic.VerdictHoldSeconds
                    + V6HudLogic.VerdictFallSeconds,
                V6HudLogic.VerdictTotalSeconds, 0.001f);
            Assert.GreaterOrEqual(V6HudLogic.VerdictHoldSeconds, 0.4f,
                "頂点での読み取り時間は 0.4 秒以上");

            // フェーズの順序: 上昇 → 保持 → 下方退場。
            // 退場は保持より短く、上昇は保持より短い (素早い出入り)。
            Assert.Less(V6HudLogic.VerdictRiseSeconds, V6HudLogic.VerdictHoldSeconds);
            Assert.Less(V6HudLogic.VerdictFallSeconds, V6HudLogic.VerdictHoldSeconds);

            // 頂点は判定線のすぐ上 (読み取りに十分で、離れすぎない)。
            Assert.Greater(V6HudLogic.VerdictPeakGapAboveLine, 0f);
            Assert.Less(V6HudLogic.VerdictPeakGapAboveLine, 200f);
        }

        // --- 桁別アニメーションの差分 (Issue #445) ---

        [Test]
        public void ChangedCharFlags_OnlyChangedDigitsAnimate()
        {
            // オーナー例: 138 → 140 なら下 2 桁だけが動く。
            bool[] flags = V6HudLogic.ChangedCharFlags("138", "140");
            Assert.AreEqual(3, flags.Length);
            Assert.IsFalse(flags[0], "上の桁 (1) は変わらないので動かない");
            Assert.IsTrue(flags[1], "十の位 3→4 は動く");
            Assert.IsTrue(flags[2], "一の位 8→0 は動く");
        }

        [Test]
        public void ChangedCharFlags_SeparatorNeverAnimates()
        {
            // "999" → "1,000": ',' が新しく現れる位置は静的なのでアニメーションしない。
            bool[] flags = V6HudLogic.ChangedCharFlags("999", "1,000");
            Assert.AreEqual(',', "1,000"[1]);
            Assert.IsFalse(flags[1], "新しく現れた ',' はアニメーションしない");
            Assert.IsTrue(flags[0], "1 が現れた桁は動く");
        }

        [Test]
        public void ChangedCharFlags_RemovedCharsDoNotAnimate()
        {
            // 短くなった側 (消えた桁) はアニメーションしない。
            bool[] flags = V6HudLogic.ChangedCharFlags("100", "99");
            Assert.AreEqual(3, flags.Length);
            Assert.IsTrue(flags[0]);
            Assert.IsTrue(flags[1]);
            Assert.IsFalse(flags[2], "消えた桁は動かない");
        }

        [Test]
        public void ChangedCharFlags_SameString_HasNoChanges()
        {
            bool[] flags = V6HudLogic.ChangedCharFlags("123", "123");
            Assert.IsFalse(flags[0] || flags[1] || flags[2]);
        }

        [Test]
        public void ComboValueText_ZeroIsEmpty()
        {
            // Issue #445: コンボ 0 はコンボではないので表示自体を隠す。
            Assert.AreEqual("", V6HudLogic.ComboValueText(0));
            Assert.AreEqual("", V6HudLogic.ComboValueText(-1));
            Assert.AreEqual("1", V6HudLogic.ComboValueText(1));
            Assert.AreEqual("140", V6HudLogic.ComboValueText(140));
        }

        // --- 歌詞スロット (Issue #433) ---

        [Test]
        public void LyricBoxIsSizedToItsTextNotToADesignBlanket()
        {
            // design の 220 (3 行構成) より、本文 1 行 + Start Prompt の高さが小さい。
            float boxHeight = V6HudLogic.LyricContentHeight(48f);

            Assert.Less(boxHeight, V6HudLogic.LyricDesignHeight,
                "箱は design の空白を含めた高さより小さく、文字に合わせる");
            Assert.Greater(boxHeight, 48f, "歌詞 1 行は必ず収まる");
        }

        [Test]
        public void LyricBoxKeepsRoomForTheLyricRowAndPrompt()
        {
            // Issue #433: 歌詞本文 1 行 + Start Prompt (KeyCap 36) が収まる高さ。
            // Issue #582: 行高係数・padding の正本は V6HudLogic の定数。
            float height = V6HudLogic.LyricContentHeight(48f);
            float lyricLine = 48f * V6HudLogic.LyricLineHeightFactor;
            float expected = Mathf.Max(lyricLine, V6HudLogic.ReadyPromptCapHeight)
                + V6HudLogic.LyricPaddingY * 2f;

            Assert.AreEqual(expected, height, 0.001f);
            Assert.GreaterOrEqual(height, V6HudLogic.ReadyPromptCapHeight + V6HudLogic.LyricPaddingY * 2f,
                "Start Prompt (KeyCap) が収まる");
        }

        [Test]
        public void LyricWidthIsTheFrameColumnSpan()
        {
            // Issue #433: 幅は共有フレームのメイン領域 6 列グリッド中央 4 列
            // = 1120 を 1:1 で使う (高さ合わせで拡大しない)。
            var size = V6HudLogic.LyricSize(48f);

            Assert.AreEqual(V6HudLogic.LyricDesignWidth, size.x, 0.01f);
        }

        [Test]
        public void LargerTextMakesABiggerBox()
        {
            float small = V6HudLogic.LyricContentHeight(36f);
            float large = V6HudLogic.LyricContentHeight(48f);

            Assert.Greater(large, small, "フォントが大きければ箱も大きくなる");
        }

        // --- 歌詞スロットの共有寸法 (Issue #674) ---

        [Test]
        public void LyricSlotHeight_IsTheMaxNeededAcrossAllStates()
        {
            // Issue #674: 歌詞・準備 (Start Prompt)・カウントダウン・曲終了の
            // 全状態でパネルの高さが変わらないよう、共有スロット高は
            // 3 状態の最大必要高。各解像度プロファイルの実高で検証する
            // (lyricFs はプロファイルの LyricFs: fhd=36 / 2k・mac=48)。
            var cases = new (float height, float lyricFs)[]
            {
                (1080f, 36f),
                (1440f, 48f),
                (1664f, 48f),
            };

            foreach (var (h, fs) in cases)
            {
                float slot = V6HudLogic.LyricSlotHeight(fs, h);
                float lyricNeed = V6HudLogic.LyricContentHeight(fs);
                float countdownNeed =
                    V6HudLogic.CountdownFontSize(h) * V6HudLogic.CountdownLineFit;
                float endNeed = V6HudLogic.SongEndTitleFontSize(h / V6LaneGeometry.DesignHeight)
                    * V6HudLogic.SongEndTitleLineFit;

                Assert.AreEqual(
                    Mathf.Max(lyricNeed, Mathf.Max(countdownNeed, endNeed)), slot, 0.001f,
                    $"height {h}: 共有スロット高は最大必要高");
                Assert.GreaterOrEqual(slot, lyricNeed, $"height {h}: 歌詞本文が収まる");
                Assert.GreaterOrEqual(slot, countdownNeed, $"height {h}: カウントダウン数字が収まる");
                Assert.GreaterOrEqual(slot, endNeed, $"height {h}: 終了タイトルが収まる");
            }
        }

        [Test]
        public void LyricSlotSize_IsSharedByEveryState()
        {
            // 幅は LyricSlotWidth、高さは LyricSlotHeight の 1 系統だけ。
            // 状態ごとの分岐が無いことを、同じ入力で常に同じ (width,height)
            // が返ることで固定する。
            var size = V6HudLogic.LyricSlotSize(36f, 1920f, 1080f);
            Assert.AreEqual(V6HudLogic.LyricSlotWidth(1920f), size.x, 0.001f);
            Assert.AreEqual(V6HudLogic.LyricSlotHeight(36f, 1080f), size.y, 0.001f);
            Assert.AreEqual(size, V6HudLogic.LyricSlotSize(36f, 1920f, 1080f));
        }

        // --- キーガイド Full モード (Issue #658) ---

        [Test]
        public void GuideKeyText_FullListsEveryBoundKey()
        {
            PlaySession.SetKeyGuide(KeyGuideMode.Full);
            try
            {
                for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
                {
                    var keys = KeyBindings.GetKeys(lane);
                    var expected = string.Join(" ", System.Array.ConvertAll(keys, KeyGuideUI.GetKeyDisplayName));
                    Assert.AreEqual(expected, V6HudLogic.GuideKeyText(lane),
                        $"lane {lane}: Full は全有効キーを空白区切りで出す");
                }
            }
            finally
            {
                PlaySession.SetKeyGuide(KeyGuideMode.Compact);
            }
        }

        [Test]
        public void GuideKeyText_CompactKeepsRepresentativeKey()
        {
            PlaySession.SetKeyGuide(KeyGuideMode.Compact);
            for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
            {
                Assert.AreEqual(V6HudLogic.RepresentativeKeyText(KeyBindings.GetKeys(lane)),
                    V6HudLogic.GuideKeyText(lane), $"lane {lane}: Compact は代表キーのみ");
            }
        }

        [Test]
        public void GuideKeyFontSize_ShrinksOnlyWhenLabelOverflows()
        {
            const float designFs = 32f;
            const float capW = 75f;
            const float padding = 10f;

            // 代表キー 1 個は design fs のまま。
            Assert.AreEqual(designFs,
                V6HudLogic.GuideKeyFontSize(capW, designFs, "A", padding));

            // 全キー列挙は幅に収まるよう縮むが 1 未満にはならない。
            string full = string.Join(" ", System.Array.ConvertAll(
                KeyBindings.GetKeys(7), KeyGuideUI.GetKeyDisplayName));
            float shrunk = V6HudLogic.GuideKeyFontSize(capW, designFs, full, padding);
            Assert.Less(shrunk, designFs, "長いラベルは design fs より小さくなる");
            Assert.GreaterOrEqual(shrunk, 1f);
        }
        // --- 判定アンカー (Issue #715: 「判定線」は描画される線が正本) ---

        [Test]
        public void LegacyJudgeLineAnchor_IsTheDrawnJudgeLine_NotTheEffectPoint()
        {
            // design §10.3 / v6-screen-contract §8 の「判定線」は描画される線。
            // GameScene の JudgeLine は LineRenderer ローカル座標
            // (-4, 0.5, 0) → (4, 0.5, 0) だが、GameObject の Transform が
            // (0, -0.02, 0)・scale (8, 1, 40) なのでワールドでは
            // y = 0.48 / z = 0 に描かれる (x は scale 8 で画面全幅)。
            Assert.AreEqual(0.48f, Judge.JudgeLineWorldY, 0.0001f,
                "GameScene JudgeLine の実ワールド y (local 0.5 + transform -0.02)");
            Assert.AreEqual(0f, Judge.JudgeLineWorldZ, 0.0001f);

            Vector3 anchor = V6HudLogic.LegacyJudgeLineAnchor();
            Assert.AreEqual(0f, anchor.x, 0.0001f);
            Assert.AreEqual(Judge.JudgeLineWorldY, anchor.y, 0.0001f);
            Assert.AreEqual(Judge.JudgeLineWorldZ, anchor.z, 0.0001f);

            // 反例ガード: 判定エフェクト出現点 (y 0.76 / z 0.15 =
            // Judge.EffectPosition) への回帰を拒否する。旧アンカーは
            // 描画判定線より画面上で約 50px (FHD) 上にずれていた (Issue #715)。
            Assert.IsTrue(anchor.y < 0.76f || anchor.z < 0.15f,
                "verdict anchor must be the drawn judge line, not Judge.EffectPosition");

            // v6 プレイフィールド経路の判定線も描画値 (design y=792) を使う。
            Assert.AreEqual(792f, V6LaneGeometry.JudgeLineY, 0.001f);
        }

        [Test]
        public void LegacyVerdictPeak_SitsAboveDrawnLine_UnderGameCamera()
        {
            // GameScene Main Camera と同じ姿勢・画角
            // (V6TopShadeHoleTests.CreateLegacyCamera と同じ値)。
            var camGo = new GameObject("HudFeedbackTests Camera");
            camGo.tag = "MainCamera";
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(
                    new Vector3(0f, 4.9f, -0.8f), Quaternion.Euler(54.8f, 0f, 0f));
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 1000f;

                Vector3 lineVp = cam.WorldToViewportPoint(V6HudLogic.LegacyJudgeLineAnchor());
                Vector3 effectVp = cam.WorldToViewportPoint(
                    new Vector3(Judge.LaneWorldX(0), 0.76f, 0.15f));
                Assert.Greater(lineVp.z, 0f, "judge line anchor must be in front of the camera");
                Assert.Greater(effectVp.z, 0f);

                // エフェクト点は描画判定線より画面上 (viewport y 大) に投影される。
                // Issue #715 の「約 50px (FHD) ≒ viewport 0.046」のずれを固定し、
                // 旧アンカーへの回帰 (ずれの消失 or 逆転) を検出する。
                Assert.Greater(effectVp.y - lineVp.y, 0.02f,
                    "effect point must project visibly above the drawn judge line " +
                    "(Issue #715: it is ~50px too high at FHD)");

                // 上昇の頂点 = 描画判定線の投影 + VerdictPeakGapAboveLine
                // (72px、画面下原点)。アンカーが描画線そのものなので、
                // 頂点は常に線の真上 72px — design §10.3 の契約そのもの
                // (Issue #731: 「静止位置」ではなく上昇の頂点)。
                Assert.AreEqual(72f, V6HudLogic.VerdictPeakGapAboveLine, 0.001f);
                float screenH = Mathf.Max(1f, Screen.height);
                float lineFromBottom = lineVp.y * screenH;
                float peakFromBottom = lineFromBottom + V6HudLogic.VerdictPeakGapAboveLine;
                Assert.Greater(peakFromBottom, lineFromBottom);
                Assert.Less(peakFromBottom, screenH,
                    "verdict peak position must stay on screen");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

    }
}
