using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 全シーン内の TextMeshProUGUI のフォントを一括でNotoSansJP-Mediumに差し替えるEditor utility。
/// Menu: Tools > FlashBeat > Fix Japanese Fonts
/// </summary>
public class JapaneseFontFixer : EditorWindow
{
    private const string JapaneseFontAssetPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset";
    private const string TmpSettingsPath = "Assets/ThirdParty/TextMesh Pro/Resources/TMP Settings.asset";
    private const string GameSceneFolder = "Assets/Game/Scenes";
    private const string GamePrefabFolder = "Assets/Game/Prefabs";

    private TMP_FontAsset targetFont;
    private string statusMessage = "";
    private int replacedCount = 0;

    [MenuItem("Tools/FlashBeat/Fix Japanese Fonts")]
    public static void ShowWindow()
    {
        GetWindow<JapaneseFontFixer>("Japanese Font Fixer");
    }

    private void OnGUI()
    {
        GUILayout.Label("TextMeshPro 日本語フォント一括修正", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "置換先フォント (NotoSansJP-Medium SDF)",
            targetFont,
            typeof(TMP_FontAsset),
            false
        );

        EditorGUILayout.HelpBox(
            "LiberationSans SDF が設定されているすべての TextMeshProUGUI コンポーネントを\n" +
            "指定したフォントに置換します。\n\n" +
            "対象: 現在開いているシーン内のすべてのオブジェクト",
            MessageType.Info
        );

        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(targetFont == null);
        if (GUILayout.Button("現在のシーンのフォントを修正", GUILayout.Height(40)))
        {
            FixCurrentScene();
        }
        if (GUILayout.Button("全シーンのフォントを修正 (保存が必要)", GUILayout.Height(40)))
        {
            FixAllScenes();
        }
        EditorGUI.EndDisabledGroup();

        if (targetFont == null)
        {
            EditorGUILayout.HelpBox("上のフィールドにフォントアセットをドラッグしてください。", MessageType.Warning);
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }
    }

    private void FixCurrentScene()
    {
        replacedCount = 0;
        var allTMP = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        foreach (var tmp in allTMP)
        {
            ReplaceFont(tmp);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        statusMessage = $"✅ 現在のシーンで {replacedCount} 個の TMP コンポーネントを修正しました。\nCtrl+S でシーンを保存してください。";
        Debug.Log($"[JapaneseFontFixer] {replacedCount} TMP components updated in current scene.");
    }

    private void FixAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            statusMessage = "キャンセルされました。";
            return;
        }

