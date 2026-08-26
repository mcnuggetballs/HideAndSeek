using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

// this file handles how the environment can be viewed (zoom in/out, look around) during editor mode
public class TopDownCameraController : MonoBehaviour
{
    public Camera targetCamera;

    // zoom function
    public float zoomSpeed = 10f;
    public float minZoom = 10f;
    public float maxZoom = 200f;

    // pan around
    private Vector3 dragStartWorldPoint;
    private bool isDraggingMap;

    // immediately
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

    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // y = 0

        if (groundPlane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        worldPoint = Vector3.zero;
        return false;
    }

}