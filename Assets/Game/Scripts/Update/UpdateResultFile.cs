using System;
using System.IO;
using UnityEngine;

namespace FlashBeat.Update
{
    /// <summary>Update.ps1 の結果種別。last-result.json の "result" フィールドに対応する。</summary>
    public enum UpdateResultKind
    {
        None,
        Success,
        Failed,
        Aborted,
    }

    /// <summary>Update.ps1 が書く last-result.json の内容 (issue #125)。</summary>
    [Serializable]
    public class UpdateResultRecord
    {
        public string result;
        public int exitCode;
        public string fromVersion;
        public string toVersion;
        public string tag;
        public string message;
        public string timeUtc;

        /// <summary>"result" フィールドの解析。未知の値は None (通知しない)。</summary>
        public UpdateResultKind Kind
        {
            get
            {
                if (string.IsNullOrEmpty(result)) { return UpdateResultKind.None; }
                switch (result.Trim().ToLowerInvariant())
                {
                    case "success": return UpdateResultKind.Success;
                    case "failed": return UpdateResultKind.Failed;
                    case "aborted": return UpdateResultKind.Aborted;
                    default: return UpdateResultKind.None;
                }
            }
        }
    }

    /// <summary>
    /// Update.ps1 がすべての終了経路で書く %LOCALAPPDATA%\FlashBeat\update\last-result.json の
    /// 読み取り、Title 通知文言の選択、一度だけ表示するための mark-as-shown (リネーム)。
    /// issue #125: 更新スクリプトの失敗 (旧版では無言だった) を次回起動時に利用者へ見せる。
    /// パスを明示する overload は EditMode テスト (一時ディレクトリ) 用。
    /// </summary>
    public static class UpdateResultFile
    {
        public const string FileName = "last-result.json";
        public const string ShownFileName = "last-result-shown.json";

        /// <summary>結果ディレクトリ (%LOCALAPPDATA%\FlashBeat\update)。不明なら null。</summary>
        public static string ResultDir
        {
            get
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData)) { return null; }
                return Path.Combine(Path.Combine(localAppData, "FlashBeat"), "update");
            }
        }

        public static string ResultPath
        {
            get
            {
                var dir = ResultDir;
                return dir == null ? null : Path.Combine(dir, FileName);
            }
        }

        public static UpdateResultRecord Read()
        {
            return Read(ResultPath);
        }

        /// <summary>結果ファイルを読む。無い・読めない・解析できない場合は null (通知なし)。</summary>
        public static UpdateResultRecord Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return null; }
            try
            {
                var json = File.ReadAllText(path);
                // JsonUtility は不正 JSON で例外とは限らないため、結果ファイルの必須キーを先に確認する。
                if (string.IsNullOrEmpty(json) || !json.Contains("\"result\"")) { return null; }
                return JsonUtility.FromJson<UpdateResultRecord>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Title 通知の文言 (純粋関数)。表示すべき結果でないなら null。
        /// 失敗 / 中止: 「前回の更新に失敗しました: &lt;理由&gt; - 現在の版 vX を使用中 / F3 で再試行」
        /// 成功: 「更新しました vX」</summary>
        public static string BuildNoticeText(UpdateResultRecord record, string currentVersionText)
        {
            if (record == null) { return null; }
            switch (record.Kind)
            {
                case UpdateResultKind.Success:
                    return "更新しました v" + (string.IsNullOrEmpty(record.toVersion) ? "?" : record.toVersion);
                case UpdateResultKind.Failed:
                case UpdateResultKind.Aborted:
                    var reason = string.IsNullOrEmpty(record.message) ? "理由不明" : record.message;
                    var version = string.IsNullOrEmpty(currentVersionText) ? "?" : currentVersionText;
                    return "前回の更新に失敗しました: " + reason +
                           " - 現在の版 v" + version + " を使用中 / F3 で再試行";
                default:
                    return null;
            }
        }

        public static void MarkShown()
        {
            MarkShown(ResultPath);
        }

        /// <summary>結果ファイルを表示済みにする (last-result-shown.json へリネーム)。
        /// リネームできない (運営者が消したい等) 場合は削除を試み、それも失敗なら諦める。
        /// 例外にはしない (通知のための補助情報であり、ゲームを止めない)。</summary>
        public static void MarkShown(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return; }
            try
            {
                var dir = Path.GetDirectoryName(path);
                var dest = string.IsNullOrEmpty(dir) ? ShownFileName : Path.Combine(dir, ShownFileName);
                if (File.Exists(dest)) { File.Delete(dest); }
                File.Move(path, dest);
            }
            catch (Exception)
            {
                try { File.Delete(path); } catch (Exception) { }
            }
        }
    }
}