        replacedCount = 0;
        var scenePaths = new List<string>();
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            if (EditorBuildSettings.scenes[i].enabled)
                scenePaths.Add(EditorBuildSettings.scenes[i].path);
        }

        if (scenePaths.Count == 0)
        {
            // Build Settings にシーンがない場合、Assets以下のシーンを検索
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (var guid in guids)
                scenePaths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        foreach (var scenePath in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var allTMP = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            int sceneCount = 0;
            foreach (var tmp in allTMP)
            {
                if (ReplaceFont(tmp)) sceneCount++;
            }
            if (sceneCount > 0)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[JapaneseFontFixer] {sceneCount} components updated in {scenePath}");
            }
        }

        statusMessage = $"✅ 全シーンで合計 {replacedCount} 個の TMP コンポーネントを修正・保存しました。";
        Debug.Log($"[JapaneseFontFixer] Total {replacedCount} TMP components updated across all scenes.");
    }

    private bool ReplaceFont(TextMeshProUGUI tmp)
    {
        bool changed = ApplyJapaneseFont(tmp, targetFont);
        if (changed)
        {
            replacedCount++;
        }
        return changed;
    }

    /// <summary>
    /// テキストコンポーネントのフォント (とマテリアル) を日本語フォントへ差し替える。
    /// LiberationSans SDF ・ 未解決参照 (font == null) ・ 既に同じフォントの場合のみ変更する。
    /// </summary>
    private static bool ApplyJapaneseFont(TMP_Text tmp, TMP_FontAsset font)
    {
        if (tmp == null || font == null)
        {
            return false;
        }

        TMP_FontAsset current = tmp.font;
        if (current != null && current != font && !current.name.Contains("LiberationSans"))
        {
            return false;
        }

        bool changed = false;

        if (current != font)
        {
            Undo.RecordObject(tmp, "Fix Japanese Font");
            tmp.font = font;
            changed = true;
        }

        if (tmp.fontSharedMaterial != font.material)
        {
            Undo.RecordObject(tmp, "Fix Japanese Font");
            tmp.fontSharedMaterial = font.material;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(tmp);
        }

        return changed;
    }

    /// <summary>
    /// ゲームシーン・プレハブ・TMP の既定フォントを日本語フォントへ一括で差し替える。
    /// バッチモードから -executeMethod JapaneseFontFixer.ApplyJapaneseFontToProject で実行できる。
    /// </summary>
    [MenuItem("Tools/FlashBeat/Apply Japanese Font To Scenes And Prefabs")]
    public static void ApplyJapaneseFontToProject()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapaneseFontAssetPath);
        if (font == null)
        {
            Debug.LogError($"[JapaneseFontFixer] Font asset not found: {JapaneseFontAssetPath}");
            return;
        }

        int total = 0;

        foreach (string scenePath in CollectScenePaths())
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int sceneCount = 0;

            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text tmp in texts)
            {
                if (ApplyJapaneseFont(tmp, font))
                {
                    sceneCount++;
                }
            }

            if (sceneCount > 0)
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            total += sceneCount;
            Debug.Log($"[JapaneseFontFixer] {scenePath}: {sceneCount} components updated.");
        }

        foreach (string prefabPath in CollectPrefabPaths())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            int prefabCount = 0;

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text tmp in texts)
            {
                if (ApplyJapaneseFont(tmp, font))
                {
                    prefabCount++;
                }
            }

            if (prefabCount > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }

            PrefabUtility.UnloadPrefabContents(root);

            if (prefabCount > 0)
            {
                Debug.Log($"[JapaneseFontFixer] {prefabPath}: {prefabCount} components updated.");
            }

            total += prefabCount;
        }

        total += ApplyDefaultFont(font);
        AssetDatabase.SaveAssets();

        Debug.Log($"[JapaneseFontFixer] Total {total} components updated.");
    }

    private static List<string> CollectScenePaths()
    {
        var paths = new List<string>();
        var seen = new HashSet<string>();

        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            if (EditorBuildSettings.scenes[i].enabled && seen.Add(EditorBuildSettings.scenes[i].path))
            {
                paths.Add(EditorBuildSettings.scenes[i].path);
            }
        }

        if (Directory.Exists(GameSceneFolder))
        {
            foreach (string path in Directory.GetFiles(GameSceneFolder, "*.unity", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                if (seen.Add(assetPath))
                {
                    paths.Add(assetPath);
                }
            }
        }

        return paths;
    }

    private static List<string> CollectPrefabPaths()
    {
        var paths = new List<string>();

        if (!Directory.Exists(GamePrefabFolder))
        {
            return paths;
        }

        foreach (string path in Directory.GetFiles(GamePrefabFolder, "*.prefab", SearchOption.AllDirectories))
        {
            paths.Add(path.Replace('\\', '/'));
        }

        return paths;
    }

    /// <summary>
    /// TMP の既定フォントを日本語フォントへ変更する (未解決参照のコンポーネントは既定フォントで描画される)。
    /// </summary>
    private static int ApplyDefaultFont(TMP_FontAsset font)
    {
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings == null)
        {
            Debug.LogWarning($"[JapaneseFontFixer] TMP settings not found: {TmpSettingsPath}");
            return 0;
        }

        SerializedObject serialized = new SerializedObject(settings);
        SerializedProperty property = serialized.FindProperty("m_defaultFontAsset");
        if (property == null || property.objectReferenceValue == font)
        {
            return 0;
        }

        property.objectReferenceValue = font;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);

        Debug.Log("[JapaneseFontFixer] TMP default font asset updated.");
        return 1;
    }
}
