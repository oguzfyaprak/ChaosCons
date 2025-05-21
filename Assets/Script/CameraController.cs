using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float rotationSpeed = 3f;
    public float zoomSpeed = 50f;

    private float yaw = 0f;
    private float pitch = 30f;

    void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        // Hareket
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = moveSpeed * Time.deltaTime * (transform.forward * v + transform.right * h);
        transform.position += move;

        // Yükseklik (Q-E)
        if (Input.GetKey(KeyCode.E)) transform.position += moveSpeed * Time.deltaTime * Vector3.up;
        if (Input.GetKey(KeyCode.Q)) transform.position += moveSpeed * Time.deltaTime * Vector3.down;

        // Fareyle dönüþ
        if (Input.GetMouseButton(1)) // Sað týk
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            pitch = Mathf.Clamp(pitch, 10, 80); // Yukarý-aþaðý sýnýrlama
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        // Zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        transform.position += scroll * Time.deltaTime * zoomSpeed * transform.forward;
    }
}
