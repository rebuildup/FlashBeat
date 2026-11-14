using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Issue #385 Phase 3: 旧 v5 View の「どこからも参照されていない」状態を
/// 可視化する参照監査テスト。
///
/// 台帳は <c>docs/architecture/v5-legacy-ui-references.md</c>。
/// v6 に置き換え済みの v5 View はフォールバック経路として残っているが、
/// 生成サイトが消えた型は削除候補になる。ここではソース走査で
/// **生成・参照の配線サイト**をピン留めし、変化があればテストを落として
/// 台帳の更新を促す (参照カウントの自動可視化)。
///
/// 判定は「UI/V5 以外のコードがその型を名指しで使っているか」。
/// コメント中の言及も拾うため厳密な参照カウントではないが、
/// 「ファイルがその型に言及しなくなった」変化は確実に検出できる。
/// </summary>
public class V5LegacyReferenceAuditTests
{
    private static string ScriptsRoot => Path.Combine(Application.dataPath, "Game/Scripts");

    private static string[] CsFilesUnder(string relativeDir)
    {
        var dir = Path.Combine(ScriptsRoot, relativeDir);
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        var result = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            result[i] = files[i];
        }
        return result;
    }

    /// <summary>UI/V5 を除く全 .cs のうち、型名を含むファイルの相対パス一覧。</summary>
    private static string[] FilesReferencing(string typeName)
    {
        var all = CsFilesUnder("");
        var v5Dir = Path.Combine(ScriptsRoot, "UI", "V5") + Path.DirectorySeparatorChar;
        var hits = new System.Collections.Generic.List<string>();
        foreach (var file in all)
        {
            if (file.StartsWith(v5Dir, System.StringComparison.Ordinal)) continue;
            string text = File.ReadAllText(file);
            if (!System.Text.RegularExpressions.Regex.IsMatch(text, @"\b" + typeName + @"\b")) continue;
            hits.Add(file.Substring(ScriptsRoot.Length + 1).Replace('\\', '/'));
        }
        hits.Sort(System.StringComparer.Ordinal);
        return hits.ToArray();
    }

    private static void AssertReferencingFiles(string typeName, params string[] expectedFiles)
    {
        var actual = FilesReferencing(typeName);
        var expected = (string[])expectedFiles.Clone();
        System.Array.Sort(expected, System.StringComparer.Ordinal);
        CollectionAssert.AreEquivalent(expected, actual,
            typeName + " を参照するファイルが台帳と違う。docs/architecture/v5-legacy-ui-references.md を確認して更新すること。");
    }

    // --- フォールバック経路として生きている型 ---

    [Test]
    public void SelectView_StillHasItsFallbackWiring()
    {
        // V6UiMode.SelectScreen == false のとき SelectSceneManager が生成する。
        AssertReferencingFiles("SelectView",
            "UI/V6/Select/Data/V6SelectDataSource.cs",
            "UI/V6/Select/View/V6SelectView.cs",
            "UI/V6/Select/View/V6SelectViewInput.cs",
            "UI/KeyConfigUI.cs",
            "UI/ResultRankingOverlay.cs",
            "UI/SceneInputGate.cs",
            "UI/SelectSceneManager.cs",
            "Common/AutoTestV6Churn.cs");
    }

    [Test]
    public void ResultView_StillHasItsFallbackWiring()
    {
        AssertReferencingFiles("ResultView",
            "UI/V6/Common/V6CursorInput.cs",
            "UI/V6/Result/V6ResultLogic.cs",
            "UI/V6/Result/V6ResultModal.cs",
            "UI/V6/Result/V6ResultRegistration.cs",
            "UI/V6/Result/V6ResultView.cs",
            "UI/KeyConfigUI.cs",
            "UI/ResultRankingOverlay.cs",
            "UI/ResultSceneManager.cs",
            "UI/SceneInputGate.cs");
    }

    [Test]
    public void PauseMenuView_StillHasItsFallbackWiring()
    {
        AssertReferencingFiles("PauseMenuView",
            "Gameplay/PauseGate.cs",
            "UI/GameSceneManager.cs",
            "UI/V6/Game/Hud/V6GameHudView.cs",
            "UI/V6/Game/Hud/V6HudChrome.cs",
            "UI/V6/Game/Pause/V6PauseRuntime.cs",
            "UI/V6/Game/Pause/V6PauseView.cs",
            "UI/V6/Game/Pause/V6PauseViewSettings.cs",
            "UI/V6/V6GameRuntime.cs");
    }

    [Test]
    public void OpeningView_IsStillTheLiveOpeningScreen()
    {
        // v6 化されていない唯一の現役 View。
        AssertReferencingFiles("OpeningView", "UI/OpeningSceneManager.cs");
    }

    // --- 生成サイトが無い = 削除候補 ---

    [Test]
    public void TitleView_HasNoInstantiationSite()
    {
        // v5 TitleView はどこからも作られていない (TitleSceneManager のコメントどおり)。
        // 残っているのは名前文字列の照合とコメントだけ。
        string[] files = FilesReferencing("TitleView");
        Assert.IsFalse(System.Array.Exists(files, f => f.EndsWith("TitleSceneManager.cs")
            && File.ReadAllText(Path.Combine(ScriptsRoot, f)).Contains("V5.TitleView.Create")),
            "v5 TitleView の生成サイトが復活したら台帳を更新すること");
    }

    [Test]
    public void OptionView_HasNoInstantiationSite()
    {
        // OptionSceneManager は V6OptionView のみを生成する。
        string optionManager = File.ReadAllText(
            Path.Combine(ScriptsRoot, "UI", "OptionSceneManager.cs"));
        Assert.IsFalse(optionManager.Contains("V5.OptionView"),
            "v5 OptionView の参照が復活したら台帳を更新すること");
    }
}
