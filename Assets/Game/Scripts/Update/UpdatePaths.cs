using System;
using System.IO;
using UnityEngine;

namespace FlashBeat.Update
{
    /// <summary>
    /// 更新データ (staging ZIP / manifest / last-result.json / ログ) の保存先 (issue #574)。
    /// アプリが書く永続データは <see cref="Application.persistentDataPath"/> 配下の
    /// アプリ専用領域に責務別ディレクトリで集約する:
    /// <code>
    /// &lt;persistentDataPath&gt;/update/
    ///     staging/    … ダウンロード済み ZIP + update-manifest.json
    ///     results/    … last-result.json / last-result-shown.json
    ///     logs/       … updater の実行ログ (世代管理は updater 側)
    ///     updater/    … macOS updater スクリプトの一時コピー置き場
    /// </code>
    /// UpdateChecker (アプリ) と配布物内のアップデータスクリプトが同じ場所を使う
    /// (スクリプト側は -ResultDir / -LogDir で明示される)。
    /// 旧レイアウト (%LOCALAPPDATA%\FlashBeat\update /
    /// ~/Library/Application Support/FlashBeat/update) の残存データは
    /// <see cref="MigrateLegacyData"/> が起動時に引き継ぐ。
    /// </summary>
    public static class UpdatePaths
    {
        /// <summary>更新データのルート (&lt;persistentDataPath&gt;/update)。解決できない環境では null。</summary>
        public static string BaseDir
        {
            get
            {
                var root = PersistentDataRoot;
                return string.IsNullOrEmpty(root) ? null : Path.Combine(root, "update");
            }
        }

        /// <summary>staging (ダウンロード済み ZIP) の置き場。解決できない環境では null。</summary>
        public static string StagingDir
        {
            get { return CombineOrNull(BaseDir, "staging"); }
        }

        /// <summary>last-result.json の置き場 (updater の -ResultDir に渡す値)。</summary>
        public static string ResultDir
        {
            get { return CombineOrNull(BaseDir, "results"); }
        }

        /// <summary>updater ログの置き場 (updater の -LogDir に渡す値)。</summary>
        public static string LogsDir
        {
            get { return CombineOrNull(BaseDir, "logs"); }
        }

        /// <summary>macOS updater スクリプトの一時コピー置き場 (起動後に updater が自分で消す)。</summary>
        public static string UpdaterTmpDir
        {
            get { return CombineOrNull(BaseDir, "updater"); }
        }

        /// <summary>Application.persistentDataPath。空の環境では null。</summary>
        public static string PersistentDataRoot
        {
            get
            {
                var path = Application.persistentDataPath;
                return string.IsNullOrEmpty(path) ? null : path;
            }
        }

        public static bool IsMacOS
        {
            get
            {
                return Application.platform == RuntimePlatform.OSXPlayer
                    || Application.platform == RuntimePlatform.OSXEditor;
            }
        }

        /// <summary>
        /// 旧レイアウトの更新データルート (issue #574 より前の場所)。
        /// Windows: %LOCALAPPDATA%\FlashBeat\update、
        /// macOS: ~/Library/Application Support/FlashBeat/update。
        /// 解決できない環境では null。
        /// </summary>
        public static string LegacyBaseDir
        {
            get { return GetLegacyBaseDir(IsMacOS); }
        }

        /// <summary>旧レイアウトのルートをプラットフォーム指定で解決する (テスト用)。</summary>
        public static string GetLegacyBaseDir(bool isMacOS)
        {
            if (isMacOS)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                if (string.IsNullOrEmpty(home)) { return null; }
                return Path.Combine(home, "Library", "Application Support", "FlashBeat", "update");
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData)) { return null; }
            return Path.Combine(localAppData, "FlashBeat", "update");
        }

        /// <summary>
        /// 旧レイアウトから新レイアウトへ既知のファイルだけを引き継ぐ (issue #574)。
        /// 対象は staging/・last-result.json・last-result-shown.json のみで、
        /// 所有が確認できないその他のファイルには触れない。失敗しても例外にしない
        /// (更新確認を止めないための best-effort 処理)。
        /// </summary>
        public static void MigrateLegacyData()
        {
            var legacy = LegacyBaseDir;
            var current = BaseDir;
            if (string.IsNullOrEmpty(legacy) || string.IsNullOrEmpty(current)) { return; }
            MigrateLegacyData(legacy, current);
        }

        /// <summary>
        /// 旧ルート <paramref name="legacyDir"/> の既知エントリを新ルート
        /// <paramref name="baseDir"/> の責務別ディレクトリへ移す。
        /// 移動先が既にある場合は旧側を残す (新しい方を優先)。
        /// 旧ルートは既知エントリをすべて移し終えたときだけ削除する
        /// (未知のファイルが残る場合はディレクトリごと残る)。
        /// EditMode テストでは一時ディレクトリを両引数に渡す。
        /// </summary>
        public static void MigrateLegacyData(string legacyDir, string baseDir)
        {
            if (string.IsNullOrEmpty(legacyDir) || string.IsNullOrEmpty(baseDir)) { return; }
            if (!Directory.Exists(legacyDir)) { return; }
            try
            {
                // staging: ディレクトリごと移す (中の ZIP + manifest がセットで使われるため)。
                var legacyStaging = Path.Combine(legacyDir, "staging");
                var staging = Path.Combine(baseDir, "staging");
                if (Directory.Exists(legacyStaging) && !Directory.Exists(staging))
                {
                    Directory.CreateDirectory(baseDir);
                    Directory.Move(legacyStaging, staging);
                }

                // last-result.json / last-result-shown.json → results/
                var results = Path.Combine(baseDir, "results");
                foreach (var name in new[] { "last-result.json", "last-result-shown.json" })
                {
                    var src = Path.Combine(legacyDir, name);
                    var dst = Path.Combine(results, name);
                    if (File.Exists(src) && !File.Exists(dst))
                    {
                        Directory.CreateDirectory(results);
                        File.Move(src, dst);
                    }
                }

                // 既知エントリをすべて移し終えたときだけ旧ルートを消す。
                // 未知のファイルが残っていれば Directory.Delete は失敗し、
                // その場合はディレクトリごと残る (無関係ファイルを消さない)。
                // 残件がある場合は毎回警告を出さないよう、先に中身を確認する。
                if (Directory.GetFileSystemEntries(legacyDir).Length == 0)
                {
                    Directory.Delete(legacyDir, false);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UpdatePaths] 旧更新データの引き継ぎに失敗しました (更新確認は続行します): " + e.Message);
            }
        }

        private static string CombineOrNull(string dir, string name)
        {
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, name);
        }
    }
}
