using UnityEngine;

/// <summary>
/// RTS-style orthographic camera: drag the middle mouse button to pan and use
/// the scroll wheel to zoom while keeping a fixed angled view of the map.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class MapCameraController : MonoBehaviour
{
    [SerializeField, Range(20f, 75f)] private float pitch = 50f;
    [SerializeField, Min(0.01f)] private float zoomSpeed = 8f;
    [SerializeField, Min(0.01f)] private float minZoom = 17f;
    [SerializeField, Min(0.01f)] private float maxZoom = 48f;
    [SerializeField] private int panMouseButton = 2;

    private Camera mapCamera;
    private Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
    private Vector3 dragStartWorld;
    private bool isDragging;

    private void Awake()
    {
        mapCamera = GetComponent<Camera>();
        transform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
    }

    private void Update()
    {
        if (mapCamera == null) return;

        float scroll = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(scroll, 0f))
        {
            mapCamera.orthographicSize = Mathf.Clamp(
                mapCamera.orthographicSize - scroll * zoomSpeed,
                minZoom,
                maxZoom);
        }

        if (Input.GetMouseButtonDown(panMouseButton) && TryGetWorldPoint(Input.mousePosition, out dragStartWorld))
            isDragging = true;

        if (isDragging && Input.GetMouseButton(panMouseButton)
            && TryGetWorldPoint(Input.mousePosition, out Vector3 currentWorld))
        {
            transform.position += dragStartWorld - currentWorld;
        }

        if (Input.GetMouseButtonUp(panMouseButton))
            isDragging = false;
    }

    private bool TryGetWorldPoint(Vector3 screenPosition, out Vector3 worldPoint)
    {
        Ray ray = mapCamera.ScreenPointToRay(screenPosition);
        if (mapPlane.Raycast(ray, out float distance))
        {
            worldPoint = ray.GetPoint(distance);
            return true;
        }

        worldPoint = default;
        return false;
    }
}
