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
    // GitHub Release タグ "v<version>-<platform>-<sha>" (platform = win64 / macos) と
    // UpdateChecker が比較する。
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
    // loading.mp4 は自作素材 (8.7MB) でローディング画面の本体なので同梱する (Issue #233)。
    private static readonly string[] MediaExcludePaths =
    {
        "Assets/Game/Resources/Musics",
        "Assets/Game/Resources/Videos/UnityYoutube",
    };
    private const string MediaExcludeRoot = "Builds/.media-excluded";

    [MenuItem("Build/Build Windows 64")]
    public static void BuildStandaloneWindows64()
    {
        string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows", "FlashBeat.exe");
        RunStandaloneBuild(BuildTarget.StandaloneWindows64, buildPath, distributionExtras: true);
    }

    // macOS 配布ビルド (batchmode: -executeMethod BuildScript.BuildStandaloneMacOS)。
    // 出力は Builds/macOS/FlashBeat.app。Windows と同じく更新経路に載せるため、
    // 配布物内へ更新スクリプト (Contents/Resources/tools/update/update-macos.sh)、
    // 更新チャンネル、メディア除外マニフェストを書き出す。
    [MenuItem("Build/Build macOS")]
    public static void BuildStandaloneMacOS()
    {
        string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "macOS", "FlashBeat.app");
        RunStandaloneBuild(BuildTarget.StandaloneOSX, buildPath, distributionExtras: true);
    }

    /// <summary>Standalone ビルドの共通フロー。第三者楽曲の音声・動画を退避してビルドし、
    /// 終了後に元へ戻す。配布物向けの後処理 (updater 複製・更新チャンネル・
    /// メディア除外マニフェスト) は distributionExtras が true のときだけ行う。</summary>
    private static void RunStandaloneBuild(BuildTarget target, string buildPath, bool distributionExtras)
    {
        string buildDir = Path.GetDirectoryName(buildPath);

        // 既存出力が残ったまま上書きビルドすると level1 等が破損するため、ビルド前に
        // 出力ディレクトリを削除する。削除対象は Builds/ 直下の期待出力ディレクトリのみで、
        // メディア退避先 (Builds/.media-excluded) は決して削除しない。
        string fullBuildDir = Path.GetFullPath(buildDir);
        bool isExpectedBuildDir =
            Path.GetFileName(Path.GetDirectoryName(fullBuildDir)) == "Builds" &&
            fullBuildDir != Path.GetFullPath(MediaExcludeRoot);
        if (Directory.Exists(buildDir) && isExpectedBuildDir)
        {
            Directory.Delete(buildDir, true);
        }

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
                target = target,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildScript] Starting {target} Build...");
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
            if (distributionExtras)
            {
                CopyUpdaterScriptIntoBuild(buildDir, target);
                WriteUpdateChannelFile(buildDir, target);
                WriteMediaPolicyManifest(buildDir, target);
            }
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
            try
            {
                MovePath(Path.Combine(excludeRoot, fileName), ProjectPath(rel));
            }
            catch (Exception e)
            {
                // ビルド自体は成功している。メディアの復元に失敗してもバッチ全体を
                // 失敗扱いにしない (メディアは Git LFS 管理なので git で戻せる)。
                Debug.LogError($"[BuildScript] 退避メディアの復元に失敗しました: {rel} ({e.Message})");
            }
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

    /// <summary>src (ファイル/ディレクトリ) と隣接する .meta を dst へ移動する。src が無ければ何もしない。
    ///
    /// 移動先が既に存在する場合は **上書きマージ** する。ビルド中に Unity が
    /// 退避先と同じ名前のフォルダを作り直すことがあるため、
    /// Directory.Move のままだと「同名がある」で例外になり、
    /// ビルド成功後にもかかわらずバッチ全体が失敗扱いになっていた (2026-11-10 実測)。</summary>
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
            File.Copy(src, dst, true);
            File.Delete(src);
        }
        else if (Directory.Exists(dst))
        {
            // 移動先がある: 中身を残したまま上書きマージし、移動元を消す。
            MergeDirectory(src, dst);
            Directory.Delete(src, true);
        }
        else
        {
            Directory.Move(src, dst);
        }
        if (File.Exists(src + ".meta"))
        {
            File.Copy(src + ".meta", dst + ".meta", true);
            File.Delete(src + ".meta");
        }
    }

    /// <summary>src ディレクトリの内容を dst へ上書きマージする (移動元のファイルは消える)。</summary>
    private static void MergeDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (string file in Directory.GetFiles(src))
        {
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            File.Delete(file);
        }
        foreach (string dir in Directory.GetDirectories(src))
        {
            MergeDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
            Directory.Delete(dir);
        }
    }

    private static string ProjectPath(string relative)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), relative.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>配布物内でアプリが起動できるよう更新スクリプトをビルド出力へ複製する
    /// (UpdateChecker が -Apply で起動するスクリプト)。Windows は tools/update/Update.ps1、
    /// macOS は .app 内の Contents/Resources/tools/update/update-macos.sh。
    /// 無い場合は警告のみ (更新は手動手順へフォールバック)。</summary>
    private static void CopyUpdaterScriptIntoBuild(string buildDir, BuildTarget target)
    {
        bool isMac = target == BuildTarget.StandaloneOSX;
        string srcRel = isMac ? "tools/update/update-macos.sh" : "tools/update/Update.ps1";
        string src = Path.Combine(Directory.GetCurrentDirectory(), srcRel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(src))
        {
            Debug.LogWarning("[BuildScript] " + srcRel + " が見つかりません (ビルド出力へ複製できませんでした)。");
            return;
        }

        string dst = isMac
            ? Path.Combine(MacBundleResourcesDir(buildDir), "tools", "update", "update-macos.sh")
            : Path.Combine(buildDir, "tools", "update", "Update.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.Copy(src, dst, true);
        Debug.Log($"[BuildScript] 更新スクリプトをビルド出力へ複製しました: {dst}");
    }

    /// <summary>macOS .app の Contents/Resources。</summary>
    private static string MacBundleResourcesDir(string buildDir)
    {
        return Path.Combine(buildDir, "FlashBeat.app", "Contents", "Resources");
    }

    /// <summary>ビルド対象から外したメディアを記録する (配布物には同梱し、検品で参照する)。
    /// resources.resource はファイル名を持たないため、第三者楽曲の音声・動画が
    /// 焼き込まれていないことはこの manifest で証明する。</summary>
    private static void WriteMediaPolicyManifest(string buildDir, BuildTarget target)
    {
        try
        {
            var excluded = new System.Collections.Generic.List<string>();
            foreach (var rel in MediaExcludePaths) { excluded.Add(rel); }
            var json = "{\n" +
                       "  \"policy\": \"song-media-excluded\",\n" +
                       "  \"excludedPaths\": [\n" +
                       string.Join(",\n", System.Array.ConvertAll(excluded.ToArray(),
                           p => "    \"" + p.Replace("\\", "/") + "\"")) + "\n" +
                       "  ],\n" +
                       "  \"builtAt\": \"" + DateTime.UtcNow.ToString("o") + "\"\n" +
                       "}\n";
            string path = target == BuildTarget.StandaloneOSX
                ? Path.Combine(MacBundleResourcesDir(buildDir), "flashbeat-media-policy.json")
                : Path.Combine(buildDir, "flashbeat-media-policy.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            Debug.Log($"[BuildScript] メディア除外マニフェストを出力しました: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildScript] メディア除外マニフェストの書き出しに失敗しました: {e.Message}");
        }
    }

    /// <summary>更新チャンネルを配布物へ書き出す (issue #41)。
    /// 環境変数 FLASHBEAT_UPDATE_CHANNEL (stable / preview) で決まる。既定は stable。
    /// 出力先は Windows: &lt;build&gt;/FlashBeat_Data/StreamingAssets、
    /// macOS: &lt;build&gt;/FlashBeat.app/Contents/Resources/Data/StreamingAssets の
    /// update-channel.txt で、UpdateChecker が起動時に読む。
    /// stable ビルドはテスト用 prerelease を自動適用しない。</summary>
    private static void WriteUpdateChannelFile(string buildDir, BuildTarget target)
    {
        string channel = ResolveUpdateChannel();
        string streamingAssets = target == BuildTarget.StandaloneOSX
            ? Path.Combine(MacBundleResourcesDir(buildDir), "Data", "StreamingAssets")
            : Path.Combine(buildDir, "FlashBeat_Data", "StreamingAssets");
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
