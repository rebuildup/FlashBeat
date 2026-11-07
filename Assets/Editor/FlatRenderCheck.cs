using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Regression check for the flat rendering of the playfield.
///
/// The playfield must not depend on light or shadows any more: the picture quad
/// faces away from the directional light, so a lit shader renders it almost
/// black, and the lane bars dropped shadow stripes over the notes.
///
/// The check
///   1. audits every scene and prefab for shadow settings that came back,
///   2. measures pixels: a probe quad with the video material has to show the
///      texture unchanged (50% grey -> 128), a note has to stay pure white,
///   3. writes the GameScene camera image to a PNG.
///
/// Run it with:
///   Unity -batchmode -quit -projectPath . -executeMethod FlatRenderCheck.Run
/// </summary>
public static class FlatRenderCheck
{
    const string GameScenePath = "Assets/Game/Scenes/GameScene.unity";
    const string VideoMaterialPath = "Assets/Game/Art/Materials/VideoPicture.mat";
    const string NoteMaterialPath = "Assets/Game/Art/Materials/Note.mat";
    const string ReportPath = "/tmp/flat_render_report.txt";
    const string SceneShotPath = "/tmp/flat_game_scene.png";

    static readonly List<string> Report = new List<string>();
    static int failures;

    public static void Run()
    {
        Audit();
        Measure();

        File.WriteAllLines(ReportPath, Report);
        Debug.Log($"[FlatRenderCheck] report written to {ReportPath} (failures: {failures})");

        if (failures > 0)
        {
            Debug.LogError($"[FlatRenderCheck] {failures} problem(s) found");
            EditorApplication.Exit(1);
        }
    }

    static void Log(string line)
    {
        Report.Add(line);
        Debug.Log("[FlatRenderCheck] " + line);
    }

    static void Fail(string line)
    {
        failures++;
        Log("FAIL " + line);
    }

    /// <summary>No light may cast shadows and no renderer may cast or receive them.</summary>
    static void Audit()
    {
        var lights = 0;
        var casters = 0;
        var receivers = 0;

        foreach (var scenePath in AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath))
        {
            if (scenePath.Contains("/NoteEditor/") || scenePath.Contains("/ThirdParty/")) continue;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.shadows == LightShadows.None) continue;
                    lights++;
                    Fail($"light still casts shadows: {scenePath} {light.name}");
                }

                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                    {
                        casters++;
                        Fail($"renderer still casts shadows: {scenePath} {renderer.name}");
                    }

                    if (renderer.receiveShadows)
                    {
                        receivers++;
                        Fail($"renderer still receives shadows: {scenePath} {renderer.name}");
                    }
                }
            }
        }

        foreach (var prefabPath in AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath))
        {
            if (!prefabPath.StartsWith("Assets/Game/")) continue;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.shadows == LightShadows.None) continue;
                lights++;
                Fail($"light still casts shadows: {prefabPath} {light.name}");
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    casters++;
                    Fail($"renderer still casts shadows: {prefabPath} {renderer.name}");
                }

                if (renderer.receiveShadows)
                {
                    receivers++;
                    Fail($"renderer still receives shadows: {prefabPath} {renderer.name}");
                }
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        Log($"audit: lights={lights} casters={casters} receivers={receivers}");
    }

    /// <summary>Measures the pixels of the picture / note materials.</summary>
    static void Measure()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        var camera = Camera.main;
        if (camera == null)
        {
            Fail("GameScene has no main camera");
            return;
        }

        var videoMaterial = AssetDatabase.LoadAssetAtPath<Material>(VideoMaterialPath);
        var noteMaterial = AssetDatabase.LoadAssetAtPath<Material>(NoteMaterialPath);
        if (videoMaterial == null || noteMaterial == null)
        {
            Fail("video or note material is missing");
            return;
        }

        var probe = new GameObject("FlatProbe");
        probe.transform.SetParent(camera.transform, false);
        probe.transform.localPosition = new Vector3(0f, 0f, 2f);
        probe.transform.localRotation = Quaternion.identity;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "FlatProbeQuad";
        quad.transform.SetParent(probe.transform, false);
        UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());

        var gray = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        gray.SetPixels32(Enumerable.Repeat(new Color32(128, 128, 128, 255), 16).ToArray());
        gray.Apply();

        var renderer = quad.GetComponent<MeshRenderer>();

        renderer.sharedMaterial = videoMaterial;
        var originalTexture = videoMaterial.mainTexture;
        videoMaterial.mainTexture = gray;
        var videoPixel = RenderCenterPixel(camera);
        videoMaterial.mainTexture = originalTexture;
        Log($"picture material '{videoMaterial.shader.name}' renders {videoPixel} (expected 0.5 grey)");
        if (Mathf.Abs(videoPixel.r - 0.5f) > 0.02f)
        {
            Fail("the picture material is not unlit - the video looks dull again");
        }

        renderer.sharedMaterial = noteMaterial;
        var notePixel = RenderCenterPixel(camera);
        Log($"note material '{noteMaterial.shader.name}' renders {notePixel} (expected white)");
        if (notePixel.r < 0.95f)
        {
            Fail("the note material is not unlit - the notes look dull again");
        }

        UnityEngine.Object.DestroyImmediate(probe);

        RenderSceneShot(camera);
    }

    static Color RenderCenterPixel(Camera camera)
    {
        var rt = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture;

        camera.targetTexture = rt;
        camera.Render();

        RenderTexture.active = rt;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture.Apply();
        var color = texture.GetPixel(rt.width / 2, rt.height / 2);

        camera.targetTexture = previousTarget;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(texture);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);

        return color;
    }

    static void RenderSceneShot(Camera camera)
    {
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(SceneShotPath, texture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(texture);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        Log("scene image " + SceneShotPath);
    }
}
