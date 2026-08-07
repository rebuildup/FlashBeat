using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class GManagerParallelArrayUpdater
{
    public class SongMeta
    {
        public string Title;
        public string Musician = "";
        public string VideoId = "";
        public int Bpm = 120;
        public int Level = 1;
        public int TotalHits = 0;
        public float Duration = 0f;
    }

    private const string GManagerPath = "Assets/Game/Scripts/Songs/GManager.cs";

    public static bool AppendSongEntry(SongMeta meta)
    {
        if (Application.isPlaying)
        {
            var captured = meta;
            DeferredEditorActions.Enqueue(() => DoAppend(captured));
            Debug.Log($"[GManagerUpdater] Play Mode detected. GManager append for '{captured.Title}' is queued and will run when Play Mode exits.");
            return true;
        }
        return DoAppend(meta);
    }

    private static bool DoAppend(SongMeta meta)
    {
        if (!File.Exists(GManagerPath))
        {
            Debug.LogError($"[GManagerUpdater] GManager.cs not found at {GManagerPath}");
            return false;
        }

        var original = File.ReadAllText(GManagerPath);
        var updated = ApplyEdits(original, meta);
        if (updated == original) return false;

        var backupPath = GManagerPath + ".bak";
        File.Copy(GManagerPath, backupPath, overwrite: true);
        try
        {
            File.WriteAllText(GManagerPath, updated);
            File.Delete(backupPath);
            AssetDatabase.Refresh();
            Debug.Log($"[GManagerUpdater] Appended '{meta.Title}' to GManager parallel arrays.");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GManagerUpdater] Failed to write GManager.cs: {e.Message}. Restoring backup.");
            File.Copy(backupPath, GManagerPath, overwrite: true);
            File.Delete(backupPath);
            return false;
        }
    }

    public static string ApplyEdits(string source, SongMeta meta)
    {
        source = InsertIntoStringArray(source, "SongName", meta.Title);
        source = InsertIntoStringArray(source, "Musician", meta.Musician ?? "");
        source = InsertIntoStringArray(source, "SongURL", meta.VideoId ?? "");
        source = InsertIntoIntArray(source, "SBPM", meta.Bpm);
        source = InsertIntoIntArray(source, "Slevel", meta.Level);
        source = InsertIntoIntArray(source, "Shit", meta.TotalHits);
        source = InsertIntoFloatArray(source, "SongLong", meta.Duration);
        source = ExtendHiscore(source);
        return source;
    }

    private static string InsertIntoStringArray(string source, string fieldName, string value)
    {
        var pattern = $@"public static readonly string\[\] {fieldName} = \{{([^{{}}]*?)\}};";
        var match = Regex.Match(source, pattern, RegexOptions.Singleline);
        if (!match.Success) return source;

        var body = match.Groups[1].Value.TrimEnd().TrimEnd(',');
        var escaped = value.Replace("\"", "\\\"");
        var newBody = body + ", \"" + escaped + "\"";
        return source.Substring(0, match.Groups[0].Index) +
               $"public static readonly string[] {fieldName} = {{{newBody}}};" +
               source.Substring(match.Groups[0].Index + match.Groups[0].Length);
    }

    private static string InsertIntoIntArray(string source, string fieldName, int value)
    {
        var pattern = $@"public static readonly int\[\] {fieldName} = \{{([^{{}}]*?)\}};";
        var match = Regex.Match(source, pattern, RegexOptions.Singleline);
        if (!match.Success) return source;

        var body = match.Groups[1].Value.TrimEnd().TrimEnd(',');
        var newBody = body + ", " + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return source.Substring(0, match.Groups[0].Index) +
               $"public static readonly int[] {fieldName} = {{{newBody}}};" +
               source.Substring(match.Groups[0].Index + match.Groups[0].Length);
    }

    private static string InsertIntoFloatArray(string source, string fieldName, float value)
    {
        var pattern = $@"public static readonly float\[\] {fieldName} = \{{([^{{}}]*?)\}};";
        var match = Regex.Match(source, pattern, RegexOptions.Singleline);
        if (!match.Success) return source;

        var body = match.Groups[1].Value.TrimEnd().TrimEnd(',');
        var f = value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "f";
        var newBody = body + ", " + f;
        return source.Substring(0, match.Groups[0].Index) +
               $"public static readonly float[] {fieldName} = {{{newBody}}};" +
               source.Substring(match.Groups[0].Index + match.Groups[0].Length);
    }

    private static string ExtendHiscore(string source)
    {
        var pattern = @"public static readonly int\[\] Hiscore = new int\[(\d+)\];";
        var match = Regex.Match(source, pattern);
        if (!match.Success) return source;
        var current = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var replacement = $"public static readonly int[] Hiscore = new int[{current + 1}];";
        return source.Substring(0, match.Index) + replacement + source.Substring(match.Index + match.Length);
    }
}
