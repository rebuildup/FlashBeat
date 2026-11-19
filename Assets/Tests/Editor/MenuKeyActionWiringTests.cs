using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;

/// <summary>
/// #142 / #146 / #148 / #740 — メニュー操作キー (KeyActions) への集約を検証する。
///
/// - 表示: ActionKeyHint は KeyActions の現在の割当から生成され、設定変更に追従する。
///   割当が空の操作でも「—」で壊れない。
/// - ソース走査: メニュー画面の凡例/キーキャップに固定のキーリテラル
///   (Esc / Space / R / BS) が残っていないこと。
///   (Enter は #146 の決定に従い生入力のまま残す箇所があるため対象外)
/// - 入力: 対象シーン/ビューが Esc と Space を直接読まず、
///   KeyActions.IsDown (Back/Confirm) 経由で読むこと。
///   KeyConfigUI (F2 トグル) / UpdateNoticeUI (F3/F4) / KeyConfigOptionButton (Tab フォーカス) は
///   KeyActions に存在しない固定ショートカットなので対象外。
///
/// Issue #740 で v5 View は削除済み。走査対象は v6 画面 (UI/V6 以下) と
/// メニュー系シーンマネージャに置き換えた。v6 側で Esc/Space の生読みが
/// 意図的に残るファイル (モーダルのキャンセル、キーコンフィグの入力待ち、
/// Option 画面の生入力) は InputExclusionSources で明示除外する。
/// </summary>
public class MenuKeyActionWiringTests
{
    private sealed class MemoryStorage : KeyActions.IStorage
    {
        public string Raw;
        public string LoadRaw() { return Raw; }
        public void SaveRaw(string value) { Raw = value; }
    }

    [SetUp]
    public void SetUp()
    {
        KeyActions.SetStorage(new MemoryStorage());
        KeyActions.ResetToDefaults();
    }

    [TearDown]
    public void TearDown()
    {
        KeyActions.SetStorage(null);
    }

