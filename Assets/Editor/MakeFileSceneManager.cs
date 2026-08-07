#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public class MakeFileSceneManager : MonoBehaviour
{
    private const string ResourcesDir = "Assets/Game/Resources";

    [MenuItem("Tools/NoteEditor/Add Song to FlashBeat...")]
    public static void OrchestrateFromMenu()
    {
        var jsonPath = EditorUtility.OpenFilePanel("譜面 JSON を選択", ResourcesDir, "json");
        if (string.IsNullOrEmpty(jsonPath))
        {
            Debug.Log("[MakeFile] Cancelled.");
            return;
        }

        Orchestrate(jsonPath);
    }

    public static void Orchestrate(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"[MakeFile] Not found: {jsonPath}");
            return;
        }

        var json = File.ReadAllText(jsonPath);
        var chart = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(json);
        var songName = Path.GetFileNameWithoutExtension(jsonPath);

        var meta = SongMetaDialog.ShowAndCollect(songName, chart.BPM);
        if (meta == null)
        {
            Debug.Log("[MakeFile] Cancelled at metadata dialog.");
            return;
        }

        WriteTextJsonStub(songName);

        var videoId = string.IsNullOrEmpty(chart.videoId) ? meta.VideoId : chart.videoId;
        if (!string.IsNullOrEmpty(videoId))
        {
            if (!YouTubeSceneWiring.WireYouTubeToGameScene(songName, videoId))
            {
                Debug.LogError($"[MakeFile] YouTube wiring failed for {songName}");
            }
        }
        else
        {
            Debug.Log($"[MakeFile] No VideoId for {songName}; skipping YouTube wiring.");
        }

        meta.VideoId = videoId ?? "";
        if (!GManagerParallelArrayUpdater.AppendSongEntry(meta))
        {
            Debug.LogError($"[MakeFile] GManager update failed for {songName}");
        }

        AssetDatabase.Refresh();
        Debug.Log($"[MakeFile] Done: {songName} (videoId='{videoId}').");
    }

    private static void WriteTextJsonStub(string songName)
    {
        var data = new TextData
        {
            StartTime = new float[] { },
            Furigana = new string[] { },
            Mondai = new string[] { },
            romaji = new string[] { },
            EndTime = new float[] { }
        };
        var json = JsonUtility.ToJson(data);
        var path = Path.Combine(ResourcesDir, songName + "_text.json");
        File.WriteAllText(path, json);
        Debug.Log($"[MakeFile] Wrote stub: {path}");
    }
}
#endif
