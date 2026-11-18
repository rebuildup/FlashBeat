using System;
using System.IO;
using UnityEngine;

namespace FlashBeat.Update
{
    /// <summary>
    /// 更新データ (staging ZIP / manifest / last-result.json / ログ / updater 一時領域) の
    /// 保存先 (issue #574 / #670)。
    /// issue #670: アプリが制御する更新関連アーティファクトはすべて
    /// <b>アプリフォルダ (インストールディレクトリ) 内</b> の
    /// <c>&lt;install&gt;/.flashbeat-update/</c> 配下に限定する:
    /// <code>
    /// &lt;install&gt;/.flashbeat-update/
    ///     staging/    … ダウンロード済み ZIP + update-manifest.json
    ///     results/    … last-result.json / last-result-shown.json
    ///     logs/       … updater の実行ログ (世代管理は updater 側)
    ///     updater/    … macOS updater スクリプトの一時コピー置き場
    /// </code>
    /// persistentDataPath (AppData / Application Support) は更新関連アーティファクトの
    /// 置き場ではない (スコア・設定など更新と無関係の利用は従来どおり)。
    /// macOS の .app バンドルは署名済みでバンドル内へ安全に書き込めないため、
    /// <see cref="InstallRoot"/> は Windows Player でのみ解決される
    /// (macOS / Editor では null → 更新ファイルを配置できず、安全側で更新は行われない)。
    /// UpdateChecker (アプリ) と配布物内のアップデータスクリプトが同じ場所を使う
    /// (スクリプト側は -ResultDir / -LogDir で明示され、境界外は拒否される)。
    /// 旧レイアウト (persistentDataPath 系の update データ) の残存データは
    /// <see cref="MigrateLegacyData"/> が起動時に引き継ぐ。
    /// </summary>
    public static class UpdatePaths
    {
        /// <summary>更新領域のフォルダ名 (インストール先直下)。updater スクリプト側と共有する予約名。</summary>
        public const string UpdateDirName = ".flashbeat-update";

        /// <summary>
        /// 更新データのルート (&lt;install&gt;/.flashbeat-update)。
        /// インストール先を解決できない環境 (macOS .app / Editor 等) では null。
        /// </summary>
        public static string BaseDir
        {
            get { return GetBaseDir(InstallRoot); }
        }

        /// <summary>staging (ダウンロード済み ZIP) の置き場。解決できない環境では null。</summary>
        public static string StagingDir
        {
            get { return GetStagingDir(InstallRoot); }
        }

        /// <summary>last-result.json の置き場 (updater の -ResultDir に渡す値)。</summary>
        public static string ResultDir
        {
            get { return GetResultDir(InstallRoot); }
        }

        /// <summary>updater ログの置き場 (updater の -LogDir に渡す値)。</summary>
        public static string LogsDir
        {
            get { return GetLogsDir(InstallRoot); }
        }

        /// <summary>macOS updater スクリプトの一時コピー置き場 (起動後に updater が自分で消す)。</summary>
        public static string UpdaterTmpDir
        {
            get { return GetUpdaterTmpDir(InstallRoot); }
        }

        /// <summary>
        /// 更新の境界ルート = アプリフォルダ (インストールディレクトリ)。
        /// Windows Player では Application.dataPath (&lt;install&gt;/FlashBeat_Data) の親。
        /// macOS .app / Editor など「バンドル内に書けない」環境では null (issue #670)。
        /// </summary>
        public static string InstallRoot
        {
            get { return GetInstallRoot(Application.platform, Application.dataPath); }
        }

        /// <summary>
        /// プラットフォームと dataPath からインストールルートを解決する (テスト用)。
        /// Windows Player のみ対応: dataPath の親フォルダを返す。
        /// それ以外 (macOS .app は署名済みバンドルで内部に書けない、Editor は
        /// プロジェクトフォルダを指してしまう) は null = 更新領域を持たない。
        /// </summary>
        public static string GetInstallRoot(RuntimePlatform platform, string dataPath)
        {
            if (platform != RuntimePlatform.WindowsPlayer) { return null; }
            if (string.IsNullOrEmpty(dataPath)) { return null; }
            var dir = Path.GetDirectoryName(dataPath);
            return string.IsNullOrEmpty(dir) ? null : dir;
        }

        /// <summary>&lt;install&gt;/.flashbeat-update。installRoot が null なら null。</summary>
        public static string GetBaseDir(string installRoot)
        {
            return CombineOrNull(installRoot, UpdateDirName);
        }

        /// <summary>&lt;install&gt;/.flashbeat-update/staging。</summary>
        public static string GetStagingDir(string installRoot)
        {
            return CombineOrNull(GetBaseDir(installRoot), "staging");
        }

        /// <summary>&lt;install&gt;/.flashbeat-update/results。</summary>
        public static string GetResultDir(string installRoot)
        {
            return CombineOrNull(GetBaseDir(installRoot), "results");
        }

        /// <summary>&lt;install&gt;/.flashbeat-update/logs。</summary>
        public static string GetLogsDir(string installRoot)
        {
            return CombineOrNull(GetBaseDir(installRoot), "logs");
        }

        /// <summary>&lt;install&gt;/.flashbeat-update/updater。</summary>
        public static string GetUpdaterTmpDir(string installRoot)
        {
            return CombineOrNull(GetBaseDir(installRoot), "updater");
        }

        /// <summary>Application.persistentDataPath。空の環境では null。</summary>
        /// 更新アーティファクトには使わない (issue #670)。旧データ移行の参照用に残す。</summary>
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
        /// issue #670: 移動先はインストール先内の .flashbeat-update であり、
        /// 境界内への書き込みのみ行う。旧側 (persistentDataPath 系) のファイルは
        /// <strong>コピーのみで移動・削除しない</strong> — 境界外の既存データは保全する。
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
        /// <paramref name="baseDir"/> の責務別ディレクトリへ<strong>コピー</strong>する。
        /// 移動先が既にある場合は旧側を残す (新しい方を優先・上書きしない)。
        /// 旧ルートとその中身は一切変更しない (issue #670: 外部ファイルの保全)。
        /// EditMode テストでは一時ディレクトリを両引数に渡す。
        /// </summary>
        public static void MigrateLegacyData(string legacyDir, string baseDir)
        {
            if (string.IsNullOrEmpty(legacyDir) || string.IsNullOrEmpty(baseDir)) { return; }
            if (!Directory.Exists(legacyDir)) { return; }
            try
            {
                // staging: ディレクトリごとコピーする (中の ZIP + manifest がセットで使われるため)。
                var legacyStaging = Path.Combine(legacyDir, "staging");
                var staging = Path.Combine(baseDir, "staging");
                if (Directory.Exists(legacyStaging) && !Directory.Exists(staging))
                {
                    Directory.CreateDirectory(baseDir);
                    CopyDirectory(legacyStaging, staging);
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
                        File.Copy(src, dst);
                    }
                }

                // 旧ルートとその中身は一切変更しない (issue #670: 外部ファイルの保全)。
                // コピーのみで、移動・削除は行わない。
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

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dir.Replace(src, dst));
            }
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(src, dst), false);
            }
        }
    }
}
