using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using NUnit.Framework.Constraints;


// this file handles spawn seeker/hider/obstacle, raycast mouseclick onto map, edit scenario. spawning related stuff is all handled here

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TestingScenarioEditor : MonoBehaviour
{
    private enum PlacementMode
    {
        None,
        Seeker,
        Hider,
        Obstacle
    }
    private PlacementMode currentPlacementMode = PlacementMode.None;

    // assign in inspector
    public Camera sceneCamera;

    public float placementYOffset = 0.02f;
    public float seekerPreviewScale = 1f;

    //private bool inPlacementMode;
    //private GameObject currentSeeker;

    [SerializeField]
    private Transform environment;
    [SerializeField]
    private GameObject emptySeekerPrefab;
    [SerializeField]
    private GameObject emptyHiderPrefab;
    [SerializeField]
    private GameObject ObstaclePrefab;

    // when this script becomes active, subscribe BeginPlaceSeeker to SpawnSeeker requested
    // += and -= are what actually connect/disconnect the event listener.
    private void OnEnable()
    {
        // testinscenarioeditor listens for spawn seeker requests
        GameEvents.SpawnSeekerRequested += BeginPlaceSeeker;
        GameEvents.SpawnHiderRequested += BeginPlaceHider;
        GameEvents.SpawnObstacleRequested += BeginPlaceObstacle;


        Debug.Log("TestingScenarioEditor is listening for object placement requests.");
    }

    // when this script becomes inactive, unsub BeginPlaceSeeker to SpawnSeeker requested
    private void OnDisable()
    {
        GameEvents.SpawnSeekerRequested -= BeginPlaceSeeker;
        GameEvents.SpawnHiderRequested -= BeginPlaceHider;
        GameEvents.SpawnObstacleRequested -= BeginPlaceObstacle;
    }

    public void BeginPlaceSeeker()
    {
        BeginPlacementMode(PlacementMode.Seeker);
    }

    public void BeginPlaceHider()
    {
        BeginPlacementMode(PlacementMode.Hider);
    }

    public void BeginPlaceObstacle()
    {
        BeginPlacementMode(PlacementMode.Obstacle);
    }

    private void BeginPlacementMode(PlacementMode placementMode)
    {
        currentPlacementMode = placementMode;
        Debug.Log($"{currentPlacementMode} placement mode is active. Click on the map to place.");
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            currentPlacementMode = PlacementMode.None; // stop placing anything
            Debug.Log("Stopped placing anything.");
            return;
        }

        // if not placing seeker or no mouse click = dont do anything 
        if (currentPlacementMode == PlacementMode.None || !TryGetMouseClick(out Vector2 mousePosition)) // if in placement mode 
        {
            return;
        }

        // check if u click UI instead of world
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("Object placement click ignored because the pointer is over UI.");
            return;
        }


        PlaceCurrentObjecttAtMousePosition(mousePosition);
    }

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

    // actually place the object
    private void PlaceCurrentObjecttAtMousePosition(Vector2 mousePosition)
    {
        GameObject prefabToPlace = GetPrefabForCurrentMode();

        if (prefabToPlace == null)
        {
            Debug.LogWarning($"No Prefab assigned for {currentPlacementMode}");
            return;
        }

        Camera cameraToUse = sceneCamera != null ? sceneCamera : Camera.main;
        if (cameraToUse == null)
        {
            Debug.LogWarning("Cannot place object because no camera is assigned.");
            return;
        }

        Vector3 surfacePosition = GetMouseSurfacePosition(cameraToUse, mousePosition);
        GameObject spawnedObject = Instantiate(prefabToPlace, environment); // now creates new seeker every click
        spawnedObject.name = $"Editable {currentPlacementMode}";

        spawnedObject.transform.rotation = Quaternion.identity;
        PlaceObjectOnSurface(spawnedObject, surfacePosition);

        NotifyObjectPlaced(spawnedObject);

#if UNITY_EDITOR
        Selection.activeGameObject = spawnedObject;
#endif
        Debug.Log($"{currentPlacementMode} placed at {spawnedObject.transform.position}.");
        currentPlacementMode = PlacementMode.None;

    }

    private GameObject GetPrefabForCurrentMode()
    {
        switch (currentPlacementMode)
        {
            case PlacementMode.Seeker:
                return emptySeekerPrefab;

            case PlacementMode.Hider:
                return emptyHiderPrefab;

            case PlacementMode.Obstacle:
                return ObstaclePrefab;
            default:
                return null;
        }

    }

    private void NotifyObjectPlaced(GameObject spawnedObject)
    {
        if (currentPlacementMode == PlacementMode.Seeker)
        {
            GameEvents.NotifyAgentSpawned(spawnedObject);
        }
    }


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

    private void PlaceObjectOnSurface(GameObject objectToPlace, Vector3 surfacePosition)
    {
        objectToPlace.transform.position = surfacePosition;

        if (!TryGetColliderBounds(objectToPlace, out Bounds bounds))
        {
            objectToPlace.transform.position = surfacePosition + Vector3.up * placementYOffset;
            return;
        }

        float liftAmount = surfacePosition.y - bounds.min.y + placementYOffset;
        objectToPlace.transform.position += Vector3.up * liftAmount;
    }

    private bool TryGetColliderBounds(GameObject targetObject, out Bounds combinedBounds)
    {
        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>();
        combinedBounds = new Bounds(targetObject.transform.position, Vector3.zero);
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


}
