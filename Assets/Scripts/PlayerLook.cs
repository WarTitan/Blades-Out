using UnityEngine;
using Mirror;

public class PlayerLook : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float sensitivity = 100f;
    [SerializeField] private float clampAngle = 80f;

    private float xRotation = 0f;
    private float yRotation = 0f;

    private void Start()
    {
        if (!isLocalPlayer)
        {
            // Disable camera for non-local players
            if (playerCamera != null)
                playerCamera.enabled = false;
            enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -clampAngle, clampAngle);

        // Apply rotation to camera and player
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}
