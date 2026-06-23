using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownCameraController : MonoBehaviour
{
    public Camera targetCamera;
    public float zoomSpeed = 20f;
    public float minZoom = 5f;
    public float maxZoom = 60f;

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
}
