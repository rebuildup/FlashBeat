using System.IO;
using NoteEditor.DTO;
using UnityEditor;
using UnityEngine;

namespace NoteEditor.Editor
{
    public static class MigrateLegacyTextJson
    {
        [MenuItem("Tools/NoteEditor/Migrate Legacy Text JSON")]
        public static void Run()
        {
            var dir = "Assets/Game/Resources";
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[Migrate] {dir} not found");
                return;
            }
            int migrated = MigrateForTest(dir);
            AssetDatabase.Refresh();
            Debug.Log($"[Migrate] Done: migrated={migrated}");
        }

        // テスト用: temp dir を渡せる純粋関数。返り値は処理件数。
        public static int MigrateForTest(string dir)
        {
            if (!Directory.Exists(dir)) return 0;

            int count = 0;
            foreach (var textPath in Directory.GetFiles(dir, "*_text.json"))
            {
                try
                {
                    var stem = Path.GetFileNameWithoutExtension(textPath).Replace("_text", "");
                    var chartPath = Path.Combine(dir, stem + ".json");
                    if (!File.Exists(chartPath))
                    {
                        Debug.LogWarning($"[Migrate] Skipped (no chart): {textPath}");
                        continue;
                    }

                    var chartText = File.ReadAllText(chartPath);

                    // lyrics 既存チェック (roundtrip せずに peek だけ)
                    var peek = JsonUtility.FromJson<ChartPeek>(chartText);
                    if (peek.lyrics != null && peek.lyrics.mondai != null && peek.lyrics.mondai.Length > 0)
                    {
                        Debug.Log($"[Migrate] Already has lyrics, skip: {stem}");
                        continue;
                    }

                    var text = JsonUtility.FromJson<TextData>(File.ReadAllText(textPath));
                    var lyricsJson = "\"lyrics\":" + JsonUtility.ToJson(new MusicDTO.LyricsDTO
                    {
                        startTime = text.StartTime ?? new float[0],
                        furigana  = text.Furigana ?? new string[0],
                        mondai    = text.Mondai ?? new string[0],
                        romaji    = text.romaji ?? new string[0],
                        endTime   = text.EndTime ?? new float[0],
                    }); // "lyrics":{...} 形式で出力される

                    // 既存 JSON の最後の } の直前に lyrics を挿入
                    var lastBrace = chartText.LastIndexOf('}');
                    if (lastBrace < 0) throw new System.InvalidOperationException("chart JSON has no closing brace");
                    var patched = chartText.Substring(0, lastBrace)
                        + "," + lyricsJson
                        + chartText.Substring(lastBrace);

                    File.WriteAllText(chartPath, patched);
                    File.Delete(textPath);
                    var metaPath = textPath + ".meta";
                    if (File.Exists(metaPath)) File.Delete(metaPath);
                    count++;
                    Debug.Log($"[Migrate] Merged + deleted: {stem}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Migrate] Failed to process {textPath}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            return count;
        }

        [System.Serializable]
        class ChartPeek
        {
            public MusicDTO.LyricsDTO lyrics;
        }
    }
}
