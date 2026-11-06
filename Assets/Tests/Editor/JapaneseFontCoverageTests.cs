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
    private const string ChartFolder = "Assets/Game/Resources";
    private const string PrefabFolder = "Assets/Game/Prefabs";

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
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            ChartData chart = JsonUtility.FromJson<ChartData>(json);

            if (chart.lyrics == null)
            {
                continue;
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

        return builder.ToString();
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
