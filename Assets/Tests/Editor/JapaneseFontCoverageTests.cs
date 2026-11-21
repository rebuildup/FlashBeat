using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// HUD の日本語表示が壊れないことを保証するテスト。
/// NotoSansJP SDF アセットは静的アトラスなので、曲名/アーティスト名/歌詞に
/// 新しい漢字が入った場合は JapaneseFontAssetBuilder で再生成 Must be regenerated.
/// </summary>
public class JapaneseFontCoverageTests
{
    private const string FontAssetPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset";
    private const string DynamicFallbackPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Dynamic SDF.asset";
    private const string ChartFolder = "Assets/Game/Resources";
    private const string PrefabFolder = "Assets/Game/Prefabs";

    /// <summary>
    /// Issue #846: クラウド楽曲の歌詞は実行時に取得されリポジトリに存在しない
    /// ため、静的アトラスの対象にはできない。その分のグリフは主フォントの
    /// fallbackFontAssetTable に登録された動的フォントが実行時に生成する
    /// (JapaneseFontAssetBuilder.EnsureDynamicFallback)。ここでは
    /// (1) 静的対象として契約 fixture (contracts/charts/*.json) の歌詞を
    /// 走査に追加し、(2) 動的フォールバックが実際に歌詞グリフを
    /// 追加できることを確認して、クラウド歌詞のカバレッジの穴を塞ぐ。
    /// </summary>
    [Test]
    public void JapaneseFontAsset_DynamicFallbackCoversLyricGlyphs()
    {
        TMP_FontAsset font = LoadJapaneseFont();
        Assert.IsNotNull(font.fallbackFontAssetTable,
            "main font asset must have a fallbackFontAssetTable");

        TMP_FontAsset dynamic = null;
        foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable)
        {
            if (fallback != null
                && fallback.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                dynamic = fallback;
                break;
            }
        }
        Assert.IsNotNull(dynamic,
            "a dynamic TMP_FontAsset must be registered as fallback " +
            "(cloud song lyrics rely on it for glyphs outside the static atlas)");
        Assert.AreEqual(DynamicFallbackPath, AssetDatabase.GetAssetPath(dynamic),
            "the dynamic fallback must be the dedicated NotoSansJP-Dynamic SDF asset");
        Assert.IsNotNull(dynamic.sourceFontFile,
            "the dynamic fallback needs its source font to generate glyphs at runtime");

        // 歌詞らしい文字列 (合成 fixture / 実歌詞で使う漢字・仮名) を
        // 動的アセットへ追加できること。静的アトラス外のグリフも
        // 実行時に同じ経路で生成される。
        const string sample = "僕を連れてって";
        string missing;
        Assert.IsTrue(dynamic.TryAddCharacters(sample, out missing),
            "dynamic fallback must accept lyric glyphs (missing: " + missing + ")");
        dynamic.HasCharacters(sample, out List<char> stillMissing);
        Assert.AreEqual(0, stillMissing.Count,
            "dynamic fallback still misses glyphs: " + string.Join("", stillMissing));
    }

    [Test]
    public void JapaneseFontAsset_CoversEverySongTitleArtistAndLyric()
    {
        TMP_FontAsset font = LoadJapaneseFont();
        string required = CollectGameText();

        List<char> missing = new List<char>();
        font.HasCharacters(required, out missing);

        Assert.AreEqual(0, missing.Count, "NotoSansJP SDF is missing glyphs: " + string.Join("", missing));
    }

    [Test]
    public void TmpDefaultFont_IsTheJapaneseFont()
    {
        TMP_FontAsset font = LoadJapaneseFont();

        Assert.AreSame(font, TMP_Settings.defaultFontAsset,
            "TMP default font asset must be the Japanese font asset, otherwise unresolved font references render as boxes.");
    }

    [Test]
    public void GamePrefabs_UseTheJapaneseFont()
    {
        TMP_FontAsset font = LoadJapaneseFont();

        foreach (string path in Directory.GetFiles(PrefabFolder, "*.prefab"))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            if (prefab == null)
            {
                continue;
            }

            foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.AreSame(font, text.font, PrefabLabel(path) + " / " + text.name + " must use the Japanese font asset.");
            }
        }
    }

    [Test]
    public void BuildScenes_UseTheJapaneseFont()
    {
        TMP_FontAsset font = LoadJapaneseFont();

        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            if (!EditorBuildSettings.scenes[i].enabled)
            {
                continue;
            }

            string path = EditorBuildSettings.scenes[i].path;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text text in texts)
            {
                Assert.AreSame(font, text.font, path + " / " + text.name + " must use the Japanese font asset.");
            }

            Assert.IsTrue(scene.IsValid(), path + " could not be opened.");
        }
    }

    private static TMP_FontAsset LoadJapaneseFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        Assert.IsNotNull(font, "Japanese font asset is missing: " + FontAssetPath);
        return font;
    }

    private static string PrefabLabel(string path)
    {
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// ゲームが画面に出す日本語 (曲名・アーティスト名・歌詞) を集める。
    /// </summary>
    private static string CollectGameText()
    {
        var builder = new System.Text.StringBuilder();

        foreach (string title in GManager.SongName)
        {
            builder.Append(title);
        }

        foreach (string musician in GManager.Musician)
        {
            builder.Append(musician);
        }

        foreach (string path in Directory.GetFiles(ChartFolder, "*.json"))
        {
            AppendChartLyrics(builder, path);
        }

        // Issue #846: クラウド譜面の歌詞は実行時取得でリポジトリに無い。
        // チャート契約 fixture (contracts/charts/*.json、譜面文書と同じ
        // lyrics ブロック形) の歌詞は静的アトラスの対象に含め、
        // それ以外のクラウド歌詞グリフは動的フォールバック
        // (JapaneseFontAsset_DynamicFallbackCoversLyricGlyphs) が担保する。
        string contractChartFolder = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "contracts", "charts"));
        if (Directory.Exists(contractChartFolder))
        {
            foreach (string path in Directory.GetFiles(contractChartFolder, "*.json"))
            {
                AppendChartLyrics(builder, path);
            }
        }

        return builder.ToString();
    }

    /// <summary>1 つのチャート JSON から歌詞文字列 (mondai / furigana) を集める。</summary>
    private static void AppendChartLyrics(System.Text.StringBuilder builder, string path)
    {
        string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        ChartData chart = JsonUtility.FromJson<ChartData>(json);

        if (chart.lyrics == null)
        {
            return;
        }

        if (chart.lyrics.mondai != null)
        {
            builder.Append(string.Join("", chart.lyrics.mondai));
        }

        if (chart.lyrics.furigana != null)
        {
            builder.Append(string.Join("", chart.lyrics.furigana));
        }
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
