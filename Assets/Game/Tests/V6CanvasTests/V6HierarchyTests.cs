using NUnit.Framework;
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
