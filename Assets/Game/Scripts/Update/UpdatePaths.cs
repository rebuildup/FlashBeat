using System;
using System.IO;
using UnityEngine;

namespace FlashBeat.Update
{
    /// <summary>
    /// 更新データ (staging ZIP / manifest / last-result.json / ログ) の保存先。
    /// Windows は <c>%LOCALAPPDATA%\FlashBeat\update</c>、
    /// macOS は <c>~/Library/Application Support/FlashBeat/update</c>。
    /// UpdateChecker (アプリ) と配布物内のアップデータスクリプトが同じ場所を使う。
    /// </summary>
    public static class UpdatePaths
    {
        public static bool IsMacOS
        {
            get
            {
                return Application.platform == RuntimePlatform.OSXPlayer
                    || Application.platform == RuntimePlatform.OSXEditor;
            }
        }

        /// <summary>更新データのルート。解決できない環境では null。</summary>
        public static string BaseDir
        {
            get
            {
                if (IsMacOS)
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    if (string.IsNullOrEmpty(home)) { return null; }
                    return Path.Combine(home, "Library", "Application Support", "FlashBeat", "update");
                }

                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData)) { return null; }
                return Path.Combine(localAppData, "FlashBeat", "update");
            }
        }

        /// <summary>staging (ダウンロード済み ZIP) の置き場。解決できない環境では null。</summary>
        public static string StagingDir
        {
            get
            {
                var baseDir = BaseDir;
                return string.IsNullOrEmpty(baseDir) ? null : Path.Combine(baseDir, "staging");
            }
        }
    }
}
