using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// this file handles spawn seeker/hider/obstacle, raycast mouseclick onto map, edit scenario. spawning related stuff is all handled here

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TestingScenarioEditor : MonoBehaviour
{
    // assign in inspector
    public Camera sceneCamera;

    public float placementYOffset = 0.02f;
    public float seekerPreviewScale = 1f;

    private bool placingSeeker;
    private GameObject currentSeeker;
    [SerializeField]
    private Transform environment;
    [SerializeField]
    private GameObject emptySeekerPrefab;

    // when this script becomes active, subscribe BeginPlaceSeeker to SpawnSeeker requested
    // += and -= are what actually connect/disconnect the event listener.
    private void OnEnable()
    {
        // testinscenarioeditor listens for spawn seeker requests
        GameEvents.SpawnSeekerRequested += BeginPlaceSeeker; // i want to listen for SpawnAeekerRequested
        Debug.Log("TestingScenarioEditor is listening for spawn seeker requests.");
    }

    // when this script becomes inactive, unsub BeginPlaceSeeker to SpawnSeeker requested
    private void OnDisable()
    {
        GameEvents.SpawnSeekerRequested -= BeginPlaceSeeker;
    }

    // enter placement mode
    public void BeginPlaceSeeker()
    {
        placingSeeker = true;
        //currentSeeker = currentSeeker != null ? currentSeeker : GameObject.Find("Editable Seeker Prefab"); // try to reuse
        Debug.Log("Spawn Seeker mode active. Click on the map to place the seeker prefab.");
    }

    void Update()
    {
        // if not placing seeker or no mouse click = dont do anything
        if ( !TryGetMouseClick(out Vector2 mousePosition))
        {
            return;
        }

        // check if u click UI instead of world
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("Seeker placement click ignored because the pointer is over UI.");
            return;
        }

        PlaceSeekerAtMousePosition(mousePosition);
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
    private void PlaceSeekerAtMousePosition(Vector2 mousePosition)
    {
        Camera cameraToUse = sceneCamera != null ? sceneCamera : Camera.main;
        if (cameraToUse == null)
        {
            Debug.LogWarning("Cannot place seeker because no camera is assigned.");
            return;
        }

        Vector3 surfacePosition = GetMouseSurfacePosition(cameraToUse, mousePosition);

        Debug.Log("Creating seeker prefab.");
        GameObject currentSeeker = Instantiate(emptySeekerPrefab, environment); // now creates new seeker every click

        currentSeeker.name = "Editable Seeker Prefab";

        GameEvents.NotifyAgentSpawned(currentSeeker); // tells simulation manager that agent exist


        currentSeeker.transform.rotation = Quaternion.identity;
        PlaceObjectOnSurface(currentSeeker, surfacePosition);

#if UNITY_EDITOR
        Selection.activeGameObject = currentSeeker;
#endif

        placingSeeker = false;
        Debug.Log($"Seeker placed on surface at {currentSeeker.transform.position}.");


    }

    private void PlaceHiderAtMousePosition(Vector2 mousePosition)
    {

    }

    private Vector3 GetMouseSurfacePosition(Camera cameraToUse, Vector2 mousePosition)
    {
        Ray ray = cameraToUse.ScreenPointToRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {
            if (currentSeeker != null && hit.transform.IsChildOf(currentSeeker.transform))
            {
                continue;
            }

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
