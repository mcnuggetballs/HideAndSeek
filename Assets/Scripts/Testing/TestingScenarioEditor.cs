using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.VisualScripting;

// edits paint scenario data                                                                                                                                                           
// this file handles mouse clicks,
//           brush selection,
//           asks scenariogrid what cell was clicked and
//           tells scenariogrid to paint the cell

public class TestingScenarioEditor : MonoBehaviour
{
    // Enums
    private enum PaintBrush
    {
        None,
        Wall,
        Erase,
        Seeker,
        Hider
    }

    // Editor States
    private PaintBrush currentBrush;
    private bool canPaint = true;

    private readonly Dictionary<Vector2Int, GameObject> paintedVisuals = new Dictionary<Vector2Int, GameObject>();

    // Inspector References
    [SerializeField] private Camera sceneCamera;

    [SerializeField] private ScenarioGrid grid;
    [SerializeField] private EnvironmentManager environmentManager;

    [SerializeField] private GameObject seekerPreviewPrefab;
    [SerializeField] private GameObject hiderPreviewPrefab;
    [SerializeField] private GameObject obstaclePreviewPrefab;

    public float placementYOffset = 0.02f;

    #region Unity Lifecycle
    private void OnEnable()
    {
        // testinscenarioeditor listens for placement requests
        GameEvents.SpawnSeekerRequested += SelectSeekerBrush;
        GameEvents.SpawnHiderRequested += SelectHiderBrush;
        GameEvents.SpawnWallRequested += SelectWallBrush;
        GameEvents.EraseRequested += SelectEraseBrush;

        GameEvents.PlayRequested += HidePaintedVisuals;
        GameEvents.ResetRequested += ShowPaintedVisuals;

        Debug.Log("TestingScenarioEditor is listening for object placement requests.");
    }
    private void OnDisable()
    {
        GameEvents.SpawnSeekerRequested -= SelectSeekerBrush;
        GameEvents.SpawnHiderRequested -= SelectHiderBrush;
        GameEvents.SpawnWallRequested -= SelectWallBrush;
        GameEvents.EraseRequested -= SelectEraseBrush;

        GameEvents.PlayRequested -= HidePaintedVisuals;
        GameEvents.ResetRequested -= ShowPaintedVisuals;
    }

    private void Update()
    {
        // was escape key pressed? 
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            currentBrush = PaintBrush.None; // stop placing anything
            Debug.Log("Stopped placing anything.");
            return;
        }

        // if not placing seeker or no mouse click = dont do anything 
        if (!canPaint || currentBrush == PaintBrush.None || !TryGetMouseClick(out Vector2 mousePosition)) // if in placement mode 
        {
            return;
        }

