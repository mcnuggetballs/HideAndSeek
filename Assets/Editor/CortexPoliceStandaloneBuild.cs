using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CortexPoliceStandaloneBuild
{
    [MenuItem("Tools/Police Curriculum/Build Cortex1 Training Player")]
    public static void Run()
    {
        const string output = "Builds/CortexPolice/CortexPolice.exe";
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/TrainingCortexPoliceScene.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Debug.Log($"CORTEX_POLICE_BUILD: {report.summary.result}, " +
            $"{report.summary.totalErrors} errors, {output}");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 2);
    }
}
