using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.VisualScripting;
using System.Collections.Generic;

// this file only listens for spawn seeker/hider/obstacle, as the name suggests "TestingScenarioEditor"

// why using +=? essentially it is like keeping track howm nay times it is being called

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TestingScenarioEditor : MonoBehaviour
{
    public Camera sceneCamera;
    public GameObject seekerPrefab;
    public float placementYOffset = 0.02f;
    public float seekerPreviewScale = 1f;
    public bool disableSpawnedAgentControl = true;

    private bool placingSeeker;
    private GameObject currentSeeker;


    // when this script becomes active, subscribe BeginPlaceSeeker to SpawnSeeker requested
    // += and -= are what actually connect/disconnect the event listener.
    private void OnEnable() 
    {
        GameEvents.SpawnSeekerRequested += BeginPlaceSeeker; // i want to listen for SpawnAeekerRequested
    }

    // when this script becomes inactive, unsub BeginPlaceSeeker to SpawnSeeker requested
    private void OnDisable() 
    {
        GameEvents.SpawnSeekerRequested -= BeginPlaceSeeker;
    }

    //EDITOR FEATURES
    public void BeginPlaceSeeker()
    {
        if (seekerPrefab == null)
        {
            Debug.LogWarning("Cannot place seeker because no seeker prefab is assigned.");
            return;
        }

        placingSeeker = true;
        currentSeeker = currentSeeker != null
            ? currentSeeker
            : GameObject.Find("Editable Seeker Prefab");

        Debug.Log("Spawn Seeker mode active. Click on the map to place the seeker prefab.");
    }

    void Update()
    {
        if (!placingSeeker || !TryGetMouseClick(out Vector2 mousePosition))
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        PlaceSeekerAtMousePosition(mousePosition);
    }

    // HELPER
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

    private void PlaceSeekerAtMousePosition(Vector2 mousePosition)
    {
        Camera cameraToUse = sceneCamera != null ? sceneCamera : Camera.main;
        if (cameraToUse == null)
        {
            Debug.LogWarning("Cannot place seeker because no camera is assigned.");
            return;
        }

        Vector3 surfacePosition = GetMouseSurfacePosition(cameraToUse, mousePosition);

        if (currentSeeker == null)
        {
            currentSeeker = Instantiate(seekerPrefab);
            currentSeeker.name = "Editable Seeker Prefab";
            currentSeeker.transform.localScale = Vector3.one * seekerPreviewScale;
            GameEvents.NotifyAgentSpawned(currentSeeker);

            //if (disableSpawnedAgentControl)
            //{
            //    DisableAgentControl(currentSeeker);
            //}
        }

        currentSeeker.transform.rotation = Quaternion.identity;
        PlaceObjectOnSurface(currentSeeker, surfacePosition);

#if UNITY_EDITOR
        Selection.activeGameObject = currentSeeker;
#endif

        placingSeeker = false;
        Debug.Log($"Seeker placed on surface at {currentSeeker.transform.position}.");
   
        
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

    // be able to drag seeker around

    // delete seeker

    // undo

    // reset
}
