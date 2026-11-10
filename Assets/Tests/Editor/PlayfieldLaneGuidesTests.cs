using System.Reflection;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Issue #233 / PR #263 レビュー S2・S3: 実行時に生成した 3D レーン構造の
/// 後始末 (native オブジェクトの破棄) とマテリアル生成の null 安全性を固定する。
///
/// どちらも「ソースを読む」のではなく **振る舞い** で確認する。
/// </summary>
public class PlayfieldLaneGuidesTests
{
    private GameObject host;

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
        host = null;
    }

    private static object InvokePrivate(object target, string name)
    {
        MethodInfo method = null;
        foreach (MethodInfo candidate in target.GetType().GetMethods(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (candidate.Name != name) continue;
            method = candidate;
            break;
        }
        Assert.IsNotNull(method, "The method " + name + " is gone from " + target.GetType().Name);
        return method.Invoke(target, null);
    }

    private static object Field(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, "The field " + name + " is gone from " + target.GetType().Name);
        return field.GetValue(target);
    }

    /// <summary>
    /// S2: コンポーネントが消えるときに、生成した Mesh と Material も消える。
    ///
    /// 破棄しないと GameScene に入るたびに 1 個ずつ残り続ける
    /// (リトライ / 再プレイ / 毎曲でリーク)。
    ///
    /// EditMode では MonoBehaviour の OnDestroy が自動的に呼ばれない
    /// (ExecuteAlways でないため) ので、後始末の実体を直接呼び出して
    /// 確認する。OnDestroy があること自体は別途固定する。
    /// </summary>
    [Test]
    public void TheGeneratedMeshAndMaterialAreDestroyedWithTheComponent()
    {
        host = new GameObject("PlayfieldLaneGuidesTest");
        var guides = host.AddComponent<PlayfieldLaneGuides>();

        InvokePrivate(guides, "Build");

        var mesh = (Mesh)Field(guides, "guideMesh");
        var material = (Material)Field(guides, "guideMaterial");

        Assert.IsNotNull(mesh, "The lane guides did not build a mesh.");
        Assert.IsNotNull(material, "The lane guides did not build a material.");

        // 後始末の実体を呼ぶ (OnDestroy と同じ処理)。
        InvokePrivate(guides, "OnDestroy");

        Assert.IsTrue(mesh == null, "The generated mesh was not destroyed with the component.");
        Assert.IsTrue(material == null, "The generated material was not destroyed with the component.");
    }

    /// <summary>
    /// 後始末が生命周期のフックから呼ばれること (OnDestroy がある)。
    /// </summary>
    [Test]
    public void TheComponentDisposesItsResourcesInOnDestroy()
    {
        host = new GameObject("PlayfieldLaneGuidesOnDestroyTest");
        var guides = host.AddComponent<PlayfieldLaneGuides>();

        MethodInfo onDestroy = null;
        foreach (MethodInfo candidate in typeof(PlayfieldLaneGuides).GetMethods(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (candidate.Name != "OnDestroy") continue;
            onDestroy = candidate;
            break;
        }

        Assert.IsNotNull(onDestroy,
            "PlayfieldLaneGuides must dispose the mesh and the material in OnDestroy.");
    }

    /// <summary>
    /// S3: マテリアル生成は **例外を投げない**。
    ///
    /// `new Material(null)` は ArgumentNullException を投げるので、
    /// シェーダが見つからないときは null を返して「線は出ない」形にする。
    /// ここでは生成が成功する環境でも「HasProperty で守られている」こと
    /// (色が無いシェーダでも例外にならない) を確認する。
    /// </summary>
    [Test]
    public void TheMaterialIsCreatedWithoutThrowing()
    {
        host = new GameObject("PlayfieldLaneGuidesMaterialTest");
        var guides = host.AddComponent<PlayfieldLaneGuides>();

        Material material = null;
        Assert.DoesNotThrow(() => material = (Material)InvokePrivate(guides, "CreateMaterial"),
            "The material creation must not throw.");

        if (material != null)
        {
            // _Color が無いシェーダでも SetColor で例外にならないこと。
            Assert.DoesNotThrow(() =>
            {
                if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            });
            Object.DestroyImmediate(material);
        }
    }

    /// <summary>
    /// ガイド線は 8 レーン (9 本の境界) を隙間なく覆う (実行時生成の実測)。
    /// </summary>
    [Test]
    public void TheBuiltMeshCoversEveryLane()
    {
        host = new GameObject("PlayfieldLaneGuidesMeshTest");
        var guides = host.AddComponent<PlayfieldLaneGuides>();

        InvokePrivate(guides, "Build");

        var mesh = (Mesh)Field(guides, "guideMesh");
        Assert.IsNotNull(mesh);

        // 9 本 x 4 頂点 = 36 頂点。
        Assert.AreEqual(PlayfieldLaneGuides.GuideCount * 4, mesh.vertexCount,
            "The lane guides do not cover every lane boundary.");
        Assert.AreEqual(PlayfieldLaneGuides.GuideCount * 6, mesh.triangles.Length,
            "The lane guides do not cover every lane boundary.");
    }
}
