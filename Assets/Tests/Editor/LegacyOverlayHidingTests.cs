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
        // Issue #400/#740: v6 構成では v5 の Ready / Loading / Error / Pause を
        // そもそも生成しないため、V6GameRuntime のフレームごとのレガシー探索
        // (LateUpdate) 自体が削除された。旧 UI の一括非表示は Start の
        // HideLegacyUi 1 回きり。
        //
        // 「毎フレーム・シーン走査しない」不変条件は、LateUpdate が存在しない
        // こと、Update 系メソッド内に探索呼び出しが無いこと、HideLegacyUi が
        // 1 箇所から 1 回だけ呼ばれることで固定する。
        string src = File.ReadAllText(
            Path.Combine(Application.dataPath, "Game/Scripts/UI/V6/V6GameRuntime.cs"));

        Assert.IsFalse(src.Contains("private void LateUpdate()"),
            "V6GameRuntime に LateUpdate が復活した (毎フレームのレガシー探索経路)");

        int updateStart = src.IndexOf("private void Update()");
        Assert.GreaterOrEqual(updateStart, 0, "Update が見つからない");
        int updateEnd = src.IndexOf("private void UpdatePause()", updateStart);
        Assert.Greater(updateEnd, updateStart, "Update の終端が取れない");
        string update = src.Substring(updateStart, updateEnd - updateStart);
        Assert.IsFalse(update.Contains("FindObjectOfType")
            || update.Contains("FindObjectsOfType")
            || update.Contains("GameObject.Find")
            || update.Contains("Resources.FindObjectsOfTypeAll"),
            "Update がシーン走査を毎フレーム行っている");

        Assert.AreEqual(1, CountOccurrences(src, "HideLegacyUi();"),
            "HideLegacyUi は Start から 1 回だけ呼ばれる (毎フレーム呼ばない)");
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
