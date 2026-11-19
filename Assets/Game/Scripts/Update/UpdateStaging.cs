using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FlashBeat.Update
{
    /// <summary>
    /// 更新 ZIP の staging (<see cref="UpdatePaths.StagingDir"/>) と manifest の読み書き、
    /// SHA-256 検証。ディレクトリを引数に取る overload は EditMode テストで一時ディレクトリに向けて使う。
    /// </summary>
    public static class UpdateStaging
    {
        [Serializable]
        public class StagedUpdate
        {
            public string tag;         // "v0.1.1-win64-abc1234" / "v0.1.1-macos-abc1234"
            public string zipName;     // staging 内の ZIP ファイル名
            public string sha256;      // ZIP の期待 SHA-256 (64 桁 hex)
            public string stagedAtUtc; // ISO 8601
        }

        public const string ManifestFileName = "update-manifest.json";

        public static string StagingDir
        {
            get { return UpdatePaths.StagingDir; }
        }

        public static string ManifestPath
        {
            get
            {
                // issue #670: staging を解決できない環境 (macOS .app / Editor) では null。
                var dir = StagingDir;
                return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, ManifestFileName);
            }
        }

        /// <summary>ファイルの SHA-256 (64 桁 hex)。ファイルが無い場合や読めない場合は例外。</summary>
        public static string ComputeSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        public static bool WriteManifest(StagedUpdate staged)
        {
            return WriteManifest(staged, StagingDir);
        }

        public static bool WriteManifest(StagedUpdate staged, string stagingDir)
        {
            try
            {
                // issue #670 review: staging (またはその親 = .flashbeat-update)
                // が reparse point なら書き込み先が境界外へ出るため拒否する。
                if (UpdatePaths.IsReparsePoint(stagingDir)
                    || UpdatePaths.IsReparsePoint(Path.GetDirectoryName(stagingDir)))
                {
                    return false;
                }
                Directory.CreateDirectory(stagingDir);
                File.WriteAllText(Path.Combine(stagingDir, ManifestFileName), JsonUtility.ToJson(staged));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static StagedUpdate ReadManifest()
        {
            return ReadManifest(StagingDir);
        }

        public static StagedUpdate ReadManifest(string stagingDir)
        {
            try
            {
                var path = Path.Combine(stagingDir, ManifestFileName);
                if (!File.Exists(path)) { return null; }
                var staged = JsonUtility.FromJson<StagedUpdate>(File.ReadAllText(path));
                if (staged == null || string.IsNullOrEmpty(staged.tag) || string.IsNullOrEmpty(staged.zipName))
                {
                    return null;
                }
                return staged;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>staging の ZIP が manifest どおり実在し SHA-256 が一致するか。失敗理由を problem に入れる。</summary>
        public static bool VerifyStaged(StagedUpdate staged, out string problem)
        {
            return VerifyStaged(staged, StagingDir, out problem);
        }

        public static bool VerifyStaged(StagedUpdate staged, string stagingDir, out string problem)
        {
            problem = null;
            if (string.IsNullOrEmpty(stagingDir))
            {
                problem = "staging ディレクトリを解決できません (インストール先フォルダ内の更新領域がありません)";
                return false;
            }
            if (staged == null || string.IsNullOrEmpty(staged.zipName) || string.IsNullOrEmpty(staged.sha256))
            {
                problem = "manifest が不正です";
                return false;
            }
            // issue #670: manifest は staging 内の「ファイル名」だけを指せる。
            // パス区切り・親参照・絶対パスを含む zipName は境界外を指しうるため拒否する
            // (検証前にファイルを開かない = 読み取りも境界外へ出さない)。
            if (!IsPlainFileName(staged.zipName))
            {
                problem = "manifest の zipName が不正です (パスを含められません): " + staged.zipName;
                return false;
            }
            var zipPath = Path.Combine(stagingDir, staged.zipName);
            if (!File.Exists(zipPath))
            {
                problem = "ZIP が staging にありません: " + staged.zipName;
                return false;
            }
            var expected = staged.sha256.Trim().ToLowerInvariant();
            string actual;
            try
            {
                actual = ComputeSha256(zipPath);
            }
            catch (Exception e)
            {
                problem = "SHA-256 の計算に失敗しました: " + e.Message;
                return false;
            }
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                problem = "SHA-256 が一致しません (actual=" + actual + ", expected=" + expected + ")";
                return false;
            }
            return true;
        }

        /// <summary>
        /// staging 内の相対「ファイル名」として安全か (issue #670)。
        /// ディレクトリ区切り・ドライブ修飾・".." を含む名前は拒否する。
        /// </summary>
        public static bool IsPlainFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) { return false; }
            if (name.IndexOfAny(new[] { '/', '\\' }) >= 0) { return false; }
            if (name.IndexOf(':') >= 0) { return false; }
            if (name == "." || name == "..") { return false; }
            return true;
        }

        /// <summary>staging ディレクトリごと消す (部分ダウンロードの掃除)。失敗しても例外にしない。</summary>
        public static void ClearStaging()
        {
            try
            {
                // issue #670 review: staging が reparse point の場合、
                // recursive delete がリンク先の中身まで消し得るため削除しない。
                if (Directory.Exists(StagingDir) && !UpdatePaths.IsReparsePoint(StagingDir))
                {
                    Directory.Delete(StagingDir, true);
                }
            }
            catch (Exception)
            {
                // 掃除の失敗は更新中止の理由にしない (次回の ClearStaging で再試行される)
            }
        }
    }
}