    [Test]
    public void HintFollowsRebindingForCentralizedActions()
    {
        Assert.AreEqual("Esc", ActionKeyHint.Primary(KeyActions.KeyAction.Back));
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Back, new[] { KeyCode.Backspace }));
        Assert.AreEqual("BS", ActionKeyHint.Primary(KeyActions.KeyAction.Back));

        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Confirm, new[] { KeyCode.F8 }));
        Assert.AreEqual("F8", ActionKeyHint.Primary(KeyActions.KeyAction.Confirm));

        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Play, new[] { KeyCode.Return }));
        Assert.AreEqual("Enter", ActionKeyHint.Primary(KeyActions.KeyAction.Play));
    }

    [Test]
    public void UnassignedActionShowsExplicitPlaceholder()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Back, new KeyCode[0]));
        Assert.AreEqual("—", ActionKeyHint.Primary(KeyActions.KeyAction.Back));
        Assert.AreEqual("—", ActionKeyHint.All(KeyActions.KeyAction.Back));

        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Play, new KeyCode[0]));
        Assert.AreEqual("—", ActionKeyHint.Primary(KeyActions.KeyAction.Play));
    }

    // ---- ソース走査: 固定キーリテラルの残存検出 ------------------------------

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
    }

    /// <summary>凡例/キーキャップを描画する v6 画面ソース (旧 V5ViewSources の置き換え)。</summary>
    private static readonly string[] MenuLegendSources =
    {
        "Game/Scripts/UI/V6/Common/V6Controls.cs",
        "Game/Scripts/UI/V6/Common/V6Legend.cs",
        "Game/Scripts/UI/V6/Common/V6Header.cs",
        "Game/Scripts/UI/V6/Game/Hud/V6GameHudView.cs",
        "Game/Scripts/UI/V6/Game/Hud/V6HudChrome.cs",
        "Game/Scripts/UI/V6/Game/Hud/V6HudLogic.cs",
        "Game/Scripts/UI/V6/Game/Pause/V6PauseViewBuild.cs",
        "Game/Scripts/UI/V6/Game/Pause/V6PauseViewLogic.cs",
        "Game/Scripts/UI/V6/History/V6HistoryViewHeader.cs",
        "Game/Scripts/UI/V6/Option/V6OptionLogic.cs",
        "Game/Scripts/UI/V6/Option/V6OptionViewBuild.cs",
        "Game/Scripts/UI/V6/Result/V6ResultLogic.cs",
        "Game/Scripts/UI/V6/Result/V6ResultModal.cs",
        "Game/Scripts/UI/V6/Result/V6ResultView.cs",
        "Game/Scripts/UI/V6/Select/View/V6SelectViewHeader.cs",
        "Game/Scripts/UI/V6/Title/V6TitleExit.cs",
        "Game/Scripts/UI/V6/Title/V6TitleLogic.cs",
        "Game/Scripts/UI/V6/Title/V6TitleViewFooter.cs",
        "Game/Scripts/UI/V6/Title/V6TitleViewMain.cs",
        "Game/Scripts/UI/V6/V6Ui.cs",
    };

    /// <summary>キーキャップ/凡例を描画する API 呼び出し。</summary>
    private static readonly Regex LegendCall = new Regex(
        @"\b(LegendItem|LegendRight|BuildLegend|AddKeyCap|KeyCap|BuildModalButton|AddAction)\s*\(",
        RegexOptions.Compiled);

    /// <summary>KeyActions に裏付けのある操作を固定文字列で書いてはいけない。
    /// (Enter は生入力のまま残す箇所があるため対象外、矢印/Tab/F キーは操作表外)</summary>
    private static readonly Regex HardCodedCap = new Regex(@"""(Esc|Space|BS|R)""", RegexOptions.Compiled);

    [Test]
    public void MenuLegendsGenerateCapsFromCurrentBindings()
    {
        foreach (string path in MenuLegendSources)
        {
            string[] lines = ReadSource(path).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!LegendCall.IsMatch(lines[i])) continue;
                Assert.IsFalse(HardCodedCap.IsMatch(lines[i]),
                    path + ":" + (i + 1) + " に固定キーリテラルが残っています (ActionKeyHint 経由にすること): " + lines[i].Trim());
            }
        }
    }

    /// <summary>
    /// Esc/Space の生読みを意図的に残す v6 ファイル (キーコンフィグの入力待ち、
    /// モーダルのキャンセル、Option 画面の生入力)。ここを増やす場合は
    /// KeyActions 化の是非を別 Issue で判断すること。
    /// </summary>
    private static readonly string[] InputExclusionSources =
    {
        "Game/Scripts/UI/V6/Common/V6KeyConfigModal.cs",
        "Game/Scripts/UI/V6/Option/V6OptionViewInput.cs",
        "Game/Scripts/UI/V6/Result/V6ResultModal.cs",
    };

    /// <summary>メニュー操作を直接読んでいるファイル (KeyActions 化済み)。</summary>
    private static string[] CentralizedInputSources()
    {
        // UI/V6 以下を再帰走査し、意図的な生読みファイルを除く。
        // シーンマネージャ側は個別に足す (v6 View は KeyActions/V6CursorInput 経由)。
        var v6Dir = Path.Combine(Application.dataPath, "Game/Scripts/UI/V6");
        var files = Directory.GetFiles(v6Dir, "*.cs", SearchOption.AllDirectories);
        var result = new System.Collections.Generic.List<string>();
        foreach (var file in files)
        {
            string relative = "Game/Scripts/" + file.Substring(
                Path.Combine(Application.dataPath, "Game/Scripts").Length + 1)
                .Replace('\\', '/');
            bool excluded = false;
            for (int i = 0; i < InputExclusionSources.Length; i++)
            {
                if (relative == InputExclusionSources[i]) { excluded = true; break; }
            }
            if (!excluded) result.Add(relative);
        }

        result.Add("Game/Scripts/UI/SelectSceneManager.cs");
        result.Add("Game/Scripts/UI/ResultSceneManager.cs");
        result.Add("Game/Scripts/UI/ResultRankingOverlay.cs");
        result.Sort(System.StringComparer.Ordinal);
        return result.ToArray();
    }

    /// <summary>Esc と Space (Back/Confirm の既定キー) の直接読みを禁止する。
    /// Enter/KeypadEnter は Confirm の既定外の併用キーとして生読みが残る。</summary>
    private static readonly Regex RawMenuRead = new Regex(
        @"Input\.GetKeyDown\(KeyCode\.(Escape|Space)\)", RegexOptions.Compiled);

    [Test]
    public void MenuScreensDoNotReadDefaultActionKeysDirectly()
    {
        foreach (string path in CentralizedInputSources())
        {
            string[] lines = ReadSource(path).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                Assert.IsFalse(RawMenuRead.IsMatch(lines[i]),
                    path + ":" + (i + 1) + " が Esc/Space を直接読んでいます。KeyActions.IsDown 経由にしてください: " + lines[i].Trim());
            }
        }
    }
}
