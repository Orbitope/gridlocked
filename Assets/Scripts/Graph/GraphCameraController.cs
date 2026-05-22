using UnityEngine;

public class GraphCameraController : MonoBehaviour
{
    public float panSpeed = 20f;
    public float zoomSpeed = 2f;
    public float minZoom = 5f;
    public float maxZoom = 100f;

    private Camera _cam;

    private void Start()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null)
        {
            _cam = Camera.main;
        }
    }

    private void Update()
    {
        // Panning with WASD or Arrow Keys
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(h, v, 0) * panSpeed * Time.deltaTime;
        
        // Scale pan speed based on zoom level for smoother feel
        if (_cam.orthographic)
        {
            move *= (_cam.orthographicSize / 10f);
        }
        else
        {
            move *= (Mathf.Abs(_cam.transform.position.z) / 10f);
        }

        transform.Translate(move, Space.World);

        // Zooming with Scroll Wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            if (_cam.orthographic)
            {
                _cam.orthographicSize -= scroll * zoomSpeed * 10f;
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, minZoom, maxZoom);
            }
            else
            {
                Vector3 pos = transform.position;
                pos.z += scroll * zoomSpeed * 100f;
                // Clamp Z distance
                pos.z = Mathf.Clamp(pos.z, -maxZoom * 2, -minZoom);
                transform.position = pos;
            }
        }
    }
}
