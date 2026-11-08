using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v6 モードで旧 Canvas が生き残る不具合の修正テスト。
/// ResultSceneManager / OptionSceneManager が旧 Canvas を両方 (全て) 隠すこと、
/// 片方しか無いシーンで例外を出さないことを検証する。
/// </summary>
public static class V6LegacyCanvasHideTests
{
    [Test]
    public static void HideLegacyCanvases_DeactivatesBothCanvases()
    {
        var v5 = new GameObject("LegacyCanvasV5").AddComponent<Canvas>();
        var v6 = new GameObject("LegacyCanvasV6").AddComponent<Canvas>();

        ResultSceneManager.HideLegacyCanvases(v5, v6);

        Assert.IsFalse(v5.gameObject.activeSelf, "v6 モードで legacyCanvasV5 が残ると v6 UI に重なる");
        Assert.IsFalse(v6.gameObject.activeSelf);
    }

    [Test]
    public static void HideLegacyCanvases_ToleratesNullCanvas()
    {
        // シーンに片方の Canvas しか無い (参照が null) 場合でも例外を出さない。
        var v6 = new GameObject("LegacyCanvasV6").AddComponent<Canvas>();
        Assert.DoesNotThrow(() => ResultSceneManager.HideLegacyCanvases(null, v6));
        Assert.IsFalse(v6.gameObject.activeSelf);
        Assert.DoesNotThrow(() => ResultSceneManager.HideLegacyCanvases(v6, null));
        Assert.DoesNotThrow(() => ResultSceneManager.HideLegacyCanvases(null, null));
    }

    [Test]
    public static void HideLegacySceneCanvases_DeactivatesAllCanvasesInActiveScene()
    {
        // Option シーンの "Canvas" 以外の名前の旧 Canvas も隠れること。
        var named = new GameObject("Canvas").AddComponent<Canvas>();
        var other = new GameObject("LegacyHintCanvas").AddComponent<Canvas>();
        var inactive = new GameObject("InactiveCanvas").AddComponent<Canvas>();
        inactive.gameObject.SetActive(false);

        OptionSceneManager.HideLegacySceneCanvases();

        Assert.IsFalse(named.gameObject.activeSelf);
        Assert.IsFalse(other.gameObject.activeSelf);
        Assert.IsFalse(inactive.gameObject.activeSelf);
    }
}
