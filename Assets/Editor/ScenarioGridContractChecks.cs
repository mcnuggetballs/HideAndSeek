using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// test file to ensure scenariogrid.cs is working as required and kept to ensure future code doesnt break how scenariogrid.cs handles data
public static class ScenarioGridContractChecks
{
    [MenuItem("Tools/Hide And Seek/Check Scenario Grid")]
    public static void Run()
    {
        var grid = new ScenarioGrid();
        RequireThrows<ArgumentOutOfRangeException>(() => grid.Initialise(0, 2, 1f, Vector3.zero));
        RequireThrows<ArgumentOutOfRangeException>(() => grid.Initialise(3, 2, 0f, Vector3.zero));
        RequireThrows<InvalidOperationException>(() => grid.WorldToCell(Vector3.zero));

        var origin = new Vector3(10f, 1f, -4f);
        grid.Initialise(3, 2, 2f, origin);
        Require(grid.LayoutRevision == 0, "A new grid should start at layout revision zero.");
        Require(grid.SavedRevision == 0, "A new grid should start at saved revision zero.");
        Require(!grid.HasUnsavedChanges, "A new grid should have no unsaved changes.");
        Require(grid.GetAllCells().Count() == 6, "A 3x2 grid must have six cells.");

        foreach (var cell in grid.GetAllCells())
        {
            Require(grid.GetCell(cell) == ScenarioGrid.EmptyCell, "New cells must contain spaces.");
            Require(grid.WorldToCell(grid.CellToWorld(cell)) == cell, "Cell center must round-trip.");
            Require(grid.CellToWorld(cell).y == origin.y, "Cell centers must use the origin Y plane.");
        }

        Require(grid.WorldToCell(new Vector3(7f, 1f, -6f)) == new Vector2Int(0, 0),
            "Lower boundaries must be inclusive.");
        Require(grid.WorldToCell(new Vector3(13f, 1f, -2f)) == new Vector2Int(3, 2),
            "Upper boundaries must be exclusive.");
        Require(!grid.IsInsideGrid(grid.WorldToCell(new Vector3(13f, 1f, -2f))),
            "The upper corner must be outside the grid.");
        Require(!grid.IsInsideGrid(grid.WorldToCell(new Vector3(6.99f, 1f, -6f))),
            "A position left of the grid must be outside.");

        var lastCell = new Vector2Int(2, 1);
        Require(grid.SetCell(lastCell, ScenarioGrid.WallCell), "Valid writes must succeed.");
        Require(grid.LayoutRevision == 1 && grid.HasUnsavedChanges,
            "A changed cell must advance the layout revision.");
        grid.MarkSaved();
        Require(grid.SavedRevision == 1 && !grid.HasUnsavedChanges,
            "MarkSaved must record the current layout revision.");
        grid.SetCell(lastCell, ScenarioGrid.WallCell);
        Require(grid.LayoutRevision == 1 && !grid.HasUnsavedChanges,
            "Writing the same value must not advance the layout revision.");
        RequireThrows<ArgumentException>(() => grid.SetCell(lastCell, '?'));
        Require(!grid.SetCell(new Vector2Int(3, 1), ScenarioGrid.WallCell),
            "Out-of-bounds writes must fail.");

        grid.Resize(5, 4);
        Require(grid.LayoutRevision == 2 && grid.HasUnsavedChanges,
            "A resize must advance the layout revision.");
        Require(grid.GetCell(lastCell) == ScenarioGrid.WallCell, "Resize must preserve overlapping cells.");
        Require(grid.GetCell(new Vector2Int(4, 3)) == ScenarioGrid.EmptyCell,
            "New cells after resize must be empty.");
        grid.MarkSaved();
        grid.Resize(5, 4);
        Require(grid.LayoutRevision == 2 && !grid.HasUnsavedChanges,
            "Resizing to the same dimensions must not advance the layout revision.");

        grid.ClearGrid();
        Require(grid.LayoutRevision == 3 && grid.HasUnsavedChanges &&
                grid.GetCell(lastCell) == ScenarioGrid.EmptyCell,
            "Clearing a nonempty grid must clear values and advance the layout revision once.");
        grid.MarkSaved();
        grid.ClearGrid();
        Require(grid.LayoutRevision == 3 && !grid.HasUnsavedChanges,
            "Clearing an empty grid must not advance the layout revision.");

        Debug.Log("ScenarioGrid contract checks passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
