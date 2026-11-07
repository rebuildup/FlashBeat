using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// NotoSansJP-Medium SDF アセットを同じフォントの TTF から再生成する Editor utility。
///
/// 既存の SDF アセットは point size 71 / padding 0 / atlas 4096x4096 で作成されたもので、
/// アトラスが 620 文字で埋まり日本語グリフの大半が欠落していた (HUD が □ 表示)。
/// この tool は同じ TTF を使い、プロジェクトが出しうる文字集合 (かな・記号・全角 + 曲名/歌詞/シーン内テキストの漢字)
/// を含むアセットを生成する。アセットのパスと GUID は維持されるので既存の参照は壊れない。
///
/// Menu: Tools > FlashBeat > Rebuild NotoSansJP SDF Asset
/// Batch: -executeMethod JapaneseFontAssetBuilder.RebuildNotoSansJpSdf
/// </summary>
public static class JapaneseFontAssetBuilder
{
    private const string FontAssetPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset";
    private const string SourceFontPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Medium.ttf";
    private const string CharsetPath = "Assets/Game/Fonts/Japanese/japanese_full.txt";
    private const string TempFolder = "Assets/Game/Fonts/Japanese/TempRebuild";
    private const string TempAssetPath = TempFolder + "/NotoSansJP-Medium SDF.asset";
    private const string ChartFolder = "Assets/Game/Resources";
    private const string SceneFolder = "Assets/Game/Scenes";
    private const string PrefabFolder = "Assets/Game/Prefabs";
    private const string CodeFolder = "Assets/Game/Scripts";
    private const string DynamicFallbackPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Dynamic SDF.asset";

    private const int PointSize = 48;
    private const int Padding = 5;
    private const int AtlasSize = 4096;

    [MenuItem("Tools/FlashBeat/Rebuild NotoSansJP SDF Asset")]
    public static void RebuildNotoSansJpSdf()
    {
        SortedSet<char> required;
        SortedSet<char> charset = BuildCharset(out required);
        Debug.Log($"[JapaneseFontAssetBuilder] Character set: {charset.Count} chars ({required.Count} required by the game).");

        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null)
        {
            throw new Exception($"[JapaneseFontAssetBuilder] Source font not found: {SourceFontPath}");
        }

