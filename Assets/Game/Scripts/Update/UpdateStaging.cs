using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace FlashBeat.Update
{
    /// <summary>
    /// 更新 ZIP の staging (%LOCALAPPDATA%\FlashBeat\update\staging) と manifest の読み書き、
    /// SHA-256 検証。ディレクトリを引数に取る overload は EditMode テストで一時ディレクトリに向けて使う。
    /// </summary>
    public static class UpdateStaging
    {
        [Serializable]
        public class StagedUpdate
        {
            public string tag;         // "v0.1.1-win64-abc1234"
            public string zipName;     // staging 内の ZIP ファイル名
            public string sha256;      // ZIP の期待 SHA-256 (64 桁 hex)
            public string stagedAtUtc; // ISO 8601
        }

        public const string ManifestFileName = "update-manifest.json";

        public static string StagingDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlashBeat", "update", "staging");
            }
        }

        public static string ManifestPath
        {
            get { return Path.Combine(StagingDir, ManifestFileName); }
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
            if (staged == null || string.IsNullOrEmpty(staged.zipName) || string.IsNullOrEmpty(staged.sha256))
            {
                problem = "manifest が不正です";
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

        /// <summary>staging ディレクトリごと消す (部分ダウンロードの掃除)。失敗しても例外にしない。</summary>
        public static void ClearStaging()
        {
            try
            {
                if (Directory.Exists(StagingDir))
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
