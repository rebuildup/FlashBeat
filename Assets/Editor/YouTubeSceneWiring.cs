using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YoutubePlayer.Components;

public static class YouTubeSceneWiring
{
    private const string GameScenePath = "Assets/Game/Scenes/GameScene.unity";

    public static bool WireYouTubeToGameScene(string songName, string videoId)
    {
        var loadedScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        var musicManager = Object.FindObjectsOfType<MusicManager>();
        if (musicManager.Length == 0)
        {
            Debug.LogError("[YouTubeSceneWiring] MusicManager not found in GameScene.");
            return false;
        }
        var mm = musicManager[0];
        var mmSO = new SerializedObject(mm);
        var yPlayerProp = mmSO.FindProperty("YPlayer");
        var vPlayerProp = mmSO.FindProperty("VPlayer");

        var template = (yPlayerProp != null && yPlayerProp.isArray && yPlayerProp.arraySize > 0)
            ? yPlayerProp.GetArrayElementAtIndex(0).objectReferenceValue as GameObject
            : null;
        if (template == null)
        {
            Debug.LogError("[YouTubeSceneWiring] MusicManager.YPlayer[0] is null. Set a template YoutubePlayer in GameScene.");
            return false;
        }

        var targetName = "YoutubePlayer_" + songName;
        var existing = GameObject.Find(targetName);
        if (existing != null)
        {
            Debug.LogWarning($"[YouTubeSceneWiring] '{targetName}' already exists in GameScene. Skipping.");
            return true;
        }

        GameObject clone = null;
        try
        {
            clone = (GameObject)PrefabUtility.InstantiatePrefab(template);
            clone.name = targetName;

            var ivp = clone.GetComponent<InvidiousVideoPlayer>();
            if (ivp != null)
            {
                var so = new SerializedObject(ivp);
                var prop = so.FindProperty("VideoId");
                if (prop != null)
                {
                    prop.stringValue = videoId;
                    so.ApplyModifiedProperties();
                }
                else
                {
                    Debug.LogWarning($"[YouTubeSceneWiring] VideoId property not found on '{targetName}'. Clone will have empty VideoId.");
                }
            }

            if (yPlayerProp != null && yPlayerProp.isArray)
            {
                yPlayerProp.arraySize++;
                yPlayerProp.GetArrayElementAtIndex(yPlayerProp.arraySize - 1).objectReferenceValue = clone;
                mmSO.ApplyModifiedProperties();
            }

            if (vPlayerProp != null && vPlayerProp.isArray)
            {
                vPlayerProp.arraySize++;
                vPlayerProp.GetArrayElementAtIndex(vPlayerProp.arraySize - 1).objectReferenceValue = null;
                vPlayerProp.serializedObject.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(loadedScene);
            EditorSceneManager.SaveScene(loadedScene);
            Debug.Log($"[YouTubeSceneWiring] Wired '{targetName}' with VideoId '{videoId}'.");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[YouTubeSceneWiring] Wiring failed: {ex.Message}");
            if (clone != null)
            {
                Object.DestroyImmediate(clone);
            }
            EditorSceneManager.CloseScene(loadedScene, false);
            return false;
        }
    }
}