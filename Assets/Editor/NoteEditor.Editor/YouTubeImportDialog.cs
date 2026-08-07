using System.IO;
using NoteEditor.Model;
using UnityEditor;
using UnityEngine;

namespace NoteEditor.Presenter.YouTube
{
    public class YouTubeImportDialog : EditorWindow
    {
        private const string ResourcesDir = "Assets/Game/Resources";

        private string _url = "";
        private string _title = "";
        private string _errorMessage = "";

        [MenuItem("Tools/NoteEditor/Import YouTube URL...")]
        public static void Open()
        {
            var window = GetWindow<YouTubeImportDialog>(true, "YouTube 取り込み → FlashBeat へ登録", true);
            window.minSize = new Vector2(480, 200);
            window.maxSize = new Vector2(480, 200);
        }

        void OnEnable()
        {
            _title = ResolveInitialTitle(EditData.Name.Value);
        }

        private static string ResolveInitialTitle(string currentName)
        {
            if (string.IsNullOrWhiteSpace(currentName) || currentName == "Note Editor")
            {
                return "";
            }
            return currentName;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("YouTube URL を入力すると、譜面の保存 + GameScene への配線まで自動で行います。", EditorStyles.boldLabel);
            _url = EditorGUILayout.TextField("YouTube URL", _url);
            _title = EditorGUILayout.TextField("曲名 (空なら現在の譜面の名前を使用)", _title);

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
            }

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_url)))
                {
                    if (GUILayout.Button("Import"))
                    {
                        if (!YouTubeVideoIdParser.TryParse(_url, out var videoId))
                        {
                            _errorMessage = "VideoId を抽出できませんでした。形式を確認してください。";
                            return;
                        }

                        var songName = ResolveInitialTitle(_title);
                        if (string.IsNullOrWhiteSpace(songName))
                        {
                            _errorMessage = "曲名が空です。譜面に名前を付けるか、曲名を入力してください。";
                            return;
                        }

                        EditData.VideoId.Value = videoId;
                        EditData.Name.Value = songName;

                        if (CommitChartAndWire(songName, videoId, out var error))
                        {
                            Close();
                            return;
                        }

                        _errorMessage = error;
                    }
                }
            }
        }

        private static bool CommitChartAndWire(string songName, string videoId, out string error)
        {
            error = null;
            try
            {
                if (!Directory.Exists(ResourcesDir))
                {
                    Directory.CreateDirectory(ResourcesDir);
                }

                var chartPath = Path.Combine(ResourcesDir, songName + ".json");
                var textPath = Path.Combine(ResourcesDir, songName + "_text.json");

                var chartJson = EditDataSerializer.Serialize();
                File.WriteAllText(chartPath, chartJson);
                Debug.Log($"[YouTubeImport] Saved chart: {chartPath}");

                var textData = new TextData
                {
                    StartTime = new float[] { },
                    Furigana = new string[] { },
                    Mondai = new string[] { },
                    romaji = new string[] { },
                    EndTime = new float[] { }
                };
                File.WriteAllText(textPath, JsonUtility.ToJson(textData));
                Debug.Log($"[YouTubeImport] Saved text stub: {textPath}");

                if (!YouTubeSceneWiring.WireYouTubeToGameScene(songName, videoId))
                {
                    error = $"YouTube 配線に失敗しました。Console を確認してください。";
                    return false;
                }

                var meta = new GManagerParallelArrayUpdater.SongMeta
                {
                    Title = songName,
                    Musician = "",
                    VideoId = videoId,
                    Bpm = EditData.BPM.Value,
                    Level = 1,
                    TotalHits = EditData.Notes.Count,
                    Duration = 120f
                };
                if (!GManagerParallelArrayUpdater.AppendSongEntry(meta))
                {
                    error = $"GManager 更新に失敗しました。Console を確認してください。";
                    return false;
                }

                if (!Application.isPlaying)
                {
                    AssetDatabase.Refresh();
                }

                Debug.Log($"[YouTubeImport] Done: {songName} (videoId='{videoId}')");

                var message = Application.isPlaying
                    ? $"「{songName}」の譜面を保存しました。\nPlay Mode を終了すると、YouTube 配線と GManager 登録が自動で反映されます。\n\n曲は Resources/{songName}.json に保存されています。"
                    : $"「{songName}」を FlashBeat に追加しました。\nSelectScene から選曲できます。\n\n曲は Resources/{songName}.json に保存されています。";

                EditorUtility.DisplayDialog(
                    "YouTube 取り込み完了",
                    message,
                    "OK");
                return true;
            }
            catch (System.Exception ex)
            {
                error = $"予期しないエラー: {ex.Message}";
                Debug.LogError($"[YouTubeImport] {ex}");
                return false;
            }
        }
    }
}
