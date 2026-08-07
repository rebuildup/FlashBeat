using NoteEditor.Presenter.YouTube;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class NoteEditorToolbarButtonInstaller
{
    private const string ScenePath = "Assets/NoteEditor/Scenes/NoteEditor.unity";
    private const string ButtonName = "YouTubeImportButton";

    [MenuItem("Tools/NoteEditor/Install YouTube Import Button")]
    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var existing = GameObject.Find(ButtonName);
        if (existing != null)
        {
            Debug.Log($"[Installer] '{ButtonName}' already exists. Skipping.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        var toolstripRoot = GameObject.Find("Toolstrip");
        if (toolstripRoot == null)
        {
            Debug.LogError("[Installer] 'Toolstrip' GameObject not found in NoteEditor.unity. " +
                           "Open the scene and identify the toolbar parent GameObject name.");
            return;
        }

        var go = new GameObject(ButtonName);
        go.transform.SetParent(toolstripRoot.transform, worldPositionStays: false);
        go.AddComponent<RectTransform>();
        go.AddComponent<CanvasRenderer>();
        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.6f, 0.9f, 1f);

        var button = go.AddComponent<Button>();
        var presenter = go.AddComponent<YouTubeImportButtonPresenter>();

        var soButton = new SerializedObject(button);
        var soPresenter = new SerializedObject(presenter);

        soButton.FindProperty("m_Colors.m_NormalColor").colorValue = new Color(1f, 1f, 1f, 1f);
        soButton.ApplyModifiedProperties();

        soPresenter.FindProperty("importButton").objectReferenceValue = button;
        soPresenter.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Installer] Installed '{ButtonName}' under Toolstrip. Button opens YouTubeImportDialog on click.");
    }
}
