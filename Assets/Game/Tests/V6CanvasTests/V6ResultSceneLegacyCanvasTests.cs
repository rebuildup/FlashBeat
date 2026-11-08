using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlashBeat.UI.V6.Tests
{
    /// <summary>
    /// ResultScene.unity の v5 レガシー Canvas 割り当てを検証する。
    /// - ResultSceneManager.legacyCanvasV5 がシーン配置 "Canvas" GameObject に割り当て済み
    ///   (v6 モードの BuildV6View → HideLegacyCanvases が旧 UI を隠せる)
    /// - "BackGround" / "スクリーン" は legacy Canvas でもその子でもない
    ///   (v6 経路の隠蔽で巻き込まれない)
    /// シーンは Additive で開き、保存せず閉じる (アセットを変更しない)。
    /// </summary>
    public static class V6ResultSceneLegacyCanvasTests
    {
        private const string ScenePath = "Assets/Game/Scenes/ResultScene.unity";

        [Test]
        public static void ResultScene_AssignsLegacyCanvasV5ToSceneCanvas()
        {
            var scene = OpenResultScene();
            try
            {
                var manager = FindManager(scene);
                Assert.IsNotNull(manager, "ResultScene に ResultSceneManager がある");

                var legacyV5 = GetLegacyCanvas(manager, "legacyCanvasV5");
                Assert.IsNotNull(legacyV5, "legacyCanvasV5 がシーンで割り当て済み");
                Assert.AreEqual("Canvas", legacyV5.gameObject.name,
                    "legacyCanvasV5 は v5 旧 UI を束ねるシーン配置 \"Canvas\" を指す");

                // legacyCanvasV6 は未割り当てのまま (v6 Canvas はランタイム生成)。
                Assert.IsNull(GetLegacyCanvas(manager, "legacyCanvasV6"),
                    "legacyCanvasV6 は割り当てない (v6 Canvas は BuildV6View が生成する)");
            }
            finally
            {
                CloseWithoutSaving(scene);
            }
        }

        [Test]
        public static void ResultScene_BackGroundAndScreen_AreNotHiddenByV6Path()
        {
            var scene = OpenResultScene();
            try
            {
                var background = FindInScene(scene, "BackGround");
                Assert.IsNotNull(background, "ResultScene に BackGround (v6 のアニメーション背景) がある");
                var screen = FindInScene(scene, "スクリーン");
                Assert.IsNotNull(screen, "ResultScene に スクリーン (3D 背景/サムネイルパネル) がある");

                // v6 経路で隠れるのは legacyCanvasV5 / legacyCanvasV6 の 2 つだけ。
                // BackGround / スクリーンはその対象でも Canvas の子でもないこと。
                var manager = FindManager(scene);
                Assert.IsNotNull(manager);
                var legacyV5 = GetLegacyCanvas(manager, "legacyCanvasV5");
                Assert.IsNotNull(legacyV5);
                Assert.AreNotEqual(legacyV5.gameObject, background);
                Assert.AreNotEqual(legacyV5.gameObject, screen);
                Assert.IsFalse(background.transform.IsChildOf(legacyV5.transform),
                    "BackGround が Canvas の子だと Canvas 非表示で巻き込まれる");
                Assert.IsFalse(screen.transform.IsChildOf(legacyV5.transform),
                    "スクリーンが Canvas の子だと Canvas 非表示で巻き込まれる");
                Assert.IsTrue(background.activeSelf, "BackGround は v6 で有効のまま");
                Assert.IsTrue(screen.activeSelf, "スクリーンは v6 で有効のまま");
            }
            finally
            {
                CloseWithoutSaving(scene);
            }
        }

        private static Scene OpenResultScene()
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        private static void CloseWithoutSaving(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static ResultSceneManager FindManager(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var manager = root.GetComponent<ResultSceneManager>();
                if (manager != null)
                {
                    return manager;
                }
            }
            return null;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindRecursive(root.transform, name);
                if (found != null)
                {
                    return found.gameObject;
                }
            }
            return null;
        }

        private static Transform FindRecursive(Transform t, string name)
        {
            if (t.name == name)
            {
                return t;
            }
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindRecursive(t.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static Canvas GetLegacyCanvas(ResultSceneManager manager, string field)
        {
            var serialized = new SerializedObject(manager);
            var prop = serialized.FindProperty(field);
            Assert.IsNotNull(prop, "ResultSceneManager に " + field + " がない");
            return prop.objectReferenceValue as Canvas;
        }
    }
}
