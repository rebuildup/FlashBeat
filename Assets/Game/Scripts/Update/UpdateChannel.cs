using System;
using System.IO;
using UnityEngine;

namespace FlashBeat.Update
{
    /// <summary>
    /// 更新チャンネル (issue #41)。
    ///
    /// - <c>stable</c>: 正式 Release だけを更新候補にする。展示用ビルドの既定。
    /// - <c>preview</c>: prerelease (テスト用 Release) も更新候補にする。開発・検証用ビルド専用。
    ///
    /// チャンネルはビルド時に配布物へ書き込まれる <c>StreamingAssets/update-channel.txt</c>
    /// (内容: <c>stable</c> / <c>preview</c>) で決まる。ファイルが無い・読めない・
    /// 未知の値なら安全側 (<c>stable</c>) に倒す。展示用ビルドがテスト用 prerelease を
    /// 自動適用しないことを保証する境界はここにある。
    /// </summary>
    public static class UpdateChannel
    {
        public const string Stable = "stable";
        public const string Preview = "preview";
        public const string ChannelFileName = "update-channel.txt";

        /// <summary>prerelease を更新候補に含めるか。</summary>
        public static bool IncludesPrerelease(string channel)
        {
            return Normalize(channel) == Preview;
        }

        /// <summary>チャンネルを正規化する。未知・空・null は stable (安全側)。</summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) { return Stable; }
            var trimmed = raw.Trim();
            if (trimmed.Equals(Preview, StringComparison.OrdinalIgnoreCase)) { return Preview; }
            return Stable;
        }

        /// <summary>
        /// 実行中のビルドのチャンネルを読み取る (StreamingAssets/update-channel.txt)。
        /// ファイルが無い・読めない場合は stable。
        /// </summary>
        public static string ReadInstalledChannel()
        {
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, ChannelFileName);
                if (!File.Exists(path)) { return Stable; }
                return Normalize(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return Stable;
            }
        }
    }
}
