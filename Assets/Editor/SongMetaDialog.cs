using UnityEditor;
using UnityEngine;

public class SongMetaDialog : EditorWindow
{
    private GManagerParallelArrayUpdater.SongMeta _meta;
    private bool _accepted;

    public static GManagerParallelArrayUpdater.SongMeta ShowAndCollect(string defaultTitle, int defaultBpm)
    {
        var window = CreateInstance<SongMetaDialog>();
        window.titleContent = new GUIContent("楽曲情報入力");
        window._meta = new GManagerParallelArrayUpdater.SongMeta
        {
            Title = defaultTitle,
            Bpm = defaultBpm,
            Musician = "",
            VideoId = "",
            Level = 1,
            TotalHits = 0,
            Duration = 0f
        };
        window._accepted = false;
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 380, 260);
        window.ShowModalUtility();
        var meta = window._accepted ? window._meta : null;
        DestroyImmediate(window);
        return meta;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("新曲のメタデータを入力:", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _meta.Title = EditorGUILayout.TextField("タイトル (必須)", _meta.Title);
        _meta.Musician = EditorGUILayout.TextField("アーティスト (任意)", _meta.Musician);
        _meta.VideoId = EditorGUILayout.TextField("VideoId (任意)", _meta.VideoId);
        _meta.Bpm = EditorGUILayout.IntField("BPM", _meta.Bpm);
        _meta.Level = EditorGUILayout.IntField("レベル", _meta.Level);
        _meta.TotalHits = EditorGUILayout.IntField("総ノーツ数", _meta.TotalHits);
        _meta.Duration = EditorGUILayout.FloatField("尺 (秒)", _meta.Duration);

        EditorGUILayout.Space();
        GUILayout.FlexibleSpace();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Cancel"))
            {
                _accepted = false;
                Close();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_meta.Title)))
            {
                if (GUILayout.Button("OK"))
                {
                    _accepted = true;
                    Close();
                }
            }
        }
    }
}
