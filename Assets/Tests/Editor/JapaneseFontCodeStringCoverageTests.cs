using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Issue #140: コード内に埋め込まれた日本語文字列 (UI に出す文言) が
/// 豆腐 (□) にならないことを保証するテスト。
///
/// 判定の根拠:
/// - 主アセット (NotoSansJP-Medium SDF) は静的アトラスなので、収録済みの文字は
///   TMP_FontAsset.HasCharacters(string, out List{char}) で厳密に判定できる。
/// - 動的フォールバック (NotoSansJP-Dynamic SDF) は AtlasPopulationMode.Dynamic なので
///   アトラスには文字が入っていない (= HasCharacters は常に false を返す)。
///   したがって「主アトラスに無い文字は動的フォールバックで実行時に生成される」とみなす。
///   その前提条件 (アセットが存在する / Dynamic Population である / SourceFont がある)
///   を先に検証し、実際に生成できるwhether を SourceFont から一時的に作った
///   動的 TMP_FontAsset の TryAddCharacters で確認する。
/// - 動的フォールバックは主アセットの fallbackFontAssetTable に登録されていないと
///   実際の描画で使われないため、その登録も検証する。
/// </summary>
public class JapaneseFontCodeStringCoverageTests
{
    private const string MainFontPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset";
    private const string DynamicFallbackPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Dynamic SDF.asset";
    private const string CodeFolder = "Assets/Game/Scripts";

    /// <summary>
    /// コード内の日本語文字列リテラルに含まれる文字が、主アトラスか動的フォールバックの
    /// どちらかでレンダリング可能であること。
    /// </summary>
    [Test]
    public void CodeStringLiterals_AreCoveredByMainAtlasOrDynamicFallback()
    {
        TMP_FontAsset main = LoadFont(MainFontPath);
        TMP_FontAsset dynamicFallback = LoadFont(DynamicFallbackPath);

        // 動的フォールバックが「実行時に文字を生成できる」前提条件。
        Assert.AreEqual(AtlasPopulationMode.Dynamic, dynamicFallback.atlasPopulationMode,
            "The dynamic fallback font asset must use AtlasPopulationMode.Dynamic, otherwise missing glyphs are never generated at runtime.");
        Assert.IsNotNull(dynamicFallback.sourceFontFile,
            "The dynamic fallback font asset must have a source font file, otherwise it cannot rasterize any glyph.");

        SortedSet<char> japanese = CollectJapaneseCharsInCodeStringLiterals();
        Assert.Greater(japanese.Count, 0,
            "No Japanese string literal was found under " + CodeFolder + ". The extraction is broken - the test would pass vacuously.");

        List<char> missingFromMainAtlas = new List<char>();
        main.HasCharacters(new string(ToCharArray(japanese)), out missingFromMainAtlas);

        if (missingFromMainAtlas.Count == 0)
        {
            // すべて主アトラスに収録済み。追加の検証は不要。
            return;
        }

        // 主アトラスに無い文字は「動的フォールバックで実行時に生成できる」必要がある。
        // Dynamic アセットはアトラスが空なので、主アセットの HasCharacters では判定できない。
        // 同じ SourceFont から一時的な動的アセットを作って TryAddCharacters し、
        // 実際に生成できることを確認する (リポジトリのアセットは汚さない)。
        List<char> notGeneratable = FindCharactersThatCannotBeGenerated(dynamicFallback.sourceFontFile, missingFromMainAtlas);

        Assert.AreEqual(0, notGeneratable.Count,
            "These Japanese characters used in code string literals are in neither the static atlas nor the dynamic fallback: "
            + new string(ToCharArray(new SortedSet<char>(notGeneratable)))
            + " (missing from the static atlas: " + new string(ToCharArray(new SortedSet<char>(missingFromMainAtlas))) + ")");
    }

    /// <summary>
    /// 動的フォールバックが主アセットの fallbackFontAssetTable に登録されていること。
    /// ここが外れると「動的で賄える」判定は実際の描画には効かない。
    /// </summary>
    [Test]
    public void DynamicFallback_IsRegisteredOnTheMainFontAsset()
    {
        TMP_FontAsset main = LoadFont(MainFontPath);
        TMP_FontAsset dynamicFallback = LoadFont(DynamicFallbackPath);

        Assert.IsNotNull(main.fallbackFontAssetTable,
            "The main font asset has no fallback font asset table.");
        Assert.IsTrue(main.fallbackFontAssetTable.Contains(dynamicFallback),
            "The dynamic fallback font asset must be registered in " + MainFontPath + " / fallbackFontAssetTable, " +
            "otherwise missing glyphs are not resolved at runtime.");
    }

