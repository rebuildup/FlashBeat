using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
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
            "Assets/Scenes/Opening.unity",
            "Assets/Scenes/TitleScene.unity",
            "Assets/Scenes/SelectScene.unity",
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/ResultScene.unity",
            "Assets/Scenes/OptionScene.unity"
        };

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
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"[BuildScript] Build failed with {summary.totalErrors} errors.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
