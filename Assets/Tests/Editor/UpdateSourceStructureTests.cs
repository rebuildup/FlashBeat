using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// ソース構造の検証ヘルパ (issue #118)。
/// ネットワーク・PlayMode に依存しない「更新が勝手に行われない」ことを
/// 静的に保証するための道具。コメントと文字列リテラルを空白に置き換えてから
/// メソッド本体を取り出すので、doc comment 内の同名識別子に惑わされない。
/// </summary>
internal static class UpdateSource
{
    public const string UpdateCheckerPath = "Game/Scripts/Update/UpdateChecker.cs";
    public const string UpdateNoticeUiPath = "Game/Scripts/UI/UpdateNoticeUI.cs";
    public const string RuntimeScriptsRoot = "Game/Scripts";

    public static string ReadAssetFile(string relativePath)
    {
        var full = Path.Combine(Application.dataPath, relativePath);
        Assert.IsTrue(File.Exists(full), "ファイルが見つかりません: " + relativePath);
        return File.ReadAllText(full, Encoding.UTF8);
    }

    /// <summary>コメント (// と /* */) と文字列リテラル ("..." と '...') を空白に置き換える。
    /// 改行は残すので行番号が保たれる。</summary>
    public static string StripCommentsAndStrings(string src)
    {
        var sb = new StringBuilder(src.Length);
        int i = 0;
        while (i < src.Length)
        {
            char c = src[i];
            if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
            {
                while (i < src.Length && src[i] != '\n') { sb.Append(' '); i++; }
                continue;
            }
            if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
            {
                while (i < src.Length && !(src[i] == '*' && i + 1 < src.Length && src[i + 1] == '/'))
                {
                    sb.Append(src[i] == '\n' ? '\n' : ' '); i++;
                }
                if (i < src.Length) { sb.Append("  "); i += 2; }
                continue;
            }
            if (c == '"' || c == '\'')
            {
                char quote = c;
                sb.Append(' '); i++;
                while (i < src.Length)
                {
                    if (src[i] == '\\' && i + 1 < src.Length) { sb.Append("  "); i += 2; continue; }
                    if (src[i] == quote) { sb.Append(' '); i++; break; }
                    sb.Append(src[i] == '\n' ? '\n' : ' '); i++;
                }
                continue;
            }
            sb.Append(c); i++;
        }
        return sb.ToString();
    }

    /// <summary>メソッド宣言の本体 (中括弧の中身) を返す。見つからなければ null。
    /// 呼び出し (Foo(...) の後に ';' や '.' が続くもの) は宣言として扱わない。</summary>
    public static string MethodBody(string stripped, string name)
    {
        int open = MethodBodyRange(stripped, name);
        if (open < 0) { return null; }
        int close = MatchBrace(stripped, open);
        if (close < 0) { return null; }
        return stripped.Substring(open + 1, close - open - 1);
    }

    private static int MatchBrace(string s, int openIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < s.Length; i++)
        {
            if (s[i] == '{') { depth++; }
            else if (s[i] == '}')
            {
                depth--;
                if (depth == 0) { return i; }
            }
        }
        return -1;
    }

    /// <summary>メソッド本体の開始位置 (中括弧 '{' の位置)。見つからなければ -1。</summary>
    public static int MethodBodyRange(string stripped, string name)
    {
        var pattern = @"(?<![\w.])" + Regex.Escape(name) + @"\s*\(";
        foreach (Match m in Regex.Matches(stripped, pattern))
        {
            int close = MatchParen(stripped, m.Index + m.Length - 1);
            if (close < 0) { continue; }
            int i = close + 1;
            while (i < stripped.Length && char.IsWhiteSpace(stripped[i])) { i++; }
            if (i < stripped.Length && stripped[i] == '{') { return i; }
        }
        return -1;
    }

    private static int MatchParen(string s, int openIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < s.Length; i++)
        {
            if (s[i] == '(') { depth++; }
            else if (s[i] == ')')
            {
                depth--;
                if (depth == 0) { return i; }
            }
        }
        return -1;
    }

    /// <summary>すべての出現位置。</summary>
    public static List<int> AllIndices(string haystack, string needle)
    {
        var result = new List<int>();
        int idx = 0;
        while (true)
        {
            idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal);
            if (idx < 0) { break; }
            result.Add(idx);
            idx += needle.Length;
        }
        return result;
    }
}

/// <summary>
/// issue #118「更新は明示式」の構造的な保証。
/// ネットワークを叩かずに「起動だけでは更新しない」「自動適用経路が無い」を
/// ソース構造で固定する (UpdateChecker / UpdateNoticeUI の実コードを検査する)。
/// </summary>
public class UpdateSourceStructureTests
{
    private string checkerSrc;
    private string checkerStripped;
    private string noticeSrc;
    private string noticeStripped;

    [SetUp]
    public void SetUp()
    {
        checkerSrc = UpdateSource.ReadAssetFile(UpdateSource.UpdateCheckerPath);
        checkerStripped = UpdateSource.StripCommentsAndStrings(checkerSrc);
        noticeSrc = UpdateSource.ReadAssetFile(UpdateSource.UpdateNoticeUiPath);
        noticeStripped = UpdateSource.StripCommentsAndStrings(noticeSrc);
    }

    /// <summary>受入条件 1: 起動時の確認 (CheckForUpdate) はダウンロードも適用も行わない。</summary>
    [Test]
    public void CheckForUpdate_NeverDownloadsOrApplies()
    {
        var body = UpdateSource.MethodBody(checkerStripped, "CheckForUpdate");
        Assert.IsNotNull(body, "CheckForUpdate が見つかりません");
        foreach (var forbidden in new[] { "DownloadAndStage", "RequestDownload", "RequestApply",
                                         "TryLaunchUpdater", "Application.Quit", "StartUpdaterProcess" })
        {
            Assert.IsFalse(body.Contains(forbidden),
                           "起動時の確認が '" + forbidden + "' を呼んでいます (起動では更新しないこと)");
        }
    }

