using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 全シーン内の TextMeshProUGUI のフォントを一括でNotoSansJP-Mediumに差し替えるEditor utility。
/// Menu: Tools > FlashBeat > Fix Japanese Fonts
/// </summary>
public class JapaneseFontFixer : EditorWindow
{
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
        // LiberationSans SDF または null の場合に置換
        if (tmp.font == null || tmp.font.name.Contains("LiberationSans"))
        {
            Undo.RecordObject(tmp, "Fix Japanese Font");
            tmp.font = targetFont;
            EditorUtility.SetDirty(tmp);
            replacedCount++;
            return true;
        }
        return false;
    }
}
