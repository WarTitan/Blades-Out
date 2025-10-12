using UnityEngine;
using Mirror;

public class LocalCameraController : NetworkBehaviour
{
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private Transform playerBody;

    private float xRotation;

    void Start()
    {
        if (!isLocalPlayer) { enabled = false; return; }
        Cursor.lockState = CursorLockMode.Locked;

        if (playerBody == null)
            playerBody = transform.root; // fallback to player root
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        if (playerBody) playerBody.Rotate(Vector3.up * mouseX);
    }
}