    /// <summary>受入条件 2: ダウンロードは RequestDownload (ユーザー操作) からしか始まらない。</summary>
    [Test]
    public void DownloadAndStage_IsStartedOnlyByRequestDownload()
    {
        var requestDownload = UpdateSource.MethodBody(checkerStripped, "RequestDownload");
        Assert.IsNotNull(requestDownload, "RequestDownload が見つかりません");
        Assert.IsTrue(requestDownload.Contains("DownloadAndStage"),
                      "RequestDownload が DownloadAndStage を始めていない");

        // CheckForUpdate 以外のメソッドが DownloadAndStage を始めていないこと
        var check = UpdateSource.MethodBody(checkerStripped, "CheckForUpdate");
        Assert.IsFalse(check.Contains("DownloadAndStage"),
                       "CheckForUpdate がダウンロードを開始しています");
    }

    /// <summary>受入条件 3 + 8: 適用の入口は RequestApply (ユーザー操作) だけ。
    /// プロセス終了 (Application.Quit) もその入口の中だけ。</summary>
    [Test]
    public void ApplicationQuit_ExistsOnlyInsideRequestApply()
    {
        var requestApply = UpdateSource.MethodBody(checkerStripped, "RequestApply");
        Assert.IsNotNull(requestApply, "RequestApply が見つかりません");

        var hits = UpdateSource.AllIndices(checkerStripped, "Application.Quit");
        Assert.AreEqual(1, hits.Count,
                        "UpdateChecker 内の Application.Quit は RequestApply だけのはず (自動適用・自動終了の禁止)");
        Assert.IsTrue(requestApply.Contains("Application.Quit"),
                      "RequestApply が Application.Quit を呼んでいません (適用の後は更新スクリプトが再起動する)");
    }

    /// <summary>受入条件 8: 他のスクリプトも更新を勝手に起動しない。</summary>
    [Test]
    public void NoOtherRuntimeScript_CallsUpdateActions()
    {
        var root = Path.Combine(Application.dataPath, UpdateSource.RuntimeScriptsRoot);
        Assert.IsTrue(Directory.Exists(root), "ランタイムスクリプトのルートが見つかりません: " + root);

        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var rel = file.Substring(Application.dataPath.Length).Replace('\\', '/').TrimStart('/');
            if (rel == UpdateSource.UpdateCheckerPath || rel == UpdateSource.UpdateNoticeUiPath) { continue; }
            var stripped = UpdateSource.StripCommentsAndStrings(File.ReadAllText(file, Encoding.UTF8));
            if (stripped.Contains(".RequestApply(") || stripped.Contains(".RequestDownload(") ||
                stripped.Contains("ApplyStagedUpdateNow"))
            {
                offenders.Add(rel);
            }
        }
        Assert.AreEqual(0, offenders.Count,
                        "更新の入口を呼ぶスクリプトが UpdateChecker / UpdateNoticeUI 以外にあります: " +
                        string.Join(", ", offenders));
    }

    /// <summary>受入条件 3: 通知 UI の操作は RequestDownload / RequestApply の 2 つだけ。</summary>
    [Test]
    public void UpdateNoticeUI_Trigger_CallsOnlyTheExplicitEntries()
    {
        var trigger = UpdateSource.MethodBody(noticeStripped, "Trigger");
        Assert.IsNotNull(trigger, "Trigger が見つかりません");
        Assert.IsTrue(trigger.Contains("RequestDownload") || trigger.Contains("RequestApply"),
                      "Trigger が更新の入口を呼んでいません");
        foreach (var forbidden in new[] { "Application.Quit", "TryLaunchUpdater", "StartCoroutine" })
        {
            Assert.IsFalse(trigger.Contains(forbidden),
                           "Trigger が '" + forbidden + "' を呼んでいます (操作は入口の呼び出しだけ)");
        }

        // Dismiss は状態機械を触らない (押さない = 更新しない・staging は保持)
        var dismiss = UpdateSource.MethodBody(noticeStripped, "Dismiss");
        Assert.IsNotNull(dismiss, "Dismiss が見つかりません");
        Assert.IsFalse(dismiss.Contains("RequestApply"), "Dismiss が適用しています");
        Assert.IsFalse(dismiss.Contains("RequestDownload"), "Dismiss がダウンロードしています");
    }

    /// <summary>受入条件 7: チャンネル判定は pure 関数の EvaluateCheck に集約されている
    /// (prerelease を stable に自動適用しない経路が残っていないこと)。</summary>
    [Test]
    public void EvaluateCheck_TakesChannelFlagExplicitly()
    {
        var body = UpdateSource.MethodBody(checkerStripped, "EvaluateCheck");
        Assert.IsNotNull(body, "EvaluateCheck が見つかりません");
        Assert.IsTrue(body.Contains("includePrerelease"),
                      "EvaluateCheck がチャンネル (includePrerelease) を受け取っていません");
        // EvaluateCheck 自身は「候補の判定」だけ — 通信もダウンロードも_apply もしない。
        foreach (var forbidden in new[] { "UnityWebRequest", "Download", "Apply", "Quit" })
        {
            Assert.IsFalse(body.Contains(forbidden),
                           "EvaluateCheck が '" + forbidden + "' を含んでいます (判定だけのはず)");
        }
    }
}
