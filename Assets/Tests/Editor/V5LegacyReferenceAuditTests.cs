using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Issue #385 Phase 3 / #740: 旧 v5 View の参照状況を可視化する参照監査テスト。
///
/// 台帳は <c>docs/architecture/v5-legacy-ui-references.md</c>。
/// Issue #740 でフォールバック経路だった v5 View (Select / Result / Pause /
/// GameHud / GameLoading / GameReady / GameError) は削除済み。
/// ここではソース走査で **型名への言及 (コメント含む)** をピン留めし、
/// 変化があればテストを落として台帳の更新を促す。
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

    // --- Issue #740 で削除した v5 View: ファイルが復活しないこと ---

    [Test]
    public void V5Views_AreDeleted()
    {
        // フォールバック経路だった v5 View は Issue #740 で削除済み。
        // ファイルが復活したら台帳 (docs/architecture/v5-legacy-ui-references.md) を更新すること。
        string v5Dir = Path.Combine(ScriptsRoot, "UI", "V5");
        foreach (string name in new[]
        {
            "SelectView.cs", "ResultView.cs", "PauseMenuView.cs",
            "GameHudView.cs", "GameLoadingView.cs", "GameReadyView.cs",
            "GameErrorView.cs", "TitleLinks.cs", "GameUiExclusivity.cs",
        })
        {
            Assert.IsFalse(File.Exists(Path.Combine(v5Dir, name)),
                "v5 " + name + " が復活した。削除済みのはず (Issue #740)。");
        }
    }

    // --- 残る言及 (コメント / 互換契約) のピン留め ---

    [Test]
    public void SelectView_RemainingMentions_ArePinned()
    {
        // 型は削除済み。残るのはコメント・ドキュメント言及と、
        // v6 側が引き継いだ契約 (CanFetchOnlineRanking 相当の判定など) への言及。
        AssertReferencingFiles("SelectView",
            "Common/AutoTestV6Churn.cs",
            "Songs/PlaySession.cs",
            "UI/KeyConfigUI.cs",
            "UI/ResultRankingOverlay.cs",
            "UI/SceneInputGate.cs",
            "UI/SelectSceneManager.cs",
            "UI/V6/Select/Data/V6SelectDataSource.cs",
            "UI/V6/Select/View/V6SelectView.cs",
            "UI/V6/Select/View/V6SelectViewInput.cs");
    }

    [Test]
    public void ResultView_RemainingMentions_ArePinned()
    {
        AssertReferencingFiles("ResultView",
            "UI/KeyConfigUI.cs",
            "UI/ResultRankingOverlay.cs",
            "UI/ResultSceneManager.cs",
            "UI/SceneInputGate.cs",
            "UI/V6/Common/V6CursorInput.cs",
            "UI/V6/Result/V6ResultLogic.cs",
            "UI/V6/Result/V6ResultModal.cs",
            "UI/V6/Result/V6ResultRegistration.cs",
            "UI/V6/Result/V6ResultRules.cs",
            "UI/V6/Result/V6ResultView.cs");
    }

    [Test]
    public void PauseMenuView_RemainingMentions_ArePinned()
    {
        AssertReferencingFiles("PauseMenuView",
            "Gameplay/PauseGate.cs",
            "UI/GameSceneManager.cs",
            "UI/V6/Common/V6VolumeSettings.cs",
            "UI/V6/Game/Hud/V6GameHudView.cs",
            "UI/V6/Game/Hud/V6HudChrome.cs",
            "UI/V6/Game/Pause/V6PauseView.cs");
    }

    [Test]
    public void OpeningView_IsStillTheLiveOpeningScreen()
    {
        // v6 化されていない唯一の現役 View。
        AssertReferencingFiles("OpeningView", "UI/OpeningSceneManager.cs");
    }

    // --- 生成サイトが無い = 削除済みの型 ---

    [Test]
    public void DeletedV5Views_StayDeleted()
    {
        // Issue #497 / #740 で削除済みの型。ファイルが復活したら台帳を更新すること。
        string v5Dir = Path.Combine(ScriptsRoot, "UI", "V5");
        foreach (string name in new[] { "TitleView.cs", "OptionView.cs", "TitleLayout.cs" })
        {
            Assert.IsFalse(File.Exists(Path.Combine(v5Dir, name)),
                "v5 " + name + " が復活した。削除済みのはず (Issue #497)。");
        }
    }
}
