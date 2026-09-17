using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using static ScenarioSystem;

public static class ScenarioGenerationChecks
{
    [MenuItem("Tools/Hide And Seek/Check Scenario Generation")]
    public static void Run()
    {
        TextAsset map = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Map Files/Cortex1.txt");
        Require(map != null, "Cortex1.txt was not found.");

        ScenarioGrid fixedGrid = ScenarioTextFormat.Parse(map.text, 1f, Vector3.zero);
        Require(fixedGrid.Width == 33 && fixedGrid.Height == 20, "Fixed map dimensions are wrong.");
        Require(Count(fixedGrid, ScenarioGrid.SeekerCell) == 2, "Fixed map must have two seekers.");
        Require(Count(fixedGrid, ScenarioGrid.HiderCell) == 2, "Fixed map must have two hiders.");

        string savedText = ScenarioTextFormat.Serialize(fixedGrid);
        ScenarioGrid loaded = ScenarioTextFormat.Parse(savedText + "\n", 1f, Vector3.zero);
        Require(ScenarioTextFormat.Serialize(loaded) == savedText,
            "Saving and loading must preserve every map cell.");
        RequireThrows<FormatException>(() => ScenarioTextFormat.Parse("XX\nX", 1f, Vector3.zero));
        RequireThrows<FormatException>(() => ScenarioTextFormat.Parse("X?\nXX", 1f, Vector3.zero));

        string checkName = "scenario_check_" + Guid.NewGuid().ToString("N");
        string checkPath = ScenarioStorage.GetPath(checkName);
        try
        {
            ScenarioStorage.Save(checkName, fixedGrid);
            ScenarioGrid stored = ScenarioStorage.Load(checkName, 1f, Vector3.zero);
            Require(ScenarioTextFormat.Serialize(stored) == savedText,
                "Saved scenarios must load without changing their cells.");
        }
        finally
        {
            if (File.Exists(checkPath)) File.Delete(checkPath);
        }

        var temporary = new GameObject("ScenarioGenerationCheck") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            ScenarioSystem generator = temporary.AddComponent<ScenarioSystem>();
            generator.obstacleCount = 8;
            generator.seekerCount = 2;
            generator.hiderCount = 2;
            var config = new SimulationController.WorldConfig
            {
                width = 7,
                height = 4,
                cellSize = 1f,
                origin = Vector3.zero
            };
            ScenarioGrid randomGrid = generator.Generate(ScenarioType.Random, null, config);
            Require(randomGrid != null, "Random generation failed.");
            Require(Count(randomGrid, ScenarioGrid.WallCell) == 8, "Random wall count is wrong.");
            Require(Count(randomGrid, ScenarioGrid.SeekerCell) == 2, "Random seeker count is wrong.");
            Require(Count(randomGrid, ScenarioGrid.HiderCell) == 2, "Random hider count is wrong.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temporary);
        }

        Debug.Log("Scenario generation checks passed: fixed map, text and file round trips, and random counts.");
    }

    private static int Count(ScenarioGrid grid, char value)
    {
        int count = 0;
        foreach (Vector2Int cell in grid.GetAllCells())
            if (grid.GetCell(cell) == value) count++;
        return count;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireThrows<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
