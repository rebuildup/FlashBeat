using NUnit.Framework;
using TMPro;
using UnityEngine;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Option;

/// <summary>
/// v6 のゾーン作り直し (<see cref="V6Hierarchy.ClearChildren"/>) の契約テスト。
///
/// 背景: 再生中の破棄は <c>Destroy</c> (フレーム末) なので、破棄だけに任せると
/// 同じフレーム中に古い子と新しい子が二重に残る。Option の実機監査が
/// pass=359 / 181 で揺れたのはこれが原因だった (毎フレーム Refresh →
/// Rebuild → Destroy 予約の繰り返し)。ここでは「呼び出し直後に階層から
/// 消えている」ことと、Option の Rebuild が重複を生まないことを固定する。
/// </summary>
public class V6HierarchyTests
{
    [TearDown]
    public void TearDown()
    {
        // Create した View を片付ける (Current は Dispose で解除される)。
        var view = V6OptionView.Current;
        if (view != null)
        {
            view.Dispose();
        }

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name == "V6HierarchyTestParent" || go.name == "V6HierarchyTestRoot"))
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    [Test]
    public void ClearChildren_RemovesEveryChildFromTheHierarchy()
    {
        var parent = new GameObject("V6HierarchyTestParent", typeof(RectTransform));
        new GameObject("a").transform.SetParent(parent.transform, false);
        new GameObject("b").transform.SetParent(parent.transform, false);
        new GameObject("c").transform.SetParent(parent.transform, false);
        Assert.AreEqual(3, parent.transform.childCount);

        V6Hierarchy.ClearChildren(parent);

        Assert.AreEqual(0, parent.transform.childCount,
            "ClearChildren の直後には子が階層から消えていること (Destroy はフレーム末でも数えない)");
    }

    [Test]
    public void ClearChildren_NullAndEmptyAreSafe()
    {
        Assert.DoesNotThrow(() => V6Hierarchy.ClearChildren((GameObject)null));
        Assert.DoesNotThrow(() => V6Hierarchy.ClearChildren((Transform)null));

        var parent = new GameObject("V6HierarchyTestParent", typeof(RectTransform));
        Assert.DoesNotThrow(() => V6Hierarchy.ClearChildren(parent));
        Assert.AreEqual(0, parent.transform.childCount);
    }

    [Test]
    public void DetachAndDestroy_RemovesSingleChildFromTheHierarchy()
    {
        var parent = new GameObject("V6HierarchyTestParent", typeof(RectTransform));
        var child = new GameObject("V6HierarchyTestChild", typeof(RectTransform));
        child.transform.SetParent(parent.transform, false);
        var sibling = new GameObject("V6HierarchyTestSibling", typeof(RectTransform));
        sibling.transform.SetParent(parent.transform, false);

        V6Hierarchy.DetachAndDestroy(child);

        Assert.AreEqual(1, parent.transform.childCount, "対象の子だけが階層から外れる");
        Assert.AreSame(sibling.transform, parent.transform.GetChild(0));
        Assert.IsNull(child.transform.parent, "外した子は root へ戻る (Destroy はフレーム末)");

        // TearDown の名前一致で拾えないよう、外れた子はここで明示的に片付ける。
        if (child != null)
        {
            Object.DestroyImmediate(child);
        }
    }

    [Test]
    public void DetachAndDestroy_NullIsSafe()
    {
        Assert.DoesNotThrow(() => V6Hierarchy.DetachAndDestroy(null));
    }

    [Test]
    public void SetChildText_WritesFoundTmp_AndIgnoresMissingPath()
    {
        var root = new GameObject("V6HierarchyTestParent", typeof(RectTransform));
        var child = new GameObject("Row", typeof(RectTransform));
        child.transform.SetParent(root.transform, false);
        var tmpGo = new GameObject("Title", typeof(RectTransform));
        tmpGo.transform.SetParent(child.transform, false);
        var tmp = tmpGo.AddComponent<TextMeshProUGUI>();

        V6Hierarchy.SetChildText(root, "Row/Title", "hello");
        Assert.AreEqual("hello", tmp.text);

        Assert.DoesNotThrow(() => V6Hierarchy.SetChildText(root, "Row/Missing", "x"));
        Assert.DoesNotThrow(() => V6Hierarchy.SetChildText(null, "Row/Title", "x"));
    }

    [Test]
    public void OptionDisplay_HasNoRowsForFeaturesThatDoNotExist()
    {
        // design EzLfb の Display は 9 行だが、Live Rank / Hit Timing / Judge Text /
        // Progress Bar はこのゲームに設定も挙動も無い。動かない行を描くと嘘の操作に
        // なるため出さない (V5 OptionView と Pause Display と同じ判断 — Issue #167
        // の PauseDisplay_HasNoDeadRows)。実データのある 5 行だけを固定する。
        var canvasGo = new GameObject("V6HierarchyTestRoot", typeof(RectTransform), typeof(Canvas));
        var view = V6OptionView.Create(canvasGo.GetComponent<RectTransform>(), () => { });
        view.SelectTabForAudit(1); // Display

        var settings = view.Root.Find("Main/Settings");
        Assert.IsNotNull(settings, "Settings panel");

        var rows = new System.Collections.Generic.List<string>();
        foreach (var rt in settings.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.name.StartsWith("Set ", System.StringComparison.Ordinal))
            {
                rows.Add(rt.name.Substring("Set ".Length));
            }
        }

        CollectionAssert.AreEqual(
            new[] { "Flash", "FlashTiming", "FlashSpeed", "Key Guide", "Lyrics" }, rows,
            "Display の行 = 実データのある 5 行 (design の並び順)");

        foreach (string dead in new[] { "Live Rank", "Hit Timing", "Judge Text", "Progress Bar" })
        {
            CollectionAssert.DoesNotContain(rows, dead,
                dead + " は実装が無いので行を描かない (死んだ設定行にしない)");
        }
    }

    [Test]
    public void OptionRebuild_DoesNotDuplicateTheFooterLegend()
    {
        var canvasGo = new GameObject("V6HierarchyTestRoot", typeof(RectTransform), typeof(Canvas));
        var view = V6OptionView.Create(canvasGo.GetComponent<RectTransform>(), () => { });

        // 入力 1 回ごとに Rebuild が走る (旧実装は毎フレーム走っていた)。
        view.Refresh();
        view.Refresh();
        view.Refresh();

        var footer = view.Root.Find("Footer");
        Assert.IsNotNull(footer, "共通フレームの Footer ゾーンが存在する");

        int arrows = 0;
        foreach (Transform child in footer)
        {
            if (child.name == "KeyCap ↑↓")
            {
                arrows++;
            }
        }

        Assert.AreEqual(1, arrows,
            "Footer の KeyCap ↑↓ は 1 個だけ (作り直しで古い世代が残らない)");
    }
}
