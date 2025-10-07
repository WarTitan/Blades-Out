// 10/7/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public int targetDisplay = 1; // Set this in the Inspector for each camera (1 for Display 1, 2 for Display 2)
    public float lookSpeed = 100f; // Speed of camera rotation

    private Vector2 lookInput; // Stores input for looking around
    private PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.look.performed += OnLook; // Correctly accessing the 'look' action
        inputActions.Player.look.canceled += OnLook;
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Player.look.performed -= OnLook;
        inputActions.Player.look.canceled -= OnLook;
        inputActions.Disable();
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    void Update()
    {
        // Check if this camera's display is the active display
        if (Display.displays.Length >= targetDisplay && Display.displays[targetDisplay - 1].active)
        {
            RotateCamera();
        }
    }

    private void RotateCamera()
    {
        // Rotate the camera based on mouse input
        float yaw = lookInput.x * lookSpeed * Time.deltaTime;
        float pitch = -lookInput.y * lookSpeed * Time.deltaTime;

        transform.Rotate(Vector3.up, yaw, Space.World); // Rotate around the Y-axis (world space)
        transform.Rotate(Vector3.right, pitch, Space.Self); // Rotate around the X-axis (local space)
    }
}