        // 動的モードで生成して文字を追加し、保存時に静的アセットへ戻す
        // (TMP_FontAsset.TryAddCharacters は静的アセットへの追加を拒否するため)
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
            source,
            PointSize,
            Padding,
            GlyphRenderMode.SDFAA,
            AtlasSize,
            AtlasSize,
            AtlasPopulationMode.Dynamic,
            true);

        asset.name = "NotoSansJP-Medium SDF";
        bool allAdded = asset.TryAddCharacters(new string(charset.ToArray()), false);

        SerializedObject serialized = new SerializedObject(asset);
        serialized.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Static;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        List<char> missing = new List<char>();
        asset.HasCharacters(new string(charset.ToArray()), out missing);

        List<char> missingRequired = missing.Where(c => required.Contains(c)).ToList();
        if (missingRequired.Count > 0)
        {
            throw new Exception(
                $"[JapaneseFontAssetBuilder] {missingRequired.Count} required glyph(s) are missing: {new string(missingRequired.ToArray())}");
        }

        if (missing.Count > 0)
        {
            Debug.Log($"[JapaneseFontAssetBuilder] {missing.Count} optional glyph(s) are not renderable and are skipped: {new string(missing.ToArray())}");
        }

        Debug.Log($"[JapaneseFontAssetBuilder] TryAddCharacters complete: {allAdded}, atlas textures: {asset.atlasTextures.Length}.");

        WriteAssetInPlace(asset);

        TMP_FontAsset reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (reloaded == null)
        {
            throw new Exception($"[JapaneseFontAssetBuilder] Rebuilt asset could not be reloaded: {FontAssetPath}");
        }

        List<char> reloadedMissing = new List<char>();
        reloaded.HasCharacters(new string(required.ToArray()), out reloadedMissing);
        if (reloadedMissing.Count > 0)
        {
            throw new Exception(
                $"[JapaneseFontAssetBuilder] Rebuilt asset is missing {reloadedMissing.Count} required glyph(s): {new string(reloadedMissing.ToArray())}");
        }

        EnsureDynamicFallback();

        Debug.Log(
            $"[JapaneseFontAssetBuilder] Rebuilt {FontAssetPath}: all {required.Count} required glyphs present, " +
            $"source font = {(reloaded.sourceFontFile != null ? reloaded.sourceFontFile.name : "<null>")}.");
    }

    /// <summary>
    /// 生成済みアセットを、meta (GUID) を保ったまま対象パスへ書き換える。
    /// </summary>
    private static void WriteAssetInPlace(TMP_FontAsset asset)
    {
        AssetDatabase.DeleteAsset(TempFolder);
        AssetDatabase.CreateFolder("Assets/Game/Fonts/Japanese", "TempRebuild");
        AssetDatabase.CreateAsset(asset, TempAssetPath);

        Texture2D[] atlasTextures = asset.atlasTextures;
        for (int i = 0; i < atlasTextures.Length; i++)
        {
            if (atlasTextures[i] != null)
            {
                AssetDatabase.AddObjectToAsset(atlasTextures[i], asset);
            }
        }

        if (asset.material != null)
        {
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        AssetDatabase.SaveAssets();

        File.Copy(TempAssetPath, FontAssetPath, true);
        AssetDatabase.DeleteAsset(TempFolder);
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
    }

    /// <summary>
    /// プロジェクトが出しうる文字集合を組み立てる。
    /// japanese_full.txt の非漢字 (ASCII / 記号 / かな / 全角) に加えて、
    /// 曲名・アーティスト名・歌詞・シーン/プレハブ内テキストの文字を加える。
    /// required にはゲームが必ず表示する日本語 (曲名・アーティスト名・歌詞) だけが入る。
    /// </summary>
    private static SortedSet<char> BuildCharset(out SortedSet<char> required)
    {
        var chars = new SortedSet<char>();
        required = new SortedSet<char>();

        string full = File.ReadAllText(CharsetPath, Encoding.UTF8);
        foreach (char c in full)
        {
            if (!IsKanji(c))
            {
                chars.Add(c);
            }
        }

        foreach (string text in CollectCriticalTexts())
        {
            foreach (char c in text)
            {
                if (c > 0x7F)
                {
                    chars.Add(c);
                    required.Add(c);
                }
            }
        }

        foreach (string text in CollectSceneTexts())
        {
            foreach (char c in text)
            {
                if (c > 0x7F)
                {
                    chars.Add(c);
                }
            }
        }

        // C# コード内の日本語 (コードで構築した UI の文言)。コメント内の文字も含む上位集合で構わない。
        foreach (string text in CollectCodeTexts())
        {
            foreach (char c in text)
            {
                if (c > 0x7F && !char.IsControl(c) && !char.IsSurrogate(c))
                {
                    chars.Add(c);
                }
            }
        }

        return chars;
    }

    /// <summary>
    /// 曲名・アーティスト名・歌詞 (ゲームが必ず表示する日本語)。
    /// </summary>
    private static List<string> CollectCriticalTexts()
    {
        var texts = new List<string>();
        texts.AddRange(GManager.SongName);
        texts.AddRange(GManager.Musician);

        foreach (string path in Directory.GetFiles(ChartFolder, "*.json"))
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            ChartData chart = JsonUtility.FromJson<ChartData>(json);

            if (chart.lyrics == null)
            {
                continue;
            }

            if (chart.lyrics.mondai != null)
            {
                texts.AddRange(chart.lyrics.mondai);
            }

            if (chart.lyrics.furigana != null)
            {
                texts.AddRange(chart.lyrics.furigana);
            }
        }

        return texts;
    }

    /// <summary>Assets/Game/Scripts 以下の .cs の全文 (日本語文言を拾うための上位集合)。</summary>
    private static List<string> CollectCodeTexts()
    {
        var texts = new List<string>();
        if (!Directory.Exists(CodeFolder)) { return texts; }
        foreach (string path in Directory.GetFiles(CodeFolder, "*.cs", SearchOption.AllDirectories))
        {
            texts.Add(File.ReadAllText(path, Encoding.UTF8));
        }
        return texts;
    }

    /// <summary>
    /// 動的 NotoSansJP をフォールバックとして主アセットに追加する。静的アトラスに無い文字
    /// (来場者が登録した曲名の新しい漢字など) は実行時に生成される。主アセットは再生成のたびに
    /// 書き換わるため、再生成のたびに呼ぶ。
    /// </summary>
    private static void EnsureDynamicFallback()
    {
        TMP_FontAsset main = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (main == null) { throw new Exception("[JapaneseFontAssetBuilder] main font asset missing: " + FontAssetPath); }

        TMP_FontAsset dynamicAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DynamicFallbackPath);
        if (dynamicAsset == null)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            dynamicAsset = TMP_FontAsset.CreateFontAsset(source, PointSize, Padding, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            dynamicAsset.name = "NotoSansJP-Dynamic SDF";
            AssetDatabase.CreateAsset(dynamicAsset, DynamicFallbackPath);
            Texture2D[] textures = dynamicAsset.atlasTextures;
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null) { AssetDatabase.AddObjectToAsset(textures[i], dynamicAsset); }
            }
            if (dynamicAsset.material != null) { AssetDatabase.AddObjectToAsset(dynamicAsset.material, dynamicAsset); }
            AssetDatabase.SaveAssets();
        }

        if (main.fallbackFontAssetTable == null) { main.fallbackFontAssetTable = new List<TMP_FontAsset>(); }
        if (!main.fallbackFontAssetTable.Contains(dynamicAsset))
        {
            main.fallbackFontAssetTable.Add(dynamicAsset);
            EditorUtility.SetDirty(main);
            AssetDatabase.SaveAssets();
        }
        Debug.Log("[JapaneseFontAssetBuilder] dynamic fallback attached: " + DynamicFallbackPath);
    }

    /// <summary>
    /// シーン / プレハブの m_text に書かれた文字列。
    /// </summary>
    private static List<string> CollectSceneTexts()
    {
        var texts = new List<string>();

        foreach (string folder in new[] { SceneFolder, PrefabFolder })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (string path in Directory.GetFiles(folder, "*.unity", SearchOption.AllDirectories))
            {
                texts.AddRange(ExtractTexts(File.ReadAllText(path, Encoding.UTF8)));
            }

            foreach (string path in Directory.GetFiles(folder, "*.prefab", SearchOption.AllDirectories))
            {
                texts.AddRange(ExtractTexts(File.ReadAllText(path, Encoding.UTF8)));
            }
        }

        return texts;
    }

    private static List<string> ExtractTexts(string yaml)
    {
        var texts = new List<string>();

        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(yaml, "m_text: (\\\"(?:[^\\\"\\\\]|\\\\.)*\\\")"))
        {
            texts.Add(Unescape(match.Groups[1].Value));
        }

        return texts;
    }

    /// <summary>
    /// YAML/JSON の文字列リテラル (引用符付き) を素の文字列へ戻す。
    /// </summary>
    private static string Unescape(string literal)
    {
        if (literal.Length < 2)
        {
            return string.Empty;
        }

        string body = literal.Substring(1, literal.Length - 2);
        var builder = new StringBuilder();

        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] != '\\' || i + 1 >= body.Length)
            {
                builder.Append(body[i]);
                continue;
            }

            char next = body[i + 1];
            if (next == 'u' && i + 5 < body.Length)
            {
                int code = int.Parse(body.Substring(i + 2, 4), System.Globalization.NumberStyles.HexNumber);
                builder.Append((char)code);
                i += 5;
                continue;
            }

            builder.Append(next);
            i++;
        }

        return builder.ToString();
    }

    private static bool IsKanji(char c)
    {
        int code = c;
        return (code >= 0x4E00 && code <= 0x9FFF)
            || (code >= 0x3400 && code <= 0x4DBF)
            || (code >= 0xF900 && code <= 0xFAFF);
    }

    [Serializable]
    private class ChartData
    {
        public Lyrics lyrics;
    }

    [Serializable]
    private class Lyrics
    {
        public string[] mondai;
        public string[] furigana;
    }
}
