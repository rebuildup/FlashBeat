using System;
using System.Globalization;

namespace FlashBeat.Update
{
    /// <summary>
    /// Release タグ (例: v0.1.0-win64-743c88d) とビルド埋め込み識別子
    /// (例: 0.1.0+743c88d、PlayerSettings.bundleVersion / Application.version) の解析と比較。
    /// 副作用のない純粋関数のみ。EditMode テスト対象 (UpdateCheckerTests)。
    /// </summary>
    public static class UpdateVersion
    {
        public struct ReleaseId
        {
            public string Version;  // "0.1.0"
            public string ShortSha; // "743c88d"
            public bool Valid;
        }

        public const string TagSeparator = "-win64-";
        public const char BuildIdSeparator = '+';

        /// <summary>"v0.1.0-win64-743c88d" 形式の Release タグを解析する。</summary>
        public static ReleaseId ParseTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) { return default; }
            var body = tag.Trim();
            if (body.StartsWith("v", StringComparison.Ordinal)) { body = body.Substring(1); }
            var sep = body.IndexOf(TagSeparator, StringComparison.Ordinal);
            if (sep < 0) { return default; }
            return Build(body.Substring(0, sep), body.Substring(sep + TagSeparator.Length));
        }

        /// <summary>"0.1.0+743c88d" 形式のビルド版識別子を解析する。</summary>
        public static ReleaseId ParseBuildId(string buildId)
        {
            if (string.IsNullOrEmpty(buildId)) { return default; }
            var body = buildId.Trim();
            var sep = body.IndexOf(BuildIdSeparator);
            if (sep < 0) { return default; }
            return Build(body.Substring(0, sep), body.Substring(sep + 1));
        }

        private static ReleaseId Build(string version, string shortSha)
        {
            if (ParseVersion3(version) == null) { return default; }
            if (!IsValidShortSha(shortSha)) { return default; }
            return new ReleaseId { Version = version, ShortSha = shortSha.ToLowerInvariant(), Valid = true };
        }

        private static bool IsValidShortSha(string sha)
        {
            if (string.IsNullOrEmpty(sha) || sha.Length < 7 || sha.Length > 40) { return false; }
            foreach (var c in sha)
            {
                if (!Uri.IsHexDigit(c)) { return false; }
            }
            return true;
        }

        /// <summary>candidate が current より新しいか。同一バージョンで SHA が違えば新ビルド扱い。
        /// candidate が古いバージョン (ダウングレード) の場合は false。</summary>
        public static bool IsNewerThan(ReleaseId candidate, ReleaseId current)
        {
            if (!candidate.Valid || !current.Valid) { return false; }
            var cmp = CompareVersions(candidate.Version, current.Version);
            if (cmp != 0) { return cmp > 0; }
            return !string.Equals(candidate.ShortSha, current.ShortSha, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSame(ReleaseId a, ReleaseId b)
        {
            if (!a.Valid || !b.Valid) { return false; }
            return a.Version == b.Version
                && string.Equals(a.ShortSha, b.ShortSha, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>"0.1.0" を major/minor/patch の整数で比較する。不正な形式は null。</summary>
        public static int[] ParseVersion3(string version)
        {
            if (string.IsNullOrEmpty(version)) { return null; }
            var parts = version.Split('.');
            if (parts.Length != 3) { return null; }
            var nums = new int[3];
            for (var i = 0; i < 3; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out nums[i]))
                {
                    return null;
                }
            }
            return nums;
        }

        /// <summary>セマンティック比較 (major.minor.patch)。不正な形式は文字列比較にフォールバック。</summary>
        public static int CompareVersions(string a, string b)
        {
            var va = ParseVersion3(a);
            var vb = ParseVersion3(b);
            if (va == null || vb == null) { return string.CompareOrdinal(a, b); }
            for (var i = 0; i < 3; i++)
            {
                if (va[i] != vb[i]) { return va[i].CompareTo(vb[i]); }
            }
            return 0;
        }
    }
}
