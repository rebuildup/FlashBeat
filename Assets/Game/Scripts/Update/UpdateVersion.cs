using System;
using System.Globalization;

namespace FlashBeat.Update
{
    /// <summary>配布プラットフォーム。Release タグとアセット名に含まれる。</summary>
    public enum UpdatePlatform
    {
        Windows64,
        MacOS,
    }

    /// <summary>
    /// Release タグ (例: v0.1.0-win64-743c88d / v0.1.0-macos-743c88d) とビルド埋め込み識別子
    /// (例: 0.1.0+743c88d、PlayerSettings.bundleVersion / Application.version) の解析と比較。
    /// 副作用のない純粋関数のみ。EditMode テスト対象 (UpdateCheckerTests)。
    /// </summary>
    public static class UpdateVersion
    {
        public struct ReleaseId
        {
            public string Version;  // "0.1.0"
            public string ShortSha; // "743c88d"
            public UpdatePlatform Platform;
            public bool Valid;
        }

        public const string WindowsTagToken = "win64";
        public const string MacTagToken = "macos";

        // 後方互換: 既存の win64 タグ区切り。プラットフォーム別の区切りは GetTagSeparator を使う。
        public const string TagSeparator = "-" + WindowsTagToken + "-";
        public const char BuildIdSeparator = '+';

        /// <summary>Release タグ / アセット名に使うプラットフォーム token。</summary>
        public static string GetTagToken(UpdatePlatform platform)
        {
            return platform == UpdatePlatform.MacOS ? MacTagToken : WindowsTagToken;
        }

        /// <summary>Release タグのプラットフォーム区切り (例: "-win64-" / "-macos-")。</summary>
        public static string GetTagSeparator(UpdatePlatform platform)
        {
            return "-" + GetTagToken(platform) + "-";
        }

        /// <summary>既知のいずれかのプラットフォーム形式の Release タグを解析する。
        /// プラットフォームを問わないため、候補の絞り込みには
        /// <see cref="ParseTag(string, UpdatePlatform)"/> を使う。</summary>
        public static ReleaseId ParseTag(string tag)
        {
            return ParseTagCore(tag, null);
        }

        /// <summary>指定プラットフォームの Release タグだけを解析する。
        /// 他プラットフォームのタグ (Windows 版が macOS の候補になる等) は Valid=false。</summary>
        public static ReleaseId ParseTag(string tag, UpdatePlatform platform)
        {
            return ParseTagCore(tag, GetTagToken(platform));
        }

        private static ReleaseId ParseTagCore(string tag, string expectedToken)
        {
            if (string.IsNullOrEmpty(tag)) { return default; }
            var body = tag.Trim();
            if (body.StartsWith("v", StringComparison.Ordinal)) { body = body.Substring(1); }

            // 既知のプラットフォーム区切りだけを受け付ける (-linux- 等は不正)。
            var separatorIndex = -1;
            var platform = UpdatePlatform.Windows64;
            foreach (var candidate in new[]
                     {
                         (token: WindowsTagToken, platform: UpdatePlatform.Windows64),
                         (token: MacTagToken, platform: UpdatePlatform.MacOS),
                     })
            {
                var sep = "-" + candidate.token + "-";
                var index = body.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (index < 0) { continue; }
                if (separatorIndex >= 0 && index >= separatorIndex) { continue; }
                if (expectedToken != null &&
                    !string.Equals(candidate.token, expectedToken, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                separatorIndex = index;
                platform = candidate.platform;
            }

            if (separatorIndex < 0) { return default; }
            var tokenLength = GetTagSeparator(platform).Length;
            var id = Build(body.Substring(0, separatorIndex), body.Substring(separatorIndex + tokenLength));
            id.Platform = platform;
            return id;
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

        /// <summary>candidate が current より新しいか。
        /// バージョンが**厳密に大きい**ときだけ true。同一バージョンの別ビルドは
        /// 「新しい」と判定できない (SHA には大小関係がない) ので自動更新の対象にしない。
        /// 運用: リリースを出すたびに基準バージョン (FLASHBEAT_VERSION_BASE) を上げる。
        /// 古いバージョンへのダウングレードは行わない。</summary>
        public static bool IsNewerThan(ReleaseId candidate, ReleaseId current)
        {
            if (!candidate.Valid || !current.Valid) { return false; }
            return CompareVersions(candidate.Version, current.Version) > 0;
        }

        /// <summary>candidate と current が同じバージョンで別のビルドか (SHA が違う)。
        /// 自動更新はしないが、運営者に伝えるために使う。</summary>
        public static bool IsSameVersionDifferentBuild(ReleaseId candidate, ReleaseId current)
        {
            if (!candidate.Valid || !current.Valid) { return false; }
            if (CompareVersions(candidate.Version, current.Version) != 0) { return false; }
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
