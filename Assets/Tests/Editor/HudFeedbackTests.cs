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
    /// - 判定は画面上端 (ノーツの出現位置) から判定線のすぐ上へ落ちてきて、
    ///   十分な時間静止表示され自然に消える
    ///   (Issue #671 / design §10.3。#605 の滞在時間は維持)。
    /// - コンボもスコアと同じ時間割でポップする。
    /// - 歌詞スロットは文字に合わせて大きさが決まる (箱だけ大きくしない)。
    /// </summary>
    public class HudFeedbackTests
    {
        // --- 判定ポップアップ (Issue #671 / design §10.3: 落下 + 静止保持 + フェード) ---

        [Test]
        public void VerdictAppearsImmediatelyThenFadesOut()
        {
            const float total = V6HudLogic.VerdictTotalSeconds;

            // §10.3: 出現は即時 (フェードインしない)。残りが十分あれば不透明。
            Assert.AreEqual(1f, V6HudLogic.VerdictAlpha(total), 0.001f);
            Assert.AreEqual(1f, V6HudLogic.VerdictAlpha(V6HudLogic.VerdictFadeOutSeconds), 0.001f,
                "フェードアウト開始時点 (= 残り 220ms) でもまだ不透明");

            // フェードアウト: 残り半分で 0.5、寿命末で 0。
            Assert.AreEqual(0.5f,
                V6HudLogic.VerdictAlpha(V6HudLogic.VerdictFadeOutSeconds * 0.5f), 0.01f);
            Assert.AreEqual(0f, V6HudLogic.VerdictAlpha(0f), 0.001f);

            // 負の残りは常に透明。
            Assert.AreEqual(0f, V6HudLogic.VerdictAlpha(-1f), 0.001f);
        }

        [Test]
        public void VerdictFallsLocallyAboveRestAndSettles()
        {
            // Issue #671: 判定は静止位置 (判定線 + VerdictRestGapAboveLine の直上) の
            // 真上 VerdictFallDistancePx に即時出現し、VerdictFallSeconds で
            // 静止位置へ落ちる。距離は画面高さに比例しない固定値 — ノーツの
            // 全画面落下と判定フィードバックを区別する (実機レビュー指摘)。
            // 落下距離は判定ボックス高さ (design メトリクス由来)。FHD で評価する。
            float dist = V6HudLogic.VerdictFallDistancePx(V6HudMetrics.Fhd(1f));
            Assert.AreEqual(0.30f, V6HudLogic.VerdictFallSeconds, 0.0001f);

            Assert.AreEqual(dist, V6HudLogic.VerdictFallOffset(0f, dist), 0.001f,
                "出現時は静止位置より fallDistance だけ上");
            Assert.AreEqual(0f, V6HudLogic.VerdictFallOffset(V6HudLogic.VerdictFallSeconds, dist), 0.001f,
                "VerdictFallSeconds で静止位置へ到達する");
            Assert.AreEqual(0f, V6HudLogic.VerdictFallOffset(1f, dist), 0.001f,
                "落下完了後は静止位置に留まる");

            // 落下中は単調減少 (上 → 静止位置の一方向)。
            float prev = dist;
            for (int i = 1; i <= 15; i++)
            {
                float off = V6HudLogic.VerdictFallOffset(V6HudLogic.VerdictFallSeconds * i / 15f, dist);
                Assert.Less(off, prev, "落下は一方向に進む");
                prev = off;
            }

            // EaseOut 到達: 時間の中間で残り距離は半分より小さい (序盤に大きく進む)。
            float half = V6HudLogic.VerdictFallOffset(V6HudLogic.VerdictFallSeconds * 0.5f, dist);
            Assert.Less(half, dist * 0.5f,
                "EaseOut は序盤に大きく進み終盤で減速する");
        }

        [Test]
        public void VerdictFallDistanceStaysLocalToJudgeLine()
        {
            // 反例ガード: 判定テキストが画面上端 (ノーツの出現位置) から
            // 落ちる実装への再導入を拒否する。落下距離は判定線付近の
            // ローカル距離でなければならず、画面高さ (FHD 1080px) に対して
            // 十分小さいこと。
            // 落下距離は判定ボックス高さそのもの — 文字1個分の drop-in。
            // 本番と同じ ForResolution 経路で全プロファイルを確認し、
            // 画面高さやノーツ経路に由来しないことを固定する。
            foreach (var res in new[] {
                new Vector2(1920f, 1080f), new Vector2(2560f, 1440f), new Vector2(2560f, 1664f) })
            {
                var mm = V6HudMetrics.ForResolution(res.x, res.y);
                Assert.AreEqual(mm.VerdictH,
                    V6HudLogic.VerdictFallDistancePx(mm), 0.001f,
                    "落下距離は判定ボックス高さに一致する (実装導出値ではなく design メトリクス)");
                Assert.Greater(mm.VerdictH, 0f);
                Assert.Less(mm.VerdictH, 200f,
                    "落下距離は判定線付近に局所化する (画面上端からの全距離落下は誤り)");
            }

            // 出現位置 = 静止位置 + VerdictH は、判定線から見て十分近い。
            var fhd = V6HudMetrics.ForResolution(1920f, 1080f);
            float appearFromBottom =
                V6HudLogic.VerdictRestGapAboveLine + fhd.VerdictH;
            Assert.Less(appearFromBottom, 400f,
                "出現位置は判定線のすぐ上の領域に留まる");
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
            // Issue #671: 落下を独立フェーズとして足したため総寿命は延びるが、
            // 静止保持は読み取りに必要な 0.4 秒以上を確保する。
            Assert.GreaterOrEqual(V6HudLogic.VerdictTotalSeconds, 0.5f);
            Assert.LessOrEqual(V6HudLogic.VerdictTotalSeconds, 1.0f + 0.0001f);
            Assert.AreEqual(
                V6HudLogic.VerdictFallSeconds + V6HudLogic.VerdictHoldSeconds + V6HudLogic.VerdictFadeOutSeconds,
                V6HudLogic.VerdictTotalSeconds, 0.001f);
            Assert.GreaterOrEqual(V6HudLogic.VerdictHoldSeconds, 0.4f,
                "静止位置での読み取り時間は 0.4 秒以上");

            // §10.3: フェードアウトは寿命の最後 220ms。
            Assert.AreEqual(0.22f, V6HudLogic.VerdictFadeOutSeconds, 0.0001f);
            // 落下は保持時間より短く、フェードアウトと重ならない。
            Assert.LessOrEqual(V6HudLogic.VerdictFallSeconds, V6HudLogic.VerdictHoldSeconds);
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
            FlashBeat.UI.V5.PlaySession.SetKeyGuide(FlashBeat.UI.V5.KeyGuideMode.Full);
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
                FlashBeat.UI.V5.PlaySession.SetKeyGuide(FlashBeat.UI.V5.KeyGuideMode.Compact);
            }
        }

        [Test]
        public void GuideKeyText_CompactKeepsRepresentativeKey()
        {
            FlashBeat.UI.V5.PlaySession.SetKeyGuide(FlashBeat.UI.V5.KeyGuideMode.Compact);
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
        public void LegacyVerdictRest_SitsAboveDrawnLine_UnderGameCamera()
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

                // 静止位置 = 描画判定線の投影 + VerdictRestGapAboveLine (72px、
                // 画面下原点)。アンカーが描画線そのものなので、静止位置は
                // 常に線の真上 72px — design §10.3 の契約そのもの。
                Assert.AreEqual(72f, V6HudLogic.VerdictRestGapAboveLine, 0.001f);
                float screenH = Mathf.Max(1f, Screen.height);
                float lineFromBottom = lineVp.y * screenH;
                float restFromBottom = lineFromBottom + V6HudLogic.VerdictRestGapAboveLine;
                Assert.Greater(restFromBottom, lineFromBottom);
                Assert.Less(restFromBottom, screenH,
                    "verdict rest position must stay on screen");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

    }
}
