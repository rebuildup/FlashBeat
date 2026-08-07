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
                var stem = Path.GetFileNameWithoutExtension(textPath).Replace("_text", "");
                var chartPath = Path.Combine(dir, stem + ".json");
                if (!File.Exists(chartPath))
                {
                    Debug.LogWarning($"[Migrate] Skipped (no chart): {textPath}");
                    continue;
                }

                var chart = JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(chartPath));
                if (chart.lyrics != null && chart.lyrics.mondai != null && chart.lyrics.mondai.Length > 0)
                {
                    Debug.Log($"[Migrate] Already has lyrics, skip: {stem}");
                    continue;
                }

                var text = JsonUtility.FromJson<TextData>(File.ReadAllText(textPath));
                chart.lyrics = new MusicDTO.LyricsDTO
                {
                    startTime = text.StartTime ?? new float[0],
                    furigana  = text.Furigana ?? new string[0],
                    mondai    = text.Mondai ?? new string[0],
                    romaji    = text.romaji ?? new string[0],
                    endTime   = text.EndTime ?? new float[0],
                };

                File.WriteAllText(chartPath, JsonUtility.ToJson(chart, prettyPrint: true));
                File.Delete(textPath);
                var metaPath = textPath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
                count++;
                Debug.Log($"[Migrate] Merged + deleted: {stem}");
            }
            return count;
        }
    }
}
