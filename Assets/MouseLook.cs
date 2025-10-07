// 10/7/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour, PlayerInputActions.IPlayerActions
{
    public float mouseSensitivity = 100f; // Sensitivity of the mouse
    public Transform playerBody;         // Reference to the player's body for rotation

    private float xRotation = 0f;        // To keep track of vertical rotation
    private Vector2 mouseDelta;          // Stores mouse movement
    private PlayerInputActions inputActions;

    void Awake()
    {
        // Initialize the Input Actions
        inputActions = new PlayerInputActions();
        inputActions.Player.SetCallbacks(this);
    }

    void OnEnable()
    {
        // Enable the Player Input Action Map
        inputActions.Player.Enable();
    }

    void OnDisable()
    {
        // Disable the Player Input Action Map
        inputActions.Player.Disable();
    }

    void Start()
    {
        // Lock the cursor to the center of the screen
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Rotate the camera vertically (up and down)
        xRotation -= mouseDelta.y * mouseSensitivity * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Clamp the rotation to prevent over-rotation
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotate the player horizontally (left and right)
        playerBody.Rotate(Vector3.up * mouseDelta.x * mouseSensitivity * Time.deltaTime);
    }

    // This method is called by the Input System when the Look action is performed
    public void OnLook(InputAction.CallbackContext context)
    {
        mouseDelta = context.ReadValue<Vector2>();
    }
}