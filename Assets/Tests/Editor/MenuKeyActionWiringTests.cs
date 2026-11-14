using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;

/// <summary>
/// #142 / #146 / #148 — メニュー操作キー (KeyActions) への集約を検証する。
///
/// - 表示: ActionKeyHint は KeyActions の現在の割当から生成され、設定変更に追従する。
///   割当が空の操作でも「—」で壊れない。
/// - ソース走査: V5 ビューの凡例/キーキャップに固定のキーリテラル
///   (Esc / Space / R / BS) が残っていないこと。
///   (Enter は #146 の決定に従い生入力のまま残す箇所があるため対象外)
/// - 入力: 対象シーン/ビューが Esc と Space を直接読まず、
///   KeyActions.IsDown (Back/Confirm) 経由で読むこと。
///   KeyConfigUI (F2 トグル) / UpdateNoticeUI (F3/F4) / KeyConfigOptionButton (Tab フォーカス) は
///   KeyActions に存在しない固定ショートカットなので対象外。
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

    /// <summary>走査対象の V5 ビュー (Owned paths)。</summary>
    private static readonly string[] V5ViewSources =
    {
        "Game/Scripts/UI/V5/SelectView.cs",
        "Game/Scripts/UI/V5/PauseMenuView.cs",
        "Game/Scripts/UI/V5/ResultView.cs",
        "Game/Scripts/UI/V5/GameErrorView.cs",
        "Game/Scripts/UI/V5/GameReadyView.cs",
    };

    /// <summary>キーキャップ/凡例を描画する API 呼び出し。</summary>
    private static readonly Regex LegendCall = new Regex(
        @"\b(LegendItem|LegendRight|BuildLegend|AddKeyCap|KeyCap|BuildModalButton|AddAction)\s*\(",
        RegexOptions.Compiled);

    /// <summary>KeyActions に裏付けのある操作を固定文字列で書いてはいけない。
    /// (Enter は生入力のまま残す箇所があるため対象外、矢印/Tab/F キーは操作表外)</summary>
    private static readonly Regex HardCodedCap = new Regex(@"""(Esc|Space|BS|R)""", RegexOptions.Compiled);

    [Test]
    public void V5LegendsGenerateCapsFromCurrentBindings()
    {
        foreach (string path in V5ViewSources)
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

    /// <summary>メニュー操作を直接読んでいるファイル (KeyActions 化済み)。</summary>
    private static readonly string[] CentralizedInputSources =
    {
        "Game/Scripts/UI/V5/SelectView.cs",
        "Game/Scripts/UI/V5/PauseMenuView.cs",
        "Game/Scripts/UI/V5/ResultView.cs",
        "Game/Scripts/UI/V5/GameErrorView.cs",
        "Game/Scripts/UI/V5/GameReadyView.cs",
        "Game/Scripts/UI/SelectSceneManager.cs",
        "Game/Scripts/UI/ResultRankingOverlay.cs",
    };

    /// <summary>Esc と Space (Back/Confirm の既定キー) の直接読みを禁止する。
    /// Enter/KeypadEnter は Confirm の既定外の併用キーとして生読みが残る。</summary>
    private static readonly Regex RawMenuRead = new Regex(
        @"Input\.GetKeyDown\(KeyCode\.(Escape|Space)\)", RegexOptions.Compiled);

    [Test]
    public void MenuScreensDoNotReadDefaultActionKeysDirectly()
    {
        foreach (string path in CentralizedInputSources)
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
