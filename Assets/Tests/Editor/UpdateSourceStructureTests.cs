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
        var span = MethodBodySpan(stripped, name);
        if (span == null) { return null; }
        return stripped.Substring(span[0] + 1, span[1] - span[0] - 1);
    }

    /// <summary>メソッド本体の範囲 [開始 '{' の位置, 対応する '}' の位置]。見つからなければ null。</summary>
    public static int[] MethodBodySpan(string stripped, string name)
    {
        int open = MethodBodyRange(stripped, name);
        if (open < 0) { return null; }
        int close = MatchBrace(stripped, open);
        if (close < 0) { return null; }
        return new[] { open, close };
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

    /// <summary>
    /// 呼び出し構文で現れる <paramref name="name"/> の位置 (宣言は除く)。
    /// 呼び出し = 「.Foo(」形式、または裸の「Foo(」で閉じ括弧の直後が ';' / '.' などのもの
    /// (閉じ括弧の直後が '{' ならメソッド宣言なので除外する)。
    /// </summary>
    public static List<int> FindCallSites(string stripped, string name)
    {
        var sites = new List<int>();
        var last = name.Contains(".") ? name.Substring(name.LastIndexOf('.') + 1) : name;

        // メンバアクセス呼び出し (obj.Foo(...)) は常に呼び出し。
        foreach (Match m in Regex.Matches(stripped, @"\.\s*" + Regex.Escape(last) + @"\s*\("))
        {
            sites.Add(m.Index);
        }

        // 裸の呼び出しは、閉じ括弧のあとが '{' なら宣言とみなす。
        foreach (Match m in Regex.Matches(stripped, @"(?<![\w.])" + Regex.Escape(last) + @"\s*\("))
        {
            int close = MatchParen(stripped, m.Index + m.Length - 1);
            if (close < 0) { continue; }
            int i = close + 1;
            while (i < stripped.Length && char.IsWhiteSpace(stripped[i])) { i++; }
            if (i < stripped.Length && stripped[i] == '{') { continue; } // 宣言
            sites.Add(m.Index);
        }
        return sites;
    }

    private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
    {
        "if", "else", "for", "foreach", "while", "do", "switch", "case", "return", "yield",
        "new", "lock", "using", "fixed", "typeof", "sizeof", "nameof", "default", "checked",
        "unchecked", "throw", "try", "catch", "finally", "when", "in", "is", "as",
    };

    /// <summary>本文中に現れる呼び出し先の名前 (重複排除)。宣言と C# キーワードは含まない。
    /// メンバ呼び出し (obj.Foo(...)) も "Foo" として拾う。</summary>
    public static HashSet<string> CallNames(string body)
    {
        var names = new HashSet<string>();
        foreach (Match m in Regex.Matches(body, @"(?<![\w])(?<name>[A-Za-z_]\w*)\s*\("))
        {
            int close = MatchParen(body, m.Index + m.Length - 1);
            if (close < 0) { continue; }
            int i = close + 1;
            while (i < body.Length && char.IsWhiteSpace(body[i])) { i++; }
            if (i < body.Length && body[i] == '{') { continue; } // 宣言
            var name = m.Groups["name"].Value;
            if (CSharpKeywords.Contains(name)) { continue; }
            names.Add(name);
        }
        return names;
    }

    /// <summary>利用者操作の入口から到達できるコードの範囲 (呼び出しグラフの閉包)。
    /// roots から直接・間接に呼ばれるメソッドの本体範囲をすべて返す。</summary>
    public static List<int[]> ReachableSpans(string stripped, string[] roots)
    {
        var spans = new List<int[]>();
        var queue = new Queue<string>();
        var visited = new HashSet<string>();
        foreach (var root in roots)
        {
            var span = MethodBodySpan(stripped, root);
            if (span == null) { continue; }
            spans.Add(span);
            if (visited.Add(root)) { queue.Enqueue(root); }
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var body = MethodBody(stripped, current);
            if (body == null) { continue; }
            foreach (var callee in CallNames(body))
            {
                if (!visited.Add(callee)) { continue; }
                var span = MethodBodySpan(stripped, callee);
                if (span == null) { continue; }
                spans.Add(span);
                queue.Enqueue(callee);
            }
        }
        return spans;
    }

    public static bool IsInside(List<int[]> spans, int index)
    {
        foreach (var span in spans)
        {
            if (index >= span[0] && index <= span[1]) { return true; }
        }
        return false;
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
///
/// なぜソース構造の走査なのか (代替できない理由):
/// - 本命の振る舞い「起動したら自動で更新.Apply されない」は、PlayMode でアプリを起動して
///   GitHub Releases API まで通信させないと再現できない。CI の EditMode スイートでは
///   「通信也不会起きない」ので、振る舞いテストでは観測できない。
/// - 反射では private メソッドの *呼び出し元* を検査できない
///   (「Awake から RequestApply() を呼ぶコード」的 な混入を検出する手段が無い)。
/// - よって「適用の入口の呼び出しがどこにあるか」だけは静的に見るしかない。
///
/// 保証できる範囲の限界 (N2):
/// - 保証するのは *このチェックアウトのソースツリー* 上での呼び出し位置であって、
///   コンパイル済みアセンブリそのものではない。Unity はテスト実行前に再コンパイルするので
///   現実には乖離しないが、Library キャッシュが古い状況では「テストは緑・バイナリは別物」の
///   可能性が理論上ある。
/// - 文字列ベースの検査なので、`nameof` やリフレクション経由の間接呼び出しは検出できない。
///   それを補うため、更新の入口は public メソッドに限定し、
///   入口の数をシフトする (テスト 1: UpdateFlowTests.UpdateChecker_PublicApplyEntries_AreExplicitOnly)
///   と 2 本で守っている。
/// - 既存の UpdateFlowTests (反射ベース) と役割が重複する部分はあるが、
///   こちらは「呼び出し元」を見るため代替はできない (N3)。
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

    /// <summary>受入条件 1: 起動時の確認 (CheckForUpdate) はダウンロードも適用も行わない。
    /// 禁止語は「呼び出し構文」で判定する (局所変数の名前では落ちない)。</summary>
    [Test]
    public void CheckForUpdate_NeverDownloadsOrApplies()
    {
        var body = UpdateSource.MethodBody(checkerStripped, "CheckForUpdate");
        Assert.IsNotNull(body, "CheckForUpdate が見つかりません");
        foreach (var forbidden in new[] { "DownloadAndStage", "RequestDownload", "RequestApply",
                                         "TryLaunchUpdater", "Application.Quit", "StartUpdaterProcess" })
        {
            Assert.AreEqual(0, UpdateSource.FindCallSites(body, forbidden).Count,
                            "起動時の確認が '" + forbidden + "' を呼んでいます (起動では更新しないこと)");
        }
    }

    /// <summary>受入条件 2: ダウンロードは RequestDownload (ユーザー操作) からしか始まらない。</summary>
    [Test]
    public void DownloadAndStage_IsStartedOnlyByRequestDownload()
    {
        var requestDownload = UpdateSource.MethodBody(checkerStripped, "RequestDownload");
        Assert.IsNotNull(requestDownload, "RequestDownload が見つかりません");
        Assert.AreEqual(1, UpdateSource.FindCallSites(requestDownload, "DownloadAndStage").Count,
                        "RequestDownload が DownloadAndStage を始めていない");

        var check = UpdateSource.MethodBody(checkerStripped, "CheckForUpdate");
        Assert.AreEqual(0, UpdateSource.FindCallSites(check, "DownloadAndStage").Count,
                        "CheckForUpdate がダウンロードを開始しています");
    }

    /// <summary>
    /// 受入条件 3 + 8 (S1): <b>UpdateChecker.cs 自身</b> の適用の入口の呼び出しが、
    /// 「利用者操作の入口」 (RequestApply / ApplyStagedUpdateNow / RequestDownload) から
    /// しか行われないことを検査する。
    /// 以前は UpdateChecker.cs が走査対象から除外されていたため、
    /// 例えば Awake() に RequestApply() を足しても全テストが green のままだった。
    /// 許可範囲は入口から呼び出しグラフで到達できる範囲 (ヘルパーを経由してよい)。
    /// </summary>
    [Test]
    public void UpdateChecker_ApplyEntries_AreCalledOnlyFromUserEntryPoints()
    {
        var allowed = UpdateSource.ReachableSpans(checkerStripped,
            new[] { "RequestApply", "ApplyStagedUpdateNow", "RequestDownload" });
        Assert.IsTrue(allowed.Count >= 3, "利用者操作の入口が見つかりません");

        foreach (var entry in new[] { "RequestApply", "RequestDownload", "ApplyStagedUpdateNow",
                                      "TryLaunchUpdater", "Application.Quit", "StartUpdaterProcess",
                                      "DownloadAndStage" })
        {
            foreach (var site in UpdateSource.FindCallSites(checkerStripped, entry))
            {
                Assert.IsTrue(UpdateSource.IsInside(allowed, site),
                              "'" + entry + "' の呼び出し (offset " + site + ") が利用者操作の入口の外にあります " +
                              "(同意なしに更新しないこと: UpdateChecker.cs 全体)");
            }
        }
    }

    /// <summary>受入条件 8 (N4 の緩和): プロセス終了 (Application.Quit) は
    /// 「利用者操作の入口から到達できるコード」の中にしか無い。
    /// 終了処理がヘルパーに切り出されても落ちないよう、入口からの到達可能性で判定する。</summary>
    [Test]
    public void ApplicationQuit_OnlyInsideUserReachableCode()
    {
        var allowed = UpdateSource.ReachableSpans(checkerStripped,
            new[] { "RequestApply", "ApplyStagedUpdateNow" });
        var sites = UpdateSource.FindCallSites(checkerStripped, "Application.Quit");
        Assert.IsTrue(sites.Count >= 1, "Application.Quit の呼び出しが 1 つも無い (期待外)");
        foreach (var site in sites)
        {
            Assert.IsTrue(UpdateSource.IsInside(allowed, site),
                          "Application.Quit (offset " + site + ") が利用者操作の入口から到達できない箇所にある");
        }
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
            Assert.AreEqual(0, UpdateSource.FindCallSites(trigger, forbidden).Count,
                            "Trigger が '" + forbidden + "' を呼んでいます (操作は入口の呼び出しだけ)");
        }

        // Dismiss は状態機械を触らない (押さない = 更新しない・staging は保持)
        var dismiss = UpdateSource.MethodBody(noticeStripped, "Dismiss");
        Assert.IsNotNull(dismiss, "Dismiss が見つかりません");
        Assert.AreEqual(0, UpdateSource.FindCallSites(dismiss, "RequestApply").Count, "Dismiss が適用しています");
        Assert.AreEqual(0, UpdateSource.FindCallSites(dismiss, "RequestDownload").Count, "Dismiss がダウンロードしています");
    }

    /// <summary>通知 UI 側の入口呼び出しも Trigger (ユーザー操作) の中だけ。</summary>
    [Test]
    public void UpdateNoticeUI_EntryCalls_AreOnlyInTrigger()
    {
        var allowed = UpdateSource.ReachableSpans(noticeStripped, new[] { "Trigger" });
        Assert.IsTrue(allowed.Count >= 1, "Trigger が見つかりません");
        foreach (var entry in new[] { "RequestApply", "RequestDownload" })
        {
            foreach (var site in UpdateSource.FindCallSites(noticeStripped, entry))
            {
                Assert.IsTrue(UpdateSource.IsInside(allowed, site),
                              "'" + entry + "' の呼び出し (offset " + site + ") が Trigger の外にあります");
            }
        }
    }

    /// <summary>受入条件 7: チャンネル判定は pure 関数の EvaluateCheck に集約されている
    /// (prerelease を stable に自動適用しない経路が残っていないこと)。
    /// 禁止語は呼び出し構文で判定する (N1)。</summary>
    [Test]
    public void EvaluateCheck_TakesChannelFlagExplicitly()
    {
        var body = UpdateSource.MethodBody(checkerStripped, "EvaluateCheck");
        Assert.IsNotNull(body, "EvaluateCheck が見つかりません");
        Assert.IsTrue(body.Contains("includePrerelease"),
                      "EvaluateCheck がチャンネル (includePrerelease) を受け取っていません");

        // EvaluateCheck 自身は「候補の判定」だけ — 通信もダウンロードも適用もしない。
        foreach (var callee in UpdateSource.CallNames(body))
        {
            foreach (var forbidden in new[] { "Download", "Apply", "Quit", "UnityWebRequest", "StartCoroutine" })
            {
                Assert.IsFalse(callee.Contains(forbidden),
                               "EvaluateCheck が '" + callee + "' を呼んでいます (判定だけのはず)");
            }
        }
    }

    /// <summary>本 PR の機能的変更 (確認失敗 3 経路で Checking に残さない) の固定。
    /// CheckForUpdate には FinishCheckWithoutUpdate の呼び出しが 3 経路ぶんある。</summary>
    [Test]
    public void CheckForUpdate_HasThreeFailurePaths_ReturningToIdle()
    {
        var body = UpdateSource.MethodBody(checkerStripped, "CheckForUpdate");
        Assert.IsNotNull(body, "CheckForUpdate が見つかりません");
        Assert.AreEqual(3, UpdateSource.FindCallSites(body, "FinishCheckWithoutUpdate").Count,
                        "確認失敗の 3 経路 (ビルド版解釈不能 / 通信失敗 / 新版なし) が " +
                        "FinishCheckWithoutUpdate を呼んでいません");
    }

    /// <summary>
    /// S3: EvaluateCheck の「現行ビルド識別子が解釈できない」分岐は production から到達不能。
    /// 呼び出し元 (CheckForUpdate) が通信前に current.Valid を判定して yield break するため。
    /// なので「CheckForUpdate が current.Valid を判定してから EvaluateCheck を呼ぶ」順序と、
    /// その失敗経路が Checking に残らないこと (FinishCheckWithoutUpdate) の 2 点を分けて固定する。
    /// </summary>
    [Test]
    public void EvaluateCheck_InvalidBuildBranch_IsGuardedByTheCaller()
    {
        var body = UpdateSource.MethodBody(checkerStripped, "CheckForUpdate");
        Assert.IsNotNull(body, "CheckForUpdate が見つかりません");

        int validCheck = body.IndexOf("current.Valid", StringComparison.Ordinal);
        int evaluateCall = body.IndexOf("EvaluateCheck(", StringComparison.Ordinal);
        Assert.IsTrue(validCheck >= 0, "CheckForUpdate が current.Valid を判定していません");
        Assert.IsTrue(evaluateCall > validCheck,
                      "CheckForUpdate が current.Valid を判定する前に EvaluateCheck を呼んでいます " +
                      "(EvaluateCheck の防御ガードが production で到達可能な状態)");

        // 失敗経路 (current.Valid が false) は FinishCheckWithoutUpdate で Idle へ戻す。
        Assert.AreEqual(3, UpdateSource.FindCallSites(body, "FinishCheckWithoutUpdate").Count,
                        "確認失敗 3 経路が FinishCheckWithoutUpdate を呼んでいません");
    }
}
