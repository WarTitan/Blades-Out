// 10/6/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    public Transform playerBody;
    public float mouseSensitivity = 100f;

    private Vector2 lookInput;
    private float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; // Lock the cursor to the center of the screen
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        // Read the mouse input from the Input System
        lookInput = context.ReadValue<Vector2>();
    }

    void Update()
    {
        // Apply mouse input to rotation
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        // Rotate the camera up and down
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Clamp rotation to prevent flipping
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotate the player body left and right
        playerBody.Rotate(Vector3.up * mouseX);
    }
}