using System;
using System.Collections.Generic;
using System.Text;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// メディア URL を **形だけ** ログに出すための純粋ロジック (Issue #13)。
    ///
    /// Windows 実機で VideoPlayer が googlevideo の URL を開けず
    /// (WindowsVideoMedia 0x80070005 / MFCreateSourceReaderFromURL)、
    /// その原因候補 (署名の期限切れ・URL 長・Range 非対応) を切り分けたい。
    /// ただし URL には署名・期限・IP が入るので **値を出してはいけない**。
    /// ここでは「長さ」「クエリのキー名」「Range 指定の有無」だけを返す。
    /// </summary>
    public static class MediaUrlDiagnostics
    {
        /// <summary>ログ 1 行に収める上限 (これを超えたら切り詰める)。</summary>
        public const int MaxLength = 400;

        /// <summary>クエリキー名を列挙する上限。</summary>
        public const int MaxParams = 40;

        /// <summary>
        /// URL の「形」を 1 行で返す。値・パス・IP は出さない。
        /// null / 空文字でも例外を投げない。
        /// </summary>
        public static string Summarize(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "url=<empty>";
            }

            var sb = new StringBuilder();
            sb.Append("len=").Append(url.Length);

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                // 絶対 URL として解釈できない = ローカルパスや壊れた URL。
                // パスは出さず、拡張子の有無だけを残す。
                sb.Append(", shape=<not-absolute>");
                sb.Append(", hasExtension=").Append(HasKnownExtension(url) ? "true" : "false");
                return Truncate(sb.ToString());
            }

            sb.Append(", scheme=").Append(uri.Scheme);
            sb.Append(", host=").Append(HostShape(uri.Host));
            sb.Append(", pathExtension=").Append(ExtensionOf(uri.AbsolutePath));
            sb.Append(", queryKeys=").Append(QueryKeys(uri.Query));

            return Truncate(sb.ToString());
        }

        /// <summary>
        /// ホスト名は最大 3 ラベルまで。IP アドレスはそのまま出す
        /// (ホスト名に埋まった IP は秘密ではないが、固有ノード名は切り落とす)。
        /// </summary>
        private static string HostShape(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return "<none>";
            }

            // IPv4 / IPv6 はそのまま (ラベル分割すると意味が壊れる)。
            if (host.IndexOf(':') >= 0 || IsAllDigitsAndDots(host))
            {
                return host;
            }

            var labels = host.Split('.');
            int take = Math.Min(3, labels.Length);
            var sb = new StringBuilder();
            for (int i = 0; i < take; i++)
            {
                if (i > 0) { sb.Append('.'); }
                sb.Append(labels[i]);
            }

            if (labels.Length > take) { sb.Append(".<…>"); }
            return sb.ToString();
        }

        private static bool IsAllDigitsAndDots(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '.' && (c < '0' || c > '9')) { return false; }
            }
            return text.IndexOf('.') >= 0;
        }

        /// <summary>パスの拡張子だけ (パス本体は出さない)。</summary>
        private static string ExtensionOf(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/")
            {
                return "<none>";
            }

            int dot = path.LastIndexOf('.');
            if (dot < 0 || dot == path.Length - 1)
            {
                // Unity のログと同じ言い方: 拡張子が無いと Media Foundation は
                // 形式を仮定する。この状態を切り分けたいので明示する。
                return "<none>";
            }

            string ext = path.Substring(dot);
            return ext.Length <= 8 ? ext : "<long>";
        }

        private static bool HasKnownExtension(string url)
        {
            int cut = url.IndexOf('?');
            string path = cut >= 0 ? url.Substring(0, cut) : url;
            int dot = path.LastIndexOf('.');
            int slash = path.LastIndexOf('/');
            return dot > slash && dot < path.Length - 1;
        }

        /// <summary>クエリの **キー名だけ** をアルファベット順で返す (値は絶対に出さない)。</summary>
        private static string QueryKeys(string query)
        {
            if (string.IsNullOrEmpty(query) || query == "?")
            {
                return "[]";
            }

            var keys = new List<string>();
            string body = query[0] == '?' ? query.Substring(1) : query;
            foreach (string pair in body.Split('&'))
            {
                if (pair.Length == 0) { continue; }
                int eq = pair.IndexOf('=');
                string key = eq >= 0 ? pair.Substring(0, eq) : pair;
                if (key.Length == 0) { continue; }
                keys.Add(key);
            }

            keys.Sort(StringComparer.Ordinal);
            if (keys.Count > MaxParams)
            {
                int extra = keys.Count - MaxParams;
                keys = keys.GetRange(0, MaxParams);
                keys.Add("…+" + extra);
            }

            return "[" + string.Join(",", keys) + "]";
        }

        private static string Truncate(string text)
        {
            if (text.Length <= MaxLength) { return text; }
            return text.Substring(0, MaxLength - 1) + "…";
        }
    }
}
