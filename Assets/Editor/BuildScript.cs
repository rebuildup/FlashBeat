using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class BuildScript
{
    // 配布版の基準バージョン。ビルド時 git SHA と組み合わせて
    // "<version>+<sha>" を PlayerSettings.bundleVersion に埋め込み、
    // GitHub Release タグ "v<version>-win64-<sha>" と UpdateChecker が比較する。
    // Release をバージョンアップするときは環境変数 FLASHBEAT_VERSION_BASE で上書きする
    // (例: v0.1.1 Release → FLASHBEAT_VERSION_BASE=0.1.1)。未設定時は 0.1.0。
    private const string DefaultVersionBase = "0.1.0";

    // 更新チャンネル (issue #41)。既定は stable = 正式 Release のみを自動更新の対象にする。
    // 検証用の prerelease を取り込みたいときだけ環境変数 FLASHBEAT_UPDATE_CHANNEL=preview で
    // ビルドする。チャンネルは配布物の StreamingAssets/update-channel.txt に書き出され、
    // UpdateChecker が起動時に読む (ファイルが無い場合は safe 側の stable)。
    private const string DefaultUpdateChannel = "stable";
    private const string PreviewUpdateChannel = "preview";

    // 配布 ZIP に含めない第三者楽曲の音声・動画。Resources 配下はビルドに
    // 無条件で取り込まれるため、ビルド中だけプロジェクト外 (Builds/.media-excluded) へ退避し、
    // ビルド後に元へ戻す。GameScene の VideoPlayer は yt-dlp で解決したストリーム URL を
    // 再生するため、プレイヤーではローカルの音声・動画を使わない。
    private static readonly string[] MediaExcludePaths =
    {
        "Assets/Game/Resources/Musics",
        "Assets/Game/Resources/Videos/UnityYoutube",
        "Assets/Game/Resources/Videos/loading.mp4",
    };
    private const string MediaExcludeRoot = "Builds/.media-excluded";

    [MenuItem("Build/Build Windows 64")]
    public static void BuildStandaloneWindows64()
    {
        string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows", "FlashBeat.exe");

        string buildDir = Path.GetDirectoryName(buildPath);
        if (!Directory.Exists(buildDir))
        {
            Directory.CreateDirectory(buildDir);
        }

        string[] scenes = new string[]
        {
            "Assets/Game/Scenes/Opening.unity",
            "Assets/Game/Scenes/TitleScene.unity",
            "Assets/Game/Scenes/SelectScene.unity",
            "Assets/Game/Scenes/GameScene.unity",
            "Assets/Game/Scenes/ResultScene.unity",
            "Assets/Game/Scenes/OptionScene.unity"
        };

        string shortSha = GetGitShortSha();
        string versionBase = Environment.GetEnvironmentVariable("FLASHBEAT_VERSION_BASE");
        if (string.IsNullOrEmpty(versionBase)) { versionBase = DefaultVersionBase; }
        PlayerSettings.bundleVersion = versionBase + "+" + shortSha;
        Debug.Log($"[BuildScript] bundleVersion = {PlayerSettings.bundleVersion}");

        ExcludeMediaFromBuild();
        bool failed = false;
        try
        {
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log("[BuildScript] Starting StandaloneWindows64 Build...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] Build succeeded: {summary.totalSize} bytes, output to {buildPath}");
            }
            else
            {
                failed = true;
                Debug.LogError($"[BuildScript] Build failed with {summary.totalErrors} errors.");
            }
        }
        finally
        {
            RestoreMediaAfterBuild();
            CopyUpdaterScriptIntoBuild(buildDir);
            WriteUpdateChannelFile(buildDir);
        }

        if (failed && Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }

    /// <summary>git rev-parse --short HEAD。失敗時は "unknown" (bundleVersion は 0.1.0+unknown)。</summary>
    private static string GetGitShortSha()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                if (process.ExitCode == 0 && output.Length > 0)
                {
                    return output;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BuildScript] git rev-parse に失敗しました: {e.Message}");
        }
        return "unknown";
    }

    /// <summary>第三者楽曲の音声・動画をビルド対象から外す (退避 → Refresh)。
    /// 前回ビルド異常終了で退避が戻っていない場合は先に自己修復する。</summary>
    private static void ExcludeMediaFromBuild()
    {
        RestoreMediaAfterBuild(); // 前回残留の自己修復を兼ねる (退避が無ければ何もしない)

        string excludeRoot = ProjectPath(MediaExcludeRoot);
        Directory.CreateDirectory(excludeRoot);
        foreach (string rel in MediaExcludePaths)
        {
            MovePath(ProjectPath(rel), Path.Combine(excludeRoot, Path.GetFileName(rel.TrimEnd('/'))));
        }
        AssetDatabase.Refresh();
        Debug.Log("[BuildScript] 第三者楽曲の音声・動画をビルド対象から除外しました (Musics / Videos/UnityYoutube / loading.mp4)");
    }

    /// <summary>退避したメディアを元の位置へ戻す。退避フォルダが無い/空なら何もしない。</summary>
    private static void RestoreMediaAfterBuild()
    {
        string excludeRoot = ProjectPath(MediaExcludeRoot);
        if (!Directory.Exists(excludeRoot))
        {
            return;
        }
        foreach (string rel in MediaExcludePaths)
        {
            string fileName = Path.GetFileName(rel.TrimEnd('/'));
            MovePath(Path.Combine(excludeRoot, fileName), ProjectPath(rel));
        }
        try
        {
            if (Directory.GetFileSystemEntries(excludeRoot).Length == 0)
            {
                Directory.Delete(excludeRoot);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BuildScript] 退避フォルダを削除できませんでした: {excludeRoot} ({e.Message})");
        }
        AssetDatabase.Refresh();
    }

    /// <summary>src (ファイル/ディレクトリ) と隣接する .meta を dst へ移動する。src が無ければ何もしない。</summary>
    private static void MovePath(string src, string dst)
    {
        if (!File.Exists(src) && !Directory.Exists(src))
        {
            return;
        }
        string dstDir = Path.GetDirectoryName(dst);
        if (!Directory.Exists(dstDir))
        {
            Directory.CreateDirectory(dstDir);
        }
        if (File.Exists(src))
        {
            File.Move(src, dst);
        }
        else
        {
            Directory.Move(src, dst);
        }
        if (File.Exists(src + ".meta"))
        {
            File.Move(src + ".meta", dst + ".meta");
        }
    }

    private static string ProjectPath(string relative)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), relative.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>配布物内でアプリが起動できるよう tools/update/Update.ps1 をビルド出力へ複製する
    /// (UpdateChecker が -Apply で起動するスクリプト)。無い場合は警告のみ (更新は手動手順へフォールバック)。</summary>
    private static void CopyUpdaterScriptIntoBuild(string buildDir)
    {
        string src = Path.Combine(Directory.GetCurrentDirectory(), "tools", "update", "Update.ps1");
        if (!File.Exists(src))
        {
            Debug.LogWarning("[BuildScript] tools/update/Update.ps1 が見つかりません (ビルド出力へ複製できませんでした)。");
            return;
        }
        string dst = Path.Combine(buildDir, "tools", "update", "Update.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.Copy(src, dst, true);
        Debug.Log($"[BuildScript] 更新スクリプトをビルド出力へ複製しました: {dst}");
    }

    /// <summary>更新チャンネルを配布物へ書き出す (issue #41)。
    /// 環境変数 FLASHBEAT_UPDATE_CHANNEL (stable / preview) で決まる。既定は stable。
    /// 出力先は &lt;build&gt;/FlashBeat_Data/StreamingAssets/update-channel.txt で、
    /// UpdateChecker が起動時に読む。stable ビルドはテスト用 prerelease を自動適用しない。</summary>
    private static void WriteUpdateChannelFile(string buildDir)
    {
        string channel = ResolveUpdateChannel();
        string streamingAssets = Path.Combine(buildDir, "FlashBeat_Data", "StreamingAssets");
        try
        {
            Directory.CreateDirectory(streamingAssets);
            string path = Path.Combine(streamingAssets, "update-channel.txt");
            File.WriteAllText(path, channel);
            Debug.Log($"[BuildScript] 更新チャンネル '{channel}' を書き出しました: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildScript] 更新チャンネルの書き出しに失敗しました: {e.Message} " +
                           "(UpdateChecker は safe 側 (stable) に倒します)。");
        }
    }

    /// <summary>環境変数 FLASHBEAT_UPDATE_CHANNEL を正規化する。未知の値は stable。</summary>
    private static string ResolveUpdateChannel()
    {
        string raw = Environment.GetEnvironmentVariable("FLASHBEAT_UPDATE_CHANNEL");
        if (!string.IsNullOrEmpty(raw) &&
            raw.Trim().Equals(PreviewUpdateChannel, StringComparison.OrdinalIgnoreCase))
        {
            return PreviewUpdateChannel;
        }
        if (!string.IsNullOrEmpty(raw) &&
            !raw.Trim().Equals(DefaultUpdateChannel, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[BuildScript] FLASHBEAT_UPDATE_CHANNEL='{raw}' は未知です。{DefaultUpdateChannel} として扱います。");
        }
        return DefaultUpdateChannel;
    }
}
