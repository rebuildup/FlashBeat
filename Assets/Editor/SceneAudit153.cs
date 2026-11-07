using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batch-mode audit for Issue #153: opens TitleScene / SelectScene (plus any scene
/// under Assets/Game/Scenes/__fb153_variants/ when that folder exists), captures the
/// load-time errors, dumps the loaded hierarchy, and re-saves the two production
/// scenes to /tmp for YAML diffing.
///
/// Usage: Unity -batchmode -projectPath . -executeMethod SceneAudit153.Run -quit
/// </summary>
public static class SceneAudit153
{
    private const string VariantFolder = "Assets/Game/Scenes/__fb153_variants";

    private static readonly string[] ProductionScenes =
    {
        "Assets/Game/Scenes/TitleScene.unity",
        "Assets/Game/Scenes/SelectScene.unity",
    };

    public static void Run()
    {
        var report = new StringBuilder();
        var scenes = new List<string>(ProductionScenes);
        if (Directory.Exists(VariantFolder))
        {
            scenes.AddRange(Directory.GetFiles(VariantFolder, "*.unity", SearchOption.AllDirectories));
        }

        foreach (var scenePath in scenes)
        {
            AuditScene(report, scenePath, IsProduction(scenePath));
        }

        File.WriteAllText("/tmp/fb153_audit.txt", report.ToString());
        Console.WriteLine(report.ToString());
    }

    private static bool IsProduction(string scenePath)
    {
        foreach (var production in ProductionScenes)
        {
            if (production == scenePath)
            {
                return true;
            }
        }

        return false;
    }

    private static void AuditScene(StringBuilder report, string scenePath, bool resave)
    {
        var errors = new List<string>();
        Application.LogCallback handler = (condition, stackTrace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors.Add(condition);
            }
        };
        Application.logMessageReceived += handler;
        try
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            report.AppendLine($"=== {scenePath} ===");
            report.AppendLine($"load errors: {errors.Count}");
            foreach (var error in errors)
            {
                report.AppendLine($"  ERROR: {error}");
            }
            report.AppendLine($"loaded root count: {scene.rootCount}");
            foreach (var root in scene.GetRootGameObjects())
            {
                Dump(report, root.transform, 0);
            }

            if (resave)
            {
                var target = Path.Combine("/tmp", "fb153_resaved_" + Path.GetFileName(scenePath));
                EditorSceneManager.SaveScene(scene, target);
                report.AppendLine($"re-saved to: {target}");
            }
        }
        finally
        {
            Application.logMessageReceived -= handler;
        }
    }

    private static void Dump(StringBuilder report, Transform transform, int depth)
    {
        report.AppendLine(
            $"{new string(' ', depth * 2)}{transform.name} [children={transform.childCount}] active={transform.gameObject.activeSelf}");
        for (var i = 0; i < transform.childCount; i++)
        {
            Dump(report, transform.GetChild(i), depth + 1);
        }
    }
}
