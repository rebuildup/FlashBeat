using System.IO;
using FlashBeat.UI.V6;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Issue #381: 旧 v5 ローディング Canvas の非表示処理が、毎フレーム
/// 子 Graphic を収集し直していないことを固定する。
///
/// 以前の <c>V6GameRuntime.LateUpdate</c> は毎フレーム
/// <c>GetComponentsInChildren&lt;Graphic&gt;(true)</c> を呼んでいた。構成は
/// GameScene の生存中に変わらないので、初回だけ集めて使い回す。
///
/// アロケーション量では判定していない: Unity 6 の
/// <c>GetComponentsInChildren&lt;T&gt;(true)</c> は内部バッファを使い回すため、
/// 呼んでも確保バイト数に差が出ないことを実測で確認した。したがって
/// 「収集は 1 箇所だけ」「LateUpdate の中に無い」を直接固定する。
/// </summary>
public class LegacyOverlayHidingTests
{
    private GameObject root;
    private readonly System.Collections.Generic.List<Object> created = new System.Collections.Generic.List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < created.Count; i++)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }

        created.Clear();

        if (root != null)
        {
            Object.DestroyImmediate(root);
            root = null;
        }
    }

    private Image AddImage(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        var image = go.AddComponent<Image>();
        created.Add(image);
        return image;
    }

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Legacy Overlay Probe", typeof(RectTransform));
    }

    [Test]
    public void EveryEnabledGraphicIsHidden()
    {
        Image a = AddImage("A");
        Image b = AddImage("B");
        Assert.IsTrue(a.enabled && b.enabled);

        bool allHidden = V6GameRuntime.HideLegacyOverlayGraphics(new Graphic[] { a, b });

        Assert.IsTrue(allHidden, "すべて無効化できたら true");
        Assert.IsFalse(a.enabled);
        Assert.IsFalse(b.enabled);
    }

    [Test]
    public void ARedisplayedGraphicIsHiddenAgain()
    {
        // v5 の GameHudController は loading.SetVisible(true) で出し直す。
        // キャッシュした配列でも、それを拾えなければ二重表示になる。
        Image a = AddImage("A");
        var graphics = new Graphic[] { a };

        Assert.IsTrue(V6GameRuntime.HideLegacyOverlayGraphics(graphics));
        a.enabled = true; // v5 が出し直した

        Assert.IsTrue(V6GameRuntime.HideLegacyOverlayGraphics(graphics),
            "出し直された Graphic を再度隠す");
        Assert.IsFalse(a.enabled);
    }

    [Test]
    public void ADestroyedGraphicDoesNotThrow()
    {
        // 要素が破棄されていても MissingReferenceException にしない。
        Image a = AddImage("A");
        var graphics = new Graphic[] { a, null };

        Object.DestroyImmediate(a);

        Assert.DoesNotThrow(() => V6GameRuntime.HideLegacyOverlayGraphics(graphics));
    }

    [Test]
    public void ANullArrayIsTreatedAsNothingToHide()
    {
        Assert.DoesNotThrow(() => V6GameRuntime.HideLegacyOverlayGraphics(null));
    }

    [Test]
    public void LateUpdateDoesNotScanTheSceneEveryFrame()
    {
        // 本題: LateUpdate が毎フレームシーン全体を探し直さないこと。
        // 見つからないまま探し続けると、V5OverlayCanvas が無いシーンで
        // 毎フレーム FindObjectsOfType<Canvas> (配列確保 + 全走査) が走る。
        string src = File.ReadAllText(
            Path.Combine(Application.dataPath, "Game/Scripts/UI/V6/V6GameRuntime.cs"));

        string lateUpdate = LateUpdateSource(src);

        // 探索は上限付きフラグで囲まれていること。
        Assert.IsTrue(lateUpdate.Contains("mayLookUp"),
            "探索に上限が無い (毎フレーム探し続ける)");
        Assert.IsTrue(src.Contains("LegacyLookupFrameLimit"),
            "探索の上限値が定義されていない");

        // 各探索が mayLookUp で守られていること (無条件の Find を残さない)。
        Assert.IsTrue(lateUpdate.Contains("legacyPause == null && mayLookUp"));
        Assert.IsTrue(lateUpdate.Contains("legacyReadyCanvas == null && mayLookUp"));
        Assert.IsTrue(lateUpdate.Contains("legacyOverlayCanvas == null && mayLookUp"));

        // 収集はキャッシュ変数への初回代入の 1 箇所だけ。
        // 数えるのは実際の呼び出し (`= ... GetComponentsInChildren...`) で、
        // 説明コメント中の同じ文字列は含めない。
        int collects = CountOccurrences(src, "legacyOverlayGraphics = legacyOverlayCanvas.GetComponentsInChildren<Graphic>(true);");
        Assert.AreEqual(1, collects,
            "Graphic の収集は初回の 1 箇所だけにする (見つかった数: " + collects + ")");
        Assert.IsTrue(src.Contains("if (legacyOverlayGraphics == null)"),
            "キャッシュ済みなら収集しない条件が無い");
    }

    [Test]
    public void HidingKeepsRunningAfterTheLookupStops()
    {
        // 探索を止めても「隠し直し」は止めない: v5 の GameHudController は
        // loading.SetVisible(true) で出し直すため、止めると二重表示になる。
        string src = File.ReadAllText(
            Path.Combine(Application.dataPath, "Game/Scripts/UI/V6/V6GameRuntime.cs"));

        string lateUpdate = LateUpdateSource(src);
        Assert.IsTrue(lateUpdate.Contains("HideLegacyOverlayGraphics(legacyOverlayGraphics);"),
            "隠し直しが毎フレーム走らない (v5 の再表示を取り逃す)");

        // 隠し直しは mayLookUp で囲まれていないこと (探索の上限に巻き込まない)。
        int hide = lateUpdate.IndexOf("HideLegacyOverlayGraphics(legacyOverlayGraphics);");
        int ifMayLookUp = lateUpdate.LastIndexOf("if (legacyOverlayCanvas == null && mayLookUp)", hide);
        int ifCanvas = lateUpdate.LastIndexOf("if (legacyOverlayCanvas != null)", hide);
        Assert.Greater(ifCanvas, ifMayLookUp,
            "隠し直しが探索の上限の条件の中に入っている");
    }

    /// <summary>LateUpdate の本体を切り出す (次のメンバー宣言まで)。</summary>
    private static string LateUpdateSource(string src)
    {
        int late = src.IndexOf("private void LateUpdate()");
        Assert.GreaterOrEqual(late, 0, "LateUpdate が見つからない");
        int boundary = src.IndexOf("public static bool HideLegacyOverlayGraphics", late);
        Assert.Greater(boundary, late, "LateUpdate の終端 (ヘルパー宣言) が取れない");
        return src.Substring(late, boundary - late);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = haystack.IndexOf(needle, System.StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, System.StringComparison.Ordinal);
        }

        return count;
    }
}
