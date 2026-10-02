using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ChaseStandaloneBuild
{
    [MenuItem("Tools/Chase/Build Training Player")]
    public static void Run()
    {
        string output = "Builds/Chase/Chase.exe";
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/TrainingChaseScene.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"CHASE_BUILD: {report.summary.result}, {report.summary.totalErrors} errors, " +
            $"{report.summary.totalWarnings} warnings, {output}");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 2);
    }
}
