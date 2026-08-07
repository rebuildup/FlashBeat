using NoteEditor.Model;
using UnityEditor;
using UnityEngine;

namespace NoteEditor.Presenter.YouTube
{
    public class YouTubeImportDialog : EditorWindow
    {
        private string _input = "";
        private string _errorMessage = "";

        [MenuItem("Tools/NoteEditor/Import YouTube URL...")]
        public static void Open()
        {
            var window = GetWindow<YouTubeImportDialog>(true, "YouTube VideoId 取り込み", true);
            window.minSize = new Vector2(420, 120);
            window.maxSize = new Vector2(420, 120);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("YouTube URL または VideoId (11文字) を入力:", EditorStyles.boldLabel);
            _input = EditorGUILayout.TextField(_input);

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

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_input)))
                {
                    if (GUILayout.Button("Import"))
                    {
                        if (YouTubeVideoIdParser.TryParse(_input, out var videoId))
                        {
                            EditData.VideoId.Value = videoId;
                            Debug.Log($"[YouTubeImport] VideoId set: {videoId}");
                            Close();
                        }
                        else
                        {
                            _errorMessage = "VideoId を抽出できませんでした。形式を確認してください。";
                        }
                    }
                }
            }
        }
    }
}