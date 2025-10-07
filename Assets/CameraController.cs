// 10/7/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.InputSystem;

Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;

public class CameraController : MonoBehaviour
{
    public float mouseSensitivity = 100f; // Sensitivity of the mouse
    private Vector2 mouseDelta;          // Stores mouse movement
    private float xRotation = 0f;        // To keep track of vertical rotation

    private InputAction lookAction;

    void Awake()
    {
        // Initialize Input Actions
        var playerInput = GetComponent<PlayerInput>();
        lookAction = playerInput.actions["Look"];
    }

    void Start()
    {
        // Lock the cursor to the center of the screen and hide it
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        GetPlayerInput();

        // Rotate the camera vertically (up and down)
        xRotation -= mouseDelta.y * mouseSensitivity * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Clamp the rotation to prevent over-rotation
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotate the camera horizontally (left and right)
        transform.parent.Rotate(Vector3.up * mouseDelta.x * mouseSensitivity * Time.deltaTime);
    }

    void GetPlayerInput()
    {
        // Get mouse delta from the new Input System
        mouseDelta = lookAction.ReadValue<Vector2>();
    }
}