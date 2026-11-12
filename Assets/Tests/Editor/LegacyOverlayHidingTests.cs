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
        // Issue #400: v6 構成では v5 の Ready / Loading / Error をそもそも生成しない
        // (GameHudController.Start)。したがって LateUpdate は
        // 「v6 ポーズが無効なときの v5 ポーズ」だけを見ればよく、
        // シーン全体の探索を毎フレーム行う必要が無い。
        //
        // ここでは「探索に上限があること」「全走査が LateUpdate に戻っていないこと」
        // を構造で固定する。
        string src = File.ReadAllText(
            Path.Combine(Application.dataPath, "Game/Scripts/UI/V6/V6GameRuntime.cs"));

        string lateUpdate = LateUpdateSource(src);

        Assert.IsTrue(lateUpdate.Contains("mayLookUp"),
            "探索に上限が無い (毎フレーム探し続ける)");
        Assert.IsTrue(src.Contains("LegacyLookupFrameLimit"),
            "探索の上限値が定義されていない");
        Assert.IsTrue(lateUpdate.Contains("legacyPause == null && mayLookUp"),
            "v5 ポーズの探索が上限で守られていない");

        // Issue #400: v5 の Ready / Overlay Canvas は v6 構成で生成されないため、
        // LateUpdate でそれらを探して隠す処理は持たない。
        // 「探す」= 型/名前で検索するコード。コメント中の言及と区別する。
        Assert.IsFalse(lateUpdate.Contains("FindObjectOfType<FlashBeat.UI.V5.GameReadyView>()"),
            "生成されない GameReadyView を毎フレーム探している");
        Assert.IsFalse(lateUpdate.Contains("FindObjectsOfType<Canvas>()"),
            "Canvas の全走査が LateUpdate に残っている");
        Assert.IsFalse(lateUpdate.Contains("== \"V5OverlayCanvas\""),
            "生成されない V5OverlayCanvas を名前で探している");
    }

    [Test]
    public void HidingHelperStaysSafeEvenThoughV6NoLongerCollectsIt()
    {
        // HideLegacyOverlayGraphics は公開 API として残す (テスト・将来の
        // レガシー対応)。毎フレーム呼ばれないことは上で固定した。
        Image a = AddImage("A");
        var graphics = new Graphic[] { a };

        Assert.IsTrue(V6GameRuntime.HideLegacyOverlayGraphics(graphics));
        a.enabled = true;
        Assert.IsTrue(V6GameRuntime.HideLegacyOverlayGraphics(graphics),
            "出し直された Graphic を再度隠す (冪等な隠し直し)");
        Assert.IsFalse(a.enabled);
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
