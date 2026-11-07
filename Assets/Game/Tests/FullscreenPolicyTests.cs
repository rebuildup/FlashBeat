using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// Issue #169: フルスクリーン方針とプロファイル契約の固定テスト (EditMode)。
    /// - ProjectSettings: FullScreenWindow / defaultIsNativeResolution=1 / resizableWindow=0
    ///   (ウィンドウリサイズによる擬似全画面を許さない — Issue #162 オーナー指示)。
    /// - Assets/Game/Scripts 配下に Screen.SetResolution が 1 つも無いこと
    ///   (ウィンドウリサイズでの擬似全画面の混入防止)。
    /// - design/ui-size-profiles.json の mac.screen == [2560,1664] かつ
    ///   UISizeProfile.Mac() の値が json と一致すること (json↔C# 一致の固定)。
    /// 実行は Supervisor が TestRunner (EditMode) で行う。
    /// </summary>
    public class FullscreenPolicyTests
    {
        private static string ProjectRoot()
        {
            // Application.dataPath = <project>/Assets。ProjectSettings / design はその兄弟。
            return Path.Combine(Application.dataPath, "..");
        }

        private static string ReadProjectSettings()
        {
            var path = Path.Combine(ProjectRoot(), "ProjectSettings", "ProjectSettings.asset");
            Assert.IsTrue(File.Exists(path), "ProjectSettings.asset が読めない: " + path);
            return File.ReadAllText(path);
        }

        private static int SettingsInt(string settings, string key)
        {
            var m = new Regex(@"^\s*" + key + @":\s*(-?\d+)\s*$", RegexOptions.Multiline)
                .Match(settings);
            Assert.IsTrue(m.Success, "ProjectSettings.asset に " + key + " が見つからない");
            return int.Parse(m.Groups[1].Value);
        }

        // --- (2) PlayerSettings のフルスクリーン方針 ---

        [Test]
        public void ProjectSettings_FullScreenMode_IsFullScreenWindow()
        {
            // PlayerSettings.fullScreenMode の serialized 値: 1 = FullScreenWindow。
            Assert.AreEqual(1, SettingsInt(ReadProjectSettings(), "fullscreenMode"),
                "fullscreenMode は FullScreenWindow (1) であること");
        }

        [Test]
        public void ProjectSettings_DefaultIsNativeResolution_IsTrue()
        {
            Assert.AreEqual(1, SettingsInt(ReadProjectSettings(), "defaultIsNativeResolution"),
                "defaultIsNativeResolution=1 (ネイティブ解像度で起動) であること");
        }

        [Test]
        public void ProjectSettings_ResizableWindow_IsFalse()
        {
            Assert.AreEqual(0, SettingsInt(ReadProjectSettings(), "resizableWindow"),
                "resizableWindow=0 (ウィンドウリサイズによる擬似全画面を許さない) であること");
        }

        // --- (3) Screen.SetResolution の混入防止 ---

        [Test]
        public void GameScripts_HaveNoScreenSetResolution()
        {
            var root = Path.Combine(Application.dataPath, "Game", "Scripts");
            Assert.IsTrue(Directory.Exists(root), "Assets/Game/Scripts が見つからない");
            var hits = new System.Collections.Generic.List<string>();
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (File.ReadAllText(file).Contains("Screen.SetResolution"))
                {
                    hits.Add(file);
                }
            }

            Assert.AreEqual(0, hits.Count,
                "Screen.SetResolution は擬似全画面 (ウィンドウリサイズ) になるため禁止: " +
                string.Join(", ", hits));
        }

        // --- (4) design/ui-size-profiles.json の mac と UISizeProfile.Mac() の一致 ---

        private static string JsonProfiles()
        {
            return JsonProfileBlock("mac");
        }

        /// <summary>design/ui-size-profiles.json の profiles.&lt;key&gt; オブジェクトを取り出す。</summary>
        private static string JsonProfileBlock(string key)
        {
            var path = Path.Combine(ProjectRoot(), "design", "ui-size-profiles.json");
            Assert.IsTrue(File.Exists(path), "ui-size-profiles.json が読めない: " + path);
            var json = File.ReadAllText(path);
            return ExtractObject(ExtractObject(json, "profiles"), key);
        }

        private static string ExtractObject(string json, string key)
        {
            var keyMatch = new Regex("\"" + key + "\"").Match(json);
            Assert.IsTrue(keyMatch.Success, "json に key " + key + " が見つからない");
            var open = json.IndexOf('{', keyMatch.Index);
            Assert.GreaterOrEqual(open, 0, "json の key " + key + " にオブジェクトがない");
            var depth = 0;
            for (int i = open; i < json.Length; i++)
            {
                if (json[i] == '{')
                {
                    depth++;
                }
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(open, i - open + 1);
                    }
                }
            }

            Assert.Fail("json の key " + key + " が閉じていない");
            return null;
        }

        private static int JsonInt(string block, string key)
        {
            var m = new Regex("\"" + key + "\"\\s*:\\s*(-?\\d+)").Match(block);
            Assert.IsTrue(m.Success, "json の mac ブロックに " + key + " がない");
            return int.Parse(m.Groups[1].Value);
        }

        private static Vector2 JsonVector2(string block, string key)
        {
            var m = new Regex("\"" + key + "\"\\s*:\\s*\\[\\s*(-?\\d+)\\s*,\\s*(-?\\d+)\\s*\\]")
                .Match(block);
            Assert.IsTrue(m.Success, "json の mac ブロックに " + key + " (2 要素配列) がない");
            return new Vector2(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
        }

        [Test]
        public void JsonMacScreen_Is2560x1664()
        {
            var mac = JsonProfileBlock("mac");
            var screen = JsonVector2(mac, "screen");
            Assert.AreEqual(2560, (int)screen.x, "mac.screen は 2560 (旧 1440x900 設計は無効)");
            Assert.AreEqual(1664, (int)screen.y, "mac.screen は 1664 (旧 1440x900 設計は無効)");
        }

        [Test]
        public void JsonFhdScreen_Is1920x1080()
        {
            var screen = JsonVector2(JsonProfileBlock("fhd"), "screen");
            Assert.AreEqual(1920, (int)screen.x, "fhd.screen は 1920");
            Assert.AreEqual(1080, (int)screen.y, "fhd.screen は 1080");
        }

        [Test]
        public void JsonTwoKScreen_Is2560x1440()
        {
            var screen = JsonVector2(JsonProfileBlock("2k"), "screen");
            Assert.AreEqual(2560, (int)screen.x, "2k.screen は 2560");
            Assert.AreEqual(1440, (int)screen.y, "2k.screen は 1440");
        }

        [Test]
        public void FhdProfile_MatchesJson()
        {
            var json = JsonProfileBlock("fhd");
            var p = UISizeProfile.Fhd();

            Assert.AreEqual(JsonInt(json, "margin"), p.Margin, "margin");
            Assert.AreEqual((float)JsonInt(json, "fs_xl"), p.FsXl, "fs_xl");
            Assert.AreEqual((float)JsonInt(json, "fs_l"), p.FsL, "fs_l");
            Assert.AreEqual((float)JsonInt(json, "fs_m"), p.FsM, "fs_m");
            Assert.AreEqual((float)JsonInt(json, "fs_s"), p.FsS, "fs_s");
            Assert.AreEqual((float)JsonInt(json, "fs_rank"), p.FsRank, "fs_rank");
            Assert.AreEqual((float)JsonInt(json, "fs_combo"), p.FsCombo, "fs_combo");
            Assert.AreEqual(JsonInt(json, "icon_s"), p.IconS, "icon_s");
            Assert.AreEqual(JsonInt(json, "icon_m"), p.IconM, "icon_m");
            Assert.AreEqual(JsonInt(json, "icon_l"), p.IconL, "icon_l");
            Assert.AreEqual(JsonInt(json, "gap_s"), p.GapS, "gap_s");
            Assert.AreEqual(JsonInt(json, "gap_m"), p.GapM, "gap_m");
            Assert.AreEqual(JsonInt(json, "gap_l"), p.GapL, "gap_l");
            Assert.AreEqual(JsonVector2(json, "card_center"), p.CardCenter, "card_center");
            Assert.AreEqual(JsonVector2(json, "card_mid"), p.CardMid, "card_mid");
            Assert.AreEqual(JsonVector2(json, "card_side"), p.CardSide, "card_side");
            Assert.AreEqual(JsonInt(json, "drawer_w"), p.DrawerW, "drawer_w");
            Assert.AreEqual(JsonInt(json, "stepper_w"), p.StepperW, "stepper_w");
            Assert.AreEqual(JsonInt(json, "row_h"), p.RowH, "row_h");
            Assert.AreEqual(JsonVector2(json, "btn"), p.Btn, "btn");
        }

        [Test]
        public void TwoKProfile_MatchesJson()
        {
            var json = JsonProfileBlock("2k");
            var p = UISizeProfile.TwoK();

            Assert.AreEqual(JsonInt(json, "margin"), p.Margin, "margin");
            Assert.AreEqual((float)JsonInt(json, "fs_xl"), p.FsXl, "fs_xl");
            Assert.AreEqual((float)JsonInt(json, "fs_l"), p.FsL, "fs_l");
            Assert.AreEqual((float)JsonInt(json, "fs_m"), p.FsM, "fs_m");
            Assert.AreEqual((float)JsonInt(json, "fs_s"), p.FsS, "fs_s");
            Assert.AreEqual((float)JsonInt(json, "fs_rank"), p.FsRank, "fs_rank");
            Assert.AreEqual((float)JsonInt(json, "fs_combo"), p.FsCombo, "fs_combo");
            Assert.AreEqual(JsonInt(json, "icon_s"), p.IconS, "icon_s");
            Assert.AreEqual(JsonInt(json, "icon_m"), p.IconM, "icon_m");
            Assert.AreEqual(JsonInt(json, "icon_l"), p.IconL, "icon_l");
            Assert.AreEqual(JsonInt(json, "gap_s"), p.GapS, "gap_s");
            Assert.AreEqual(JsonInt(json, "gap_m"), p.GapM, "gap_m");
            Assert.AreEqual(JsonInt(json, "gap_l"), p.GapL, "gap_l");
            Assert.AreEqual(JsonVector2(json, "card_center"), p.CardCenter, "card_center");
            Assert.AreEqual(JsonVector2(json, "card_mid"), p.CardMid, "card_mid");
            Assert.AreEqual(JsonVector2(json, "card_side"), p.CardSide, "card_side");
            Assert.AreEqual(JsonInt(json, "drawer_w"), p.DrawerW, "drawer_w");
            Assert.AreEqual(JsonInt(json, "stepper_w"), p.StepperW, "stepper_w");
            Assert.AreEqual(JsonInt(json, "row_h"), p.RowH, "row_h");
            Assert.AreEqual(JsonVector2(json, "btn"), p.Btn, "btn");
        }

        [Test]
        public void MacProfile_MatchesJson()
        {
            var mac = JsonProfiles();
            var p = UISizeProfile.Mac();

            Assert.AreEqual(JsonInt(mac, "margin"), p.Margin, "margin");
            Assert.AreEqual((float)JsonInt(mac, "fs_xl"), p.FsXl, "fs_xl");
            Assert.AreEqual((float)JsonInt(mac, "fs_l"), p.FsL, "fs_l");
            Assert.AreEqual((float)JsonInt(mac, "fs_m"), p.FsM, "fs_m");
            Assert.AreEqual((float)JsonInt(mac, "fs_s"), p.FsS, "fs_s");
            Assert.AreEqual((float)JsonInt(mac, "fs_rank"), p.FsRank, "fs_rank");
            Assert.AreEqual((float)JsonInt(mac, "fs_combo"), p.FsCombo, "fs_combo");
            Assert.AreEqual(JsonInt(mac, "icon_s"), p.IconS, "icon_s");
            Assert.AreEqual(JsonInt(mac, "icon_m"), p.IconM, "icon_m");
            Assert.AreEqual(JsonInt(mac, "icon_l"), p.IconL, "icon_l");
            Assert.AreEqual(JsonInt(mac, "gap_s"), p.GapS, "gap_s");
            Assert.AreEqual(JsonInt(mac, "gap_m"), p.GapM, "gap_m");
            Assert.AreEqual(JsonInt(mac, "gap_l"), p.GapL, "gap_l");
            Assert.AreEqual(JsonVector2(mac, "card_center"), p.CardCenter, "card_center");
            Assert.AreEqual(JsonVector2(mac, "card_mid"), p.CardMid, "card_mid");
            Assert.AreEqual(JsonVector2(mac, "card_side"), p.CardSide, "card_side");
            Assert.AreEqual(JsonInt(mac, "drawer_w"), p.DrawerW, "drawer_w");
            Assert.AreEqual(JsonInt(mac, "stepper_w"), p.StepperW, "stepper_w");
            Assert.AreEqual(JsonInt(mac, "row_h"), p.RowH, "row_h");
            Assert.AreEqual(JsonVector2(mac, "btn"), p.Btn, "btn");
        }
    }
}