        // check if u click UI instead of world
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("Object placement click ignored because the pointer is over UI.");
            return;
        }

        PaintAtMousePosition(mousePosition);
    }
    #endregion

    #region Brush Selection
    public void SelectSeekerBrush()
    {
        SelectBrush(PaintBrush.Seeker);
    }

    public void SelectHiderBrush()
    {
        SelectBrush(PaintBrush.Hider);
    }

    public void SelectWallBrush()
    {
        SelectBrush(PaintBrush.Wall);
    }

    public void SelectEraseBrush()
    {
        SelectBrush(PaintBrush.Erase);
    }

    private void SelectBrush(PaintBrush brushMode)
    {
        currentBrush = brushMode;
        Debug.Log($"{currentBrush} placement mode is active. Click on the map to place.");
    }

    #endregion

    #region Input
    // check if left mouse button was click this frame, return the mouse screen position
    private bool TryGetMouseClick(out Vector2 mousePosition)
    {
        mousePosition = Vector2.zero;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return false;
        }

        mousePosition = Mouse.current.position.ReadValue();
        return true;
    }
    #endregion

    #region Painting
    private void PaintAtMousePosition(Vector2 mousePosition)
    {
        if (grid == null)
        {
            Debug.LogWarning($"Cannot paint because no scenarioGrid assigned.");
            return;
        }

        Camera cameraToUse = sceneCamera != null ? sceneCamera : Camera.main;
        if (cameraToUse == null)
        {
            Debug.LogWarning("Cannot paint because no camera is assigned.");
            return;
        }

        // get mouse position based on mouse click
        Vector3 surfacePosition = GetMouseSurfacePosition(cameraToUse, mousePosition);
        // convert mouse position to grid position
        Vector2Int cell = grid.WorldToCell(surfacePosition);

        // if cell not inside grid, skip
        if (!grid.IsInsideGrid(cell))
        {
            Debug.LogWarning("Cannot paint outside grid!");
            return;
        }

        ApplyBrushToCell(cell);
    }

    private void ApplyBrushToCell(Vector2Int cell)
    {
        switch (currentBrush)
        {
            case PaintBrush.Wall:
                PaintCell(cell, ScenarioGrid.WallCell, obstaclePreviewPrefab);
                break;
            case PaintBrush.Seeker:
                PaintCell(cell, ScenarioGrid.SeekerCell, seekerPreviewPrefab);
                break;
            case PaintBrush.Hider:
                PaintCell(cell, ScenarioGrid.HiderCell, hiderPreviewPrefab);
                break;
            case PaintBrush.Erase:
                EraseCell(cell);
                break;
            default:
                break;
        }
    }

    private void PaintCell(Vector2Int cell, char cellValue, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"No prefab assigned for {currentBrush}.");
            return;
        }

        EraseCell(cell); // if painting over "seeker", removes seeker visual first so one cell = one thing

        // updates data layer, change map data
        grid.SetCell(cell, cellValue);

        // updates visual layer
        Vector3 worldPosition = grid.CellToWorld(cell); // need world position to place object
        GameObject visual = Instantiate(prefab, worldPosition, Quaternion.identity, environmentManager.GetEditorRoot());

        PlaceObjectOnSurface(visual, worldPosition);

        paintedVisuals[cell] = visual; // store spawned gameObject based on grid cell

        // update runtime if simulation started
        if (environmentManager.IsStarted())
        {
            environmentManager.UpdateRuntimeCell(cell, cellValue); // mini update again
        }

    }

    // Remove Visual + Data layer
    private void EraseCell(Vector2Int cell)
    {
        // remove visual
        if (paintedVisuals.TryGetValue(cell, out GameObject obj))
        {
            Destroy(obj);
            paintedVisuals.Remove(cell);
        }

        // update data layer first
        grid.SetCell(cell, ScenarioGrid.EmptyCell);

        if (environmentManager.IsStarted())
        {
            environmentManager.UpdateRuntimeCell(cell, ScenarioGrid.EmptyCell);
        }
    }
    #endregion

    #region Placement
    // using camera to get mouse click position
    private Vector3 GetMouseSurfacePosition(Camera cameraToUse, Vector2 mousePosition)
    {
        Ray ray = cameraToUse.ScreenPointToRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null)
            {
                return hit.point;
            }
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return Vector3.zero;
    }

    // purely to lift the agent on top of plane
    private void PlaceObjectOnSurface(GameObject objectToPlace, Vector3 surfacePosition)
    {
        if (!TryGetColliderBounds(objectToPlace, out Bounds bounds))
        {
            objectToPlace.transform.position = surfacePosition + Vector3.up * placementYOffset;
            return;
        }

        float liftAmount = surfacePosition.y - bounds.min.y + placementYOffset;
        objectToPlace.transform.position += Vector3.up * liftAmount;
    }

    // this function collects all non-trigger colliders in object and children. combines them into one big world space "bounds"
    private bool TryGetColliderBounds(GameObject targetObject, out Bounds combinedBounds)
    {
        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>();
        combinedBounds = new Bounds(targetObject.transform.position, Vector3.zero);
        //combinedBounds = new Bounds();
        bool hasBounds = false;

        foreach (Collider collider in colliders)
        {
            if (collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(collider.bounds);
        }

        return hasBounds;
    }
    #endregion

    #region Visual Control

    private void HidePaintedVisuals()
    {
        canPaint = false;
        currentBrush = PaintBrush.None;
        SetPaintedVisualsActive(false);
    }

    private void ShowPaintedVisuals()
    {
        canPaint = true;
        SetPaintedVisualsActive(true);
    }

    public void ResetEditorState()
    {
        Debug.Log("Resetting editor state");
        canPaint = true;
        currentBrush = PaintBrush.None;
    }

    private void SetPaintedVisualsActive(bool isActive)
    {
        foreach (GameObject visual in paintedVisuals.Values)
        {
            if (visual != null)
            {
                visual.SetActive(isActive);
            }
        }
    }

    public void ClearEditorVisuals()
    {
        foreach (var kvp in paintedVisuals)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        paintedVisuals.Clear();
    }
    #endregion

}