    private static TMP_FontAsset LoadFont(string path)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        Assert.IsNotNull(font, "Font asset is missing: " + path);
        return font;
    }

    private static char[] ToCharArray(SortedSet<char> chars)
    {
        char[] array = new char[chars.Count];
        chars.CopyTo(array);
        return array;
    }

    /// <summary>
    /// 与えられた文字のうち、動的 TMP_FontAsset として生成できないものを返す。
    /// 判定には SourceFont から一時的な動的アセットを作る (既存アセットは変更しない)。
    /// </summary>
    private static List<char> FindCharactersThatCannotBeGenerated(Font sourceFont, List<char> characters)
    {
        var notGeneratable = new List<char>();

        TMP_FontAsset probe = null;

        try
        {
            // 実行時と同じ経路 (SourceFont -> 動的 TMP_FontAsset -> TryAddCharacters) で判定する。
            probe = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                48,
                5,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[JapaneseFontCodeStringCoverageTests] could not create a probe font asset (" + e.Message +
                "); falling back to Font.HasCharacter.");
        }

        if (probe != null)
        {
            try
            {
                foreach (char c in characters)
                {
                    if (!probe.TryAddCharacters(c.ToString(), false))
                    {
                        notGeneratable.Add(c);
                    }
                }

                return notGeneratable;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        foreach (char c in characters)
        {
            if (!sourceFont.HasCharacter(c))
            {
                notGeneratable.Add(c);
            }
        }

        return notGeneratable;
    }

    /// <summary>
    /// Assets/Game/Scripts 以下の .cs から、**文字列リテラルに含まれる日本語文字** だけを集める。
    ///
    /// 抽出方法 (偽陽性を避けるための厳密な字句走査):
    /// - 行コメント (//) とブロックコメント (/* */) は読み飛ばす。
    /// - 文字リテラル ('x') は読み飛ばす。
    /// - 通常の文字列 ("...") / verbatim 文字列 (@"...") / 補間文字列 ($"..." , $@"...")
    ///   の中身だけを見る。エスケープ列は復号し、\uXXXX は実際の文字として扱う。
    /// - 補間文字列の {} ホールの中身 (コード) は読み飛ばす。
    /// - 抽出した文字列のうち、日本語レンジに含まれる文字だけを採用する。
    ///   (変数名・コメント・識別子との取り違えを防ぐため)
    /// </summary>
    private static SortedSet<char> CollectJapaneseCharsInCodeStringLiterals()
    {
        var result = new SortedSet<char>();

        if (!Directory.Exists(CodeFolder))
        {
            return result;
        }

        foreach (string path in Directory.GetFiles(CodeFolder, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path, Encoding.UTF8);
            foreach (string literal in ExtractStringLiterals(source))
            {
                foreach (char c in literal)
                {
                    if (IsJapanese(c))
                    {
                        result.Add(c);
                    }
                }
            }
        }

        return result;
    }

    private static bool IsJapanese(char c)
    {
        int code = c;
        return (code >= 0x3000 && code <= 0x30FF)   // CJK 記号・ひらがな・カタカナ
            || (code >= 0x3400 && code <= 0x4DBF)   // CJK 拡張 A
            || (code >= 0x4E00 && code <= 0x9FFF)   // CJK 統合漢字
            || (code >= 0xF900 && code <= 0xFAFF)   // CJK 互換漢字
            || (code >= 0xFF00 && code <= 0xFFEF)   // 全角・全角英数字
            || (code >= 0x20000 && code <= 0x2FFFF); // CJK 拡張 B 以降
    }

    /// <summary>
    /// C# のソースから文字列リテラルの中身だけを返す (コメント・文字リテラルは除外)。
    /// </summary>
    private static List<string> ExtractStringLiterals(string source)
    {
        var literals = new List<string>();
        int i = 0;
        int length = source.Length;

        while (i < length)
        {
            char c = source[i];

            // 行コメント
            if (c == '/' && i + 1 < length && source[i + 1] == '/')
            {
                while (i < length && source[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            // ブロックコメント
            if (c == '/' && i + 1 < length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < length && !(source[i] == '*' && source[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(i + 2, length);
                continue;
            }

            // 文字リテラル ('x') は読み飛ばす。文字列リテラルには影響しない。
            if (c == '\'')
            {
                i++;
                while (i < length)
                {
                    if (source[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (source[i] == '\'' || source[i] == '\n')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                continue;
            }

            if (c != '"')
            {
                i++;
                continue;
            }

            // @"..." / $"..." / $@"..." の判定
            bool verbatim = i > 0 && source[i - 1] == '@';
            bool interpolated = (i > 0 && source[i - 1] == '$')
                || (i > 1 && source[i - 1] == '@' && source[i - 2] == '$');

            i++;
            var builder = new StringBuilder();

            while (i < length)
            {
                char d = source[i];

                if (verbatim)
                {
                    if (d == '"')
                    {
                        if (i + 1 < length && source[i + 1] == '"')
                        {
                            builder.Append('"');
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    builder.Append(d);
                    i++;
                    continue;
                }

                if (d == '\\')
                {
                    if (i + 1 < length)
                    {
                        char escaped = source[i + 1];
                        if (escaped == 'u' && i + 5 < length)
                        {
                            int code = int.Parse(source.Substring(i + 2, 4), System.Globalization.NumberStyles.HexNumber);
                            builder.Append((char)code);
                            i += 6;
                            continue;
                        }

                        if (escaped == 'x' && i + 3 < length)
                        {
                            int code = int.Parse(source.Substring(i + 2, 2), System.Globalization.NumberStyles.HexNumber);
                            builder.Append((char)code);
                            i += 4;
                            continue;
                        }

                        builder.Append(escaped);
                        i += 2;
                        continue;
                    }

                    i++;
                    continue;
                }

                if (d == '"')
                {
                    i++;
                    break;
                }

                if (d == '\n')
                {
                    break;
                }

                // 補間文字列のホール (コード) は読み飛ばす。
                if (interpolated && d == '{')
                {
                    i = SkipInterpolationHole(source, i + 1);
                    continue;
                }

                builder.Append(d);
                i++;
            }

            literals.Add(builder.ToString());
        }

        return literals;
    }

    /// <summary>
    /// 補間文字列の {} ホール (式) を読み飛ばして、閉じ括弧の直後のインデックスを返す。
    /// </summary>
    private static int SkipInterpolationHole(string source, int start)
    {
        int depth = 1;
        int i = start;

        while (i < source.Length && depth > 0)
        {
            char c = source[i];

            if (c == '"')
            {
                // ホールの中の文字列リテラルはここでは評価しない (コードなので)。
                i++;
                while (i < source.Length && source[i] != '"')
                {
                    if (source[i] == '\\')
                    {
                        i++;
                    }

                    i++;
                }

                i++;
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
            }

            i++;
        }

        return i;
    }
}
