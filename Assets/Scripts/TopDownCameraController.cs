using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.UI;
using UnityEngine.UIElements;

// this file handles how the environment can be viewed (zoom in/out, look around) during editor mode
public class TopDownCameraController : MonoBehaviour
{
    public Camera targetCamera;

    // zoom function
    public float zoomSpeed = 20f;
    public float minZoom = 5f;
    public float maxZoom = 60f;

    // pan around
    public float panSpeed = 0.01f;
    public Collider environmentCollider;
    private Vector3 dragStartWorldPoint;
    private bool isDraggingMap;


    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }
    }
    // Update is called once per frame
    void Update()
    {
        if (Mouse.current == null || targetCamera == null)
        {
            return;
        }

        HandleZoom();
        HandlePan();
        //ClampCameraToBounds();
    }

    private void HandleZoom()
    {
        // for zoom in/out scroll
        float scroll = Mouse.current.scroll.ReadValue().y;
        if ((Mathf.Abs(scroll) < 0.01f))
        {
            return;
        }
        targetCamera.orthographicSize -= scroll * zoomSpeed;
        targetCamera.orthographicSize = Mathf.Clamp(
            targetCamera.orthographicSize,
            minZoom,
            maxZoom);
    }

    // if right mouse button is held:
    //      read how much mouse moved
    //      move the camera in opposite direction
    private void HandlePan()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (TryGetMouseWorldPoint(out dragStartWorldPoint))
            {
                isDraggingMap = true;
            }
        }

        if (Mouse.current.rightButton.isPressed && isDraggingMap)
        {
            if (TryGetMouseWorldPoint(out Vector3 currentWorldPoint))
            {
                Vector3 difference = dragStartWorldPoint - currentWorldPoint;
                transform.position += difference;
            }
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isDraggingMap = false;
        }

        //Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        //Vector3 cameraRight = targetCamera.transform.right;
        //Vector3 cameraUp = targetCamera.transform.up;

        //cameraRight.y = 0f;
        //cameraUp.y = 0f;

        //// swap y and x worked
        //Vector3 panMovement = (-cameraRight * mouseDelta.x - cameraUp * mouseDelta.y)
        //* panSpeed
        //* targetCamera.orthographicSize;

        //transform.position += panMovement;

    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (environmentCollider.Raycast(ray, out RaycastHit hit, 500f))
        {
            worldPoint = hit.point;
            return true;
        }

        worldPoint = Vector3.zero;
        return false;
    }

    // ensure camera does not go out of view
    // TODO: height in centre so wont force clamp even though too small
    private void ClampCameraToBounds()
    {
        Camera cam = Camera.main;
        Bounds bounds = environmentCollider.bounds;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        float boundsWidth = bounds.size.x;
        float boundsHeight = bounds.size.z;

        Vector3 pos = transform.position;

        // X
        if (halfWidth * 2 >= boundsWidth)
        {
            // Camera is wider than the bounds → stay centered
            pos.x = bounds.center.x;
        }
        else
        {
            pos.x = Mathf.Clamp(
                pos.x,
                bounds.min.x + halfWidth,
                bounds.max.x - halfWidth);
        }

        // Z
        if (halfHeight * 2 >= boundsHeight)
        {
            // Camera is taller than the bounds → stay centered
            pos.z = bounds.center.z;
        }
        else
        {
            pos.z = Mathf.Clamp(
                pos.z,
                bounds.min.z + halfHeight,
                bounds.max.z - halfHeight);
        }

        cam.transform.position = pos;
    }
}
