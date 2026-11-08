using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>v6 Option 画面の純ロジック (V6OptionLogic) のテスト。</summary>
    public static class V6OptionLogicTests
    {
        [Test]
        public static void DesignScale_FitsThreeResolutions()
        {
            Assert.AreEqual(1f, V6OptionLogic.DesignScale(1728, 972), 0.0001f);
            Assert.AreEqual(1f, V6OptionLogic.DesignScale(1920, 1080), 0.0001f);
            // 16:10 は幅より高さが制約になる
            Assert.AreEqual(972f / 1200f, V6OptionLogic.DesignScale(1920, 1200), 0.0001f);
            Assert.AreEqual(1f, V6OptionLogic.DesignScale(0, 0)); // 異常系は等倍
        }

        [Test]
        public static void RowStep_LooseRowsFitPanel()
        {
            // 9 行 (design 06 Display の行数) を詰め行間で置いてもパネルに収まる
            float step = V6OptionLogic.RowStep(9);
            float bottom = V6OptionLogic.SettingsRowFirstY + V6OptionLogic.SettingsRowH + step * 8f;
            Assert.LessOrEqual(bottom, V6OptionLogic.SettingsPanelH);
        }

        [Test]
        public static void AdjustVolume_ClampsAndSteps()
        {
            Assert.AreEqual(1f, V6OptionLogic.AdjustVolume(0.95f, 1), 0.0001f);
            Assert.AreEqual(0f, V6OptionLogic.AdjustVolume(-0.1f, -1), 0.0001f);
            Assert.AreEqual(0.3f, V6OptionLogic.AdjustVolume(0.2f, 1), 0.0001f);
        }

        [Test]
        public static void FormatVolume_ShowsPercent()
        {
            Assert.AreEqual("100%", V6OptionLogic.FormatVolume(1f));
            Assert.AreEqual("30%", V6OptionLogic.FormatVolume(0.3f), "0.3 * 100 の誤差で 29% にならない");
            Assert.AreEqual("0%", V6OptionLogic.FormatVolume(0f));
        }

        [Test]
        public static void CycleFlashSpeed_LoopsThroughPowersOfTwo()
        {
            Assert.AreEqual(2, V6OptionLogic.CycleFlashSpeed(1, 1));
            Assert.AreEqual(8, V6OptionLogic.CycleFlashSpeed(4, 1));
            Assert.AreEqual(1, V6OptionLogic.CycleFlashSpeed(8, 1)); // 8 の次は 1 に戻る
            Assert.AreEqual(8, V6OptionLogic.CycleFlashSpeed(1, -1)); // 逆順
        }

        [Test]
        public static void DisplayLaneOf_MapsKeyBindingsLaneIndexToOneBased()
        {
            var table = new[]
            {
                new[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A, KeyCode.Z },
                new[] { KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X },
            };

            Assert.AreEqual(1, V6OptionLogic.DisplayLaneOf(KeyCode.Alpha1, table));
            Assert.AreEqual(1, V6OptionLogic.DisplayLaneOf(KeyCode.Q, table));
            Assert.AreEqual(2, V6OptionLogic.DisplayLaneOf(KeyCode.Alpha2, table));
            Assert.AreEqual(-1, V6OptionLogic.DisplayLaneOf(KeyCode.F12, table)); // 未割当
            Assert.AreEqual(-1, V6OptionLogic.DisplayLaneOf(KeyCode.None, null)); // 表無し
        }

        [Test]
        public static void MapKey_ConsumesOnlyExpectedActions()
        {
            Assert.AreEqual(V6OptionAction.MoveUp, V6OptionLogic.MapKey(false, true, false, false, false, false, false, false, false));
            Assert.AreEqual(V6OptionAction.AdjustNext, V6OptionLogic.MapKey(false, false, false, true, false, false, false, false, false));
            // モーダル中はタブ切替や行移動を消費しない (Confirm / Cancel だけ)
            Assert.AreEqual(V6OptionAction.Cancel, V6OptionLogic.MapKey(true, false, false, false, false, false, false, false, true));
            Assert.AreEqual(V6OptionAction.None, V6OptionLogic.MapKey(true, false, false, false, false, false, false, false, false));
        }

        [Test]
        public static void KeyDisplayName_MapsJisSymbols()
        {
            Assert.AreEqual("@", V6OptionLogic.KeyDisplayName(KeyCode.At));
            Assert.AreEqual("^", V6OptionLogic.KeyDisplayName(KeyCode.Caret));
            Assert.AreEqual("_", V6OptionLogic.KeyDisplayName(KeyCode.Underscore));
            Assert.AreEqual("Q", V6OptionLogic.KeyDisplayName(KeyCode.Q));
        }

        [Test]
        public static void JisGrid_CoversAllEightLanes()
        {
            // design の JIS キーボードは全 8 レーンの番号を含む (1..8)。
            // 既定配列の写し (KeyConfigUI.DefaultLayout と同じ。ずれは KeyConfigLayoutTests が検出する)。
            var defaultLayout = new[]
            {
                new[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A, KeyCode.Z },
                new[] { KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X },
                new[] { KeyCode.Alpha3, KeyCode.E, KeyCode.D, KeyCode.C },
                new[] { KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.R, KeyCode.T, KeyCode.F, KeyCode.G, KeyCode.V, KeyCode.B },
                new[] { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Y, KeyCode.U, KeyCode.H, KeyCode.J, KeyCode.N, KeyCode.M },
                new[] { KeyCode.Alpha8, KeyCode.I, KeyCode.K, KeyCode.Less, KeyCode.Comma },
                new[] { KeyCode.Alpha9, KeyCode.O, KeyCode.L, KeyCode.Greater, KeyCode.Period },
                new[] { KeyCode.Alpha0, KeyCode.Equals, KeyCode.Caret, KeyCode.Backslash, KeyCode.P, KeyCode.At, KeyCode.LeftBracket, KeyCode.Semicolon, KeyCode.Colon, KeyCode.RightBracket, KeyCode.Slash, KeyCode.Underscore, KeyCode.Minus, KeyCode.Asterisk, KeyCode.BackQuote }
            };
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var row in V6OptionLogic.JisGridKeys)
            {
                foreach (var key in row)
                {
                    int lane = V6OptionLogic.DisplayLaneOf(key, defaultLayout);
                    if (lane > 0)
                    {
                        seen.Add(lane);
                    }
                }
            }

            for (int lane = 1; lane <= 8; lane++)
            {
                Assert.IsTrue(seen.Contains(lane), $"レーン {lane} に届くキーが JIS グリッドに無い");
            }
        }

        [Test]
        public static void WrapIndex_WrapsForwardAndBackward()
        {
            // 剰余ベースの折り返し: 正負どちらの delta でも 0..count-1 に収まる
            const int count = 5;

            // 前方向の折り返し
            Assert.AreEqual(1, V6OptionLogic.WrapIndex(0, count, 1));
            Assert.AreEqual(2, V6OptionLogic.WrapIndex(0, count, 2));
            Assert.AreEqual(0, V6OptionLogic.WrapIndex(0, count, 5));
            Assert.AreEqual(1, V6OptionLogic.WrapIndex(0, count, 6));
            Assert.AreEqual(3, V6OptionLogic.WrapIndex(4, count, 4)); // 4+4=8→3

            // 後方向の折り返し
            Assert.AreEqual(4, V6OptionLogic.WrapIndex(0, count, -1));
            Assert.AreEqual(3, V6OptionLogic.WrapIndex(0, count, -2));
            Assert.AreEqual(0, V6OptionLogic.WrapIndex(0, count, -5));
            Assert.AreEqual(4, V6OptionLogic.WrapIndex(0, count, -6));
            Assert.AreEqual(1, V6OptionLogic.WrapIndex(1, count, -5)); // 1-5=-4→1

            // 大きな delta
            Assert.AreEqual(2, V6OptionLogic.WrapIndex(0, count, 102));
            Assert.AreEqual(2, V6OptionLogic.WrapIndex(0, count, -98));
        }

        [Test]
        public static void WrapIndex_HandlesEdgeCases()
        {
            Assert.AreEqual(0, V6OptionLogic.WrapIndex(0, 0, 5));   // count=0
            Assert.AreEqual(0, V6OptionLogic.WrapIndex(0, -1, 5));  // count<0
            Assert.AreEqual(0, V6OptionLogic.WrapIndex(5, 1, 10));  // count=1
            Assert.AreEqual(0, V6OptionLogic.WrapIndex(-3, 1, -7)); // count=1, 負 index
        }
    }
}
