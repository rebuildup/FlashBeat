#if UNITY_EDITOR
using System.IO;
using UnityEngine;

public class MakeFileSceneManager : MonoBehaviour
{
    private const string ResourcesDir = "Assets/Game/Resources";

    void Start()
    {
        var jsonPath = UnityEditor.EditorUtility.OpenFilePanel("譜面 JSON を選択", ResourcesDir, "json");
        if (string.IsNullOrEmpty(jsonPath)) return;

        var json = File.ReadAllText(jsonPath);
        var chart = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(json);
        var songName = Path.GetFileNameWithoutExtension(jsonPath);

        var meta = SongMetaDialog.ShowAndCollect(songName, chart.BPM);
        if (meta == null) return;

        WriteTextJsonStub(songName);

        if (!string.IsNullOrEmpty(chart.videoId))
        {
            if (!YouTubeSceneWiring.WireYouTubeToGameScene(songName, chart.videoId))
            {
                Debug.LogError($"[MakeFile] YouTube wiring failed for {songName}");
            }
        }

        meta.VideoId = chart.videoId ?? "";
        if (!GManagerParallelArrayUpdater.AppendSongEntry(meta))
        {
            Debug.LogError($"[MakeFile] GManager update failed for {songName}");
        }

        UnityEditor.AssetDatabase.Refresh();
        Debug.Log($"[MakeFile] Done: {songName}");
    }

    private void WriteTextJsonStub(string songName)
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
