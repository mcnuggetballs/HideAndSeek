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
    public float panSpeed = 0.005f;

    public Collider environmentCollider;

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
        // for pan around
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            Vector3 panMovement = new Vector3(
                    -mouseDelta.y, // swapped with x
                    0f,
                    -mouseDelta.x // swapped with y
                ) * panSpeed * targetCamera.orthographicSize;

            transform.position += panMovement;
        }
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
