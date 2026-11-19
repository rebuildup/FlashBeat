using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v6 画面が文字グリフ (矢印・ chevron 記号) をアイコン代わりに描かないことの
/// ガード (Issue #750)。
///
/// #733 で Option / Pause / KeyConfig / VolumeSlider、#750 で残りの画面
/// (Select / History / Ranking / Title / Result モーダル / HUD メニュー) を
/// <see cref="FlashBeat.UI.V6.V6Icons"/> / V6Legend.IconItem のスプライト経路へ
/// 統一した。文字列リテラルへのグリフの再混入をソース走査で防ぐ。
///
/// 対象は <c>Game/Scripts/UI/V6/**</c> の .cs。コメント中の矢印言及は
/// 日本語コメントの普通の表記なので対象外 (行コメントを除いてから走査する)。
/// V5 のキー名マッピング (ActionKeyHint / ShortcutKeyCap) は「キーの名前」で
/// あってアイコン代用ではないため対象外。
/// </summary>
public class V6GlyphIconGuardTests
{
    /// <summary>アイコン代用として使われてはいけない文字グリフ。</summary>
    private const string GlyphChars = "←→↑↓›«»";

    private static readonly Regex GlyphLiteral = new Regex(
        "\"[^\"\\n]*[" + GlyphChars + "][^\"\\n]*\"|'[" + GlyphChars + "]'",
        RegexOptions.Compiled);

    [Test]
    public void V6Sources_DoNotRenderArrowGlyphsAsIcons()
    {
        string root = Path.Combine(Application.dataPath, "Game/Scripts/UI/V6");
        Assert.IsTrue(Directory.Exists(root), "v6 UI ソースが見つからない: " + root);

        var offenders = new StringBuilder();
        int scanned = 0;
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            scanned++;
            string src = StripLineComments(File.ReadAllText(file));
            foreach (Match m in GlyphLiteral.Matches(src))
            {
                offenders.Append(Path.GetFileName(file)).Append(": ")
                    .Append(m.Value).Append('\n');
            }
        }

        Assert.Greater(scanned, 0, "走査対象の .cs が 0 件 (パス誤りの可能性)");
        Assert.AreEqual(string.Empty, offenders.ToString(),
            "v6 UI の文字列リテラルに矢印グリフがあります。"
            + "アイコンは V6Icons / V6Legend.IconItem (Resources/Icons スプライト) で描いてください:\n"
            + offenders);
    }

    /// <summary>
    /// 行コメント (// ...) を除去する。文字列リテラル内の // は残す
    /// ("a//b" のような誤切断を避けるため、文字列中かどうかを追跡する)。
    /// このコードベースに /* */ ブロックコメントと @"..." verbatim 文字列は無いが、
    /// verbatim は一応処理する。
    /// </summary>
    private static string StripLineComments(string src)
    {
        var sb = new StringBuilder(src.Length);
        bool inString = false;
        bool verbatim = false;
        for (int i = 0; i < src.Length; i++)
        {
            char c = src[i];
            if (inString)
            {
                sb.Append(c);
                if (verbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < src.Length && src[i + 1] == '"')
                        {
                            sb.Append(src[++i]); // "" エスケープ
                        }
                        else
                        {
                            inString = false;
                        }
                    }
                }
                else if (c == '\\')
                {
                    if (i + 1 < src.Length)
                    {
                        sb.Append(src[++i]);
                    }
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '@' && i + 1 < src.Length && src[i + 1] == '"')
            {
                inString = true;
                verbatim = true;
                sb.Append(c);
                continue;
            }

            if (c == '"')
            {
                inString = true;
                sb.Append(c);
                continue;
            }

            if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
            {
                while (i < src.Length && src[i] != '\n')
                {
                    i++;
                }

                if (i < src.Length)
                {
                    sb.Append('\n');
                }

                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
