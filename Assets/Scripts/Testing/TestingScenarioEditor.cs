using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// edits paint scenario data                                                                                                                                                           
// this file handles mouse clicks,
//           brush selection,
//           asks scenariogrid what cell was clicked and
//           tells scenariogrid to paint the cell

public class TestingScenarioEditor : MonoBehaviour
{
    private enum PaintBrush
    {
        None,
        Wall,
        Erase,
        Seeker,
        Hider
    }

    private PaintBrush currentBrush = PaintBrush.None;
    private bool canPaint = true;

    // why dictionary?  when erase can find and destroy exact object
    private readonly Dictionary<Vector2Int, GameObject> paintedVisuals = new Dictionary<Vector2Int, GameObject>();

    // assign in inspector
    public Camera sceneCamera;

    public float placementYOffset = 0.02f;
    public float seekerPreviewScale = 1f;

    [SerializeField] private ScenarioGrid grid; // not serialised so that can create in own environment 
    [SerializeField] private Transform environment; // parent? 

    // refine placement logic
    [SerializeField] private Collider placementArea;
    [SerializeField] private LayerMask placementBlockerLayers; // for overlap check

    [SerializeField] private GameObject emptySeekerPrefab;
    [SerializeField] private GameObject emptyHiderPrefab;
    [SerializeField] private GameObject ObstaclePrefab;

    // when this script becomes active, subscribe SelectSeekerBrush to SpawnSeeker requested
    // += and -= are what actually connect/disconnect the event listener.

    // when this script becomes inactive, stop listening for placement requests
    private void OnEnable()
    {
        // testinscenarioeditor listens for placement requests
        GameEvents.SpawnSeekerRequested += SelectSeekerBrush;
        GameEvents.SpawnHiderRequested += SelectHiderBrush;
        GameEvents.SpawnWallRequested += SelectWallBrush;
        GameEvents.EraseRequested += SelectEraseBrush;
        GameEvents.PlayRequested += HidePaintedVisuals;
        GameEvents.PauseRequested += ShowPaintedVisuals;
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
        GameEvents.PauseRequested -= ShowPaintedVisuals;
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

    // BRUSH SYSTEM //
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

    
    // INPUT SYSTEM //
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
                PaintCell(cell, ScenarioGrid.WallCell, ObstaclePrefab);
                break;
            case PaintBrush.Seeker:
                PaintCell(cell, ScenarioGrid.SeekerCell, emptySeekerPrefab);
                break;
            case PaintBrush.Hider:
                PaintCell(cell, ScenarioGrid.HiderCell, emptyHiderPrefab);
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
        GameObject visual = Instantiate(prefab, worldPosition, Quaternion.identity, environment);
        PlaceObjectOnSurface(visual, worldPosition);

        paintedVisuals[cell] = visual; // store spawned gameObject based on grid cell


    }

    private void EraseCell(Vector2Int cell)
    {
        // update data layer first
        grid.SetCell(cell, ScenarioGrid.EmptyCell);
        // then update visual layer, which is to literally remove
        if (paintedVisuals.TryGetValue(cell, out GameObject visual))
        {
            Destroy(visual);
            paintedVisuals.Remove(cell);
        }

        Debug.Log($"Erased cell from row {cell.y}, col {cell.x}");
    }

    
    // PLACEMENT LOGIC //

    // using camera to get mouse click position
    private Vector3 GetMouseSurfacePosition(Camera cameraToUse, Vector2 mousePosition)
    {
        Ray ray = cameraToUse.ScreenPointToRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {

            return hit.point;
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

   
    // HIDE & SHOW PAINT VISUALS //
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


}
