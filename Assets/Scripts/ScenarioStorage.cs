using System;
using System.IO;
using System.Text;
using UnityEngine;

// saves and loads text layouts under unity's persistent data directory, validate scenario filenames

public static class ScenarioStorage
{
    public static string GetPath(string scenarioName)
    {
        if (string.IsNullOrWhiteSpace(scenarioName))
            throw new ArgumentException("Scenario name cannot be empty.", nameof(scenarioName));

        foreach (char character in scenarioName)
            if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                throw new ArgumentException("Scenario names may contain letters, digits, underscores, and hyphens only.", nameof(scenarioName));

        return Path.Combine(Application.persistentDataPath, "Scenarios", scenarioName + ".txt");
    }

    public static string Save(string scenarioName, ScenarioGrid grid)
    {
        string path = GetPath(scenarioName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, ScenarioTextFormat.Serialize(grid), new UTF8Encoding(false));
        grid.MarkSaved();
        return path;
    }

    public static ScenarioGrid Load(string scenarioName, float cellSize, Vector3 origin)
    {
        string path = GetPath(scenarioName);
        return LoadFile(path, cellSize, origin);
    }

    // Imports the same map format from a user-selected file without copying or modifying it.
    public static ScenarioGrid LoadFile(string path, float cellSize, Vector3 origin)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Select a scenario text file.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Scenario file does not exist: {path}", path);
        return ScenarioTextFormat.Parse(File.ReadAllText(path), cellSize, origin);
    }
}
