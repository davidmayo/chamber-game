using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectBuildPipeline
{
    private const string BuildMenuPath =
        "Tools/Build/Clean and Build Windows + Linux";

    [MenuItem(BuildMenuPath, priority = 2000)]
    public static void CleanAndBuildAll()
    {
        if (!Application.isBatchMode
            && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("Build cancelled while saving modified scenes.");
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string buildsRoot = GetValidatedBuildsRoot(projectRoot);
        string[] scenes = GetEnabledScenes();

        ValidateTargetSupport(BuildTarget.StandaloneWindows64, "Windows");
        ValidateTargetSupport(BuildTarget.StandaloneLinux64, "Linux");

        try
        {
            CleanBuildsDirectory(buildsRoot);

            List<BuildReport> reports = new()
            {
                Build(
                    scenes,
                    Path.Combine(buildsRoot, "Windows", "Chamber.exe"),
                    BuildTarget.StandaloneWindows64),
                Build(
                    scenes,
                    Path.Combine(buildsRoot, "Linux", "Chamber.x86_64"),
                    BuildTarget.StandaloneLinux64),
            };

            ulong totalBytes = 0;
            TimeSpan totalTime = TimeSpan.Zero;
            foreach (BuildReport report in reports)
            {
                totalBytes += report.summary.totalSize;
                totalTime += report.summary.totalTime;
            }

            Debug.Log(
                $"Windows and Linux builds completed in {totalTime:g}. " +
                $"Combined player size: {EditorUtility.FormatBytes((long)totalBytes)}. " +
                $"Output: {buildsRoot}");

            if (!Application.isBatchMode)
            {
                EditorUtility.RevealInFinder(buildsRoot);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static BuildReport Build(
        string[] scenes,
        string outputPath,
        BuildTarget target)
    {
        string platformName = target == BuildTarget.StandaloneWindows64
            ? "Windows"
            : "Linux";
        EditorUtility.DisplayProgressBar(
            "Building Chamber",
            $"Building {platformName} player...",
            target == BuildTarget.StandaloneWindows64 ? 0.25f : 0.7f);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        BuildPlayerOptions options = new()
        {
            scenes = scenes,
            locationPathName = outputPath,
            targetGroup = BuildTargetGroup.Standalone,
            target = target,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"{platformName} build {report.summary.result}: " +
                $"{report.summary.totalErrors} error(s), " +
                $"{report.summary.totalWarnings} warning(s).");
        }

        return report;
    }

    private static string[] GetEnabledScenes()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new BuildFailedException(
                "No enabled scenes were found in Build Profiles/Scene List.");
        }

        foreach (string scene in scenes)
        {
            if (!File.Exists(scene))
            {
                throw new BuildFailedException(
                    $"Enabled build scene does not exist: {scene}");
            }
        }

        return scenes;
    }

    private static void ValidateTargetSupport(BuildTarget target, string name)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
        {
            throw new BuildFailedException(
                $"{name} build support is not installed for this Unity Editor version. " +
                "Add the platform module from Unity Hub before running the build command.");
        }
    }

    private static string GetValidatedBuildsRoot(string projectRoot)
    {
        string buildsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Builds"));
        string expectedParent = Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string actualParent = Directory.GetParent(buildsRoot)?.FullName
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!string.Equals(
                actualParent,
                expectedParent,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                Path.GetFileName(buildsRoot),
                "Builds",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BuildFailedException(
                $"Refusing to clean unexpected build path: {buildsRoot}");
        }

        return buildsRoot;
    }

    private static void CleanBuildsDirectory(string buildsRoot)
    {
        if (Directory.Exists(buildsRoot))
        {
            Directory.Delete(buildsRoot, recursive: true);
        }
        Directory.CreateDirectory(buildsRoot);
        Debug.Log($"Cleaned build output: {buildsRoot}");
    }
}
