using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class OrderedPoliceStandaloneBuild
{
    [MenuItem("Tools/Police Curriculum/Build Training Player")]
    public static void Run()
    {
        const string output = "Builds/OrderedPolice/OrderedPolice.exe";
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/TrainingOrderedPoliceScene.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Debug.Log($"ORDERED_POLICE_BUILD: {report.summary.result}, " +
            $"{report.summary.totalErrors} errors, {output}");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 2);
    }
}
